using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleTornado146Resources(NativeBattleEffectHeapAllocation Camera, NativeBattleModuleResource Spark, NativeBattleStaticMeshResource Cloud, ImmutableArray<byte> CloudTarget, NativeBattleStaticMeshResource Crater, ImmutableArray<NativeBattleStaticMeshResource> Pieces, ImmutableArray<int> LiftHeights, ImmutableArray<int> PieceX, ImmutableArray<int> PieceZ, ImmutableArray<byte> CapturePacket, uint WindSound, uint LandSound);
