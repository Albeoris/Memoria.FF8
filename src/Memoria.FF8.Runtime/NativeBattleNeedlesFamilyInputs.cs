namespace Memoria.FF8.Runtime;

public sealed record NativeBattleNeedlesFamilyFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleNeedlesFamilyState State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}
public sealed record NativeBattleNeedlesFamilyPointStep(NativeBattleNeedlesFamilyFrame Frame, NativeBattleVector Position);
public abstract record NativeBattleNeedlesFamilyOperation;
public sealed record NativeBattleNeedlesFamilyRegisterCamera(NativeBattleEffectHeapAllocation Camera) : NativeBattleNeedlesFamilyOperation;
public sealed record NativeBattleNeedlesFamilyEnqueueTim(int ArenaOffset) : NativeBattleNeedlesFamilyOperation;
public sealed record NativeBattleNeedlesFamilySound(uint Packed) : NativeBattleNeedlesFamilyOperation;
public sealed record NativeBattleNeedlesFamilyContact(int Index) : NativeBattleNeedlesFamilyOperation;
public sealed record NativeBattleNeedlesFamilyDrawNeedle(uint Temporary220, int Index) : NativeBattleNeedlesFamilyOperation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleNeedlesFamilyDrawSpark(uint Sprite40, uint Temporary180, int Index) : NativeBattleNeedlesFamilyOperation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleNeedlesFamilyInputs(NativeBattleActionPacketsState Packets, Func<int, byte> ReadBoneCount, Func<NativeBattleNeedlesFamilyFrame, byte, byte, short, NativeBattleNeedlesFamilyPointStep> ReadBone502170, Func<NativeBattleNeedlesFamilyFrame, NativeBattlePackedMatrix32> ReadCamera, Func<NativeBattleNeedlesFamilyFrame, uint> ReadGraphicsFlags, Func<NativeBattleNeedlesFamilyFrame, NativeBattleNeedlesFamilyOperation, NativeBattleNeedlesFamilyFrame> Execute);
