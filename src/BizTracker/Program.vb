Friend Module Program

    <STAThread()>
    Friend Sub Main(args As String())
        Application.SetHighDpiMode(HighDpiMode.SystemAware)
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)
        ' Phase 1: temporary startup form with a Test Connection button.
        ' Phase 4 will change this to LoginForm.
        Application.Run(New StartupForm())
    End Sub

End Module
