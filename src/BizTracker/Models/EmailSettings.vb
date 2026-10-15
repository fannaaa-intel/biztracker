''' <summary>
''' Gmail account used to send e-mails, read from email-settings.json next to the .exe.
''' That file is NOT in Git (it holds the App Password); see docs/EMAIL_SETUP.md.
''' </summary>
Public Class EmailSettings
    Public Property GmailAddress As String = ""
    ''' <summary>16-letter Gmail App Password (not the normal Gmail password).</summary>
    Public Property AppPassword As String = ""
    ''' <summary>Name shown as the sender, e.g. "BizTracker - Municipality of Aparri".</summary>
    Public Property SenderName As String = "BizTracker"
End Class
