namespace Memoria.FF8.Runtime;

public abstract record NativeBattleUltraWaves184Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleUltraWaves184BindCaster(byte Actor, uint SaveAddress, uint ResourceAddress) : NativeBattleUltraWaves184Operation;
public sealed record NativeBattleUltraWaves184RingSequence(byte Actor, byte Sequence) : NativeBattleUltraWaves184Operation;
public sealed record NativeBattleUltraWaves184Sound(uint Value, int Argument1, int Argument2) : NativeBattleUltraWaves184Operation;
public sealed record NativeBattleUltraWaves184ContactAll : NativeBattleUltraWaves184Operation;
public sealed record NativeBattleUltraWaves184CaptureStage : NativeBattleUltraWaves184Operation;
public sealed record NativeBattleUltraWaves184DrawGrid(short Age, uint Color) : NativeBattleUltraWaves184Operation;
public sealed record NativeBattleUltraWaves184StageVisibility(bool Visible) : NativeBattleUltraWaves184Operation;
public sealed record NativeBattleUltraWaves184RestoreTexture(uint Destination, uint CompletionAddress) : NativeBattleUltraWaves184Operation;
public sealed record NativeBattleUltraWaves184TaskStep(NativeBattleUltraWaves184Frame Frame, int Result);
public sealed record NativeBattleUltraWaves184AnchorStep(NativeBattleUltraWaves184Frame Frame, NativeBattleModuleAnchor Anchor);
public sealed record NativeBattleUltraWaves184Inputs(NativeBattleActionPacketsState Packets, uint GraphicsFlags, Func<int, NativeBattleActionMatrix> ReadWorld, Func<NativeBattleUltraWaves184Frame, int, short, short, NativeBattleUltraWaves184AnchorStep> ReadAnchor502170, Func<NativeBattleUltraWaves184Frame, NativeBattleUltraWaves184Frame> InitializeGrid, Func<NativeBattleUltraWaves184Frame, uint, NativeBattleUltraWaves184Resources, NativeBattleUltraWaves184TaskStep> AdvanceEmitter, Func<NativeBattleUltraWaves184Frame, NativeBattleUltraWaves184Operation, NativeBattleUltraWaves184Frame> Execute);
