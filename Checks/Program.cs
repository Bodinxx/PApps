using System.Reflection;
using PApps;

internal static class Checks
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Length == 2 && args[0] == "--fonts")
        {
            using var catalog = new FontCatalog();
            var errors = catalog.Refresh(args[1], true);
            Console.WriteLine("Local: " + catalog.LocalCount);
            Console.WriteLine(string.Join("\n", catalog.Names.Where(catalog.IsLocal)));
            Console.WriteLine("Errors: " + string.Join(", ", errors));
            return;
        }
        if (args.Length == 1)
        {
            using var actual = new Launcher();
            var access = BindingFlags.NonPublic | BindingFlags.Instance;
            var library = (LauncherSettings)typeof(Launcher).GetField("settings", access)!.GetValue(actual)!;
            library.Repository = args[0];
            typeof(Launcher).GetMethod("Scan", access)!.Invoke(actual, null);
            var names = library.Apps.Select(a => a.Name).OrderBy(n => n).ToArray();
            if (!names.SequenceEqual(new[] { "gridcraft", "pdfcraft", "wordcraft" })) throw new Exception("Unexpected actual app discovery: " + string.Join(", ", names));
            Console.WriteLine("PASS actual repository: " + string.Join(", ", names));
            return;
        }
        var root = Path.Combine(Path.GetTempPath(), "PApps-check-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "Apps", "EditorPortable", "App", "AppInfo"));
        File.WriteAllText(Path.Combine(root, "Apps", "EditorPortable", "EditorPortable.exe"), "fixture");
        File.WriteAllText(Path.Combine(root, "IgnoredOutsideApps.exe"), "fixture");
        File.WriteAllText(Path.Combine(root, "Apps", "EditorPortable", "App", "AppInfo", "appinfo.ini"), "[Details]\nName=Example Editor\nCategory=Development\n[Control]\nStart=EditorPortable.exe");
        using var form = new Launcher();
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var settings = (LauncherSettings)typeof(Launcher).GetField("settings", flags)!.GetValue(form)!;
        settings.Repository = root;
        void Call(string name) => typeof(Launcher).GetMethod(name, flags)!.Invoke(form, null);
        Call("Scan");
        if (settings.Apps.Count != 1 || settings.Apps[0].Name != "Example Editor" || settings.Apps[0].Category != "Development") throw new Exception("PortableApps scan failed");
        settings.Apps[0].Executable = "EditorPortable\\EditorPortable.exe";
        settings.Apps[0].Category = "Custom category";
        Call("Scan");
        if (settings.Apps.Count != 1) throw new Exception("Duplicate scan failed");
        if (settings.Apps[0].Executable != "Apps\\EditorPortable\\EditorPortable.exe" || settings.Apps[0].Category != "Custom category") throw new Exception("Moved app metadata migration failed");
        settings.Apps[0].Category = "Development";
        settings.Apps[0].Description = "A custom description.\r\nSecond line.";
        Directory.CreateDirectory(Path.Combine(root, "Apps", "group", "nested", "gridcraft"));
        File.WriteAllText(Path.Combine(root, "Apps", "RootTool.exe"), "fixture");
        File.WriteAllText(Path.Combine(root, "Apps", "group", "nested", "gridcraft", "gridcraft.exe"), "fixture");
        File.WriteAllText(Path.Combine(root, "Apps", "group", "nested", "gridcraft", "gridcraft-cli.exe"), "fixture");
        File.WriteAllText(Path.Combine(root, "Apps", "EditorPortable", "App", "internal.exe"), "fixture");
        Call("Scan");
        if (settings.Apps.Count != 3 || !settings.Apps.Any(a => a.Name == "RootTool") || !settings.Apps.Any(a => a.Name == "gridcraft"))
            throw new Exception("Root/nested discovery or helper exclusion failed");
        Call("Scan");
        if (settings.Apps.Count != 3) throw new Exception("Nested duplicate scan failed");
        var search = (TextBox)typeof(Launcher).GetField("search", flags)!.GetValue(form)!;
        var list = (ListView)typeof(Launcher).GetField("apps", flags)!.GetValue(form)!;
        search.Text = "absent";
        if (list.Items.Count != 0) throw new Exception("Search exclusion failed");
        search.Text = "development";
        if (list.Items.Count != 1) throw new Exception("Category search failed");
        try
        {
            typeof(Launcher).GetMethod("Resolve", flags)!.Invoke(form, [new AppEntry { Executable = "..\\outside.exe" }]);
            throw new Exception("Traversal accepted");
        }
        catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { }
        using var reloaded = new Launcher();
        typeof(Launcher).GetMethod("LoadSettings", flags)!.Invoke(reloaded, null);
        var persisted = (LauncherSettings)typeof(Launcher).GetField("settings", flags)!.GetValue(reloaded)!;
        if (persisted.Apps.Count != 3 || persisted.Repository != root) throw new Exception("Persistence failed");
        if (persisted.Apps[0].Description != "A custom description.\r\nSecond line.") throw new Exception("Description did not survive scanning and saving");
        if (System.Text.Json.JsonSerializer.Deserialize<AppEntry>("{\"Name\":\"Old app\"}")!.Description != "") throw new Exception("Old entries need an empty description");
        search.Text = "";
        settings.Apps.Add(new AppEntry { Name = "Zulu Portable", Category = "AAA", Executable = "missing-z.exe" });
        settings.Apps.Add(new AppEntry { Name = "AlphaPortable", Category = "AAA", Executable = "missing-a.exe" });
        settings.HidePortable = true;
        Call("RefreshList");
        if (list.Items[0].Text != "Alpha" || list.Items[1].Text != "Zulu") throw new Exception("Category/title sort or Portable display failed");
        if (settings.Apps[^1].Name != "AlphaPortable") throw new Exception("Display option mutated saved names");
        Directory.CreateDirectory(Path.Combine(root, "documents"));
        Call("DetectDocuments");
        if (!settings.DocumentsFolder.Equals(Path.Combine(root, "Documents"), StringComparison.OrdinalIgnoreCase)) throw new Exception("Documents detection failed");
        var fontsFolder = Path.Combine(settings.DocumentsFolder, "Fonts");
        if (!Directory.Exists(fontsFolder)) throw new Exception("Default Fonts folder was not created");
        File.WriteAllText(Path.Combine(fontsFolder, "invalid.ttf"), "invalid font fixture");
        var systemFont = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
        if (!File.Exists(systemFont)) throw new Exception("Font test fixture unavailable");
        File.Copy(systemFont, Path.Combine(fontsFolder, "test.ttf"));
        Call("RefreshFonts");
        var fontItems = (ListBox)typeof(Launcher).GetField("fontList", flags)!.GetValue(form)!;
        var localCatalog = (FontCatalog)typeof(Launcher).GetField("fontCatalog", flags)!.GetValue(form)!;
        if (fontItems.Items.Count == 0 || !localCatalog.IsLocal((string)fontItems.Items[0])) throw new Exception("Refreshed local fonts not visible first");
        var tabs = form.Controls.OfType<TabControl>().Single();
        tabs.SelectedTab = tabs.TabPages.Cast<TabPage>().Single(t => t.Text == "Fonts");
        var responsive = (TableLayoutPanel)tabs.SelectedTab.Controls[0];
        responsive.Dock = DockStyle.None;
        responsive.Size = new Size(1000, 600);
        responsive.PerformLayout();
        var listHost = fontItems.Parent!;
        if (responsive.GetColumn(listHost) != 1) throw new Exception("Wide Fonts layout failed");
        responsive.Size = new Size(820, 600);
        responsive.PerformLayout();
        if (responsive.GetRow(listHost) != 1) throw new Exception("Narrow Fonts layout failed");
        using (var catalog = new FontCatalog())
        {
            var failures = catalog.Refresh(fontsFolder, true);
            if (catalog.LocalCount == 0 || !failures.Contains("invalid.ttf")) throw new Exception("Local font loading or invalid-font handling failed");
            var localName = catalog.Names.First(catalog.IsLocal);
            using var previewFont = catalog.Create(localName, 14);
            if (previewFont.FontFamily.Name != localName) throw new Exception("Private font selection failed");
            catalog.Refresh(fontsFolder, false);
            if (catalog.LocalCount != 0) throw new Exception("Disable local fonts failed");
            using var fallback = catalog.Create("PApps Missing Font Fixture", 10);
            if (fallback.FontFamily.Name != "Segoe UI") throw new Exception("Missing font fallback failed");
        }
        settings.SetupLargeFonts = true;
        settings.MainLargeFonts = true;
        Call("ApplyFonts");
        if (form.Font.Size != 13) throw new Exception("Setup font failed");
        Call("Save");
        using var preferencesReloaded = new Launcher();
        typeof(Launcher).GetMethod("LoadSettings", flags)!.Invoke(preferencesReloaded, null);
        var preferences = (LauncherSettings)typeof(Launcher).GetField("settings", flags)!.GetValue(preferencesReloaded)!;
        if (!preferences.HidePortable || !preferences.SetupLargeFonts || !preferences.MainLargeFonts || preferences.DocumentsFolder != settings.DocumentsFolder)
            throw new Exception("Preferences round trip failed");
        if (typeof(Launcher).GetMethod("Launch", flags) is not null) throw new Exception("Setup still has launch capability");
        settings.Apps[0].Description = new string('x', 700);
        Call("RefreshList");
        var descriptionItem = list.Items.Cast<ListViewItem>().Single(i => ReferenceEquals(i.Tag, settings.Apps[0]));
        if (descriptionItem.SubItems[3].Text.Length != 500 || settings.Apps[0].Description.Length != 700) throw new Exception("Description preview truncation failed");
        appsLayoutCheck();
        void appsLayoutCheck()
        {
            list.Columns[0].Width = 210;
            list.Columns[1].Width = 110;
            list.Columns[2].Width = 250;
            Call("CaptureLayout");
            if (!settings.ColumnWidths.SequenceEqual(new[] { 210, 110, 250 })) throw new Exception("Column sizing persistence failed");
        }
        var uninstallFolder = typeof(Launcher).GetMethod("UninstallFolder", flags)!;
        var target = (string)uninstallFolder.Invoke(form, [settings.Apps[0]])!;
        if (target != Path.Combine(root, "Apps", "EditorPortable")) throw new Exception("Wrong uninstall folder");
        foreach (var blocked in new[] { settings.Apps.Single(a => a.Name == "RootTool"), new AppEntry { Executable = "documents\\test.exe" }, new AppEntry { Executable = "..\\outside.exe" } })
        {
            try { uninstallFolder.Invoke(form, [blocked]); throw new Exception("Unsafe uninstall target accepted"); }
            catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { }
        }
        settings.Apps.Add(new AppEntry { Name = "Shared", Executable = "Apps\\EditorPortable\\second.exe" });
        try { uninstallFolder.Invoke(form, [settings.Apps[0]]); throw new Exception("Shared app folder accepted"); }
        catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { }
        Directory.Delete(root, true);
        Console.WriteLine("PASS: discovery, search, containment, persistence, category/title sorting, display names, Documents detection, font preferences, setup-only behavior.");
    }
}

