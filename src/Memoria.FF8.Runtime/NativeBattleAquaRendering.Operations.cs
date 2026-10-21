using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAquaSpriteTask(NativeBattleModulePoolTask Task) : NativeBattleAquaOperation;
public sealed record NativeBattleAquaWaveTask(NativeBattleModulePoolTask Task) : NativeBattleAquaOperation;
public sealed record NativeBattleAquaAnchor(int Actor, short Selector, int Fraction) : NativeBattleAquaOperation;
public sealed record NativeBattleAquaRotate(NativeBattleActionMatrix Matrix, char Axis, short Angle) : NativeBattleAquaOperation
{
    public ushort? Padding18 { get; init; }
}
public sealed record NativeBattleAquaMatrix(NativeBattleActionMatrix Matrix, bool Rotation, bool Translation) : NativeBattleAquaOperation
{
    public ushort? Padding18 { get; init; }
    public int? TemporaryMatrixOffset { get; init; }
}
public sealed record NativeBattleAquaTransform(NativeBattleActionMatrix Matrix, NativeBattleVector Point, bool Translation) : NativeBattleAquaOperation
{
    public ushort? Padding18 { get; init; }
    public short? W { get; init; }
}
public sealed record NativeBattleAquaTransforms(NativeBattleActionMatrix Matrix, ImmutableArray<NativeBattleVector> Points, bool Translation) : NativeBattleAquaOperation
{
    public ushort? Padding18 { get; init; }
    public ImmutableArray<short?> Ws { get; init; }
}
public sealed record NativeBattleAquaProjection(NativeBattleActionMatrix Matrix, NativeBattleCameraProjection Projection, ImmutableArray<NativeBattleVector> Points, bool FinalCue) : NativeBattleAquaOperation
{
    public ImmutableArray<short?> Ws { get; init; }
    public int? TemporaryVectorOffset { get; init; }
}
public sealed record NativeBattleAquaClip(ImmutableArray<uint> Screens) : NativeBattleAquaOperation;
public sealed record NativeBattleAquaAverageDepth(ImmutableArray<ushort> Depths) : NativeBattleAquaOperation;
public sealed record NativeBattleAquaFarColor(uint Color) : NativeBattleAquaOperation;
public sealed record NativeBattleAquaColor(ImmutableArray<uint> Materials, int Cue, uint FarColor, bool Gouraud) : NativeBattleAquaOperation;
public sealed record NativeBattleAquaTemporaryBegin(int Size) : NativeBattleAquaOperation;
public sealed record NativeBattleAquaTemporaryPatch(int Offset, ImmutableArray<byte> Bytes) : NativeBattleAquaOperation;
public sealed record NativeBattleAquaTemporaryEnd(int Size) : NativeBattleAquaOperation;
public sealed record NativeBattleAquaTemporaryUnknown(int Offset, int Length) : NativeBattleAquaOperation;

public sealed record NativeBattleAquaTemporaryFlags(int Offset) : NativeBattleAquaOperation;
public sealed record NativeBattleAquaTemporaryCopy(int Source, int Destination, int Length) : NativeBattleAquaOperation;

public sealed record NativeBattleAquaMatrixProduct(NativeBattleActionMatrix Left, NativeBattleActionMatrix Right) : NativeBattleAquaOperation
{
    public ushort? Padding18 { get; init; }
    public int? TemporaryLeftMatrixOffset { get; init; }
}
public sealed record NativeBattleAquaLongTransform(NativeBattleActionMatrix Matrix, NativeBattleVector Point) : NativeBattleAquaOperation
{
    public bool LoadRotation { get; init; }
    public ushort? Padding18 { get; init; }
    public int? TemporaryMatrixOffset { get; init; }
}
