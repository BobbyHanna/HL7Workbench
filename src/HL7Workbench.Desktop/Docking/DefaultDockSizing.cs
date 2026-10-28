using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;

namespace HL7Workbench.Desktop.Docking;

/// <summary>Applies fresh/reset ratios once a real Dock viewport exists, never on ordinary resize or saved-layout restore.</summary>
internal static class DefaultDockSizing
{
    internal static Action Arm(DockControl dock, WorkspaceDockFactory factory)
    {
        var expected = factory.Root; var canceled = false;
        void Apply()
        {
            if (canceled || !ReferenceEquals(dock.Layout, expected) || !ReferenceEquals(factory.Root, expected)) return;
            Set("workspace-content", .73); Set("diagnostics-dock", .27);
            Set("documents", .78); Set("inspection-stack", .22);
            dock.InvalidateMeasure();
        }
        void Set(string id, double value)
        { if (factory.FindDockable(expected, item => item.Id == id) is { } item) item.Proportion = value; }
        void Sized(object? sender, SizeChangedEventArgs args) => Ready();
        void Ready()
        {
            if (dock.Bounds.Width <= 0 || dock.Bounds.Height <= 0) return;
            dock.SizeChanged -= Sized;
            // Dock may normalize proportions while templates first measure with
            // zero/unbounded space. Reassert defaults after finite measurement.
            // The root-identity guard makes an Opened saved-layout restore win.
            Dispatcher.UIThread.Post(Apply, DispatcherPriority.Loaded);
        }
        dock.SizeChanged += Sized; Ready();
        return () => { canceled = true; dock.SizeChanged -= Sized; };
    }
}
