using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Acroball.UI.ViewModels;

namespace Acroball.UI.Views;

/// <summary>Code-behind for the merge tool view.</summary>
public partial class MergeView : UserControl
{
    /// <summary>Creates the view.</summary>
    public MergeView()
    {
        InitializeComponent();
    }

    private MergeViewModel? ViewModel => DataContext as MergeViewModel;

    private async void OnFileItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not string path)
        {
            return;
        }

        if (!e.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var data = new DataTransfer();
        data.Add(DataTransferItem.CreateText(path));
        await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Move).ConfigureAwait(true);
    }

    private void OnFilesDragOver(object? sender, DragEventArgs e)
    {
        if (TryGetDroppedPaths(e).Any())
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void OnFilesDrop(object? sender, DragEventArgs e)
    {
        var paths = TryGetDroppedPaths(e).ToArray();
        if (paths.Length == 0 || ViewModel is null)
        {
            return;
        }

        ViewModel.AddFiles(paths);
        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void OnFileItemDragOver(object? sender, DragEventArgs e)
    {
        if (sender is Control control && control.DataContext is string && TryGetDraggedPath(e) is not null)
        {
            e.DragEffects = DragDropEffects.Move;
            e.Handled = true;
            return;
        }

        if (TryGetDroppedPaths(e).Any())
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void OnFileItemDrop(object? sender, DragEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not string targetPath || ViewModel is null)
        {
            return;
        }

        var draggedPath = TryGetDraggedPath(e);
        if (!string.IsNullOrWhiteSpace(draggedPath))
        {
            ViewModel.MoveFileBefore(draggedPath, targetPath);
            e.DragEffects = DragDropEffects.Move;
            e.Handled = true;
            return;
        }

        var paths = TryGetDroppedPaths(e).ToArray();
        if (paths.Length > 0)
        {
            ViewModel.AddFiles(paths);
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private static string? TryGetDraggedPath(DragEventArgs e)
        => e.DataTransfer.TryGetText();

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
