# PApps

Windows portable-app configuration utility built with C# / .NET 10 / Windows Forms.

Run Launcher/PApps.exe from a writable local folder. No installation is required.

## Main folder

Choose the main folder. PApps creates this structure when it is missing:

    Main folder/
      Apps/
      Documents/
        Fonts/

Move each portable app folder into Apps and scan. Scans recurse only within Apps. Already recorded relative paths are migrated when the old file is absent and its counterpart is discovered under Apps; edited names, categories and descriptions are preserved. PApps does not move files automatically.

## Setup

Applications sort by category and displayed title. Double-click or press Enter/F2 to edit an app. Descriptions support multiple lines; the table displays up to 500 characters. Removing an entry leaves its files in place. Uninstall confirms the exact app folder before permanently deleting its files and app data; it blocks the main folder, Documents, shared app folders and directory links.

Window dimensions and the first three column widths save automatically. Description fills the remaining width, or uses a standard width with scrolling when space is limited.

Settings includes the Hide Portable display option, Documents location and update controls. Theming is reserved. Get Apps opens external download directories in your browser.

## Fonts

Windows and PApps-local fonts appear in their typeface with a source label. Put .ttf, .otf or .ttc files directly inside Documents/Fonts and refresh. Local families appear first; a local-only filter and name search are available. Options sit beside the list in wide windows and above it in narrow windows. Separate Setup and future main-interface font preferences are saved. Local fonts are loaded only inside PApps. Invalid files are skipped; unavailable selected fonts fall back to Segoe UI. Restart after replacing an already loaded font file.

## Settings compatibility

launcher.settings.json is saved beside the executable. At startup, existing settings are read and missing options are written with their defaults. Existing values and unknown fields, including unknown per-app fields, are preserved. A missing file is created. An unreadable or invalid file is left untouched, with saving disabled until it is repaired and PApps restarted.

## Updates and releases

Settings > Check for main app update reads the latest public stable release from https://github.com/Bodinxx/PApps/releases. The app compares the release tag with its embedded version. No releases currently exist, so the first check will report that none are available.

A compatible release must have:

- A version tag in vMAJOR.MINOR.PATCH format, for example v0.1.0.
- A Windows x64 self-contained single-file asset named PApps.exe.
- GitHub's SHA-256 digest for that uploaded asset.
- An executable product version matching the release tag.

When a newer compatible release exists, Download and restart downloads it, checks its size, SHA-256 digest and embedded version, then starts a private helper that waits for PApps to exit, atomically replaces only PApps.exe and restarts it. Settings, Apps, Documents and fonts are not replaced. The previous executable is retained as previous.exe in the .papps-update-* staging folder beside PApps.exe for recovery. Failed downloads or updates may leave staging folders; they can be removed after PApps and the updater have closed. No credentials are stored or required for the public repository. Checks are manual.

The local baseline version is 0.1.0. The .github/workflows/release.yml workflow builds and publishes PApps.exe when a vMAJOR.MINOR.PATCH tag is pushed. Commit and push the source/workflow first, then push the version tag. This workflow has not been run or published by this chat. Only trusted commits should be tagged for release.

For a manual release build:

    dotnet publish src/PApps.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:Version=0.1.0 -o Launcher

Attach Launcher/PApps.exe to the matching GitHub release. launcher.settings.json must never be included in an update asset.

## Development checks

    dotnet run --project Checks/Checks.csproj -c Release

Checks use isolated fixtures and cover discovery, sorting, descriptions, fonts, responsive layout, uninstall guards, release validation, checksum verification and settings migration. No real applications are uninstalled. A real update cannot be tested against GitHub until a compatible release is published.

Open Apps folder opens the configured main folder's Apps subfolder in File Explorer, creating Apps if missing. Choose a main folder first. After copying new portable app folders there, choose Scan folder.

## Main interface (0.2.0)
Normal launches now open the main PApps window. Missing/invalid settings or a main folder not yet marked configured opens Setup; choose a main folder and select Open launcher. Existing installations are treated as already configured when upgrading from older settings without that marker. Changing the main folder in Setup requires confirming it with Open launcher. Settings & options returns to Setup without launching another copy.

The main interface provides:
- Favourites above the library; toggle the star on any app. Favourites persist in settings.
- A library grouped by category, with List, Small, Medium and Large views.
- Click an app card to launch. Enter/Space also launch a focused card; Ctrl+D toggles its favourite status.
- Tooltips showing full descriptions.
- Sidebar search matching application names, categories and descriptions, filtering favourites and the library.
- Documents, Fonts and Apps folder links.
- A Show document files option for a lazy expandable document tree. Folders expand with the arrow; double-click a file to open it.
- Settings/options, help, close, app count and a background calculation of Apps-folder file size. The size calculation skips directory links and reports metadata size rather than physical disk usage.

Main window dimensions, maximized state, selected view, document browsing and favourites are saved separately from Setup preferences. The main interface uses the configured main font, including PApps-local fonts. It uses a navy sidebar, neutral workspace and teal accents. The Theming tab remains reserved.

## Views and sidebar (0.2.1)
The view selector now applies to both favourites and the library. Windows-style views are Large Icons, Medium Icons, Small Icons and List. The original card views remain available as Large Tiles, Medium Tiles, Small Tiles and Detailed Tiles. Older saved Small/Medium/Large choices migrate to the corresponding tiled labels; the original List migrates to Detailed Tiles.

Documents/Fonts and Apps folder/Settings appear in two-column rows with icons. Help and Close PApps share an equal-width bottom row with icons. Find stays fixed at the top, while information and the bottom actions stay fixed at the bottom. All middle workspace content, including the document browser, lives inside a scrolling region.

In 0.2.2, the document tree has no internal scrollbars. Its height follows the visible expanded nodes, leaving the middle sidebar as the single scroll area. Category headings use compact rounded light-blue bands with reduced spacing.

In 0.2.3, Settings > Theming controls Padding inside app items and Gap between app items (0–24 px, default 5 px each). Both apply to every library and favourite view. Item sizes now follow icon/text content and the padding setting. Spacing saves automatically and applies when returning to the launcher. Reset spacing to 5 px restores both defaults.
