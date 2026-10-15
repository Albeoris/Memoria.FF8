namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    /// <summary>Transient host output of executed commands and animation contacts; device state never participates in VM waits or saves.</summary>
    public event Action<object>? AudioCommandExecuted;
}
