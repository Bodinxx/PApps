using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace PApps;

public sealed record UpdateRelease(Version Version, string DownloadUrl, string Sha256, long Size, string Tag = "", bool IsPrerelease = false, DateTimeOffset PublishedAt = default);
public sealed record UpdateRequest(string Target, string Incoming, string Sha256, int ParentId, string Helper, string ReleaseTag = "", bool IsPrerelease = false);
public sealed record UpdateReceipt(string Sha256, string Tag, bool IsPrerelease);

public static class AppUpdater
{
    const string ReleasePrefix = "https://github.com/Bodinxx/PApps/releases/download/";
    public static Version CurrentVersion => Normalize(Assembly.GetExecutingAssembly().GetName().Version!);
    public static string CurrentDisplayVersion => Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? CurrentVersion.ToString();
    static Version Normalize(Version version) => new(version.Major, version.Minor, Math.Max(0, version.Build));
    public static Version ParseVersion(string tag)
    {
        var match = System.Text.RegularExpressions.Regex.Match(tag,
            @"^(?:v)?(?<core>[0-9]+\.[0-9]+\.[0-9]+)(?:-(?:alpha|beta|rc)(?:[.-]?[0-9]+)?)?$|^(?:alpha|beta|rc)-(?<core>[0-9]+\.[0-9]+\.[0-9]+)(?:[.-][0-9]+)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!match.Success || !Version.TryParse(match.Groups["core"].Value, out var version))
            throw new InvalidDataException("Use a numeric version tag, optionally with an alpha, beta or rc suffix.");
        return Normalize(version);
    }
    static HttpClient Client(TimeSpan timeout)
    {
        var client = new HttpClient { Timeout = timeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PApps/" + CurrentVersion);
        return client;
    }
    public static UpdateRelease ParseRelease(string json, bool includePrereleases = false)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var tag = root.GetProperty("tag_name").GetString()!;
        var version = ParseVersion(tag);
        var isPrerelease = root.GetProperty("prerelease").GetBoolean() || System.Text.RegularExpressions.Regex.IsMatch(tag, "alpha|beta|rc", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (root.GetProperty("draft").GetBoolean() || (isPrerelease && !includePrereleases))
            throw new InvalidDataException("This release is not available on the selected update channel.");
        var asset = root.GetProperty("assets").EnumerateArray().FirstOrDefault(a => a.GetProperty("name").GetString() == "PApps.exe");
        if (asset.ValueKind == JsonValueKind.Undefined) throw new InvalidDataException("The release has no PApps.exe download attached.");
        var url = asset.GetProperty("browser_download_url").GetString()!;
        if (!url.StartsWith(ReleasePrefix, StringComparison.Ordinal) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https")
            throw new InvalidDataException("The download must be an asset in Bodinxx/PApps Releases.");
        var digest = asset.TryGetProperty("digest", out var d) ? d.GetString() : null;
        if (digest is null || !digest.StartsWith("sha256:", StringComparison.Ordinal) || digest.Length != 71 || !digest[7..].All(Uri.IsHexDigit))
            throw new InvalidDataException("The release asset does not provide a SHA-256 verification digest. Re-upload PApps.exe to the release.");
        var size = asset.GetProperty("size").GetInt64();
        if (size <= 0 || size > 512L * 1024 * 1024) throw new InvalidDataException("The release download size is invalid.");
        var published = root.TryGetProperty("published_at", out var date) && date.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(date.GetString(), out var parsedDate) ? parsedDate : DateTimeOffset.MinValue;
        return new UpdateRelease(version, url, digest[7..], size, tag, isPrerelease, published);
    }
    public static UpdateRelease? SelectLatest(string json, bool includePrereleases)
    {
        using var document = JsonDocument.Parse(json);
        var releases = new List<UpdateRelease>();
        foreach (var release in document.RootElement.EnumerateArray())
        {
            try { releases.Add(ParseRelease(release.GetRawText(), includePrereleases)); }
            catch (Exception ex) when (ex is InvalidDataException or KeyNotFoundException or InvalidOperationException or FormatException) { }
        }
        return releases.OrderByDescending(r => r.PublishedAt).ThenByDescending(r => r.Version).FirstOrDefault();
    }
    public static async Task<UpdateRelease?> CheckAsync(CancellationToken cancellationToken, bool includePrereleases = false)
    {
        using var client = Client(TimeSpan.FromSeconds(30));
        UpdateRelease? latest = null;
        for (var page = 1; page <= 10; page++)
        {
            using var response = await client.GetAsync($"https://api.github.com/repos/Bodinxx/PApps/releases?per_page=100&page={page}", cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            if (response.StatusCode == HttpStatusCode.Forbidden) throw new HttpRequestException("GitHub denied the request or its rate limit was reached. Try again later.");
            response.EnsureSuccessStatusCode();
            var candidate = SelectLatest(await response.Content.ReadAsStringAsync(cancellationToken), includePrereleases);
            if (candidate is not null && (latest is null || candidate.PublishedAt > latest.PublishedAt || candidate.PublishedAt == latest.PublishedAt && candidate.Version > latest.Version)) latest = candidate;
            if (!response.Headers.TryGetValues("Link", out var links) || !links.Any(l => l.Contains("rel=\"next\""))) return latest;
        }
        throw new InvalidDataException("Too many release pages to determine the newest release. Please check GitHub directly.");
    }
    public static bool IsUpdate(UpdateRelease release, bool includePrereleases, string installedSha256, bool installedPrerelease)
    {
        if (release.IsPrerelease && !includePrereleases || release.Version < CurrentVersion) return false;
        if (release.Sha256.Equals(installedSha256, StringComparison.OrdinalIgnoreCase)) return false;
        return release.Version > CurrentVersion || includePrereleases || installedPrerelease;
    }
    public static async Task<bool> IsUpdateAsync(UpdateRelease release, bool includePrereleases, CancellationToken cancellationToken)
    {
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate PApps.exe.");
        var hash = await Task.Run(() => { using var file = File.OpenRead(executable); return Convert.ToHexString(SHA256.HashData(file)); }, cancellationToken);
        var installedPrerelease = CurrentDisplayVersion.Contains('-');
        try
        {
            var receipt = JsonSerializer.Deserialize<UpdateReceipt>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "launcher.update.json")));
            if (receipt is not null && string.Equals(receipt.Sha256, hash, StringComparison.OrdinalIgnoreCase)) installedPrerelease |= receipt.IsPrerelease;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
        return IsUpdate(release, includePrereleases, hash, installedPrerelease);
    }
    public static void Verify(string file, string expectedHash)
    {
        using var stream = File.OpenRead(file);
        var actual = Convert.ToHexString(SHA256.HashData(stream));
        if (!actual.Equals(expectedHash, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Downloaded file failed SHA-256 verification.");
    }
    public static async Task<string> PrepareAsync(UpdateRelease release, IProgress<int> progress, CancellationToken cancellationToken)
    {
        if (release.Version < CurrentVersion) throw new InvalidOperationException("An older app version cannot be installed as an update.");
        var target = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate PApps.exe.");
        if (!Path.GetFileName(target).Equals("PApps.exe", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Run the published PApps.exe to install updates.");
        var stage = Path.Combine(AppContext.BaseDirectory, ".papps-update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        var incoming = Path.Combine(stage, "incoming.exe");
        using var client = Client(TimeSpan.FromMinutes(15));
        using var response = await client.GetAsync(release.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var output = new FileStream(incoming, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
        {
            var buffer = new byte[81920];
            long count = 0;
            var lastPercent = -1;
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
            {
                count += read;
                if (count > release.Size) throw new InvalidDataException("Download exceeded the expected file size.");
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                var percent = (int)(100 * count / release.Size);
                if (percent != lastPercent) { progress.Report(percent); lastPercent = percent; }
            }
            if (count != release.Size) throw new InvalidDataException("Download was incomplete.");
        }
        Verify(incoming, release.Sha256);
        var productVersion = FileVersionInfo.GetVersionInfo(incoming).ProductVersion?.Split('+')[0] ?? "";
        if (ParseVersion(productVersion) != release.Version) throw new InvalidDataException("The executable version does not match the release tag.");
        var helper = Path.Combine(stage, "updater.exe");
        File.Copy(target, helper);
        var requestFile = Path.Combine(stage, "request.json");
        File.WriteAllText(requestFile, JsonSerializer.Serialize(new UpdateRequest(target, incoming, release.Sha256, Environment.ProcessId, helper, release.Tag, release.IsPrerelease)));
        return requestFile;
    }
    public static void StartHelper(string requestFile)
    {
        var request = JsonSerializer.Deserialize<UpdateRequest>(File.ReadAllText(requestFile))!;
        var start = new ProcessStartInfo(request.Helper) { UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add("--apply-update");
        start.ArgumentList.Add(requestFile);
        _ = Process.Start(start) ?? throw new IOException("Could not start the updater.");
    }
    public static void Apply(string requestFile)
    {
        string? target = null;
        string? backup = null;
        var replaced = false;
        try
        {
            var stage = Path.GetDirectoryName(Path.GetFullPath(requestFile))!;
            if (!Path.GetFileName(stage).StartsWith(".papps-update-", StringComparison.Ordinal)) throw new InvalidDataException("Invalid updater folder.");
            var request = JsonSerializer.Deserialize<UpdateRequest>(File.ReadAllText(requestFile)) ?? throw new InvalidDataException("Missing update request.");
            target = Path.GetFullPath(request.Target);
            if (!Path.GetDirectoryName(target)!.Equals(Path.GetDirectoryName(stage), StringComparison.OrdinalIgnoreCase)
                || !Path.GetFullPath(request.Helper).Equals(Environment.ProcessPath, StringComparison.OrdinalIgnoreCase)
                || !Path.GetFullPath(request.Incoming).Equals(Path.Combine(stage, "incoming.exe"), StringComparison.OrdinalIgnoreCase)
                || !Path.GetFileName(target).Equals("PApps.exe", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid updater paths.");
            Verify(request.Incoming, request.Sha256);
            try
            {
                using var parent = Process.GetProcessById(request.ParentId);
                if (!parent.WaitForExit(60000)) throw new IOException("PApps did not close. Close it before trying again.");
            }
            catch (ArgumentException) { /* Parent already exited. */ }
            backup = Path.Combine(stage, "previous.exe");
            // Atomic replacement leaves all other files, including launcher.settings.json, untouched.
            for (var attempt = 0; ; attempt++)
            {
                try { File.Replace(request.Incoming, target, backup); break; }
                catch (IOException) when (attempt < 39) { Thread.Sleep(250); }
            }
            replaced = true;
            var receipt = Path.Combine(Path.GetDirectoryName(target)!, "launcher.update.json");
            File.WriteAllText(receipt + ".tmp", JsonSerializer.Serialize(new UpdateReceipt(request.Sha256, request.ReleaseTag, request.IsPrerelease)));
            File.Move(receipt + ".tmp", receipt, true);
            _ = Process.Start(new ProcessStartInfo(target) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(target) })
                ?? throw new IOException("Could not restart PApps.");
        }
        catch (Exception ex)
        {
            if (replaced && target is not null && backup is not null)
            {
                try { File.Copy(backup, target, true); } catch { }
            }
            MessageBox.Show("Update could not be completed. Your settings were not replaced.\n\n" + ex.Message + "\n\nUpdate files: " + Path.GetDirectoryName(requestFile), "PApps update", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
