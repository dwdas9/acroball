using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Acroball.UI.ViewModels;

namespace Acroball.UI.Views;

/// <summary>Code-behind for the Organize tool view: drag-and-drop page reordering and file drops.</summary>
public partial class OrganizeView : UserControl
{
    /// <summary>Creates the view.</summary>
    public OrganizeView()
    {
        InitializeComponent();
    }

    private OrganizeViewModel? ViewModel => DataContext as OrganizeViewModel;

    private void OnFilesDragOver(object? sender, DragEventArgs e)
    {
        if (TryGetDroppedPaths(e).Any())
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private async void OnFilesDrop(object? sender, DragEventArgs e)
    {
        var paths = TryGetDroppedPaths(e).ToArray();
        if (paths.Length == 0 || ViewModel is null)
        {
            return;
        }

        foreach (var path in paths)
        {
            await ViewModel.AddFileAsync(path).ConfigureAwait(true);
        }

        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private async void OnPageTilePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not OrganizePageViewModel page)
        {
            return;
        }

        if (!e.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var data = new DataTransfer();
        data.Add(DataTransferItem.CreateText(page.Id.ToString()));
        await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Move).ConfigureAwait(true);
    }

    private void OnPageTileDragOver(object? sender, DragEventArgs e)
    {
        if (sender is Control control && control.DataContext is OrganizePageViewModel && TryGetDraggedPageId(e) is not null)
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

    private async void OnPageTileDrop(object? sender, DragEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not OrganizePageViewModel targetPage || ViewModel is null)
        {
            return;
        }

        var draggedId = TryGetDraggedPageId(e);
        if (draggedId is not null)
        {
            ViewModel.MovePageBefore(draggedId.Value, targetPage.Id);
            e.DragEffects = DragDropEffects.Move;
            e.Handled = true;
            return;
        }

        var paths = TryGetDroppedPaths(e).ToArray();
        if (paths.Length > 0)
        {
            foreach (var path in paths)
            {
                await ViewModel.AddFileAsync(path).ConfigureAwait(true);
            }

            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private static Guid? TryGetDraggedPageId(DragEventArgs e)
        => Guid.TryParse(e.DataTransfer.TryGetText(), out var id) ? id : null;

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
        if (string.IsNullOrWhiteSpace(text) || Guid.TryParse(text, out _))
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
