namespace PApps;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        if (args.Length == 2 && args[0] == "--apply-update")
        {
            AppUpdater.Apply(args[1]);
            return;
        }
        var needsSetup = true;
        try { needsSetup = SettingsStore.NeedsSetup(SettingsStore.Load()); } catch { }
        if (needsSetup)
        {
            using var setup = new Launcher();
            setup.ShowDialog();
            try { if (SettingsStore.NeedsSetup(SettingsStore.Load())) return; }
            catch { return; }
        }
        Application.Run(new MainWindow());
    }    
}

