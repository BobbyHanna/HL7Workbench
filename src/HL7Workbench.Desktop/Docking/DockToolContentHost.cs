using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Dock.Model.Core;

namespace HL7Workbench.Desktop.Docking;

/// <summary>Tracks replaceable feature content while Dock retains the same dockable/template instance.</summary>
internal sealed class DockToolContentHost(IDockable dockable, Func<object?, Control> build) : ContentControl
{
    private object? displayed;
    private bool subscribed;
    private void Refresh()
    {
        if (ReferenceEquals(displayed, dockable.Context) && Content is not null) return;
        // Detach the old host before attaching its replacement so bounded reads are canceled
        // and feature subscriptions cannot remain attached to a released tool model.
        Content = null; displayed = dockable.Context;
        if (displayed is not null) Content = build(displayed);
    }
    private void Changed(object? sender, PropertyChangedEventArgs e)
    { if (e.PropertyName is nameof(IDockable.Context) or null or "") Refresh(); }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!subscribed && dockable is INotifyPropertyChanged changes) { changes.PropertyChanged += Changed; subscribed = true; }
        Refresh();
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (subscribed && dockable is INotifyPropertyChanged changes) changes.PropertyChanged -= Changed;
        subscribed = false; Content = null; displayed = null;
        base.OnDetachedFromVisualTree(e);
    }
}
