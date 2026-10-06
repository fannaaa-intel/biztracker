''' <summary>
''' Database access for Real Property Tax: rpt_assessments (yearly tax per property)
''' and rpt_payments (quarterly payments against an assessment).
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

    Private Const PaymentSql As String = "SELECT pay.* FROM rpt_payments pay "

    Public Shared Function GetPaymentById(paymentId As Integer) As RptPayment
        Return ToPayments(Db.GetDataTable(PaymentSql & "WHERE pay.payment_id = @id",
                                          Db.P("@id", paymentId))).FirstOrDefault()
    End Function

    Public Shared Function GetPaymentsByAssessmentId(assessmentId As Integer) As List(Of RptPayment)
        Return ToPayments(Db.GetDataTable(PaymentSql & "WHERE pay.assessment_id = @aid ORDER BY pay.quarter",
                                          Db.P("@aid", assessmentId)))
    End Function

    ''' <summary>All payments for all properties of a business.</summary>
    Public Shared Function GetPaymentsByBusinessId(businessId As Integer) As List(Of RptPayment)
        Const sql As String =
            PaymentSql &
            "JOIN rpt_assessments a ON a.assessment_id = pay.assessment_id " &
            "JOIN properties p ON p.property_id = a.property_id " &
            "WHERE p.business_id = @bid ORDER BY pay.payment_date DESC"
        Return ToPayments(Db.GetDataTable(sql, Db.P("@bid", businessId)))
    End Function

    ''' <summary>Returns the new payment_id, or -1 on error (e.g. quarter already paid).</summary>
    Public Shared Function InsertPayment(pay As RptPayment) As Integer
        Const sql As String =
            "INSERT INTO rpt_payments (assessment_id, quarter, amount_paid, penalty, or_no, payment_date, received_by) " &
            "VALUES (@aid, @quarter, @amount, @penalty, @or, @date, @by)"
        Return Db.ExecuteInsert(sql,
            Db.P("@aid", pay.AssessmentId),
            Db.P("@quarter", pay.Quarter),
            Db.P("@amount", pay.AmountPaid),
            Db.P("@penalty", pay.Penalty),
            Db.P("@or", pay.OrNo),
            Db.P("@date", pay.PaymentDate.Date),
            Db.P("@by", pay.ReceivedBy))
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
                .OrNo = row.GetString("or_no"),
                .PaymentDate = row.GetDate("payment_date"),
                .ReceivedBy = row.GetNullableInt("received_by"),
                .CreatedAt = row.GetDate("created_at")
            }).ToList()
    End Function

End Class
