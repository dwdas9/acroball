namespace Acroball.UI.Services;

/// <summary>Abstraction over file and folder picking operations.</summary>
public interface IFileDialogService
{
    /// <summary>Prompts the user to select one or more files.</summary>
    Task<IReadOnlyList<string>?> PickFilesAsync();

    /// <summary>Prompts the user to choose a destination file.</summary>
    Task<string?> PickSaveFileAsync(string initialFileName = "");

    /// <summary>Prompts the user to choose a folder.</summary>
    Task<string?> PickFolderAsync();
}
