namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEvilEye282Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEvilEye282State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEvilEye282MuzzleStep(NativeBattleEvilEye282Frame Frame, int X, int Y, int Z);
public sealed record NativeBattleEvilEye282PointStep(NativeBattleEvilEye282Frame Frame, NativeBattleEffectArena Point8, int Result);
public sealed record NativeBattleEvilEye282Inputs(NativeBattleActionPacketsState Packets, NativeBattleSummonColors Colors, uint GraphicsFlags, Func<NativeBattleEvilEye282Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEvilEye282Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEvilEye282Frame, uint, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleEvilEye282Frame, uint, int> ReadBoneCount, Func<NativeBattleEvilEye282Frame, uint, int, short> ReadBoneParent, Func<NativeBattleEvilEye282Frame, uint, int, byte> ReadBoneAlias, Func<NativeBattleEvilEye282Frame, uint, int, NativeBattlePackedMatrix32> ReadCurrentBoneMatrix, Func<NativeBattleEvilEye282Frame, uint, NativeBattlePackedMatrix32> ReadModelWorld, Func<NativeBattleEvilEye282Frame, uint, int, int, NativeBattleEvilEye282PointStep> ReadBone502170, Func<NativeBattleEvilEye282Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEvilEye282Frame, NativeBattleEvilEye282Operation, NativeBattleEvilEye282Frame> Execute)
{
    public bool CapturePresentation { get; init; }
}

public abstract record NativeBattleEvilEye282Operation;
public sealed record NativeBattleEvilEye282ContactsAll506BA0(int HeaderIndex) : NativeBattleEvilEye282Operation;
public sealed record NativeBattleEvilEye282Sound501330(uint Packed, uint Flags, byte Pan) : NativeBattleEvilEye282Operation;
public sealed record NativeBattleEvilEye282Flash5712C0(int Arg0, int Arg1, int Arg2, int Arg3) : NativeBattleEvilEye282Operation;
public sealed record NativeBattleEvilEye282DrawMesh7043B0(uint TemporaryAddress, uint SourceAddress, uint VertexAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleEvilEye282Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleEvilEye282DrawSprite180(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleEvilEye282Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleEvilEye282Stage5713E0(int Value, uint Flags) : NativeBattleEvilEye282Operation;
