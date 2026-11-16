namespace Memoria.FF7.Events;

public sealed record FieldScriptDispatch(int SourceEntity, int SourcePriority, int SourceProgramCounter, int TargetEntity, int TargetScript, int TargetPriority, int RequestMode, bool Accepted, bool Queued, long Tick);

public sealed partial class FieldMachine
{
    public Action<FieldScriptDispatch>? ScriptDispatched { get; set; }
}
