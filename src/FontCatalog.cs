using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace PApps;

public sealed class FontCatalog : IDisposable
{
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    static extern int AddFontResourceEx(string name, uint flags, IntPtr reserved);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    static extern bool RemoveFontResourceEx(string name, uint flags, IntPtr reserved);
    readonly string[] installed;
    readonly Dictionary<string, PrivateFontCollection> loaded = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, FontFamily> active = new(StringComparer.OrdinalIgnoreCase);
    public IEnumerable<string> Names => installed.Concat(active.Keys).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n);
    public bool IsLocal(string name) => active.ContainsKey(name);
    public int LocalCount => active.Count;
    public FontCatalog()
    {
        using var fonts = new InstalledFontCollection();
        installed = fonts.Families.Select(f => f.Name).ToArray();
    }
    public List<string> Refresh(string folder, bool enabled)
    {
        active.Clear();
        var errors = new List<string>();
        if (!enabled || !Directory.Exists(folder)) return errors;
        try
        {
            foreach (var file in Directory.EnumerateFiles(folder).Where(p => new[] { ".ttf", ".otf", ".ttc" }.Contains(Path.GetExtension(p).ToLowerInvariant())))
            {
                try
                {
                    if (!loaded.TryGetValue(file, out var collection))
                    {
                        collection = new PrivateFontCollection();
                        try
                        {
                            collection.AddFontFile(file);
                            // WinForms uses GDI as well as GDI+. FR_PRIVATE exposes this font only to this process.
                            if (AddFontResourceEx(file, 0x10, IntPtr.Zero) == 0) throw new InvalidOperationException("Unsupported font file.");
                            loaded[file] = collection;
                        }
                        catch { collection.Dispose(); throw; }
                    }
                    foreach (var family in collection.Families) active[family.Name] = family;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException or OutOfMemoryException or System.Runtime.InteropServices.ExternalException)
                { errors.Add(Path.GetFileName(file)); }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { errors.Add("Fonts folder unavailable"); }
        return errors;
    }
    public Font Create(string name, float size)
    {
        try
        {
            if (active.TryGetValue(name, out var family))
            {
                var style = new[] { FontStyle.Regular, FontStyle.Bold, FontStyle.Italic, FontStyle.Bold | FontStyle.Italic }.First(family.IsStyleAvailable);
                return new Font(family, size, style);
            }
            return new Font(installed.Contains(name, StringComparer.OrdinalIgnoreCase) ? name : "Segoe UI", size);
        }
        catch (ArgumentException) { return new Font("Segoe UI", size); }
    }
    public void Dispose()
    {
        foreach (var pair in loaded) { RemoveFontResourceEx(pair.Key, 0x10, IntPtr.Zero); pair.Value.Dispose(); }
        loaded.Clear();
    }
}
