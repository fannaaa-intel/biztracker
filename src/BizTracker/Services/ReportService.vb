Imports System.IO
Imports System.Net
Imports System.Text

''' <summary>Result of ReportService.Print.</summary>
Public Class PrintResult
    ''' <summary>Nothing when it worked.</summary>
    Public Property ErrorMessage As String
    ''' <summary>The saved HTML file.</summary>
    Public Property FilePath As String = ""
End Class

''' <summary>
''' Printable documents: fills an HTML template from Templates/ with the document's details
''' (LGU header from settings, reference no., dates, verification code), saves it and opens it in the
''' default browser, where the user prints it or saves it as PDF. Every value is HTML-encoded.
''' Only issued, unexpired documents can be printed; each print is written to the audit log (PRINT).
''' </summary>
Public NotInheritable Class ReportService

    Private Sub New()
    End Sub

    ''' <summary>Template file of each document kind.</summary>
    Public Shared Function GetTemplateName(kind As String) As String
        Select Case kind
            Case DocumentKinds.MayorsPermit : Return "mayors_permit.html"
            Case DocumentKinds.OrderOfPayment : Return "order_of_payment.html"
            Case DocumentKinds.SanitaryPermit : Return "sanitary_permit.html"
            Case DocumentKinds.HealthCertificate : Return "health_certificate.html"
            Case DocumentKinds.OfficialReceipt : Return "official_receipt.html"
            Case DocumentKinds.TaxClearance : Return "tax_clearance.html"
            Case DocumentKinds.InspectionCertificate : Return "inspection_certificate.html"
            Case Else : Return "construction_clearance.html"
        End Select
    End Function

    ''' <summary>The office that issues each kind (printed under the LGU name) and who signs it.</summary>
    Private Shared Function OfficeOf(d As IssuedDocument) As String()
        Select Case d.Kind
            Case DocumentKinds.MayorsPermit : Return {"Business Permits and Licensing Office", "Municipal Mayor"}
            Case DocumentKinds.OrderOfPayment : Return {"Business Permits and Licensing Office", "BPLO Assessor"}
            Case DocumentKinds.SanitaryPermit, DocumentKinds.HealthCertificate : Return {"Municipal Health Office", "Municipal Health Officer"}
            Case DocumentKinds.OfficialReceipt : Return {"Office of the Municipal Treasurer", "Collecting Officer"}
            Case DocumentKinds.TaxClearance : Return {"Office of the Municipal Treasurer", "Municipal Treasurer"}
            Case DocumentKinds.InspectionCertificate : Return {"Joint Inspection Team", "Team Leader, Joint Inspection Team"}
            Case Else
                Return If(d.ReferenceNo.StartsWith("LC-"),
                          {"Municipal Planning and Development Office", "Zoning Administrator"},
                          {"Office of the Building Official", "Municipal Building Official"})
        End Select
    End Function

    Public Shared Function GetTemplateFolder() As String
        Return Path.Combine(AppContext.BaseDirectory, "Templates")
    End Function

    Private Shared Function ReadTemplate(name As String) As String
        Return File.ReadAllText(Path.Combine(GetTemplateFolder(), name), Encoding.UTF8)
    End Function

    ''' <summary>The full HTML page of a document.</summary>
    Public Shared Function BuildHtml(d As IssuedDocument) As String
        Dim b = BusinessRepository.GetById(d.BusinessId)
        Dim lgu = SettingsRepository.GetValue("lgu_name", "Municipality")
        Dim office = OfficeOf(d)
        Dim isCard = d.Kind = DocumentKinds.HealthCertificate

        ' Values (encoded when inserted)
        Dim values As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
            {"lgu_name", lgu},
            {"province", SettingsRepository.GetValue("province", "")},
            {"seal", UiHelper.GetInitials(lgu.Replace("Municipality of", "").Replace("City of", "").Trim())},
            {"office", office(0)},
            {"signatory", office(1)},
            {"signatory_title", lgu},
            {"title", d.Title},
            {"reference_no", d.ReferenceNo},
            {"business_name", If(b?.BusinessName, d.BusinessName)},
            {"owner_name", If(b?.OwnerName, "")},
            {"address", If(b Is Nothing, "", b.Address & If(b.Barangay <> "", ", Brgy. " & b.Barangay, "") & ", " & lgu)},
            {"holder", d.Holder},
            {"issue_date", UiHelper.FormatDate(d.IssueDate)},
            {"valid_until", If(d.ValidUntil.HasValue, UiHelper.FormatDate(d.ValidUntil), "—")},
            {"valid_until_text", If(d.ValidUntil.HasValue, " · Valid until " & UiHelper.FormatDate(d.ValidUntil), "")},
            {"status", d.Status},
            {"verification_code", DocumentService.GetVerificationCode(d)},
            {"legal_basis", "RA 7160 · RA 11032"},
            {"printed_at", Date.Now.ToString("MMM d, yyyy h:mm tt")},
            {"printed_by", If(Session.FullName <> "", Session.FullName, "BizTracker")},
            {"page_size", If(isCard, "3.6in 2.4in", "A4")},
            {"sheet_width", If(isCard, "4.2in", "760px")},
            {"sheet_padding", If(isCard, "18px", "40px 48px")}
        }
        For Each kv In d.Fields
            values(kv.Key) = kv.Value
        Next

        ' Raw HTML pieces first: header / footer partials and the table rows (each cell encoded)
        Dim body = ReadTemplate(GetTemplateName(d.Kind))
        body = body.Replace("{{header}}", ReadTemplate("_header.html")).Replace("{{footer}}", ReadTemplate("_footer.html"))
        Dim rows As New StringBuilder()
        For Each r In d.Rows
            rows.Append("      <tr>")
            For i = 0 To r.Length - 1
                Dim isAmount = r(i).StartsWith(ChrW(&H20B1))     ' peso amounts align right
                rows.Append(If(isAmount, "<td class=""num"">", "<td>")).Append(WebUtility.HtmlEncode(r(i))).Append("</td>")
            Next
            rows.AppendLine("</tr>")
        Next
        If d.Rows.Count = 0 Then rows.AppendLine("      <tr><td colspan=""4"">None on record.</td></tr>")
        body = body.Replace("{{rows}}", rows.ToString())
        Dim page = ReadTemplate("_layout.html").Replace("{{body}}", body)

        ' Then every {{key}} with its encoded value; unknown keys become empty
        Return Text.RegularExpressions.Regex.Replace(page, "\{\{(\w+)\}\}",
            Function(m)
                Dim v As String = Nothing
                Return If(values.TryGetValue(m.Groups(1).Value, v), WebUtility.HtmlEncode(v), "")
            End Function)
    End Function

    ''' <summary>Where printed documents are saved (one HTML file per reference number).</summary>
    Public Shared Function GetOutputFolder() As String
        Return Path.Combine(Path.GetTempPath(), "BizTracker", "Documents")
    End Function

    ''' <summary>
    ''' Saves the document as HTML and opens it in the default browser (openInBrowser = False only saves it).
    ''' Checks access and the print rules, and logs PRINT in the audit log.
    ''' </summary>
    Public Shared Function Print(d As IssuedDocument, Optional openInBrowser As Boolean = True) As PrintResult
        Dim result As New PrintResult
        If d Is Nothing OrElse Not DocumentService.CanView() OrElse Not AccessService.CanSeeBusiness(d.BusinessId) Then
            result.ErrorMessage = "You are not allowed to print this document."
            Return result
        End If
        Dim blocker = DocumentService.GetPrintBlocker(d)
        If blocker IsNot Nothing Then
            result.ErrorMessage = blocker
            Return result
        End If
        Try
            Dim folder = GetOutputFolder()
            Directory.CreateDirectory(folder)
            Dim safeName = String.Concat(d.ReferenceNo.Select(Function(c) If(Path.GetInvalidFileNameChars().Contains(c), "_"c, c)))
            result.FilePath = Path.Combine(folder, safeName & ".html")
            File.WriteAllText(result.FilePath, BuildHtml(d), New UTF8Encoding(True))
            If openInBrowser Then Process.Start(New ProcessStartInfo(result.FilePath) With {.UseShellExecute = True})
        Catch ex As Exception
            result.ErrorMessage = "The document could not be created or opened:" & vbCrLf & ex.Message
            Return result
        End Try
        AuditService.Log(AuditActions.Print, d.TableName, d.RelatedId, "Printed " & d.Kind & " " & d.ReferenceNo & " (" & d.BusinessName & ")")
        Return result
    End Function

End Class
