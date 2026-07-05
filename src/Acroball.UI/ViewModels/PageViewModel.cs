namespace Acroball.UI.ViewModels;

/// <summary>
/// Base class for view models hosted in the shell's content area. The
/// <see cref="ViewLocator"/> matches on this type.
/// </summary>
public abstract class PageViewModel : ViewModelBase
{
    /// <summary>Page title.</summary>
    public abstract string Title { get; }

    /// <summary>Icon key resolved against Theme/Icons.axaml.</summary>
    public abstract string IconKey { get; }
}

