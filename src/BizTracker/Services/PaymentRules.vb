Imports System.Text.RegularExpressions

''' <summary>What the cashier typed in a Record Payment dialog (RPT or business tax).</summary>
Public Class PaymentInput
    Public Property PaymentDate As Date = Date.Today
    Public Property Amount As Decimal
    Public Property Method As String = PaymentMethods.Cash
    Public Property ReferenceNo As String = ""
    Public Property OrNo As String = ""
    Public Property Remarks As String = ""
End Class

''' <summary>
''' Rules shared by every walk-in payment (Real Property Tax and business tax):
''' amount, date, method + reference, the OR number typed from the AF 51 booklet (unique across ALL payments,
''' voided ones too - see official_receipts), remarks and void reasons. Module services add their own checks
''' (what is due, paying in order) and call ValidateCommon.
''' Who may record / void is decided in AccessService.CanCollectPayments / CanVoidPayments.
''' </summary>
Public NotInheritable Class PaymentRules

    Private Sub New()
    End Sub

    Public Const MaxOrNoLength As Integer = 30          ' or_no columns
    Public Const MaxReferenceLength As Integer = 50     ' reference_no columns
    Public Const MaxRemarksLength As Integer = 255      ' remarks columns
    Public Const MaxVoidReasonLength As Integer = 200

    ''' <summary>Display status of a payment.</summary>
    Public Const Active As String = "Active"
    Public Const Voided As String = "Voided"

    ''' <summary>Shown when another cashier saved the same OR number a moment earlier.</summary>
    Public Shared Function OrTakenMessage(orNo As String) As String
        Return "OR " & If(orNo, "").Trim() & " was just used by another cashier. Use the next AF 51 number."
    End Function

    ''' <summary>
    ''' Checks what the cashier typed. Keys: "date", "amount", "method", "reference", "or", "remarks".
    '''   earliest / earliestMessage : the first allowed payment date and the message when it is earlier
    '''   totalDueOn                 : what is due on a payment date (the amount may not be more)
    ''' </summary>
    Public Shared Function ValidateCommon(input As PaymentInput, earliest As Date, earliestMessage As String,
                                          totalDueOn As Func(Of Date, Decimal)) As Dictionary(Of String, String)
        Dim errors As New Dictionary(Of String, String)
        Dim payDate = input.PaymentDate.Date

        ' Date: not in the future, not before the earliest allowed date
        If payDate > Date.Today Then
            errors("date") = "Cannot be in the future."
        ElseIf payDate < earliest.Date Then
            errors("date") = earliestMessage
        End If

        ' Amount: more than 0, whole centavos, at most what is due on that date
        If input.Amount <= 0D Then
            errors("amount") = "Enter more than 0."
        ElseIf Math.Round(input.Amount, 2) <> input.Amount Then
            errors("amount") = "At most 2 decimals."
        ElseIf totalDueOn IsNot Nothing AndAlso Not errors.ContainsKey("date") Then
            Dim due = totalDueOn(payDate)
            If input.Amount > due Then errors("amount") = "Balance is only " & UiHelper.FormatMoney(due) & "."
        End If

        ' Method + reference (every method except cash needs one)
        If Not PaymentMethods.All.Contains(input.Method) Then
            errors("method") = "Choose how it was paid."
        ElseIf PaymentMethods.NeedsReference(input.Method) Then
            Dim reference = If(input.ReferenceNo, "").Trim()
            If reference = "" Then
                errors("reference") = "Enter the " & If(input.Method = PaymentMethods.Check, "check number.", "transaction reference no.")
            ElseIf reference.Length > MaxReferenceLength Then
                errors("reference") = "At most " & MaxReferenceLength & " characters."
            End If
        End If

        ' OR number from the AF 51 booklet: unique across RPT and business tax payments
        Dim orNo = If(input.OrNo, "").Trim()
        If orNo = "" Then
            errors("or") = "Type the AF 51 OR number."
        ElseIf orNo.Length > MaxOrNoLength Then
            errors("or") = "At most " & MaxOrNoLength & " characters."
        ElseIf Not Regex.IsMatch(orNo, "^[A-Za-z0-9-]+$") Then
            errors("or") = "Letters, numbers, dashes only."
        ElseIf OfficialReceiptRepository.Exists(orNo) Then
            errors("or") = "OR " & orNo & " already used."
        End If

        If If(input.Remarks, "").Trim().Length > MaxRemarksLength Then errors("remarks") = "At most " & MaxRemarksLength & " characters."
        Return errors
    End Function

    ''' <summary>Nothing when the void reason is acceptable.</summary>
    Public Shared Function ValidateVoidReason(reason As String) As String
        reason = If(reason, "").Trim()
        If reason = "" Then Return "Enter the reason for voiding."
        If reason.Length > MaxVoidReasonLength Then Return "The reason can be at most " & MaxVoidReasonLength & " characters."
        Return Nothing
    End Function

End Class
