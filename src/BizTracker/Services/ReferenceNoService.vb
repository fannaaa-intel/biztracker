''' <summary>
''' Generates reference numbers in the format PREFIX-YYYY-00001.
''' The next number continues from the highest one already in the database for that prefix and year.
''' (The UNIQUE constraints in the database are the final guard against duplicates.)
''' </summary>
Public NotInheritable Class ReferenceNoService

    Private Sub New()
    End Sub

    ' ---------- Prefixes ----------
    Public Const BusinessPermit As String = "BP"          ' business_permits.reference_no
    Public Const MayorsPermit As String = "MP"            ' business_permits.mayors_permit_no
    Public Const SanitaryPermit As String = "SP"          ' sanitary_permits.permit_no
    Public Const HealthCertificate As String = "HC"       ' health_certificates.certificate_no
    Public Const Inspection As String = "INS"             ' inspections.reference_no
    Public Const InspectionCertificate As String = "AIC"  ' inspections.certificate_no
    Public Const ConstructionProject As String = "CP"     ' construction_projects.reference_no
    Public Const OfficialReceipt As String = "OR"         ' rpt_payments.or_no

    ''' <summary>
    ''' Next number for a prefix, e.g. GetNext("BP") -> "BP-2026-00004".
    ''' Year defaults to the current year (business permits pass permit_year).
    ''' </summary>
    Public Shared Function GetNext(prefix As String, Optional year As Integer = 0) As String
        If year = 0 Then year = Date.Today.Year
        Dim lastSequence = ReferenceNoRepository.GetLastSequence(prefix, year)
        Return FormatNumber(prefix, year, lastSequence + 1)
    End Function

    ''' <summary>Clearance numbers use a prefix per type: LC / FSEC / BLDG / OCC.</summary>
    Public Shared Function GetNextClearanceNo(clearanceType As String, Optional year As Integer = 0) As String
        Return GetNext(GetClearancePrefix(clearanceType), year)
    End Function

    Public Shared Function GetClearancePrefix(clearanceType As String) As String
        Select Case clearanceType
            Case ClearanceTypes.Locational : Return "LC"
            Case ClearanceTypes.FSEC : Return "FSEC"
            Case ClearanceTypes.Building : Return "BLDG"
            Case ClearanceTypes.Occupancy : Return "OCC"
            Case Else : Throw New ArgumentException("Unknown clearance type: " & clearanceType)
        End Select
    End Function

    ''' <summary>FormatNumber("BP", 2026, 7) -> "BP-2026-00007".</summary>
    Public Shared Function FormatNumber(prefix As String, year As Integer, sequence As Integer) As String
        Return prefix & "-" & year.ToString() & "-" & sequence.ToString("D5")
    End Function

End Class
