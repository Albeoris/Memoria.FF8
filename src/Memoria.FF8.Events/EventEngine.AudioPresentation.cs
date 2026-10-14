namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    /// <summary>Transient host output of executed commands; never participates in VM state, waits, saves or replay.</summary>
    public event Action<object>? AudioCommandExecuted;
}
