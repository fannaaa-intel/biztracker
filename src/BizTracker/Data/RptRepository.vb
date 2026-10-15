Imports MySql.Data.MySqlClient

''' <summary>
''' Database access for Real Property Tax: rpt_assessments (yearly tax per property)
''' and rpt_payments (walk-in payments for a quarter of an assessment; a quarter can have several).
''' </summary>
Public NotInheritable Class RptRepository

    Private Sub New()
    End Sub

    ' ==================== Assessments ====================

    Private Const AssessmentSql As String = "SELECT a.* FROM rpt_assessments a "

    Public Shared Function GetAllAssessments() As List(Of RptAssessment)
        Return ToAssessments(Db.GetDataTable(AssessmentSql & "ORDER BY a.tax_year DESC, a.property_id"))
    End Function

    Public Shared Function GetAssessmentById(assessmentId As Integer) As RptAssessment
        Return ToAssessments(Db.GetDataTable(AssessmentSql & "WHERE a.assessment_id = @id",
                                             Db.P("@id", assessmentId))).FirstOrDefault()
    End Function

    Public Shared Function GetAssessmentsByPropertyId(propertyId As Integer) As List(Of RptAssessment)
        Return ToAssessments(Db.GetDataTable(AssessmentSql & "WHERE a.property_id = @pid ORDER BY a.tax_year DESC",
                                             Db.P("@pid", propertyId)))
    End Function

    ''' <summary>All assessments for all properties of a business.</summary>
    Public Shared Function GetAssessmentsByBusinessId(businessId As Integer) As List(Of RptAssessment)
        Const sql As String =
            AssessmentSql &
            "JOIN properties p ON p.property_id = a.property_id " &
            "WHERE p.business_id = @bid ORDER BY a.tax_year DESC, a.property_id"
        Return ToAssessments(Db.GetDataTable(sql, Db.P("@bid", businessId)))
    End Function

    ''' <summary>Returns the new assessment_id, or -1 on error (e.g. year already assessed).</summary>
    Public Shared Function InsertAssessment(a As RptAssessment) As Integer
        Const sql As String =
            "INSERT INTO rpt_assessments (property_id, tax_year, basic_tax, sef_tax, total_due) " &
            "VALUES (@pid, @year, @basic, @sef, @total)"
        Return Db.ExecuteInsert(sql,
            Db.P("@pid", a.PropertyId),
            Db.P("@year", a.TaxYear),
            Db.P("@basic", a.BasicTax),
            Db.P("@sef", a.SefTax),
            Db.P("@total", a.TotalDue))
    End Function

    ' ==================== Payments ====================

    ''' <summary>Payments with the tax year, property and the names of who received / voided them.</summary>
    Private Const PaymentSql As String =
        "SELECT pay.*, a.tax_year, a.property_id, p.pin, " &
        "  ru.full_name AS received_by_name, vu.full_name AS voided_by_name " &
        "FROM rpt_payments pay " &
        "JOIN rpt_assessments a ON a.assessment_id = pay.assessment_id " &
        "JOIN properties p ON p.property_id = a.property_id " &
        "LEFT JOIN users ru ON ru.user_id = pay.received_by " &
        "LEFT JOIN users vu ON vu.user_id = pay.voided_by "

    Public Shared Function GetPaymentById(paymentId As Integer) As RptPayment
        Return ToPayments(Db.GetDataTable(PaymentSql & "WHERE pay.payment_id = @id",
                                          Db.P("@id", paymentId))).FirstOrDefault()
    End Function

    ''' <summary>Every payment of an assessment (voided ones too), in the order they were recorded.</summary>
    Public Shared Function GetPaymentsByAssessmentId(assessmentId As Integer) As List(Of RptPayment)
        Return ToPayments(Db.GetDataTable(PaymentSql & "WHERE pay.assessment_id = @aid ORDER BY pay.payment_id",
                                          Db.P("@aid", assessmentId)))
    End Function

    ''' <summary>Every payment for all properties of a business (voided ones too), in the order they were recorded.</summary>
    Public Shared Function GetPaymentsByBusinessId(businessId As Integer) As List(Of RptPayment)
        Return ToPayments(Db.GetDataTable(PaymentSql & "WHERE p.business_id = @bid ORDER BY pay.payment_id",
                                          Db.P("@bid", businessId)))
    End Function

    ''' <summary>
    ''' Saves a payment and (optionally) the automatic RPT endorsement in ONE transaction.
    ''' The assessment row is locked first and its active payments are compared with what the screen saw
    ''' (count + total), so two cashiers cannot pay the same balance twice.
    ''' The OR number is registered in official_receipts in the same transaction (unique across all payments).
    ''' Returns the new payment_id, 0 if the payments changed in the meantime (nothing saved),
    ''' -2 if the OR number was taken by another cashier a moment earlier, or -1 on a DB error.
    ''' </summary>
    Public Shared Function SavePayment(pay As RptPayment, expectedCount As Integer, expectedTotal As Decimal,
                                       endorsement As EndorsementChange) As Integer
        Dim newId = 0
        Dim duplicateOr = False
        Dim ok = Db.RunInTransaction(
            Sub(conn, tx)
                If Not ActivePaymentsUnchanged(conn, tx, pay.AssessmentId, expectedCount, expectedTotal) Then Return
                newId = Db.TxExecute(conn, tx,
                    "INSERT INTO rpt_payments (assessment_id, quarter, amount_paid, penalty, payment_method, reference_no, " &
                    "  or_no, payment_date, received_by, remarks) " &
                    "VALUES (@aid, @quarter, @amount, @penalty, @method, @ref, @or, @date, @by, @remarks)",
                    Db.P("@aid", pay.AssessmentId),
                    Db.P("@quarter", pay.Quarter),
                    Db.P("@amount", pay.AmountPaid),
                    Db.P("@penalty", pay.Penalty),
                    Db.P("@method", pay.PaymentMethod),
                    Db.P("@ref", Db.NullIfEmpty(pay.ReferenceNo)),
                    Db.P("@or", pay.OrNo.Trim()),
                    Db.P("@date", pay.PaymentDate.Date),
                    Db.P("@by", pay.ReceivedBy),
                    Db.P("@remarks", Db.NullIfEmpty(pay.Remarks)))
                OfficialReceiptRepository.TxRegister(conn, tx, pay.OrNo, OfficialReceiptRepository.SourceRpt, newId)
                If endorsement IsNot Nothing Then BusinessPermitRepository.TxSetEndorsement(conn, tx, endorsement)
            End Sub, duplicateOr)
        If duplicateOr Then Return -2
        Return If(ok, newId, -1)
    End Function

    ''' <summary>
    ''' Voids a payment (never deletes it) and (optionally) puts the RPT endorsement back to Pending, in ONE transaction.
    ''' Only the newest active payment of its assessment can be voided; this is checked again under a lock.
    ''' Returns 1 when voided, 0 if it is no longer the newest active payment (nothing saved), -1 on a DB error.
    ''' </summary>
    Public Shared Function VoidPayment(pay As RptPayment, userId As Integer?, reason As String,
                                       endorsement As EndorsementChange) As Integer
        Dim stillNewest = False
        Dim ok = Db.RunInTransaction(
            Sub(conn, tx)
                Db.TxScalar(conn, tx, "SELECT assessment_id FROM rpt_assessments WHERE assessment_id = @aid FOR UPDATE",
                            Db.P("@aid", pay.AssessmentId))
                Dim newest = Db.TxScalar(conn, tx,
                    "SELECT MAX(payment_id) FROM rpt_payments WHERE assessment_id = @aid AND voided_at IS NULL",
                    Db.P("@aid", pay.AssessmentId))
                stillNewest = newest IsNot Nothing AndAlso newest IsNot DBNull.Value AndAlso Convert.ToInt32(newest) = pay.PaymentId
                If Not stillNewest Then Return
                Db.TxExecute(conn, tx,
                    "UPDATE rpt_payments SET voided_at = NOW(), voided_by = @uid, void_reason = @reason " &
                    "WHERE payment_id = @id AND voided_at IS NULL",
                    Db.P("@uid", userId), Db.P("@reason", reason.Trim()), Db.P("@id", pay.PaymentId))
                If endorsement IsNot Nothing Then BusinessPermitRepository.TxSetEndorsement(conn, tx, endorsement)
            End Sub)
        If Not ok Then Return -1
        Return If(stillNewest, 1, 0)
    End Function

    ''' <summary>Locks the assessment row and checks its active payments still match (count and total).</summary>
    Private Shared Function ActivePaymentsUnchanged(conn As MySqlConnection, tx As MySqlTransaction, assessmentId As Integer,
                                                    expectedCount As Integer, expectedTotal As Decimal) As Boolean
        Db.TxScalar(conn, tx, "SELECT assessment_id FROM rpt_assessments WHERE assessment_id = @aid FOR UPDATE",
                    Db.P("@aid", assessmentId))
        Dim count = Convert.ToInt32(Db.TxScalar(conn, tx,
            "SELECT COUNT(*) FROM rpt_payments WHERE assessment_id = @aid AND voided_at IS NULL", Db.P("@aid", assessmentId)))
        Dim total = Convert.ToDecimal(Db.TxScalar(conn, tx,
            "SELECT COALESCE(SUM(amount_paid + penalty), 0) FROM rpt_payments WHERE assessment_id = @aid AND voided_at IS NULL",
            Db.P("@aid", assessmentId)))
        Return count = expectedCount AndAlso total = expectedTotal
    End Function

    ' ---------------- helpers ----------------

    Private Shared Function ToAssessments(table As DataTable) As List(Of RptAssessment)
        Return table.Rows.Cast(Of DataRow)().Select(
            Function(row) New RptAssessment With {
                .AssessmentId = row.GetInt("assessment_id"),
                .PropertyId = row.GetInt("property_id"),
                .TaxYear = row.GetInt("tax_year"),
                .BasicTax = row.GetDecimal("basic_tax"),
                .SefTax = row.GetDecimal("sef_tax"),
                .TotalDue = row.GetDecimal("total_due"),
                .CreatedAt = row.GetDate("created_at")
            }).ToList()
    End Function

    Private Shared Function ToPayments(table As DataTable) As List(Of RptPayment)
        Return table.Rows.Cast(Of DataRow)().Select(
            Function(row) New RptPayment With {
                .PaymentId = row.GetInt("payment_id"),
                .AssessmentId = row.GetInt("assessment_id"),
                .Quarter = row.GetInt("quarter"),
                .AmountPaid = row.GetDecimal("amount_paid"),
                .Penalty = row.GetDecimal("penalty"),
                .PaymentMethod = row.GetString("payment_method"),
                .ReferenceNo = row.GetString("reference_no"),
                .OrNo = row.GetString("or_no"),
                .PaymentDate = row.GetDate("payment_date"),
                .ReceivedBy = row.GetNullableInt("received_by"),
                .Remarks = row.GetString("remarks"),
                .VoidedAt = row.GetNullableDate("voided_at"),
                .VoidedBy = row.GetNullableInt("voided_by"),
                .VoidReason = row.GetString("void_reason"),
                .CreatedAt = row.GetDate("created_at"),
                .ReceivedByName = row.GetString("received_by_name"),
                .VoidedByName = row.GetString("voided_by_name"),
                .TaxYear = row.GetInt("tax_year"),
                .PropertyId = row.GetInt("property_id"),
                .Pin = row.GetString("pin")
            }).ToList()
    End Function

End Class
