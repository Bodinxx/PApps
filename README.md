# PApps Setup

Run Launcher/PApps.exe. This is now a configuration screen; app launching has been removed. Double-click an app or press Enter/F2 to edit its name and category.

## Applications
Choose your main apps folder, scan recursively, add executables, edit entries, or remove library entries. Files are never deleted by removing an entry. The table sorts by category and then displayed application title. App icons come from the executables. PortableApps manifests are preferred; otherwise ordinary executables are discovered. CLI companions, common uninstallers and directory links are skipped.

## Settings
Font face and Large fonts are separate preferences for Setup and the future main interface. Setup uses 10 or 13 point text; the main-interface preview uses 10 or 16 points. Main-interface preferences are saved for future use.

Hide Portable removes Portable from displayed names without altering saved names or executable paths.

If the main apps folder contains documents, PApps records it as its My Documents location. Detection refreshes on startup, folder changes and scans, or with Refresh documents folder. This does not alter the Windows Documents folder or redirect other applications' file dialogs.

Check for main app update is a placeholder until a Git update location is supplied. It clearly reports that no update check was performed.

## Theming
Reserved blank tab.

## Get Apps
Browser links to PortableApps.com, The Portable Freeware Collection and winPenPack. Download/extract into the main apps folder and scan, or use Add app. PApps does not download or install apps.

## Development
C# / .NET 10 / Windows Forms. Open src/PApps.csproj with a compatible Visual Studio version.

    dotnet run --project Checks/Checks.csproj -c Release
    dotnet publish src/PApps.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o Launcher

Settings are saved beside the executable in launcher.settings.json. Remote caching and package updates are not implemented.

Each app has an optional multiline Description in Edit app. Existing entries default to an empty description. Newly discovered PortableApps entries import Details/Description from appinfo.ini. Rescanning preserves edited descriptions. Descriptions are saved for use in the future main interface and tooltips.

The Description table column shows a single-line preview of up to 500 characters; the full text stays in Edit app. The first three column widths and window dimensions/maximized state save automatically after resizing and on close. Description fills the remaining width when at least 160 pixels remain; otherwise it uses 320 pixels with horizontal scrolling.

Remove only removes a library entry. Uninstall permanently deletes the selected application's top-level folder within the main apps folder, including its app data, after showing the exact folder and requesting confirmation. It refuses main-folder executables, Documents, shared library app folders, apps outside the repository, and directory links. Close the app before uninstalling. Failed deletion can leave a partially removed folder; the library entry is retained.

## Fonts tab
Font settings now live on the Fonts tab. Windows and PApps-local families are listed with names rendered in their own typeface, a source label and a font-name search box. Separate Setup and future main-interface font selections remain saved independently.

When an available main apps folder is selected or opened, Documents and Documents\Fonts are created if absent. Existing files are preserved. Add .ttf, .otf or .ttc files directly inside Documents\Fonts and select Refresh fonts. Open fonts folder opens that location. Local-font loading can be disabled. Fonts are registered privately inside the PApps process, never installed for Windows or other apps. Missing selected fonts fall back to Segoe UI. Invalid font files are skipped. Restart PApps after replacing an already loaded font file.


## Main folder structure
The selected main folder contains Apps and Documents. Fonts are inside Documents\Fonts. Missing default folders are created automatically. Scans recurse only inside Apps, ignoring executables elsewhere in the main folder. Place each portable app folder inside Apps, then scan. Previously recorded relative app paths are updated when the original file is absent and the same relative app location is found under Apps; edited metadata is preserved. Existing files are not moved automatically. Uninstall is restricted to an app folder within Apps.

The Fonts tab places options on the left and the font list on the right at wide widths, stacking the list below on narrow windows. Refresh rechecks the current Documents\Fonts folder and rebuilds the visible list. Local font families appear first; use Show PApps local fonts only to filter the list. Font files must be directly inside Documents\Fonts.
