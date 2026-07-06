using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Acroball.UI.ViewModels;

namespace Acroball.UI.Views;

/// <summary>Code-behind for the metadata tool view.</summary>
public partial class MetadataView : UserControl
{
    /// <summary>Creates the view.</summary>
    public MetadataView()
    {
        InitializeComponent();
    }

    private MetadataViewModel? ViewModel => DataContext as MetadataViewModel;

    private void OnInputDragOver(object? sender, DragEventArgs e)
    {
        if (TryGetDroppedPaths(e).Any())
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private async void OnInputDrop(object? sender, DragEventArgs e)
    {
        var path = TryGetDroppedPaths(e).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(path) || ViewModel is null)
        {
            return;
        }

        await ViewModel.SetInputFileAsync(path).ConfigureAwait(true);
        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private static IEnumerable<string> TryGetDroppedPaths(DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles();
        if (files is not null)
        {
            foreach (var item in files)
            {
                if (item.TryGetLocalPath() is { Length: > 0 } path)
                {
                    yield return path;
                }
            }
        }

        var text = e.DataTransfer.TryGetText();
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        foreach (var line in text.Split(new[] { '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = line.Trim().Trim('"');
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                yield return candidate;
            }
        }
    }
}
