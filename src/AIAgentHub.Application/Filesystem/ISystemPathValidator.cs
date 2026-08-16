namespace AIAgentHub.Application.Filesystem;

public interface ISystemPathValidator
{
    /// <summary>
    /// Gets the list of critical system directory paths and wildcard patterns forbidden for workspaces and browsing.
    /// </summary>
    IReadOnlyList<string> ForbiddenFolders { get; }

    /// <summary>
    /// Checks whether a given path is forbidden from being selected as a workspace root (e.g. root drives like C:\ or protected system folders).
    /// </summary>
    bool IsForbiddenForWorkspace(string? path, out string? reason);

    /// <summary>
    /// Checks whether a given path is forbidden from being navigated to during browsing (protected system folders, but permits root drives).
    /// </summary>
    bool IsForbiddenForBrowsing(string? path, out string? reason);

    /// <summary>
    /// Default path validation checking workspace validity.
    /// </summary>
    bool IsPathForbidden(string? path, out string? reason);
}
