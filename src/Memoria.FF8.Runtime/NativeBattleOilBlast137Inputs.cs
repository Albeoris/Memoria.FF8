namespace Memoria.FF8.Runtime;

public sealed record NativeBattleOilBlast137Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleOilBlast137State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleOilBlast137PointStep(NativeBattleOilBlast137Frame Frame, NativeBattleVector Point);
public sealed record NativeBattleOilBlast137ActStep(NativeBattleOilBlast137Frame Frame, int Remaining);
public abstract record NativeBattleOilBlast137Operation;
public sealed record NativeBattleOilBlast137Sound(uint Packed) : NativeBattleOilBlast137Operation;
public sealed record NativeBattleOilBlast137Contact(int Index) : NativeBattleOilBlast137Operation;
public sealed record NativeBattleOilBlast137InitializeAct(uint TaskAddress, int WorkspaceOffset, int WorkspaceBytes, NativeBattleGfAnimationSource Source) : NativeBattleOilBlast137Operation;
public sealed record NativeBattleOilBlast137DrawCustom84(uint Temporary84) : NativeBattleOilBlast137Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleOilBlast137DrawStatic88(uint Temporary88, uint SourceAddress) : NativeBattleOilBlast137Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleOilBlast137DrawSprite180(uint Temporary180, uint SourceAddress) : NativeBattleOilBlast137Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}

public sealed record NativeBattleOilBlast137Inputs(NativeBattleActionPacketsState Packets, NativeBattleSummonColors Colors, Func<NativeBattleOilBlast137Frame, uint, byte, short, NativeBattleOilBlast137PointStep> ReadBone502170, Func<NativeBattleOilBlast137Frame, NativeBattlePackedMatrix32> ReadCamera, Func<uint, short> ReadModelYaw, Func<NativeBattleOilBlast137Frame, uint, int, int, NativeBattleGfAnimationSource, NativeBattleEffectArena, NativeBattleOilBlast137ActStep> AdvanceAct, Func<NativeBattleOilBlast137Frame, NativeBattleOilBlast137Operation, NativeBattleOilBlast137Frame> Execute)
{
    public bool CapturePresentation { get; init; }
}
