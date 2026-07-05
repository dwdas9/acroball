namespace Acroball.Application.Models;

/// <summary>
/// One entry in the recent-files list.
/// </summary>
/// <param name="FilePath">Absolute path of the file.</param>
/// <param name="LastOpenedUtc">When the file was last opened in Acroball.</param>
public sealed record RecentFileEntry(string FilePath, DateTimeOffset LastOpenedUtc);

