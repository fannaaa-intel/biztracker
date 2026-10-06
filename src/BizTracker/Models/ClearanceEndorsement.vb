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
