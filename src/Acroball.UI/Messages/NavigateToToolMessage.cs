namespace Acroball.UI.Messages;

/// <summary>
/// Requests navigation to a page by id (a tool id, or the well-known
/// <c>"home"</c>/<c>"settings"</c> ids). Sent via the CommunityToolkit
/// weak-reference messenger so pages never hold the shell.
/// </summary>
/// <param name="ToolId">The target page id.</param>
public sealed record NavigateToToolMessage(string ToolId);

