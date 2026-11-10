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
        var validRelease = System.Text.Json.JsonSerializer.Serialize(new
        {
            draft = false, prerelease = false, tag_name = "v9.2.1",
            assets = new[] { new { name = "PApps.exe", browser_download_url = "https://github.com/Bodinxx/PApps/releases/download/v9.2.1/PApps.exe", digest = "sha256:" + new string('a', 64), size = 100 } }
        });
        if (AppUpdater.ParseRelease(validRelease).Version != new Version(9, 2, 1)) throw new Exception("Release version parsing failed");
        foreach (var tag in new[] { "v9.2.1-beta.2", "v9.2.1-rc.1", "rc-9.2.1-1", "beta-9.2.1-2" })
        {
            var testing = validRelease.Replace("v9.2.1", tag).Replace("\"prerelease\":false", "\"prerelease\":true");
            var parsed = AppUpdater.ParseRelease(testing, true);
            if (parsed.Version != new Version(9, 2, 1) || !parsed.IsPrerelease) throw new Exception("Prerelease parsing failed: " + tag);
            try { AppUpdater.ParseRelease(testing); throw new Exception("Stable channel accepted a prerelease"); }
            catch (InvalidDataException) { }
        }
        var olderStable = validRelease.Replace("\"assets\":", "\"published_at\":\"2026-01-01T00:00:00Z\",\"assets\":");
        var newerBeta = validRelease.Replace("v9.2.1", "v9.2.1-beta.3").Replace("\"prerelease\":false", "\"prerelease\":true").Replace("\"assets\":", "\"published_at\":\"2026-02-01T00:00:00Z\",\"assets\":");
        var feed = "[" + olderStable + "," + newerBeta + "]";
        if (AppUpdater.SelectLatest(feed, false)!.Tag != "v9.2.1" || AppUpdater.SelectLatest(feed, true)!.Tag != "v9.2.1-beta.3") throw new Exception("Channel selection failed");
        var sameVersionBeta = new UpdateRelease(AppUpdater.CurrentVersion, "", new string('a', 64), 100, "rc-0.2.3-2", true);
        if (!AppUpdater.IsUpdate(sameVersionBeta, true, new string('b', 64), false)) throw new Exception("Same-version candidate update missing");
        if (AppUpdater.IsUpdate(sameVersionBeta, true, new string('a', 64), true)) throw new Exception("Identical candidate offered repeatedly");
        if (AppUpdater.IsUpdate(sameVersionBeta, false, new string('b', 64), false)) throw new Exception("Stable channel offered beta");
        if (AppUpdater.IsUpdate(sameVersionBeta with { Version = new Version(0, 0, 1) }, true, new string('b', 64), true)) throw new Exception("Testing channel offered a numeric downgrade");
        if (!AppUpdater.IsUpdate(sameVersionBeta with { IsPrerelease = false }, false, new string('b', 64), true)) throw new Exception("Candidate to stable transition failed");
        foreach (var invalid in new[] { validRelease.Replace("sha256:", "md5:"), validRelease.Replace("Bodinxx/PApps/releases/download/", "other/project/releases/download/"), validRelease.Replace("v9.2.1", "v9.2.1-beta"), validRelease.Replace("\"draft\":false", "\"draft\":true") })
        {
            try { AppUpdater.ParseRelease(invalid); throw new Exception("Invalid release accepted"); }
            catch (InvalidDataException) { }
        }
        var root = Path.Combine(Path.GetTempPath(), "PApps-check-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "Apps", "EditorPortable", "App", "AppInfo"));
        File.WriteAllText(Path.Combine(root, "Apps", "EditorPortable", "EditorPortable.exe"), "fixture");
        File.WriteAllText(Path.Combine(root, "IgnoredOutsideApps.exe"), "fixture");
        var checksumFile = Path.Combine(root, "hash-test.txt");
        File.WriteAllText(checksumFile, "checksum fixture");
        var checksum = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(checksumFile)));
        AppUpdater.Verify(checksumFile, checksum);
        try { AppUpdater.Verify(checksumFile, new string('0', 64)); throw new Exception("Corrupt update accepted"); }
        catch (InvalidDataException) { }
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
        var tabs = form.Controls.OfType<TableLayoutPanel>().Single().Controls.OfType<TabControl>().Single();
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
        var testSettings = Path.Combine(AppContext.BaseDirectory, "launcher.settings.json");
        File.WriteAllText(testSettings, "{\"Repository\":\"\",\"SetupFontFace\":\"Arial\",\"FutureOption\":{\"preserve\":true},\"Apps\":[{\"Name\":\"Existing app\",\"Category\":\"Custom\",\"Description\":\"Keep me\",\"FutureAppOption\":42}]}");
        using (var migrated = new Launcher())
        {
            typeof(Launcher).GetMethod("LoadSettings", flags)!.Invoke(migrated, null);
            using var migratedJson = System.Text.Json.JsonDocument.Parse(File.ReadAllText(testSettings));
            var saved = migratedJson.RootElement;
            if (saved.GetProperty("SetupFontFace").GetString() != "Arial" || !saved.GetProperty("FutureOption").GetProperty("preserve").GetBoolean()
                || saved.GetProperty("Apps")[0].GetProperty("FutureAppOption").GetInt32() != 42 || saved.GetProperty("Apps")[0].GetProperty("Description").GetString() != "Keep me"
                || !saved.TryGetProperty("UseLocalFonts", out _)) throw new Exception("Non-destructive settings migration failed");
        }
        File.Delete(testSettings);
        using (var firstRun = new Launcher())
        {
            typeof(Launcher).GetMethod("LoadSettings", flags)!.Invoke(firstRun, null);
            if (!File.Exists(testSettings)) throw new Exception("First-run settings creation failed");
        }
        if (!SettingsStore.NeedsSetup(new LauncherSettings { Repository = root })) throw new Exception("New location bypasses Setup");
        if (SettingsStore.NeedsSetup(new LauncherSettings { Repository = root, SetupCompletedFor = root })) throw new Exception("Configured location still requires Setup");
        if (!SettingsStore.NeedsSetup(new LauncherSettings { Repository = root, SetupCompletedFor = Path.Combine(root, "another") })) throw new Exception("Changed location bypasses Setup");
        var workspace = new LauncherSettings
        {
            Repository = root, SetupCompletedFor = root,
            Apps = [new AppEntry { Name = "Editor", Category = "Development", Favourite = true }, new AppEntry { Name = "Grid", Category = "Office", Description = "Spreadsheet editor" }]
        };
        SettingsStore.Save(workspace);
        using (var main = new MainWindow())
        {
            main.Reload();
            var mainSearch = (TextBox)typeof(MainWindow).GetField("searchBox", flags)!.GetValue(main)!;
            mainSearch.Text = "spreadsheet";
            if (main.FilteredApps().Single().Name != "Grid") throw new Exception("Main description search failed");
            mainSearch.Text = "development";
            if (main.FilteredApps().Single().Name != "Editor") throw new Exception("Main category search failed");
            mainSearch.Text = "no match";
            if (main.FilteredApps().Any()) throw new Exception("Main empty search failed");
            mainSearch.Text = "";
            var picker = (ComboBox)typeof(MainWindow).GetField("viewPicker", flags)!.GetValue(main)!;
            foreach (var view in new[] { "Large Icons", "Medium Icons", "Small Icons", "List", "Large Tiles", "Medium Tiles", "Small Tiles", "Detailed Tiles" })
            {
                picker.SelectedItem = view; main.RenderApps();
                var favPanel = (FlowLayoutPanel)typeof(MainWindow).GetField("favourites", flags)!.GetValue(main)!;
                using var expectedCard = new AppCard(workspace.Apps[0], "Editor", null, view, main.Font, () => { }, () => { }, new ToolTip());
                if (favPanel.Controls[0].Height != expectedCard.Height) throw new Exception("Favourites view did not track library: " + view);
            }
            var savedMain = SettingsStore.Load();
            if (!savedMain.Apps[0].Favourite || savedMain.MainView != "Detailed Tiles") throw new Exception("Main preferences persistence failed");
            using var spacingTips = new ToolTip();
            using var compact = new AppCard(workspace.Apps[0], "Editor", null, "Small Tiles", main.Font, () => { }, () => { }, spacingTips, 5, 5);
            using var spacious = new AppCard(workspace.Apps[0], "Editor", null, "Small Tiles", main.Font, () => { }, () => { }, spacingTips, 15, 12);
            if (spacious.Height - compact.Height != 20 || compact.Margin.Bottom != 5 || spacious.Margin.Right != 12) throw new Exception("Theme spacing not applied");
            savedMain.AppItemPadding = 7; savedMain.AppItemGap = 3; savedMain.IncludePrereleases = true;
            SettingsStore.Save(savedMain);
            var spacingSaved = SettingsStore.Load();
            if (spacingSaved.AppItemPadding != 7 || spacingSaved.AppItemGap != 3 || !spacingSaved.IncludePrereleases) throw new Exception("Theme spacing or update channel persistence failed");
            var mainSettings = (LauncherSettings)typeof(MainWindow).GetField("settings", flags)!.GetValue(main)!;
            for (var i = 0; i < 15; i++) mainSettings.Apps.Add(new AppEntry { Name = "Utility " + i, Category = "Utilities" });
            SettingsStore.Save(mainSettings);
            main.StartPosition = FormStartPosition.Manual;
            main.Location = new Point(-20000, -20000);
            main.Show(); // Native scroll positions are not applied while the owning form is hidden.
            Application.DoEvents();
            var libraryPanel = (FlowLayoutPanel)typeof(MainWindow).GetField("library", flags)!.GetValue(main)!;
            libraryPanel.Dock = DockStyle.None;
            libraryPanel.Size = new Size(430, 180);
            _ = libraryPanel.Handle;
            foreach (var view in picker.Items.Cast<string>())
            {
                picker.SelectedItem = view;
                main.RenderApps();
                libraryPanel.PerformLayout();
                var lastSection = libraryPanel.Controls.OfType<FlowLayoutPanel>().Last();
                var lastCards = lastSection.Controls.OfType<FlowLayoutPanel>().Single();
                lastCards.PerformLayout();
                var lastCard = lastCards.Controls.Cast<Control>().Last();
                if (lastCard.Bottom > lastCards.ClientSize.Height || lastCards.Bottom > lastSection.ClientSize.Height)
                    throw new Exception("Last utility is clipped within its category: " + view);
                libraryPanel.AutoScrollPosition = new Point(0, int.MaxValue);
                libraryPanel.PerformLayout();
                var visibleBottom = lastSection.Top + lastCards.Top + lastCard.Bottom;
                if (visibleBottom > libraryPanel.ClientSize.Height)
                    throw new Exception("Last utility cannot be reached at maximum scroll: " + view + $" bottom={visibleBottom} viewport={libraryPanel.ClientSize.Height} scroll={libraryPanel.AutoScrollPosition} extent={libraryPanel.AutoScrollMinSize} display={libraryPanel.DisplayRectangle} section={lastSection.Bounds}");
            }
        }
        Directory.Delete(root, true);
        Console.WriteLine("PASS: Setup and main-interface checks, startup routing, search, favourites, view persistence, fonts, settings migration and update validation.");
    }
}
