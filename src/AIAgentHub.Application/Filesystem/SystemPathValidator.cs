using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace AIAgentHub.Application.Filesystem;

public sealed class SystemPathValidator : ISystemPathValidator
{
    private readonly Lazy<IReadOnlyList<string>> _forbiddenFoldersLazy;
    private readonly Lazy<IReadOnlyList<Regex>> _compiledPatternsLazy;

    public SystemPathValidator()
    {
        _forbiddenFoldersLazy = new Lazy<IReadOnlyList<string>>(BuildForbiddenFoldersList);
        _compiledPatternsLazy = new Lazy<IReadOnlyList<Regex>>(BuildCompiledPatterns);
    }

    public IReadOnlyList<string> ForbiddenFolders => _forbiddenFoldersLazy.Value;

    public bool IsForbiddenForBrowsing(string? path, out string? reason)
    {
        reason = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path.Trim());
        }
        catch (Exception ex)
        {
            reason = $"Invalid path format: {ex.Message}";
            return true;
        }

        var normalized = NormalizePath(fullPath);

        // Check exact match or child of any forbidden folder/pattern
        foreach (var patternRegex in _compiledPatternsLazy.Value)
        {
            if (patternRegex.IsMatch(normalized))
            {
                reason = $"Directory '{fullPath}' is a protected system folder and cannot be opened.";
                return true;
            }
        }

        return false;
    }

    public bool IsForbiddenForWorkspace(string? path, out string? reason)
    {
        reason = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            reason = "Path cannot be empty.";
            return true;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path.Trim());
        }
        catch (Exception ex)
        {
            reason = $"Invalid path format: {ex.Message}";
            return true;
        }

        // 1. Check if it is a protected system folder
        if (IsForbiddenForBrowsing(fullPath, out reason))
        {
            reason = $"Directory '{fullPath}' is a protected system folder and cannot be used as a workspace.";
            return true;
        }

        var normalized = NormalizePath(fullPath);

        // 2. Check if bare root drive directly (e.g. "C:\" or "C:" or "/")
        var root = Path.GetPathRoot(fullPath);
        if (!string.IsNullOrEmpty(root))
        {
            var normalizedRoot = NormalizePath(root);
            if (normalized.Equals(normalizedRoot, GetComparison()) || normalized.Equals(normalizedRoot.TrimEnd('/'), GetComparison()))
            {
                reason = $"The root drive '{fullPath}' cannot be used as a workspace folder.";
                return true;
            }
        }

        return false;
    }

    public bool IsPathForbidden(string? path, out string? reason) => IsForbiddenForWorkspace(path, out reason);

    private static StringComparison GetComparison() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private static string NormalizePath(string path)
    {
        var clean = path.Replace('\\', '/').TrimEnd('/');
        return string.IsNullOrEmpty(clean) ? "/" : clean;
    }

    private static IReadOnlyList<string> BuildForbiddenFoldersList()
    {
        var list = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            AddWindowsSpecialFolder(list, Environment.SpecialFolder.Windows);
            AddWindowsSpecialFolder(list, Environment.SpecialFolder.System);
            AddWindowsSpecialFolder(list, Environment.SpecialFolder.SystemX86);
            AddWindowsSpecialFolder(list, Environment.SpecialFolder.ProgramFiles);
            AddWindowsSpecialFolder(list, Environment.SpecialFolder.ProgramFilesX86);
            AddWindowsSpecialFolder(list, Environment.SpecialFolder.CommonProgramFiles);
            AddWindowsSpecialFolder(list, Environment.SpecialFolder.CommonProgramFilesX86);
            AddWindowsSpecialFolder(list, Environment.SpecialFolder.CommonApplicationData);

            // Generic wildcards across any drive letter
            list.Add(@"*:\Windows");
            list.Add(@"*:\Program Files");
            list.Add(@"*:\Program Files (x86)");
            list.Add(@"*:\ProgramData");
            list.Add(@"*:\$Recycle.Bin");
            list.Add(@"*:\System Volume Information");
            list.Add(@"*:\Recovery");
            list.Add(@"*:\Boot");
            list.Add(@"*:\Windows.old");
        }
        else
        {
            // Linux & macOS common system roots
            list.Add("/bin");
            list.Add("/sbin");
            list.Add("/boot");
            list.Add("/dev");
            list.Add("/etc");
            list.Add("/lib");
            list.Add("/lib32");
            list.Add("/lib64");
            list.Add("/proc");
            list.Add("/root");
            list.Add("/run");
            list.Add("/sys");
            list.Add("/usr");
            list.Add("/var");
            list.Add("/opt");
            list.Add("/snap");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                list.Add("/System");
                list.Add("/Library");
                list.Add("/private");
                list.Add("/Volumes");
            }
        }

        return list.ToList();
    }

    private static void AddWindowsSpecialFolder(HashSet<string> set, Environment.SpecialFolder folder)
    {
        try
        {
            var p = Environment.GetFolderPath(folder);
            if (!string.IsNullOrWhiteSpace(p))
            {
                set.Add(p);
            }
        }
        catch
        {
            // Ignore if unavailable
        }
    }

    private IReadOnlyList<Regex> BuildCompiledPatterns()
    {
        var regexes = new List<Regex>();
        var isWindowsOrMac = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        var options = RegexOptions.Compiled | (isWindowsOrMac ? RegexOptions.IgnoreCase : RegexOptions.None);

        foreach (var item in ForbiddenFolders)
        {
            var norm = NormalizePath(item);
            // Replace wildcard "*:" or "*" with regex equivalent
            var pattern = "^" + Regex.Escape(norm)
                .Replace(@"\*", "[^/]+")
                + "(/.*)?$";

            regexes.Add(new Regex(pattern, options));
        }

        return regexes;
    }
}
