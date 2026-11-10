using System.Text.Json;

namespace PApps;

public sealed class AppEntry
{
    public string Name { get; set; } = "";
    public string Executable { get; set; } = "";
    public string Category { get; set; } = "Other";
    public string Description { get; set; } = "";
}

public sealed class LauncherSettings
{
    public string Repository { get; set; } = "";
    public List<AppEntry> Apps { get; set; } = [];
    public string SetupFontFace { get; set; } = "Segoe UI";
    public bool SetupLargeFonts { get; set; }
    public string MainFontFace { get; set; } = "Segoe UI";
    public bool MainLargeFonts { get; set; }
    public bool HidePortable { get; set; }
    public string DocumentsFolder { get; set; } = "";
    public int WindowWidth { get; set; } = 1040;
    public int WindowHeight { get; set; } = 760;
    public bool WindowMaximized { get; set; }
    public int[] ColumnWidths { get; set; } = [270, 140, 380];
    public bool UseLocalFonts { get; set; } = true;
}

public sealed class Launcher : Form
{
    readonly string settingsPath = Path.Combine(AppContext.BaseDirectory, "launcher.settings.json");
    LauncherSettings settings = new();
    readonly TextBox search = new() { PlaceholderText = "Search your apps…", Dock = DockStyle.Fill };
    readonly ListView apps = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false };
    readonly Label location = new() { AutoEllipsis = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    readonly Label status = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    readonly ImageList appIcons = new() { ImageSize = new Size(24, 24), ColorDepth = ColorDepth.Depth32Bit };
    readonly Dictionary<string, int> iconIndexes = new(StringComparer.OrdinalIgnoreCase);
    readonly Label documents = new() { AutoSize = true, MaximumSize = new Size(730, 0) };
    readonly CheckBox hidePortable = new() { Text = "Hide Portable in application names", AutoSize = true };
    readonly CheckBox setupLarge = new() { Text = "Large fonts", AutoSize = true };
    readonly CheckBox mainLarge = new() { Text = "Large fonts", AutoSize = true };
    readonly ComboBox setupFace = new() { Width = 230, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly ComboBox mainFace = new() { Width = 230, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly Label mainPreview = new() { Text = "Application title · Category", AutoSize = true };
    bool loadingPreferences;
    bool layoutReady;
    bool sizingDescription;
    readonly System.Windows.Forms.Timer layoutSaveTimer = new() { Interval = 450 };
    readonly FontCatalog fontCatalog = new();
    readonly CheckBox useLocalFonts = new() { Text = "Use fonts from Documents\\Fonts in PApps", AutoSize = true };
    readonly Label fontsLocation = new() { AutoSize = true, MaximumSize = new Size(850, 0) };
    readonly Label fontsStatus = new() { AutoSize = true };
    readonly TextBox fontSearch = new() { PlaceholderText = "Search font names…", Dock = DockStyle.Fill };
    readonly ListBox fontList = new() { Dock = DockStyle.Fill, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 42, IntegralHeight = false };
    readonly CheckBox localFontsOnly = new() { Text = "Show PApps local fonts only", AutoSize = true };

    public Launcher()
    {
        Text = "PApps • Setup";
        MinimumSize = new Size(860, 620);
        Size = new Size(1040, 760);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(245, 247, 250);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 6 };
        foreach (var height in new[] { 52, 44, 44, 44 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.Controls.Add(new Label { Text = "PApps Setup", Font = new Font("Segoe UI", 24, FontStyle.Bold), Dock = DockStyle.Fill }, 0, 0);
        var folderRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        folderRow.Controls.Add(location);
        folderRow.Controls.Add(Button("Choose main folder", ChooseFolder));
        layout.Controls.Add(folderRow, 0, 1);
        layout.Controls.Add(search, 0, 2);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill };
        actions.Controls.AddRange([Button("Scan folder", Scan), Button("Add app", Add), Button("Edit app", Edit), Button("Remove", Remove), Button("Uninstall", Uninstall)]);
        layout.Controls.Add(actions, 0, 3);
        apps.Columns.Add("Application", 270);
        apps.Columns.Add("Category", 140);
        apps.Columns.Add("Location", 380);
        apps.Columns.Add("Description", 320);
        apps.SmallImageList = appIcons;
        appIcons.Images.Add(SystemIcons.Application);
        FormClosed += (_, _) => { appIcons.Dispose(); layoutSaveTimer.Dispose(); };
        Disposed += (_, _) => fontCatalog.Dispose();
        layout.Controls.Add(apps, 0, 4);
        layout.Controls.Add(status, 0, 5);
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var libraryTab = new TabPage("Applications");
        libraryTab.Controls.Add(layout);
        var preferencesTab = new TabPage("Settings") { AutoScroll = true };
        preferencesTab.Controls.Add(BuildPreferences());
        var getAppsTab = new TabPage("Get Apps");
        getAppsTab.Controls.Add(BuildGetApps());
        var fontsTab = new TabPage("Fonts");
        fontsTab.Controls.Add(BuildFonts());
        tabs.TabPages.AddRange([libraryTab, preferencesTab, fontsTab, new TabPage("Theming"), getAppsTab]);
        Controls.Add(tabs);
        search.TextChanged += (_, _) => RefreshList();
        apps.DoubleClick += (_, _) => Edit();
        apps.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Edit(); }
            if (e.KeyCode == Keys.F2) { e.SuppressKeyPress = true; Edit(); }
        };
        Shown += (_, _) => LoadSettings();
        apps.SizeChanged += (_, _) => SizeDescription();
        apps.ColumnWidthChanged += (_, e) =>
        {
            if (sizingDescription) return;
            SizeDescription();
            QueueLayoutSave();
        };
        apps.ColumnWidthChanging += (_, e) =>
        {
            if (e.ColumnIndex == 3) { e.Cancel = true; e.NewWidth = apps.Columns[3].Width; }
        };
        Resize += (_, _) => QueueLayoutSave();
        layoutSaveTimer.Tick += (_, _) => { layoutSaveTimer.Stop(); CaptureLayout(); Save(); };
        FormClosing += (_, _) => { if (layoutReady) { layoutSaveTimer.Stop(); CaptureLayout(); Save(); } };
    }

    void SizeDescription()
    {
        if (apps.Columns.Count < 4 || sizingDescription) return;
        sizingDescription = true;
        try
        {
            var remaining = apps.ClientSize.Width - apps.Columns.Cast<ColumnHeader>().Take(3).Sum(c => c.Width) - SystemInformation.VerticalScrollBarWidth - 4;
            apps.Columns[3].Width = remaining >= 160 ? remaining : 320;
        }
        finally { sizingDescription = false; }
    }

    void QueueLayoutSave()
    {
        if (!layoutReady || WindowState == FormWindowState.Minimized) return;
        layoutSaveTimer.Stop();
        layoutSaveTimer.Start();
    }

    void CaptureLayout()
    {
        var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        settings.WindowWidth = bounds.Width;
        settings.WindowHeight = bounds.Height;
        if (WindowState != FormWindowState.Minimized) settings.WindowMaximized = WindowState == FormWindowState.Maximized;
        settings.ColumnWidths = apps.Columns.Cast<ColumnHeader>().Take(3).Select(c => c.Width).ToArray();
    }

    void RestoreLayout()
    {
        var area = Screen.FromControl(this).WorkingArea;
        MinimumSize = new Size(Math.Min(860, area.Width), Math.Min(620, area.Height));
        Size = new Size(Math.Clamp(settings.WindowWidth, MinimumSize.Width, area.Width), Math.Clamp(settings.WindowHeight, MinimumSize.Height, area.Height));
        Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + (area.Height - Height) / 2);
        if (settings.ColumnWidths is { Length: >= 3 })
            for (var i = 0; i < 3; i++) apps.Columns[i].Width = Math.Clamp(settings.ColumnWidths[i], 50, 3000);
        if (settings.WindowMaximized) WindowState = FormWindowState.Maximized;
        SizeDescription();
        layoutReady = true;
    }

    static string DescriptionPreview(string? text)
    {
        var value = (text ?? "").Replace("\r", " ").Replace("\n", " ");
        if (value.Length <= 500) return value;
        var length = char.IsHighSurrogate(value[499]) ? 499 : 500;
        return value[..length];
    }

    Control BuildGetApps()
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(24) };
        panel.Controls.Add(new Label { Text = "Find portable apps", AutoSize = true, Font = new Font(Font, FontStyle.Bold) });
        panel.Controls.Add(new Label { Text = "Browse these independent websites in your browser.", AutoSize = true });
        foreach (var site in new[]
        {
            ("PortableApps.com", "https://portableapps.com/apps", "A directory of apps packaged in the PortableApps format."),
            ("The Portable Freeware Collection", "https://www.portablefreeware.com/", "A searchable collection with portability and extraction notes."),
            ("winPenPack", "https://www.winpenpack.com/en/", "A collection of portable software and portable software packs.")
        })
        {
            var link = new LinkLabel { Text = site.Item1, AutoSize = true, Margin = new Padding(3, 24, 3, 4) };
            var url = site.Item2;
            link.LinkClicked += (_, _) =>
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
                catch (Exception ex) { ShowError("Could not open the website in your browser.", ex); }
            };
            panel.Controls.Add(link);
            panel.Controls.Add(new Label { Text = site.Item3, AutoSize = true, MaximumSize = new Size(720, 0) });
        }
        panel.Controls.Add(new Label
        {
            Text = "Download and extract an app into the Apps subfolder of your main folder, then return to Applications and choose Scan folder. Use Add app if needed.",
            AutoSize = true, MaximumSize = new Size(720, 0), Margin = new Padding(3, 30, 3, 3)
        });
        return panel;
    }

    Control BuildPreferences()
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(24) };
        hidePortable.Margin = new Padding(3, 12, 3, 12);
        panel.Controls.Add(hidePortable);
        panel.Controls.Add(new Label { Text = "My Documents", AutoSize = true, Font = new Font(Font, FontStyle.Bold) });
        panel.Controls.Add(documents);
        panel.Controls.Add(Button("Refresh documents folder", () => { DetectDocuments(); RefreshFonts(); Save(); }));
        var updates = Button("Check for main app update…", () => MessageBox.Show(this,
            "The Git update location has not been configured yet. No update check was performed.", "Main app updates", MessageBoxButtons.OK, MessageBoxIcon.Information));
        updates.Margin = new Padding(3, 22, 3, 3);
        panel.Controls.Add(updates);
        panel.Controls.Add(new Label { Text = "Update source: not configured", AutoSize = true });
        hidePortable.CheckedChanged += (_, _) => PreferencesChanged();
        return panel;
    }

    Control BuildFonts()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 2, RowCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle());
        layout.ColumnStyles.Add(new ColumnStyle());
        layout.RowStyles.Add(new RowStyle());
        layout.RowStyles.Add(new RowStyle());
        var optionsHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = new Padding(0, 0, 16, 0) };
        var panel = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        optionsHost.Controls.Add(panel);
        panel.Controls.Add(useLocalFonts);
        panel.Controls.Add(fontsLocation);
        var actions = new FlowLayoutPanel { AutoSize = true };
        actions.Controls.Add(Button("Open fonts folder", () =>
        {
            try
            {
                DetectDocuments();
                if (settings.DocumentsFolder.Length == 0) { MessageBox.Show(this, "Choose an available main apps folder first."); return; }
                var folder = Path.Combine(settings.DocumentsFolder, "Fonts");
                Directory.CreateDirectory(folder);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(folder) { UseShellExecute = true });
            }
            catch (Exception ex) { ShowError("Could not open the fonts folder.", ex); }
        }));
        actions.Controls.Add(Button("Refresh fonts", RefreshFonts));
        panel.Controls.Add(actions);
        panel.Controls.Add(new Label { Text = "Add .ttf, .otf or .ttc files to this folder, then refresh. Fonts are available only inside PApps.", AutoSize = true });
        panel.Controls.Add(new Label { Text = "Setup screen fonts", AutoSize = true, Font = new Font(Font, FontStyle.Bold) });
        panel.Controls.Add(setupFace);
        panel.Controls.Add(setupLarge);
        panel.Controls.Add(new Label { Text = "Main interface fonts", AutoSize = true, Margin = new Padding(3, 22, 3, 3), Font = new Font(Font, FontStyle.Bold) });
        panel.Controls.Add(mainFace);
        panel.Controls.Add(mainLarge);
        panel.Controls.Add(mainPreview);
        panel.Controls.Add(new Label { Text = "Main interface preferences are saved for the launcher interface to come.", AutoSize = true });
        panel.Controls.Add(fontsStatus);
        var listHost = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
        listHost.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        listHost.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        listHost.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        listHost.Controls.Add(fontSearch, 0, 0);
        listHost.Controls.Add(localFontsOnly, 0, 1);
        listHost.Controls.Add(fontList, 0, 2);
        layout.Controls.Add(optionsHost, 0, 0);
        layout.Controls.Add(listHost, 1, 0);
        void ArrangeFonts()
        {
            var wide = layout.ClientSize.Width >= 900 * DeviceDpi / 96;
            layout.SuspendLayout();
            layout.SetColumnSpan(optionsHost, wide ? 1 : 2);
            layout.SetColumnSpan(listHost, wide ? 1 : 2);
            layout.SetCellPosition(listHost, wide ? new TableLayoutPanelCellPosition(1, 0) : new TableLayoutPanelCellPosition(0, 1));
            layout.ColumnStyles[0].SizeType = wide ? SizeType.Absolute : SizeType.Percent;
            layout.ColumnStyles[0].Width = wide ? 360 * DeviceDpi / 96 : 100;
            layout.ColumnStyles[1].SizeType = wide ? SizeType.Percent : SizeType.Absolute;
            layout.ColumnStyles[1].Width = wide ? 100 : 0;
            layout.RowStyles[0].SizeType = wide ? SizeType.Percent : SizeType.Absolute;
            layout.RowStyles[0].Height = wide ? 100 : Math.Min(340 * DeviceDpi / 96, layout.ClientSize.Height / 2);
            layout.RowStyles[1].SizeType = wide ? SizeType.Absolute : SizeType.Percent;
            layout.RowStyles[1].Height = wide ? 0 : 100;
            var wrapWidth = Math.Max(240, (wide ? 360 * DeviceDpi / 96 : layout.ClientSize.Width - 32) - 40);
            foreach (Control control in panel.Controls)
                if (control is Label) control.MaximumSize = new Size(wrapWidth, 0);
            panel.MaximumSize = new Size(wrapWidth, 0);
            layout.ResumeLayout();
        }
        layout.SizeChanged += (_, _) => ArrangeFonts();
        ArrangeFonts();
        fontSearch.TextChanged += (_, _) => FillFontList();
        localFontsOnly.CheckedChanged += (_, _) => FillFontList();
        fontList.DrawItem += (_, e) =>
        {
            if (e.Index < 0) return;
            var face = (string)fontList.Items[e.Index];
            e.DrawBackground();
            using var previewFont = fontCatalog.Create(face, 14);
            using var brush = new SolidBrush(e.ForeColor);
            e.Graphics.DrawString(face, previewFont, brush, e.Bounds.Left + 8, e.Bounds.Top + 6);
            var source = fontCatalog.IsLocal(face) ? "PApps local" : "Windows";
            TextRenderer.DrawText(e.Graphics, source, SystemFonts.MessageBoxFont, new Rectangle(e.Bounds.Right - 100, e.Bounds.Top, 95, e.Bounds.Height), e.ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
            e.DrawFocusRectangle();
        };
        useLocalFonts.CheckedChanged += (_, _) =>
        {
            if (loadingPreferences) return;
            settings.UseLocalFonts = useLocalFonts.Checked;
            RefreshFonts();
            Save();
        };
        setupFace.SelectedIndexChanged += (_, _) => PreferencesChanged();
        mainFace.SelectedIndexChanged += (_, _) => PreferencesChanged();
        setupLarge.CheckedChanged += (_, _) => PreferencesChanged();
        mainLarge.CheckedChanged += (_, _) => PreferencesChanged();
        return layout;
    }

    void FillFontList()
    {
        fontList.BeginUpdate();
        fontList.Items.Clear();
        fontList.Items.AddRange(fontCatalog.Names.Where(n => (!localFontsOnly.Checked || fontCatalog.IsLocal(n)) && n.Contains(fontSearch.Text, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(fontCatalog.IsLocal).ThenBy(n => n, StringComparer.CurrentCultureIgnoreCase).Cast<object>().ToArray());
        fontList.EndUpdate();
        fontList.Invalidate();
    }

    void RefreshFonts()
    {
        DetectDocuments();
        var folder = settings.DocumentsFolder.Length == 0 ? "" : Path.Combine(settings.DocumentsFolder, "Fonts");
        fontsLocation.Text = folder.Length > 0 ? folder : "Choose a main apps folder to use local fonts.";
        var errors = fontCatalog.Refresh(folder, settings.UseLocalFonts);
        var previousLoading = loadingPreferences;
        loadingPreferences = true;
        foreach (var (picker, selected) in new[] { (setupFace, settings.SetupFontFace), (mainFace, settings.MainFontFace) })
        {
            picker.Items.Clear();
            picker.Items.AddRange(fontCatalog.Names.Cast<object>().ToArray());
            if (!picker.Items.Contains(selected)) picker.Items.Add(selected);
            picker.SelectedItem = selected;
        }
        loadingPreferences = previousLoading;
        FillFontList();
        if (fontList.Items.Count > 0) fontList.TopIndex = 0;
        fontsStatus.Text = $"{fontCatalog.Names.Count()} font families · {fontCatalog.LocalCount} local" + (errors.Count > 0 ? $" · {errors.Count} files could not be loaded" : "");
        if (!fontCatalog.Names.Contains(settings.SetupFontFace, StringComparer.OrdinalIgnoreCase) || !fontCatalog.Names.Contains(settings.MainFontFace, StringComparer.OrdinalIgnoreCase))
            fontsStatus.Text += " · An unavailable selected font will use Segoe UI.";
        ApplyFonts();
    }

    void PreferencesChanged()
    {
        if (loadingPreferences) return;
        settings.SetupFontFace = setupFace.Text;
        settings.MainFontFace = mainFace.Text;
        settings.SetupLargeFonts = setupLarge.Checked;
        settings.MainLargeFonts = mainLarge.Checked;
        settings.HidePortable = hidePortable.Checked;
        ApplyFonts();
        Save();
        RefreshList();
    }

    void ApplyFonts()
    {
        Font = fontCatalog.Create(settings.SetupFontFace, settings.SetupLargeFonts ? 13 : 10);
        mainPreview.Font = fontCatalog.Create(settings.MainFontFace, settings.MainLargeFonts ? 16 : 10);
        // Private fonts need GDI+ for Label rendering; GDI controls use process-private registration.
        mainPreview.UseCompatibleTextRendering = true;
    }

    void DetectDocuments()
    {
        var folder = string.IsNullOrWhiteSpace(settings.Repository) ? "" : Path.Combine(settings.Repository, "Documents");
        if (Directory.Exists(settings.Repository))
        {
            try
            {
                Directory.CreateDirectory(Path.Combine(settings.Repository, "Apps"));
                Directory.CreateDirectory(Path.Combine(folder, "Fonts"));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { status.Text = "Could not create default folders: " + ex.Message; }
        }
        settings.DocumentsFolder = Directory.Exists(folder) ? Path.GetFullPath(folder) : "";
        documents.Text = settings.DocumentsFolder.Length > 0 ? settings.DocumentsFolder : "No documents folder found in the main apps folder.";
    }

    string DisplayName(AppEntry app)
    {
        if (!settings.HidePortable) return app.Name;
        var name = System.Text.RegularExpressions.Regex.Replace(app.Name, "Portable", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim(' ', '-', '_');
        return string.IsNullOrWhiteSpace(name) ? app.Name : name;
    }

    static Button Button(string label, Action action)
    {
        var button = new Button { Text = label, AutoSize = true, Height = 32, Padding = new Padding(8, 0, 8, 0) };
        button.Click += (_, _) => action();
        return button;
    }

    void LoadSettings()
    {
        try
        {
            if (File.Exists(settingsPath)) settings = JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(settingsPath)) ?? new();
            settings.Apps ??= [];
        }
        catch (Exception ex) { ShowError("Could not read settings. The existing file has been kept.", ex); }
        loadingPreferences = true;
        if (!setupFace.Items.Contains(settings.SetupFontFace)) setupFace.Items.Add(settings.SetupFontFace);
        if (!mainFace.Items.Contains(settings.MainFontFace)) mainFace.Items.Add(settings.MainFontFace);
        setupFace.SelectedItem = settings.SetupFontFace;
        mainFace.SelectedItem = settings.MainFontFace;
        setupLarge.Checked = settings.SetupLargeFonts;
        mainLarge.Checked = settings.MainLargeFonts;
        hidePortable.Checked = settings.HidePortable;
        useLocalFonts.Checked = settings.UseLocalFonts;
        loadingPreferences = false;
        ApplyFonts();
        DetectDocuments();
        RefreshFonts();
        RestoreLayout();
        RefreshList();
    }

    bool Save()
    {
        try
        {
            File.WriteAllText(settingsPath + ".tmp", JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(settingsPath + ".tmp", settingsPath, true);
            return true;
        }
        catch (Exception ex) { ShowError("Could not save settings. Put the launcher in a folder you can write to.", ex); return false; }
    }

    void RefreshList()
    {
        location.Text = string.IsNullOrWhiteSpace(settings.Repository) ? "Choose a main folder. PApps will create Apps and Documents\\Fonts inside it." : settings.Repository;
        apps.Items.Clear();
        foreach (var app in settings.Apps.OrderBy(a => a.Category, StringComparer.CurrentCultureIgnoreCase).ThenBy(DisplayName, StringComparer.CurrentCultureIgnoreCase))
        {
            if (!(app.Name + " " + app.Category).Contains(search.Text, StringComparison.OrdinalIgnoreCase)) continue;
            apps.Items.Add(new ListViewItem([DisplayName(app), app.Category, app.Executable, DescriptionPreview(app.Description)]) { Tag = app, ImageIndex = AppIcon(app) });
        }
        status.Text = settings.Apps.Count == 0 ? "No apps yet. Choose a folder and scan, or add an executable." : $"{apps.Items.Count} shown · {settings.Apps.Count} apps in your library";
    }

    int AppIcon(AppEntry app)
    {
        try
        {
            var path = Resolve(app);
            if (iconIndexes.TryGetValue(path, out var index)) return index;
            if (!File.Exists(path)) return 0;
            using var icon = Icon.ExtractAssociatedIcon(path);
            if (icon is null) return 0;
            appIcons.Images.Add(icon);
            index = appIcons.Images.Count - 1;
            iconIndexes[path] = index;
            return index;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or System.Security.SecurityException)
        {
            return 0;
        }
    }

    void ChooseFolder()
    {
        using var dialog = new FolderBrowserDialog { Description = "Select the main folder containing Apps and Documents", UseDescriptionForTitle = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (!string.Equals(settings.Repository, dialog.SelectedPath, StringComparison.OrdinalIgnoreCase) && settings.Apps.Count > 0 &&
            MessageBox.Show(this, "Switching folders clears this app list. Your application files will stay in place. Continue?", "Switch apps folder", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        settings.Repository = dialog.SelectedPath;
        DetectDocuments();
        RefreshFonts();
        settings.Apps.Clear();
        iconIndexes.Clear();
        appIcons.Images.Clear();
        appIcons.Images.Add(SystemIcons.Application);
        Save();
        Scan();
    }

    string Resolve(AppEntry app)
    {
        if (Path.IsPathRooted(app.Executable)) return Path.GetFullPath(app.Executable);
        if (string.IsNullOrWhiteSpace(settings.Repository)) throw new InvalidOperationException("Choose an apps folder first.");
        var root = Path.GetFullPath(settings.Repository).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(root, app.Executable));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("The app path points outside the apps folder.");
        return path;
    }

    void Scan()
    {
        DetectDocuments();
        if (!Directory.Exists(settings.Repository)) { MessageBox.Show(this, "Choose an available apps folder first."); return; }
        try
        {
            var candidates = new List<(string Path, string Name, string Category, string Description)>();
            var pending = new Stack<string>();
            pending.Push(Path.Combine(settings.Repository, "Apps"));
            var skipped = 0;
            while (pending.TryPop(out var dir))
            {
                try
                {
                var manifest = Path.Combine(dir, "App", "AppInfo", "appinfo.ini");
                var ini = File.Exists(manifest) ? ReadIni(manifest) : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var foundManifestLauncher = false;
                if (ini.TryGetValue("Control/Start", out var start))
                {
                    var executable = Path.GetFullPath(Path.Combine(dir, start));
                    if (!executable.StartsWith(Path.GetFullPath(dir).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) continue;
                    if (File.Exists(executable) && Path.GetExtension(executable).Equals(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        candidates.Add((executable, ini.GetValueOrDefault("Details/Name", Path.GetFileNameWithoutExtension(start)), ini.GetValueOrDefault("Details/Category", "Other"), ini.GetValueOrDefault("Details/Description", "")));
                        foundManifestLauncher = true;
                    }
                }
                // A PortableApps launcher owns its internal executables; don't list them separately.
                if (foundManifestLauncher) continue;
                foreach (var executable in Directory.EnumerateFiles(dir, "*.exe").Where(IsAppExecutable))
                        candidates.Add((executable, Path.GetFileNameWithoutExtension(executable), "Other", ""));
                foreach (var child in Directory.EnumerateDirectories(dir))
                {
                    // Avoid junction loops and following directory links outside the repository.
                    if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) pending.Push(child);
                }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    skipped++;
                }
            }
            var added = 0;
            foreach (var candidate in candidates)
            {
                var relative = Path.GetRelativePath(settings.Repository, candidate.Path);
                if (settings.Apps.Any(a => string.Equals(a.Executable, relative, StringComparison.OrdinalIgnoreCase))) continue;
                var legacy = settings.Apps.FirstOrDefault(a => !Path.IsPathRooted(a.Executable)
                    && string.Equals(Path.Combine("Apps", a.Executable), relative, StringComparison.OrdinalIgnoreCase)
                    && !File.Exists(Path.Combine(settings.Repository, a.Executable)));
                if (legacy is not null) { legacy.Executable = relative; continue; }
                settings.Apps.Add(new AppEntry { Name = candidate.Name, Executable = relative, Category = candidate.Category, Description = candidate.Description });
                added++;
            }
            var saved = Save();
            RefreshList();
            status.Text = $"Found {added} new apps. " + (saved ? "Library saved." : "Library could not be saved.") +
                (skipped > 0 ? $" Skipped {skipped} unavailable folders." : "");
        }
        catch (Exception ex) { ShowError("Could not scan the apps folder.", ex); }
    }

    static bool IsAppExecutable(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return !name.EndsWith("-cli", StringComparison.OrdinalIgnoreCase)
            && !name.Equals("uninstall", StringComparison.OrdinalIgnoreCase)
            && !name.Equals("unins000", StringComparison.OrdinalIgnoreCase);
    }

    static Dictionary<string, string> ReadIni(string path)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var section = "";
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.StartsWith(';') || line.Length == 0) continue;
            if (line.StartsWith('[') && line.EndsWith(']')) { section = line[1..^1]; continue; }
            var equals = line.IndexOf('=');
            if (equals > 0) values[section + "/" + line[..equals].Trim()] = line[(equals + 1)..].Trim();
        }
        return values;
    }

    void Add()
    {
        using var dialog = new OpenFileDialog { Title = "Choose a portable app executable", Filter = "Windows applications (*.exe)|*.exe", InitialDirectory = Path.Combine(settings.Repository, "Apps") };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var path = string.IsNullOrWhiteSpace(settings.Repository) ? dialog.FileName : Path.GetRelativePath(settings.Repository, dialog.FileName);
        if (path.StartsWith("..")) path = dialog.FileName;
        if (settings.Apps.Any(a => string.Equals(Resolve(a), dialog.FileName, StringComparison.OrdinalIgnoreCase))) return;
        settings.Apps.Add(new AppEntry { Name = Path.GetFileNameWithoutExtension(dialog.FileName), Executable = path });
        Save();
        RefreshList();
    }

    void Edit()
    {
        if (apps.SelectedItems.Count == 0)
        {
            MessageBox.Show(this, "Select an app to edit first.", "Edit app");
            return;
        }
        var app = (AppEntry)apps.SelectedItems[0].Tag!;
        using var dialog = new Form
        {
            Text = "Edit app", ClientSize = new Size(560, 380), Font = Font,
            StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false
        };
        var fields = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 2, RowCount = 5 };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        var name = new TextBox { Text = app.Name, Dock = DockStyle.Top, AccessibleName = "Application name" };
        var category = new ComboBox { Text = app.Category, Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDown, AccessibleName = "Category" };
        category.Items.AddRange(settings.Apps.Select(a => a.Category).Append("Other")
            .Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c).Cast<object>().ToArray());
        fields.Controls.Add(new Label { Text = "Name", AutoSize = true }, 0, 0);
        fields.Controls.Add(name, 1, 0);
        fields.Controls.Add(new Label { Text = "Category", AutoSize = true }, 0, 1);
        fields.Controls.Add(category, 1, 1);
        var hint = new Label { Text = "Choose an existing category or type a new one.", Dock = DockStyle.Fill };
        fields.Controls.Add(hint, 0, 2);
        fields.SetColumnSpan(hint, 2);
        var description = new TextBox
        {
            Text = app.Description, Multiline = true, AcceptsReturn = true,
            ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, AccessibleName = "Description"
        };
        fields.Controls.Add(new Label { Text = "Description", AutoSize = true }, 0, 3);
        fields.Controls.Add(description, 1, 3);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        var save = new Button { Text = "Save", AutoSize = true };
        buttons.Controls.AddRange([cancel, save]);
        fields.Controls.Add(buttons, 0, 4);
        fields.SetColumnSpan(buttons, 2);
        dialog.Controls.Add(fields);
        dialog.AcceptButton = save;
        dialog.CancelButton = cancel;
        save.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(name.Text))
            {
                MessageBox.Show(dialog, "Enter an application name.", "Edit app");
                name.Focus();
                return;
            }
            var oldName = app.Name;
            var oldCategory = app.Category;
            var oldDescription = app.Description;
            app.Name = name.Text.Trim();
            app.Category = string.IsNullOrWhiteSpace(category.Text) ? "Other" : category.Text.Trim();
            app.Description = description.Text.Trim();
            if (!Save()) { app.Name = oldName; app.Category = oldCategory; app.Description = oldDescription; return; }
            dialog.DialogResult = DialogResult.OK;
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        RefreshList();
        foreach (ListViewItem item in apps.Items)
            if (ReferenceEquals(item.Tag, app)) { item.Selected = true; item.EnsureVisible(); break; }
    }

    void Remove()
    {
        if (apps.SelectedItems.Count == 0) return;
        var app = (AppEntry)apps.SelectedItems[0].Tag!;
        if (MessageBox.Show(this, $"Remove {app.Name} from the library? Its files will stay in place.", "Remove app", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        settings.Apps.Remove(app);
        Save();
        RefreshList();
    }

    string UninstallFolder(AppEntry app)
    {
        if (string.IsNullOrWhiteSpace(settings.Repository)) throw new InvalidOperationException("Choose a main apps folder first.");
        var root = Path.GetFullPath(settings.Repository).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var executable = Resolve(app);
        if (!executable.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only apps inside the main apps folder can be uninstalled here.");
        var appsRoot = Path.GetFullPath(Path.Combine(root, "Apps")) + Path.DirectorySeparatorChar;
        if (!executable.StartsWith(appsRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only apps inside the Apps folder can be uninstalled here.");
        var relative = Path.GetRelativePath(appsRoot, executable);
        var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (parts.Length < 2) throw new InvalidOperationException("This executable is directly in the main folder. Remove its library entry instead; the main folder cannot be deleted.");
        var folder = Path.GetFullPath(Path.Combine(appsRoot, parts[0]));
        if (parts[0].Equals("documents", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The documents folder cannot be uninstalled.");
        var prefix = folder + Path.DirectorySeparatorChar;
        if (settings.Apps.Any(other => !ReferenceEquals(other, app) && Resolve(other).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("This folder contains other library apps. Uninstall is unavailable to avoid deleting them too.");
        if (!Directory.Exists(folder)) throw new DirectoryNotFoundException("The app folder is unavailable.");
        // Validate every directory before recursive deletion; never follow links outside the app folder.
        var pending = new Stack<string>();
        pending.Push(folder);
        while (pending.TryPop(out var dir))
        {
            if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("The app folder contains directory links. Remove it manually after checking their targets.");
            foreach (var child in Directory.EnumerateDirectories(dir)) pending.Push(child);
        }
        return folder;
    }

    void Uninstall()
    {
        if (apps.SelectedItems.Count == 0) { MessageBox.Show(this, "Select an app to uninstall first.", "Uninstall"); return; }
        var app = (AppEntry)apps.SelectedItems[0].Tag!;
        try
        {
            var folder = UninstallFolder(app);
            if (MessageBox.Show(this, $"Permanently delete {app.Name} and its entire folder?\n\n{folder}\n\nAll files and app data inside this folder will be deleted. Close the app first. This cannot be undone.",
                "Uninstall application", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            folder = UninstallFolder(app);
            Directory.Delete(folder, true);
            settings.Apps.Remove(app);
            Save();
            RefreshList();
            status.Text = $"Uninstalled {app.Name}.";
        }
        catch (Exception ex) { ShowError("Could not uninstall the app. Its library entry was kept. If deletion started, some files may already have been removed.", ex); }
    }

    void ShowError(string message, Exception ex) => MessageBox.Show(this, message + "\n\n" + ex.Message, "PApps", MessageBoxButtons.OK, MessageBoxIcon.Error);
}
