using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleReflectBeam135Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleReflectBeam135SecondaryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Head_ACT_precedes_plane_geometry_static_then_conditional_tail_and_retains_latest_task(bool intersect)
    {
        Fixture f = new((ushort)(intersect ? 3 : 0), intersect);
        Assert.True(f.Bone.Known[18] && f.Bone.Known[19]);
        Assert.Equal(new byte[] { 0xEF, 0xBE }, f.Bone.Bytes.AsSpan().Slice(18, 2).ToArray());
        var result = NativeBattleReflectBeam135Controller.AdvanceSecondary(f.Frame, f.Address, f.Resources, f.Inputs);
        Assert.Equal(0, result.Result);
        Assert.Equal(intersect ? new[] { "head", "static", "tail" } : new[] { "head", "static" }, f.Order);
        Assert.Equal((ushort)(intersect ? 4 : 1), f.Task(result.Frame).ReadWord(12));
        Assert.Equal((ushort)(intersect ? 1 : 0), f.Task(result.Frame).ReadWord(14));
        Assert.Equal(42u, f.Task(result.Frame).ReadDword(0x20));
        Assert.Equal(1, f.Task(result.Frame).Next);
        Assert.Equal(f.Frame.Random, result.Frame.Random);
        Assert.Equal(NativeBattleTemporaryMemory.Address, U32(result.Frame.Staging!, NativeBattleTemporaryMemory.Cursor));
        Assert.Throws<NotSupportedException>(() => result.Frame.State.Read(ScaleMatrixGlobal + 18, 14));
        if (intersect)
        {
            Assert.Equal(new byte[] { 100, 0, 0, 0, 44, 1 }, f.Task(result.Frame).ReadBytes(0x14, 6));
            Assert.Throws<NotSupportedException>(() => f.Task(result.Frame).ReadWord(0x1A));
            Assert.Equal((ushort)3289, result.Frame.State.ReadWord(ScaleMatrixGlobal + 16));
            Assert.Equal(new int?[] { 0, 9585216, 0 }, result.Frame.Memory.ScriptedColorRegisters!.Mac);
            Assert.Equal((uint?)0x80800000, result.Frame.Memory.ScriptedColorRegisters!.Flags);
            Assert.Equal(7u, f.Task(result.Frame).ReadDword(0x218));
        }
        else
        {
            Assert.Equal((ushort)1260, result.Frame.State.ReadWord(ScaleMatrixGlobal + 16));
            Assert.Throws<NotSupportedException>(() => f.Task(result.Frame).ReadBytes(0x14, 8));
        }
    }

    [Fact]
    public void Tail_completion_clears_flag_then_retires_on_next_callback_without_extra_age_or_allocations()
    {
        Fixture f = new(13, true);
        NativeBattleModulePoolTask task = f.Task(f.Frame).WriteWord(14, 1).WriteBytes(0x14, new byte[] { 1, 0, 0, 0, 2, 0 }).MarkPayloadUnknown(0x1A, 2);
        NativeBattleReflectBeam135Frame frame = NativeBattleReflectBeam135Controller.Write(f.Frame, f.Address, task);
        f.TailRemaining = 0;
        var lastTail = NativeBattleReflectBeam135Controller.AdvanceSecondary(frame, f.Address, f.Resources, f.Inputs);
        Assert.Equal(new[] { "tail" }, f.Order);
        Assert.Equal(0, lastTail.Result);
        Assert.Equal((ushort)0, f.Task(lastTail.Frame).ReadWord(14));
        Assert.Equal((ushort)14, f.Task(lastTail.Frame).ReadWord(12));
        Assert.Equal(0, f.BoneReads);
        var retired = NativeBattleReflectBeam135Controller.AdvanceSecondary(lastTail.Frame with { Staging = null }, f.Address, f.Resources, f.Inputs);
        Assert.Equal(2, retired.Result);
        Assert.Equal((ushort)14, f.Task(retired.Frame).ReadWord(12));
        Assert.Equal(new[] { "tail" }, f.Order);
    }

    [Fact]
    public void JSON_restored_secondary_repeats_current_frame_state_without_a_saved_bone_copy()
    {
        Fixture f = new(3, true);
        NativeBattleReflectBeam135Frame restored = JsonSerializer.Deserialize<NativeBattleReflectBeam135Frame>(JsonSerializer.Serialize(f.Frame))!;
        var expected = NativeBattleReflectBeam135Controller.AdvanceSecondary(f.Frame, f.Address, f.Resources, f.Inputs);
        var actual = NativeBattleReflectBeam135Controller.AdvanceSecondary(restored, f.Address, f.Resources, f.Inputs);
        Assert.Equal(expected.Result, actual.Result);
        Assert.Equal(JsonSerializer.Serialize(expected.Frame), JsonSerializer.Serialize(actual.Frame));
    }

    [Fact]
    public void Unknown_reached_published_rotation_fails_and_selector_must_point_to_original_table()
    {
        Fixture f = new(0, false);
        NativeBattlePackedMatrix32 bone = f.Bone with { Known = f.Bone.Known.SetItem(0, false) };
        Assert.Throws<NotSupportedException>(() => NativeBattleReflectBeam135Controller.AdvanceSecondary(f.Frame, f.Address, f.Resources, f.Inputs with { ReadCurrentPublishedBone32 = (_, _, _) => bone }));
        NativeBattleReflectBeam135Frame wrong = NativeBattleReflectBeam135Controller.Write(f.Frame, f.Address, f.Task(f.Frame).WriteDword(0x10, BoneSelectorsAddress + 1));
        Assert.Throws<InvalidDataException>(() => NativeBattleReflectBeam135Controller.AdvanceSecondary(wrong, f.Address, f.Resources, f.Inputs));
    }

    private static uint U32(NativeBattleFileStaging staging, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(address, 4));

    private sealed class Fixture
    {
        public NativeBattleReflectBeam135Frame Frame { get; }
        public NativeBattleReflectBeam135Inputs Inputs { get; }
        public NativeBattleReflectBeam135Resources Resources { get; }
        public NativeBattlePackedMatrix32 Bone { get; }
        public uint Address { get; } = ArenaAddress + SecondaryOffset;
        public List<string> Order { get; } = [];
        public int TailRemaining { get; set; } = 1;
        public int BoneReads { get; private set; }

        public Fixture(ushort age, bool intersect)
        {
            NativeBattleActionMatrix matrix = intersect ? new([4096, 0, 0, 0, 0, -4096, 0, 4096, 0], 100, -1000, 300, 4096) : NativeBattleCuragaMatrices.Identity8DD770() with { X = 100, Y = 200, Z = 300 };
            Bone = NativeBattlePackedMatrix32.FromMatrix(matrix, 0xBEEF);
            Resources = new(new(0, []), new(0, []), new(SecondaryActAddress, []), new(ImpactActAddress, []), new(0, 0, 0, 0, []), new(0, 0, 0, 0, []), new(0, 0, 0, 0, []), new(0, StaticMeshAddress, []), new("unused", []), new short[] { 29, 30, 32 }.ToImmutableArray(), 0);
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(SecondaryDescriptor, Address, SecondaryStride, SecondaryCount);
            (pool, int slot) = pool.Append(SecondaryCallback);
            NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(12, age).WriteWord(14, 0).WriteDword(0x10, BoneSelectorsAddress).MarkPayloadUnknown(0x14, SecondaryStride - 0x14);
            pool = pool with { Slots = pool.Slots.SetItem(slot, task) };
            NativeBattleOriginalEffectState effect = new(135, 134, new(135, Entry, Initializer), null, RootDescriptor) { Pools = [pool] };
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
            NativeBattleReflectBeam135State state = NativeBattleReflectBeam135State.FromColdProcess().WriteDword(CasterGlobal, 0x1D972C0 + 3 * 156).WriteDword(WorkingGlobal, ArenaAddress);
            state = state with { Globals = state.Globals.MarkUnknown(checked((int)(ScaleMatrixGlobal - BaseAddress)) + 18, 14) };
            Frame = new(memory, effect, new(44), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
            Inputs = new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, _ => throw new InvalidOperationException(), _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), (_, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), (_, actor, selector) =>
            {
                Assert.Equal(3, actor);
                Assert.Equal((short)29, selector);
                BoneReads++;
                return Bone;
            }, (frame, address, source, offset, size, context) =>
            {
                Assert.Equal(Address, address);
                Assert.Equal(ArenaAddress + MorphOffset, BinaryPrimitives.ReadUInt32LittleEndian(context.Read(12, 4)));
                Assert.Throws<NotSupportedException>(() => context.Read(6, 2));
                NativeBattleModulePoolTask current = Task(frame);
                if (offset == 0x1C)
                {
                    Order.Add("head");
                    Assert.Equal(SecondaryActAddress, source.Address);
                    Assert.Equal(0x1F8, size);
                    Assert.Null(frame.Memory.GeometryRegisters!.Rotation20[18]);
                    Assert.Null(frame.Memory.GeometryRegisters!.Rotation20[19]);
                    Assert.False(frame.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
                    Assert.False(frame.Memory.ScriptedColorRegisters!.NormalInputKnown.Value[7]);
                    Assert.Equal(-128, BinaryPrimitives.ReadInt32LittleEndian(context.Read(8, 4)));
                    frame = NativeBattleReflectBeam135Controller.Write(frame, Address, current.WriteDword(0x20, 42));
                    (frame, _) = NativeBattleReflectBeam135Controller.Append(frame, 0, ParticleACallback);
                    return new(frame, 0);
                }
                Order.Add("tail");
                Assert.Equal(ImpactActAddress, source.Address);
                Assert.Equal(0x214, offset);
                Assert.Equal(0x58, size);
                Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(context.Read(8, 4)));
                return new(NativeBattleReflectBeam135Controller.Write(frame, Address, current.WriteDword(0x218, 7)), TailRemaining);
            }, (frame, operation) =>
            {
                NativeBattleReflectBeam135DrawStatic88 draw = Assert.IsType<NativeBattleReflectBeam135DrawStatic88>(operation);
                Order.Add("static");
                Assert.Equal(StaticMeshAddress, draw.SourceAddress);
                Assert.Equal(NativeBattleTemporaryMemory.Address + 88, U32(frame.Staging!, NativeBattleTemporaryMemory.Cursor));
                Assert.Equal(0x30u, U32(frame.Staging!, draw.TemporaryAddress + 0x1C));
                Assert.Equal(StaticMeshAddress, U32(frame.Staging!, draw.TemporaryAddress));
                Assert.Null(frame.Memory.GeometryRegisters!.Rotation20[18]);
                Assert.Null(frame.Memory.GeometryRegisters!.Rotation20[19]);
                return frame;
            });
        }

        public NativeBattleModulePoolTask Task(NativeBattleReflectBeam135Frame frame) => NativeBattleReflectBeam135Controller.Read(frame, Address);
    }
}
