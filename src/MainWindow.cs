using System.Diagnostics;

namespace PApps;

public sealed class MainWindow : Form
{
    LauncherSettings settings = new();
    readonly FontCatalog fonts = new();
    readonly ToolTip tips = new() { AutoPopDelay = 15000, InitialDelay = 400, ReshowDelay = 100 };
    readonly Dictionary<string, Image> icons = new(StringComparer.OrdinalIgnoreCase);
    readonly TextBox searchBox = new() { PlaceholderText = "Find an app…", Dock = DockStyle.Top };
    readonly ComboBox viewPicker = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 165 };
    readonly FlowLayoutPanel favourites = new() { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = false };
    readonly FlowLayoutPanel library = new() { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
    readonly Label libraryCount = new() { AutoSize = true, ForeColor = Color.FromArgb(99, 115, 130) };
    readonly Label favouriteHint = new() { AutoSize = true, ForeColor = Color.FromArgb(99, 115, 130) };
    readonly Label folderLabel = new() { Dock = DockStyle.Fill, AutoEllipsis = true, ForeColor = Color.FromArgb(162, 183, 201), TextAlign = ContentAlignment.MiddleRight };
    readonly Label storage = new() { AutoSize = true, ForeColor = Color.FromArgb(173, 193, 209), MaximumSize = new Size(240, 0) };
    readonly Label activity = new() { AutoSize = true, ForeColor = Color.FromArgb(99, 115, 130), MaximumSize = new Size(700, 0) };
    readonly CheckBox expandDocs = new() { Text = "Show document files", AutoSize = true, ForeColor = Color.FromArgb(215, 228, 237) };
    readonly TreeView documentTree = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = Color.FromArgb(30, 48, 66), ForeColor = Color.White, ShowLines = false, Scrollable = false, ShowNodeToolTips = true, Height = 30, Visible = false };
    readonly Color navy = Color.FromArgb(25, 40, 57);
    readonly Color accent = Color.FromArgb(23, 132, 115);
    readonly RowStyle favouritesRow = new(SizeType.Absolute, 220);
    bool loading;
    bool ready;
    bool openingSettings;
    bool sizingLibrary;
    CancellationTokenSource sizeCancellation = new();

    public MainWindow()
    {
        Text = "PApps";
        MinimumSize = new Size(900, 650);
        Size = new Size(1120, 800);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(238, 243, 248);
        Font = new Font("Segoe UI", 10);
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 86)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = navy, Padding = new Padding(25, 12, 25, 12), ColumnCount = 2, Margin = Padding.Empty };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        var brand = new Label { Text = "PApps", Dock = DockStyle.Fill, ForeColor = Color.White, Font = new Font("Segoe UI", 28, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };
        header.Controls.Add(brand, 0, 0); header.Controls.Add(folderLabel, 1, 0);
        root.Controls.Add(header, 0, 0);
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 290));
        var content = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), RowCount = 3, ColumnCount = 1, Margin = Padding.Empty };
        content.RowStyles.Add(favouritesRow); content.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); content.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        var favCard = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(16), RowCount = 3, ColumnCount = 1, Margin = new Padding(0, 0, 0, 16) };
        favCard.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); favCard.RowStyles.Add(new RowStyle(SizeType.Absolute, 23)); favCard.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        favCard.Controls.Add(Heading("Favourites"), 0, 0); favCard.Controls.Add(favouriteHint, 0, 1); favCard.Controls.Add(favourites, 0, 2);
        content.Controls.Add(favCard, 0, 0);
        var libraryCard = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(16), RowCount = 3, ColumnCount = 1, Margin = Padding.Empty };
        libraryCard.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); libraryCard.RowStyles.Add(new RowStyle(SizeType.Absolute, 25)); libraryCard.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 175));
        toolbar.Controls.Add(Heading("Your library"), 0, 0);
        viewPicker.Items.AddRange(["Large Icons", "Medium Icons", "Small Icons", "List", "Large Tiles", "Medium Tiles", "Small Tiles", "Detailed Tiles"]); toolbar.Controls.Add(viewPicker, 1, 0);
        libraryCard.Controls.Add(toolbar, 0, 0); libraryCard.Controls.Add(libraryCount, 0, 1); libraryCard.Controls.Add(library, 0, 2);
        content.Controls.Add(libraryCard, 0, 1); content.Controls.Add(activity, 0, 2);
        body.Controls.Add(content, 0, 0);
        body.Controls.Add(BuildSidebar(), 1, 0);
        root.Controls.Add(body, 0, 1); Controls.Add(root);
        searchBox.TextChanged += (_, _) => RenderApps();
        viewPicker.SelectedIndexChanged += (_, _) => { if (!loading) { settings.MainView = viewPicker.Text; Save(); RenderApps(); } };
        expandDocs.CheckedChanged += (_, _) => { if (!loading) { settings.ExpandDocuments = expandDocs.Checked; Save(); RefreshDocuments(); } };
        library.SizeChanged += (_, _) => ResizeGroups();
        favourites.SizeChanged += (_, _) => ResizeFavourites();
        documentTree.BeforeExpand += (_, e) => { if (e.Node is not null) PopulateNode(e.Node); };
        documentTree.AfterExpand += (_, _) => SizeDocumentTree();
        documentTree.AfterCollapse += (_, _) => SizeDocumentTree();
        documentTree.FontChanged += (_, _) => SizeDocumentTree();
        documentTree.NodeMouseDoubleClick += (_, e) => { if (e.Node?.Tag is string path && File.Exists(path)) OpenPath(path); };
        Shown += (_, _) => Reload();
        ResizeEnd += (_, _) => { if (ready) SaveGeometry(); };
        FormClosing += (_, _) => { if (ready && !openingSettings) SaveGeometry(); sizeCancellation.Cancel(); };
        Disposed += (_, _) => { sizeCancellation.Cancel(); sizeCancellation.Dispose(); tips.Dispose(); foreach (var icon in icons.Values) icon.Dispose(); fonts.Dispose(); };
    }

    Label Heading(string text) => new() { Text = text, AutoSize = true, Font = new Font("Segoe UI", 13, FontStyle.Bold), ForeColor = Color.FromArgb(27, 45, 63) };
    Button NavButton(string text, Action action, string iconName, bool primary = false)
    {
        var button = new Button { Text = text, Dock = DockStyle.Fill, Height = 48, FlatStyle = FlatStyle.Flat, TextAlign = ContentAlignment.MiddleCenter, ImageAlign = ContentAlignment.MiddleCenter, TextImageRelation = TextImageRelation.ImageBeforeText, Padding = new Padding(3), ForeColor = Color.White, BackColor = primary ? accent : Color.FromArgb(36, 55, 74), Margin = new Padding(3), Image = SidebarIcon(iconName), AccessibleName = text };
        button.Disposed += (_, _) => button.Image?.Dispose();
        button.FlatAppearance.BorderSize = 0; button.Click += (_, _) => action(); return button;
    }
    Control BuildSidebar()
    {
        var side = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = navy, Padding = new Padding(16), ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
        side.RowStyles.Add(new RowStyle(SizeType.Absolute, 110)); side.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); side.RowStyles.Add(new RowStyle(SizeType.Absolute, 115));
        var search = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        search.Controls.Add(new Label { Text = "QUICK FIND", AutoSize = true, ForeColor = Color.FromArgb(139, 201, 188) });
        search.Controls.Add(searchBox); search.Controls.Add(new Label { Text = "Search apps, categories and descriptions", AutoSize = true, MaximumSize = new Size(240, 0), ForeColor = Color.FromArgb(173, 193, 209) });
        side.Controls.Add(search, 0, 0);
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = Padding.Empty, Name = "SidebarScroll" };
        var middle = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = Padding.Empty };
        middle.Controls.Add(new Label { Text = "YOUR WORKSPACE", AutoSize = true, ForeColor = Color.FromArgb(139, 201, 188), Margin = new Padding(3, 6, 0, 12) });
        var links = ButtonGrid(2);
        links.Controls.Add(NavButton("Documents", () => OpenWorkspace("Documents"), "documents"), 0, 0);
        links.Controls.Add(NavButton("Fonts", () => OpenWorkspace(Path.Combine("Documents", "Fonts")), "fonts"), 1, 0);
        links.Controls.Add(NavButton("Apps folder", () => OpenWorkspace("Apps"), "folder"), 0, 1);
        links.Controls.Add(NavButton("Settings", OpenSettings, "settings", true), 1, 1);
        middle.Controls.Add(links); middle.Controls.Add(expandDocs);
        documentTree.Dock = DockStyle.None; documentTree.Margin = new Padding(3, 8, 3, 8);
        middle.Controls.Add(documentTree);
        scroll.Controls.Add(middle);
        scroll.SizeChanged += (_, _) =>
        {
            var width = Math.Max(180, scroll.ClientSize.Width - 2);
            middle.MaximumSize = new Size(width, 0); middle.Width = width;
            links.Width = width; documentTree.Width = width - 6;
        };
        side.Controls.Add(scroll, 0, 1);
        var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); bottom.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        bottom.Controls.Add(storage, 0, 0);
        var bottomButtons = ButtonGrid(1); bottomButtons.Dock = DockStyle.Fill;
        bottomButtons.Controls.Add(NavButton("Help", () => MessageBox.Show(this, "Click an app to launch it.\nUse the star to toggle a favourite.\nSearch filters favourites and the library.\nThe view selector controls both sections.\n\nOpen Apps folder to add portable programs, then scan in Settings.\nExpand document folders using the arrow; double-click a file to open it.", "Using PApps"), "help"), 0, 0);
        bottomButtons.Controls.Add(NavButton("Close PApps", Close, "close"), 1, 0);
        bottom.Controls.Add(bottomButtons, 0, 1); side.Controls.Add(bottom, 0, 2);
        return side;
    }

    static TableLayoutPanel ButtonGrid(int rows)
    {
        var grid = new TableLayoutPanel { ColumnCount = 2, RowCount = rows, Height = 54 * rows, Margin = Padding.Empty, Padding = Padding.Empty };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (var i = 0; i < rows; i++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rows));
        return grid;
    }

    static Image SidebarIcon(string name)
    {
        var image = new Bitmap(20, 20);
        using var g = Graphics.FromImage(image);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var pen = new Pen(Color.White, 1.6f);
        if (name == "folder") { g.DrawLines(pen, [new Point(2, 16), new Point(2, 5), new Point(8, 5), new Point(10, 7), new Point(18, 7), new Point(18, 16), new Point(2, 16)]); }
        else if (name == "documents") { g.DrawRectangle(pen, 4, 2, 12, 16); g.DrawLine(pen, 7, 7, 13, 7); g.DrawLine(pen, 7, 11, 13, 11); g.DrawLine(pen, 7, 14, 12, 14); }
        else if (name == "fonts") { using var f = new Font("Segoe UI", 11, FontStyle.Bold); g.DrawString("Aa", f, Brushes.White, -1, 0); }
        else if (name == "settings")
        {
            g.DrawEllipse(pen, 5, 5, 10, 10); g.DrawEllipse(pen, 8, 8, 4, 4);
            for (var i = 0; i < 8; i++) { var angle = i * Math.PI / 4; g.DrawLine(pen, 10 + (float)Math.Cos(angle) * 6, 10 + (float)Math.Sin(angle) * 6, 10 + (float)Math.Cos(angle) * 9, 10 + (float)Math.Sin(angle) * 9); }
        }
        else if (name == "help") { g.DrawEllipse(pen, 2, 2, 16, 16); using var f = new Font("Segoe UI", 11, FontStyle.Bold); g.DrawString("?", f, Brushes.White, 4, -1); }
        else { g.DrawArc(pen, 3, 3, 14, 14, -55, 290); g.DrawLine(pen, 10, 1, 10, 10); }
        return image;
    }

    public void Reload()
    {
        var firstLoad = !ready;
        ready = false;
        try
        {
            settings = SettingsStore.Load();
            var docs = Path.Combine(settings.Repository, "Documents");
            if (Directory.Exists(settings.Repository)) { Directory.CreateDirectory(Path.Combine(settings.Repository, "Apps")); Directory.CreateDirectory(Path.Combine(docs, "Fonts")); }
            settings.DocumentsFolder = docs;
            fonts.Refresh(Path.Combine(docs, "Fonts"), settings.UseLocalFonts);
            Font = fonts.Create(settings.MainFontFace, settings.MainLargeFonts ? 13 : 10);
            loading = true;
            settings.MainView = settings.MainView switch { "Small" => "Small Tiles", "Medium" => "Medium Tiles", "Large" => "Large Tiles", "List" when !settings.MainViewNamesUpdated => "Detailed Tiles", _ => settings.MainView };
            settings.MainViewNamesUpdated = true;
            viewPicker.SelectedItem = viewPicker.Items.Contains(settings.MainView) ? settings.MainView : "Medium Tiles";
            settings.MainView = viewPicker.Text;
            expandDocs.Checked = settings.ExpandDocuments;
            loading = false;
            folderLabel.Text = settings.Repository;
            if (firstLoad)
            {
                var area = Screen.FromControl(this).WorkingArea;
                MinimumSize = new Size(Math.Min(900, area.Width), Math.Min(650, area.Height));
                Size = new Size(Math.Clamp(settings.MainWindowWidth, MinimumSize.Width, area.Width), Math.Clamp(settings.MainWindowHeight, MinimumSize.Height, area.Height));
                if (settings.MainWindowMaximized) WindowState = FormWindowState.Maximized;
            }
            ready = true;
            Save(); RenderApps(); RefreshDocuments(); UpdateSize();
        }
        catch (Exception ex) { MessageBox.Show(this, "Could not load your workspace. Your settings file was not replaced.\n\n" + ex.Message, "PApps", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
    void Save()
    {
        if (openingSettings || !ready) return;
        try { SettingsStore.Save(settings); }
        catch (Exception ex) { activity.Text = "Settings could not be saved: " + ex.Message; }
    }
    void SaveGeometry()
    {
        var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        settings.MainWindowWidth = bounds.Width; settings.MainWindowHeight = bounds.Height;
        if (WindowState != FormWindowState.Minimized) settings.MainWindowMaximized = WindowState == FormWindowState.Maximized;
        Save();
    }
    void OpenSettings()
    {
        SaveGeometry();
        openingSettings = true;
        try { using var setup = new Launcher(); setup.ShowDialog(this); }
        finally { openingSettings = false; }
        Reload();
    }
    public IEnumerable<AppEntry> FilteredApps() => settings.Apps.Where(a => (a.Name + " " + a.Category + " " + a.Description).Contains(searchBox.Text.Trim(), StringComparison.OrdinalIgnoreCase))
        .OrderBy(a => a.Category, StringComparer.CurrentCultureIgnoreCase).ThenBy(a => SettingsStore.DisplayName(settings, a), StringComparer.CurrentCultureIgnoreCase);
    static void ClearControls(Control control) { while (control.Controls.Count > 0) control.Controls[0].Dispose(); }
    public void RenderApps()
    {
        if (loading) return;
        library.SuspendLayout(); favourites.SuspendLayout();
        ClearControls(library); ClearControls(favourites);
        var visible = FilteredApps().ToList();
        foreach (var app in visible.Where(a => a.Favourite))
        {
            var card = Card(app, viewPicker.Text); favourites.Controls.Add(card);
        }
        favouritesRow.Height = favourites.Controls.Count == 0 ? 130 : favourites.Controls[0].Height + 130;
        favouriteHint.Text = favourites.Controls.Count == 0 ? "Star an app to keep it within reach." : $"{favourites.Controls.Count} pinned {(favourites.Controls.Count == 1 ? "app" : "apps")} · click to launch";
        foreach (var group in visible.GroupBy(a => string.IsNullOrWhiteSpace(a.Category) ? "Other" : a.Category, StringComparer.CurrentCultureIgnoreCase))
        {
            var section = new FlowLayoutPanel { AutoSize = false, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0, 0, 0, 4) };
            var heading = new CategoryHeader(group.Key, Font) { Margin = new Padding(0, 0, 0, 5) }; section.Controls.Add(heading);
            var cards = new FlowLayoutPanel { AutoSize = false, WrapContents = true, Margin = Padding.Empty };
            foreach (var app in group) cards.Controls.Add(Card(app, viewPicker.Text));
            section.Controls.Add(cards); library.Controls.Add(section);
        }
        if (visible.Count == 0) library.Controls.Add(new Label { Text = settings.Apps.Count == 0 ? "Your workspace is ready. Add portable apps through Settings." : "No apps match your search.", AutoSize = true, MaximumSize = new Size(500, 0), Padding = new Padding(0, 20, 0, 0) });
        libraryCount.Text = $"{visible.Count} of {settings.Apps.Count} apps · grouped by category";
        ResizeGroups();
        ResizeFavourites();
        library.ResumeLayout(); favourites.ResumeLayout();
    }
    AppCard Card(AppEntry app, string mode) => new(app, SettingsStore.DisplayName(settings, app), AppIcon(app), mode, Font,
        () => LaunchApp(app), () => { app.Favourite = !app.Favourite; Save(); BeginInvoke(new Action(RenderApps)); }, tips, settings.AppItemPadding, settings.AppItemGap);
    void ResizeGroups()
    {
        if (sizingLibrary) return;
        sizingLibrary = true;
        try
        {
            var width = Math.Max(220, library.ClientSize.Width - (library.VerticalScroll.Visible ? 0 : SystemInformation.VerticalScrollBarWidth) - 4);
            var totalHeight = 0;
            foreach (FlowLayoutPanel section in library.Controls.OfType<FlowLayoutPanel>())
            {
                section.SuspendLayout();
                section.Width = width;
                var headingHeight = 0;
                foreach (CategoryHeader heading in section.Controls.OfType<CategoryHeader>())
                {
                    heading.Width = Math.Max(100, width - 10);
                    headingHeight += heading.Height + heading.Margin.Vertical;
                }
                var cardsHeight = 0;
                foreach (FlowLayoutPanel cards in section.Controls.OfType<FlowLayoutPanel>())
                {
                    cards.SuspendLayout();
                    cards.Width = width;
                    var rowWidth = 0;
                    var rowHeight = 0;
                    var height = 0;
                    foreach (Control card in cards.Controls)
                    {
                        if (viewPicker.Text is "List" or "Detailed Tiles") card.Width = Math.Max(100, width - card.Margin.Horizontal);
                        var itemWidth = card.Width + card.Margin.Horizontal;
                        if (rowWidth > 0 && rowWidth + itemWidth > width)
                        {
                            height += rowHeight;
                            rowWidth = rowHeight = 0;
                        }
                        rowWidth += itemWidth;
                        rowHeight = Math.Max(rowHeight, card.Height + card.Margin.Vertical);
                    }
                    // Nested AutoSize panels can cap their preferred height at the viewport,
                    // leaving cards clipped inside a category even at maximum scroll.
                    cards.Height = height + rowHeight;
                    cardsHeight += cards.Height + cards.Margin.Vertical;
                    cards.ResumeLayout();
                }
                section.Height = headingHeight + cardsHeight;
                totalHeight += section.Height + section.Margin.Vertical;
                section.ResumeLayout();
            }
            library.AutoScrollMinSize = new Size(0, totalHeight + 8);
        }
        finally { sizingLibrary = false; }
    }

    void ResizeFavourites()
    {
        var rows = viewPicker.Text is "List" or "Detailed Tiles";
        favourites.FlowDirection = rows ? FlowDirection.TopDown : FlowDirection.LeftToRight;
        favourites.WrapContents = false;
        if (rows) foreach (Control card in favourites.Controls) card.Width = Math.Max(200, favourites.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 8);
    }
    Image? AppIcon(AppEntry app)
    {
        try
        {
            var path = SettingsStore.AppPath(settings, app);
            if (icons.TryGetValue(path, out var image)) return image;
            using var icon = File.Exists(path) ? Icon.ExtractAssociatedIcon(path) : null;
            image = (icon ?? SystemIcons.Application).ToBitmap(); icons[path] = image; return image;
        }
        catch { return null; }
    }
    public void LaunchApp(AppEntry app)
    {
        try
        {
            var path = SettingsStore.AppPath(settings, app);
            if (!File.Exists(path)) throw new FileNotFoundException("The app is unavailable. Check the main folder connection and scan in Settings.");
            if (!Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Select a Windows executable in Settings.");
            Process.Start(new ProcessStartInfo(path) { WorkingDirectory = Path.GetDirectoryName(path), UseShellExecute = true });
            activity.Text = "Launched " + SettingsStore.DisplayName(settings, app);
        }
        catch (Exception ex) { MessageBox.Show(this, "Could not launch " + app.Name + ".\n\n" + ex.Message, "PApps", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
    void OpenPath(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(this, "Could not open this location.\n\n" + ex.Message, "PApps"); }
    }
    void OpenWorkspace(string relative)
    {
        if (!Directory.Exists(settings.Repository)) { MessageBox.Show(this, "The main folder is unavailable.", "PApps"); return; }
        OpenPath(Path.Combine(settings.Repository, relative));
    }
    void RefreshDocuments()
    {
        documentTree.Visible = settings.ExpandDocuments;
        documentTree.Nodes.Clear();
        if (!settings.ExpandDocuments) { SizeDocumentTree(); return; }
        var root = new TreeNode("Documents") { Tag = settings.DocumentsFolder };
        root.Nodes.Add("Loading…"); documentTree.Nodes.Add(root); root.Expand();
        SizeDocumentTree();
    }
    void SizeDocumentTree()
    {
        int VisibleCount(TreeNodeCollection nodes)
        {
            var count = 0;
            foreach (TreeNode node in nodes) { count++; if (node.IsExpanded) count += VisibleCount(node.Nodes); }
            return count;
        }
        documentTree.Height = Math.Max(documentTree.ItemHeight + 8, VisibleCount(documentTree.Nodes) * documentTree.ItemHeight + 8);
    }
    void PopulateNode(TreeNode node)
    {
        if (node.Tag is not string path || node.Nodes.Count != 1 || node.Nodes[0].Tag is not null) return;
        node.Nodes.Clear();
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(path).OrderBy(Path.GetFileName))
            {
                if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0) continue;
                var child = new TreeNode(Path.GetFileName(dir)) { Tag = dir, ToolTipText = dir }; child.Nodes.Add("Loading…"); node.Nodes.Add(child);
            }
            foreach (var file in Directory.EnumerateFiles(path).OrderBy(Path.GetFileName)) node.Nodes.Add(new TreeNode(Path.GetFileName(file)) { Tag = file, ToolTipText = file });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { node.Nodes.Add("Folder unavailable"); }
    }
    async void UpdateSize()
    {
        sizeCancellation.Cancel(); sizeCancellation.Dispose(); sizeCancellation = new();
        var cancellation = sizeCancellation.Token;
        var folder = Path.Combine(settings.Repository, "Apps");
        if (!Directory.Exists(folder)) { storage.Text = $"{settings.Apps.Count} apps · Apps folder unavailable"; return; }
        storage.Text = $"{settings.Apps.Count} apps · calculating size…";
        try
        {
            var result = await Task.Run(() =>
            {
                long size = 0; var count = 0;
                if (Directory.Exists(folder)) foreach (var file in new DirectoryInfo(folder).EnumerateFiles("*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint }))
                { cancellation.ThrowIfCancellationRequested(); try { size += file.Length; count++; } catch (IOException) { } }
                return (size, count);
            }, cancellation);
            if (!IsDisposed && !cancellation.IsCancellationRequested) storage.Text = $"{settings.Apps.Count} apps · {FormatSize(result.size)}\n{result.count:N0} files in Apps";
        }
        catch (OperationCanceledException) { }
        catch { if (!IsDisposed) storage.Text = $"{settings.Apps.Count} apps · size unavailable"; }
    }
    static string FormatSize(long bytes) => bytes >= 1024L * 1024 * 1024 ? $"{bytes / (1024d * 1024 * 1024):0.0} GB" : $"{bytes / (1024d * 1024):0.0} MB";
}
