using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSearchSpriteResource(uint Address, ImmutableArray<byte> Bytes);
public sealed record NativeBattleSearchResources(NativeBattleEffectHeapAllocation Animation, NativeBattleSearchModelResources ModelProfile, NativeBattleSearchSpriteResource Sprite8, NativeBattleStaticMeshResource ParticleMesh, NativeBattleGfAnimationSource ProgramSource, NativeBattleEffectHeapAllocation TargetOverride, uint SoundAppearance, uint SoundPose1, uint SoundProgram, uint VoiceAppearance, uint VoiceDeparture);
