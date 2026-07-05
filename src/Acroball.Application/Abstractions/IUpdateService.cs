namespace Acroball.Application.Abstractions;

/// <summary>Information about an available application update.</summary>
/// <param name="Version">The available version, e.g. <c>"1.2.0"</c>.</param>
/// <param name="ReleaseNotesUrl">Web page describing the release, when any.</param>
public sealed record UpdateInfo(string Version, string? ReleaseNotesUrl);

/// <summary>
/// Checks for application updates. The production implementation (Velopack)
/// arrives with the packaging milestone; until then a null implementation is
/// registered. See ADR-0008.
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// <see langword="false"/> when this build cannot self-update (e.g. dev
    /// builds, distro packages); UI should then hide update actions.
    /// </summary>
    bool IsSupported { get; }

    /// <summary>Returns the available update, or <see langword="null"/> when up to date or unsupported.</summary>
    Task<UpdateInfo?> CheckForUpdatesAsync(CancellationToken cancellationToken = default);
}

