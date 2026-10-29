using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEnergyBomber217Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEnergyBomber217State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}
public sealed record NativeBattleEnergyBomber217AnchorStep(NativeBattleEnergyBomber217Frame Frame, NativeBattleModuleAnchor Anchor);
public sealed record NativeBattleEnergyBomber217PointStep(NativeBattleEnergyBomber217Frame Frame, NativeBattleVector Position);
public sealed record NativeBattleEnergyBomber217ActStep(NativeBattleEnergyBomber217Frame Frame, int Remaining);
public abstract record NativeBattleEnergyBomber217Operation
{
    [JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleEnergyBomber217Stage(short Depth, uint Color) : NativeBattleEnergyBomber217Operation;
public sealed record NativeBattleEnergyBomber217Sound(uint Packed) : NativeBattleEnergyBomber217Operation;
public sealed record NativeBattleEnergyBomber217Contact(int Index) : NativeBattleEnergyBomber217Operation;
public sealed record NativeBattleEnergyBomber217DrawTrail120(uint Temporary120, NativeBattleVector Position, int Cue) : NativeBattleEnergyBomber217Operation;
public sealed record NativeBattleEnergyBomber217DrawSprite180(uint TaskAddress, uint Temporary180) : NativeBattleEnergyBomber217Operation;
public sealed record NativeBattleEnergyBomber217Inputs(NativeBattleActionPacketsState Packets, Func<NativeBattleEnergyBomber217Frame, int, NativeBattleEnergyBomber217AnchorStep> ReadAnchor571400, Func<NativeBattleEnergyBomber217Frame, int, byte, short, NativeBattleEnergyBomber217PointStep> ReadBone502170, Func<NativeBattleEnergyBomber217Frame, uint, int, int, NativeBattleGfAnimationSource, NativeBattleEffectArena, NativeBattleEnergyBomber217ActStep> AdvanceAct, Func<NativeBattleEnergyBomber217Frame, NativeBattleVector, NativeBattleVector, short, short, NativeBattleEnergyBomber217PointStep> BlendPoints56CB90, Func<NativeBattleEnergyBomber217Frame, NativeBattleEnergyBomber217Operation, NativeBattleEnergyBomber217Frame> Execute)
{
    [JsonIgnore]
    public bool CapturePresentation { get; init; }
}
