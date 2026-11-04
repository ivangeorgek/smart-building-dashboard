using Microsoft.AspNetCore.Components;
using SmartBuildingDashboard.Services;

namespace SmartBuildingDashboard.Components.Shared;

/// <summary>Subscribes a live view to the circuit's state and always detaches on navigation.</summary>
public abstract class BuildingComponentBase : ComponentBase, IDisposable
{
    [Inject] protected BuildingStateService State { get; set; } = default!;
    private bool _disposed;
    protected override void OnInitialized() => State.Changed += Refresh;
    private void Refresh()
    {
        if (!_disposed) _ = InvokeAsync(() => { if (!_disposed) StateHasChanged(); });
    }
    public void Dispose()
    {
        _disposed = true;
        State.Changed -= Refresh;
        GC.SuppressFinalize(this);
    }
}
