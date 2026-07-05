using Acroball.Application.Models;

namespace Acroball.Application.Abstractions;

/// <summary>
/// Tracks recently opened files, most recent first, capped to a small number.
/// </summary>
public interface IRecentFilesService
{
    /// <summary>Returns the current list, most recently opened first.</summary>
    IReadOnlyList<RecentFileEntry> GetAll();

    /// <summary>Adds or refreshes an entry and persists the list.</summary>
    Task TouchAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>Removes an entry, when present, and persists the list.</summary>
    Task RemoveAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>Empties the list and persists it.</summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}

