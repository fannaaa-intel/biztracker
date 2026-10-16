Imports Xunit

''' <summary>Terms and Conditions on login (Revision 4E): who must accept.</summary>
Public Class TermsServiceTests

    <Theory>
    <InlineData(Nothing, "1.0", True)>        ' never accepted
    <InlineData("", "1.0", True)>
    <InlineData("1.0", "1.0", False)>        ' accepted the current version
    <InlineData(" 1.0 ", "1.0", False)>      ' spaces ignored
    <InlineData("1.0", "1.1", True)>         ' a new version: accept again
    Public Sub NeedsAcceptance(accepted As String, current As String, expected As Boolean)
        Assert.Equal(expected, TermsService.NeedsAcceptance(accepted, current))
    End Sub

End Class
