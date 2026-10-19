using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleXatmDeath276Table(uint Address, ImmutableArray<byte> Data, uint TriangleAddress, ImmutableArray<byte> Triangles);
public sealed record NativeBattleXatmDeath276Resources(NativeBattleEffectHeapAllocation Camera, ImmutableArray<NativeBattleModuleResource> Sprites, NativeBattleGfAnimationSource Prim, ImmutableArray<NativeBattleXatmDeath276Table> Events, ImmutableArray<byte> Shapes, ImmutableArray<byte> ArcLengths, ImmutableArray<byte> ArcDistances, ImmutableArray<ushort> TriangleLengths, NativeVibrationBank Vibration, uint Sound, NativeBattleActionMatrix EffectCamera);
public sealed record NativeBattleXatmDeath276State([property: JsonRequired] int BufferOffset, [property: JsonRequired] int? DrawCursor, [property: JsonRequired] uint Seed);
public sealed record NativeBattleXatmDeath276Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleXatmDeath276State State);
public sealed record NativeBattleXatmDeath276Step(NativeBattleXatmDeath276Frame Frame, int Result);
public abstract record NativeBattleXatmDeath276Operation;
public sealed record NativeBattleXatmDeath276Bones(bool World) : NativeBattleXatmDeath276Operation;
public sealed record NativeBattleXatmDeath276Model(NativeBattleGraphicsModel Model) : NativeBattleXatmDeath276Operation;
public sealed record NativeBattleXatmDeath276Caster(ImmutableArray<byte> Hidden) : NativeBattleXatmDeath276Operation;
public sealed record NativeBattleXatmDeath276Packets(NativeBattleGfGeometryBatch Batch) : NativeBattleXatmDeath276Operation;
public sealed record NativeBattleXatmDeath276Sprite(NativeBattleModuleDraw Draw) : NativeBattleXatmDeath276Operation;
public sealed record NativeBattleXatmDeath276Burst(NativeBattleXatmDeath276Table Table, short Probability, short Count, short Speed) : NativeBattleXatmDeath276Operation;
public sealed record NativeBattleXatmDeath276InitializePrim(int Slot) : NativeBattleXatmDeath276Operation;
public sealed record NativeBattleXatmDeath276Music(int Duration) : NativeBattleXatmDeath276Operation;
public sealed record NativeBattleXatmDeath276Sound(uint Sound) : NativeBattleXatmDeath276Operation;
public sealed record NativeBattleXatmDeath276White(int Level) : NativeBattleXatmDeath276Operation;
public sealed record NativeBattleXatmDeath276StopVibration(int Handle) : NativeBattleXatmDeath276Operation;
public sealed record NativeBattleXatmDeath276Fade(byte Red, byte Green, byte Blue, int Level) : NativeBattleXatmDeath276Operation;
public sealed record NativeBattleXatmDeath276Vibration(NativeBattleXatmDeath276Frame Frame, int Handle);
public sealed record NativeBattleXatmDeath276Inputs(NativeBattleCameraProjection Projection, Func<NativeBattleGraphicsModel> ReadCaster, Func<uint> ReadModelAddress, Func<int, short, NativeBattleVector> ReadBone, Func<int, int> ReadBoneVertexCount, Func<int, int, NativeBattleVector> ReadBoneVertex, Func<NativeBattleXatmDeath276Frame, NativeBattleXatmDeath276Operation, NativeBattleXatmDeath276Frame> Execute, Func<NativeBattleXatmDeath276Frame, NativeBattleXatmDeath276Vibration> StartVibration, Func<NativeBattleXatmDeath276Frame, int, NativeBattleXatmDeath276Step> AdvanceArc, Func<NativeBattleXatmDeath276Frame, int, NativeBattleXatmDeath276Frame> AdvancePrim);

public sealed partial record NativeBattleOriginalEffectState
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleXatmDeath276State? XatmDeath276 { get; init; }
}

