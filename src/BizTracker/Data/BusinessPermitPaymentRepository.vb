Imports MySql.Data.MySqlClient

''' <summary>
''' Database access for business_permit_payments (walk-in business tax payments, partial payments allowed).
''' Saving and voiding run in ONE transaction with the permit's status change (Assessed &lt;-&gt; Paid) and the
''' OR registry, with the permit row locked so two cashiers cannot pay the same balance twice.
''' Used only by BusinessTaxService.
''' </summary>
Public NotInheritable Class BusinessPermitPaymentRepository

    Private Sub New()
    End Sub

    ''' <summary>Payments with the permit reference and the names of who received / voided them.</summary>
    Private Const SelectSql As String =
        "SELECT pay.*, bp.business_id, bp.reference_no AS permit_reference_no, bp.permit_year, " &
        "  ru.full_name AS received_by_name, vu.full_name AS voided_by_name " &
        "FROM business_permit_payments pay " &
        "JOIN business_permits bp ON bp.permit_id = pay.permit_id " &
        "LEFT JOIN users ru ON ru.user_id = pay.received_by " &
        "LEFT JOIN users vu ON vu.user_id = pay.voided_by "

    Public Shared Function GetById(paymentId As Integer) As BusinessPermitPayment
        Return ToList(Db.GetDataTable(SelectSql & "WHERE pay.payment_id = @id", Db.P("@id", paymentId))).FirstOrDefault()
    End Function

    ''' <summary>Every payment of a permit application (voided ones too), in the order they were recorded.</summary>
    Public Shared Function GetByPermitId(permitId As Integer) As List(Of BusinessPermitPayment)
        Return ToList(Db.GetDataTable(SelectSql & "WHERE pay.permit_id = @pid ORDER BY pay.payment_id", Db.P("@pid", permitId)))
    End Function

    ''' <summary>Every payment of all permit applications of a business (voided ones too), in the order they were recorded.</summary>
    Public Shared Function GetByBusinessId(businessId As Integer) As List(Of BusinessPermitPayment)
        Return ToList(Db.GetDataTable(SelectSql & "WHERE bp.business_id = @bid ORDER BY pay.payment_id", Db.P("@bid", businessId)))
    End Function

    ''' <summary>
    ''' Saves a payment in ONE transaction: locks the permit, checks it is still Assessed and its active payments still
    ''' match what the screen saw (count + total), inserts the payment, registers the OR number (unique across all
    ''' payments) and - when this payment completes the balance - sets the permit to Paid with the surcharge and interest
    ''' actually charged. Returns the new payment_id, 0 if something changed meanwhile (nothing saved),
    ''' -2 if the OR number was taken by another cashier a moment earlier, or -1 on a DB error.
    ''' </summary>
    Public Shared Function SavePayment(pay As BusinessPermitPayment, expectedCount As Integer, expectedTotal As Decimal,
                                       markPaid As Boolean, finalSurcharge As Decimal, finalInterest As Decimal) As Integer
        Dim newId = 0
        Dim duplicateOr = False
        Dim ok = Db.RunInTransaction(
            Sub(conn, tx)
                Dim status = Db.TxScalar(conn, tx, "SELECT status FROM business_permits WHERE permit_id = @pid FOR UPDATE",
                                         Db.P("@pid", pay.PermitId))
                If status Is Nothing OrElse status.ToString() <> BusinessPermitService.Assessed Then Return
                If Not ActivePaymentsUnchanged(conn, tx, pay.PermitId, expectedCount, expectedTotal) Then Return
                newId = Db.TxExecute(conn, tx,
                    "INSERT INTO business_permit_payments (permit_id, amount_paid, surcharge, interest, payment_method, " &
                    "  reference_no, or_no, payment_date, received_by, remarks) " &
                    "VALUES (@pid, @amount, @surcharge, @interest, @method, @ref, @or, @date, @by, @remarks)",
                    Db.P("@pid", pay.PermitId),
                    Db.P("@amount", pay.AmountPaid),
                    Db.P("@surcharge", pay.Surcharge),
                    Db.P("@interest", pay.Interest),
                    Db.P("@method", pay.PaymentMethod),
                    Db.P("@ref", Db.NullIfEmpty(pay.ReferenceNo)),
                    Db.P("@or", pay.OrNo.Trim()),
                    Db.P("@date", pay.PaymentDate.Date),
                    Db.P("@by", pay.ReceivedBy),
                    Db.P("@remarks", Db.NullIfEmpty(pay.Remarks)))
                OfficialReceiptRepository.TxRegister(conn, tx, pay.OrNo, OfficialReceiptRepository.SourceBusinessTax, newId)
                If markPaid Then
                    Db.TxExecute(conn, tx,
                        "UPDATE business_permits SET status = @paid, surcharge = @surcharge, interest = @interest WHERE permit_id = @pid",
                        Db.P("@paid", BusinessPermitService.Paid), Db.P("@surcharge", finalSurcharge),
                        Db.P("@interest", finalInterest), Db.P("@pid", pay.PermitId))
                End If
            End Sub, duplicateOr)
        If duplicateOr Then Return -2
        Return If(ok, newId, -1)
    End Function

    ''' <summary>
    ''' Voids a payment (never deletes it) in ONE transaction: locks the permit, checks the payment is still the newest
    ''' active one, sets the void fields and - when the permit was Paid - puts it back to Assessed.
    ''' (An Issued permit stays Issued.) Returns 1 when voided, 0 if it is no longer the newest active payment, -1 on a DB error.
    ''' </summary>
    Public Shared Function VoidPayment(pay As BusinessPermitPayment, userId As Integer?, reason As String) As Integer
        Dim stillNewest = False
        Dim ok = Db.RunInTransaction(
            Sub(conn, tx)
                Dim status = Convert.ToString(Db.TxScalar(conn, tx,
                    "SELECT status FROM business_permits WHERE permit_id = @pid FOR UPDATE", Db.P("@pid", pay.PermitId)))
                Dim newest = Db.TxScalar(conn, tx,
                    "SELECT MAX(payment_id) FROM business_permit_payments WHERE permit_id = @pid AND voided_at IS NULL",
                    Db.P("@pid", pay.PermitId))
                stillNewest = newest IsNot Nothing AndAlso newest IsNot DBNull.Value AndAlso Convert.ToInt32(newest) = pay.PaymentId
                If Not stillNewest Then Return
                Db.TxExecute(conn, tx,
                    "UPDATE business_permit_payments SET voided_at = NOW(), voided_by = @uid, void_reason = @reason " &
                    "WHERE payment_id = @id AND voided_at IS NULL",
                    Db.P("@uid", userId), Db.P("@reason", reason.Trim()), Db.P("@id", pay.PaymentId))
                If status = BusinessPermitService.Paid Then
                    Db.TxExecute(conn, tx, "UPDATE business_permits SET status = @assessed WHERE permit_id = @pid",
                                 Db.P("@assessed", BusinessPermitService.Assessed), Db.P("@pid", pay.PermitId))
                End If
            End Sub)
        If Not ok Then Return -1
        Return If(stillNewest, 1, 0)
    End Function

    ''' <summary>Checks the permit's active payments still match (count and total). Call with the permit row locked.</summary>
    Private Shared Function ActivePaymentsUnchanged(conn As MySqlConnection, tx As MySqlTransaction, permitId As Integer,
                                                    expectedCount As Integer, expectedTotal As Decimal) As Boolean
        Dim count = Convert.ToInt32(Db.TxScalar(conn, tx,
            "SELECT COUNT(*) FROM business_permit_payments WHERE permit_id = @pid AND voided_at IS NULL", Db.P("@pid", permitId)))
        Dim total = Convert.ToDecimal(Db.TxScalar(conn, tx,
            "SELECT COALESCE(SUM(amount_paid + surcharge + interest), 0) FROM business_permit_payments " &
            "WHERE permit_id = @pid AND voided_at IS NULL", Db.P("@pid", permitId)))
        Return count = expectedCount AndAlso total = expectedTotal
    End Function

    ' ---------------- helpers ----------------

    Private Shared Function ToList(table As DataTable) As List(Of BusinessPermitPayment)
        Return table.Rows.Cast(Of DataRow)().Select(
            Function(row) New BusinessPermitPayment With {
                .PaymentId = row.GetInt("payment_id"),
                .PermitId = row.GetInt("permit_id"),
                .AmountPaid = row.GetDecimal("amount_paid"),
                .Surcharge = row.GetDecimal("surcharge"),
                .Interest = row.GetDecimal("interest"),
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
                .BusinessId = row.GetInt("business_id"),
                .PermitReferenceNo = row.GetString("permit_reference_no"),
                .PermitYear = row.GetInt("permit_year")
            }).ToList()
    End Function

End Class
