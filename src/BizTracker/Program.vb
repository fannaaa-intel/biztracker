Friend Module Program

    <STAThread()>
    Friend Sub Main(args As String())
        Application.SetHighDpiMode(HighDpiMode.SystemAware)
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)

        ' Never crash: show a friendly message for any unexpected error.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException)
        AddHandler Application.ThreadException,
            Sub(sender, e) UiHelper.ShowError("Something went wrong:" & vbCrLf & e.Exception.Message &
                                              vbCrLf & vbCrLf & "Please try again.", "Unexpected error")

        ' Login -> main window. Logging out returns to the login screen;
        ' closing either window exits the app.
        Do
            Using login As New LoginForm()
                If login.ShowDialog() <> DialogResult.OK Then Exit Do
            End Using
            Using main As New MainForm()
                main.ShowDialog()
                If Not main.LoggedOut Then Exit Do
            End Using
        Loop
    End Sub

End Module
