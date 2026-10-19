using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAuraResources(NativeBattleEffectHeapAllocation Animation, NativeBattleGfAnimationSource Layout, NativeBattleStaticMeshResource Flame, NativeBattleModuleResource Glow, ImmutableArray<uint> PartFlags, uint Sound);
public sealed record NativeBattleAuraFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);
public abstract record NativeBattleAuraOperation;
public sealed record NativeBattleAuraPackets(NativeBattleGfGeometryBatch Batch) : NativeBattleAuraOperation;
public sealed record NativeBattleAuraSprite(NativeBattleModuleDraw Draw) : NativeBattleAuraOperation;
public sealed record NativeBattleAuraSound(NativeBattleModuleSound Sound) : NativeBattleAuraOperation;
public sealed record NativeBattleAuraContact(int Index) : NativeBattleAuraOperation;
public sealed record NativeBattleAuraFade : NativeBattleAuraOperation;
public sealed record NativeBattleAuraInputs(NativeBattleActionPacketsState Packets, NativeBattleCameraProjection Projection, NativeBattleSummonColors Colors, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleAuraFrame, NativeBattleAuraOperation, NativeBattleAuraFrame> Execute);
