namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGravija214Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleGravija214State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleGravija214PointStep(NativeBattleGravija214Frame Frame, NativeBattleEffectArena Point8);
public sealed record NativeBattleGravija214Inputs(NativeBattleActionPacketsState Packets, NativeBattleSummonColors Colors, uint GraphicsFlags, Func<NativeBattleGravija214Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleGravija214Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleGravija214Frame, uint, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleGravija214Frame, uint, NativeBattleGravija214PointStep> ReadAnchor571400, Func<NativeBattleGravija214Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleGravija214Frame, NativeBattleGravija214Operation, NativeBattleGravija214Frame> Execute)
{
    public Func<NativeBattleGravija214Frame, uint>? ReadOrderingTable { get; init; }
}

public abstract record NativeBattleGravija214Operation;
public sealed record NativeBattleGravija214Sound501330(uint Packed, uint Flags, byte Pan) : NativeBattleGravija214Operation;
public sealed record NativeBattleGravija214ContactsAll506BA0(int HeaderIndex) : NativeBattleGravija214Operation;
public sealed record NativeBattleGravija214CommonTextureLoad508630(uint DestinationAddress, uint CompletionAddress) : NativeBattleGravija214Operation;
public sealed record NativeBattleGravija214Clear45BEE0(NativeSoftwareDrawingRectangle Rectangle, byte R, byte G, byte B) : NativeBattleGravija214Operation;
public sealed record NativeBattleGravija214SetOrigin45B5E0(int X, int Y) : NativeBattleGravija214Operation;
public sealed record NativeBattleGravija214DrawMesh7043B0(uint TemporaryAddress, uint SourceAddress, uint VertexAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleGravija214Operation;
public sealed record NativeBattleGravija214DrawSprite180(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleGravija214Operation;
