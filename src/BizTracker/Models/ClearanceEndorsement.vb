''' <summary>One row of the clearance_endorsements table (one office's endorsement of a business permit).</summary>
Public Class ClearanceEndorsement
    Public Property EndorsementId As Integer
    Public Property PermitId As Integer
    Public Property Office As String = ""           ' Barangay / Sanitary / RPT / Fire / Zoning
    Public Property Status As String = "Pending"    ' Pending / Endorsed / Rejected
    Public Property EndorsedBy As Integer?
    Public Property EndorsedAt As Date?
    Public Property Remarks As String = ""

    ' Display only (filled from a JOIN, not saved)
    Public Property EndorsedByName As String = ""
End Class

''' <summary>
''' An automatic endorsement change worked out by BusinessPermitService (e.g. RPT -> Endorsed when all dues
''' are paid), so it can be saved inside the same transaction as the action that caused it.
''' </summary>
Public Class EndorsementChange
    Public Property PermitId As Integer
    Public Property ReferenceNo As String = ""      ' the business permit application, e.g. BP-2027-00001
    Public Property Office As String = ""
    Public Property OldStatus As String = ""
    Public Property NewStatus As String = ""
    Public Property UserId As Integer?
    Public Property Remarks As String = ""
End Class
