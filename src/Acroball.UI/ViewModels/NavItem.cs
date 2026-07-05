namespace Acroball.UI.ViewModels;

/// <summary>One entry in the sidebar navigation list.</summary>
/// <param name="Id">Page id passed to navigation.</param>
/// <param name="Label">Display label.</param>
/// <param name="IconKey">Icon key resolved against Theme/Icons.axaml.</param>
public sealed record NavItem(string Id, string Label, string IconKey);

