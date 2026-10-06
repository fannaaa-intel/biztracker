''' <summary>One row of the rpt_assessments table (one property, one tax year).</summary>
Public Class RptAssessment
    Public Property AssessmentId As Integer
    Public Property PropertyId As Integer
    Public Property TaxYear As Integer
    Public Property BasicTax As Decimal             ' rpt_basic_rate x assessed value
    Public Property SefTax As Decimal               ' rpt_sef_rate x assessed value
    Public Property TotalDue As Decimal             ' annual total (paid in 4 quarters)
    Public Property CreatedAt As Date
End Class
