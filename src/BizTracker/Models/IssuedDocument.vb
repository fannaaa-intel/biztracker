''' <summary>The kinds of printable documents (one HTML template each).</summary>
Public NotInheritable Class DocumentKinds
    Private Sub New()
    End Sub
    Public Const MayorsPermit As String = "Mayor's Permit"
    Public Const OrderOfPayment As String = "Tax Order of Payment"
    Public Const SanitaryPermit As String = "Sanitary Permit"
    Public Const HealthCertificate As String = "Health Certificate"
    Public Const OfficialReceipt As String = "RPT Official Receipt"
    Public Const TaxClearance As String = "RPT Tax Clearance"
    Public Const InspectionCertificate As String = "Annual Inspection Certificate"
    Public Const ConstructionClearance As String = "Construction Clearance"

    Public Shared ReadOnly All As String() =
        {MayorsPermit, OrderOfPayment, SanitaryPermit, HealthCertificate, OfficialReceipt, TaxClearance,
         InspectionCertificate, ConstructionClearance}
End Class

''' <summary>
''' One issued document in the Permit Vault (display only - built from the module tables, not stored).
''' Status is computed: Valid / Expiring Soon / Expired for documents with a validity date,
''' otherwise Issued / Paid / Approved.
''' </summary>
Public Class IssuedDocument
    Public Property Kind As String = ""
    ''' <summary>Title printed on the document, e.g. "Mayor's Permit" or "Certificate of Occupancy".</summary>
    Public Property Title As String = ""
    Public Property ReferenceNo As String = ""
    Public Property BusinessId As Integer
    Public Property BusinessName As String = ""
    ''' <summary>Who / what it is issued for: the business, an employee, a property (PIN) or a project.</summary>
    Public Property Holder As String = ""
    Public Property IssueDate As Date
    Public Property ValidUntil As Date?
    Public Property Status As String = ""
    ''' <summary>The record the document comes from (permit_id, cert_id, payment_id, ...).</summary>
    Public Property RelatedId As Integer
    ''' <summary>Table of that record (for the PRINT audit row).</summary>
    Public Property TableName As String = ""
    ''' <summary>The module screen that manages it.</summary>
    Public Property Screen As AppScreen
    ''' <summary>Text the verification code is computed from (kept stable for the life of the document).</summary>
    Public Property CodeSeed As String = ""
    ''' <summary>Extra values for the template ({{key}} -> value).</summary>
    Public Property Fields As New Dictionary(Of String, String)
    ''' <summary>Extra table rows for the template (already-safe cells are encoded by ReportService).</summary>
    Public Property Rows As New List(Of String())
End Class
