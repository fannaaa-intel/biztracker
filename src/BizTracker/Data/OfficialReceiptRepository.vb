Imports MySql.Data.MySqlClient

''' <summary>
''' The official_receipts registry: every OR number ever used (RPT and business tax, voided ones too).
''' Its UNIQUE or_no is what keeps an AF 51 number from being used twice - even when two cashiers save
''' at the same moment - because each payment registers its OR number in the SAME transaction as the payment.
''' </summary>
Public NotInheritable Class OfficialReceiptRepository

    Private Sub New()
    End Sub

    Public Const SourceRpt As String = "RPT"
    Public Const SourceBusinessTax As String = "BP"

    ''' <summary>True if the OR number was already used by any payment.</summary>
    Public Shared Function Exists(orNo As String) As Boolean
        Return Convert.ToInt32(Db.ExecuteScalar("SELECT COUNT(*) FROM official_receipts WHERE or_no = @or",
                                                Db.P("@or", If(orNo, "").Trim()))) > 0
    End Function

    ''' <summary>The OR number registered last (a hint for the next AF 51 serial), or "".</summary>
    Public Shared Function GetLastOrNo() As String
        Dim result = Db.ExecuteScalar("SELECT or_no FROM official_receipts ORDER BY receipt_id DESC LIMIT 1")
        Return If(result Is Nothing, "", result.ToString())
    End Function

    ''' <summary>Registers an OR number inside a payment's transaction (a duplicate fails with MySQL error 1062).</summary>
    Public Shared Sub TxRegister(conn As MySqlConnection, tx As MySqlTransaction, orNo As String, source As String, paymentId As Integer)
        Db.TxExecute(conn, tx, "INSERT INTO official_receipts (or_no, source, payment_id) VALUES (@or, @source, @pid)",
                     Db.P("@or", orNo.Trim()), Db.P("@source", source), Db.P("@pid", paymentId))
    End Sub

End Class
