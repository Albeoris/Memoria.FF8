using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChoco98Resources(NativeBattleChoco98IoResources Io, ImmutableArray<NativeBattleChoco97NodeResource> Nodes, NativeBattleGfAnimationSource ProgramSource, NativeBattleChoco97ModelResources Model, NativeBattleChoco98CameraResources Camera, NativeBattleChoco97SpriteResources Sprite, NativeBattleMoogle96VibrationResource Vibration, uint SoundInitial, uint SoundFirst, uint SoundSecond);
