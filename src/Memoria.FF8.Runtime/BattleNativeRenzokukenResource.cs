using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record BattleNativeRenzokukenResource(ushort MagicId, uint ServiceAddress, uint ResourceAddress, ImmutableArray<byte> RawResource, ImmutableArray<byte> AnimationFrameCounts, NativeBattleGraphicsResource GraphicsResource, int AbOffset, byte ShowBar, ImmutableArray<byte> Segments, BattleNativeRenzokukenTimeline Timeline);
