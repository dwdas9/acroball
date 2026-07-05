using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace Acroball.UI.Services;

/// <summary>Avalonia-backed implementation of the file dialog abstraction.</summary>
public sealed class AvaloniaFileDialogService : IFileDialogService
{
    private readonly TopLevel? _topLevel = TopLevel.GetTopLevel(
        (global::Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow);

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>?> PickFilesAsync()
    {
        if (_topLevel is null)
        {
            return null;
        }

        var files = await _topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose PDF files",
            AllowMultiple = true,
            FileTypeFilter = [new FilePickerFileType("PDF files") { Patterns = ["*.pdf"] }],
        });

        return files.Select(file => file.TryGetLocalPath() ?? string.Empty)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<string?> PickSaveFileAsync(string initialFileName = "")
    {
        if (_topLevel is null)
        {
            return null;
        }

        var file = await _topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Choose output file",
            SuggestedFileName = initialFileName,
            DefaultExtension = ".pdf",
        });

        return file?.TryGetLocalPath();
    }

    /// <inheritdoc />
    public async Task<string?> PickFolderAsync()
    {
        if (_topLevel is null)
        {
            return null;
        }

        var folder = await _topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose output folder",
        });

        return folder.FirstOrDefault()?.TryGetLocalPath();
    }
}
