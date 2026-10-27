using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleSnipeLaser302Controller;
using static Memoria.FF8.Runtime.NativeBattleSnipeLaser302Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSnipeLaser302LowerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Current_bone_point_saturates_IR_to_XYZ_and_retains_output_W(bool unknownW)
    {
        var source = Point(0, 0, 0, 11);
        var retained = Point(7, 8, 9, unchecked((short)0xBEEF));
        if (unknownW)
        {
            retained = retained.MarkUnknown(6, 2);
        }
        var frame = Frame();
        int calls = 0;
        var actual = ReadMuzzle66D880(frame, source, retained, Inputs() with
        {
            ReadModelWorld = (_, model) =>
            {
                Assert.Equal(0x1D97494u, model);
                return NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 1000, Y = 2000, Z = 3000 }, null);
            },
            ReadCurrentBoneMatrix = (_, model, bone) =>
            {
                Assert.Equal(0x1D97494u, model);
                Assert.Equal(11, bone);
                calls++;
                return NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 40000, Y = -50000, Z = 60000 }, null);
            }
        });
        Assert.Equal(1, calls);
        Assert.Equal(new short[] { 32767, -32768, 32767 }, XYZ(actual.Point8));
        Assert.Equal(new int?[] { 41000, -48000, 63000 }, actual.Frame.Memory.ScriptedColorRegisters!.Mac);
        Assert.Equal(!unknownW, actual.Point8.ReadSnapshot(6, 2).Known[0]);
        if (!unknownW)
        {
            Assert.Equal((ushort)0xBEEF, W(actual.Point8, 6));
        }
        Assert.Equal((ushort)11, BinaryPrimitives.ReadUInt16LittleEndian(actual.Frame.Memory.ScriptedColorRegisters.NormalInput!.Value.AsSpan().Slice(6)));
        Assert.Null(actual.Frame.Memory.GeometryRegisters!.Rotation20[18]);
        Assert.Equal(frame.Random, actual.Frame.Random);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Two_transform_helper_uses_first_saturated_IR_WORD_addition_and_second_R_only_GTE(bool unknownW)
    {
        var frame = Frame();
        frame = frame with { Memory = frame.Memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(frame.Memory.GeometryRegisters!, NativeBattleCuragaMatrices.Identity8DD770() with { X = 11, Y = 22, Z = 33 }, false, true, null) } };
        var basis = NativeBattlePackedMatrix32.FromMatrix(new([8192, 0, 0, 0, 8192, 0, 0, 0, 8192], 0, 0, 0, 4096), 0x1234);
        basis = UnknownT(basis);
        var camera = NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Euler701310(new(0, 1024, 0)), 0xCAFE);
        camera = UnknownT(camera);
        var point = Point(20000, -20000, 123, unchecked((short)0xBEEF));
        if (unknownW)
        {
            point = point.MarkUnknown(6, 2);
        }
        var actual = TransformPoint66D810(frame, basis, Point(10, -10, 30000, 0).MarkUnknown(6, 2), point, Inputs() with { ReadView = _ => camera });
        Assert.Equal(new short[] { -32759, 32758, 30246 }, XYZ(actual.Point8));
        Assert.Equal(new int?[] { 30246, 32758, 32759 }, actual.Frame.Memory.ScriptedColorRegisters!.Mac);
        Assert.Equal(frame.Memory.GeometryRegisters!.Translation, actual.Frame.Memory.GeometryRegisters!.Translation);
        Assert.Equal((byte?)0xFE, actual.Frame.Memory.GeometryRegisters.Rotation20[18]);
        Assert.Equal((byte?)0xCA, actual.Frame.Memory.GeometryRegisters.Rotation20[19]);
        Assert.Equal(!unknownW, actual.Point8.ReadSnapshot(6, 2).Known[0]);
        Assert.Equal(!unknownW, actual.Frame.Memory.ScriptedColorRegisters.NormalInputKnown!.Value[6]);
        Assert.Equal(frame.Random, actual.Frame.Random);
    }

    [Theory]
    [InlineData(8, 0)]
    [InlineData(9, 2)]
    public void Secondary_programme_ends_only_on_its_ten_frame_remaining_counter(int oldFrame, int result)
    {
        var resources = Resources();
        var frame = Frame();
        (frame, uint address) = Append(frame, 0x2507688, 0x66D670);
        var task = Rotation(Read(frame, address), 12, NativeBattleCuragaMatrices.Identity8DD770()).WriteWord(30, 0xBEEF).WriteDword(32, 100).WriteDword(36, 200).WriteDword(40, 300);
        var workspace = new NativeBattleGfAnimationWorkspace(new byte[124].ToImmutableArray()).Initialize7016B0(resources.ProgrammeD, 124);
        byte[] cache = NativeBattleStorageSnapshots.Capture(workspace).Data.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(cache.AsSpan(4), oldFrame);
        task = CopyTask(task, 44, NativeBattleEffectArena.Clear().Write(0, cache), 124);
        frame = Write(frame, address, task);
        var actual = AdvanceProgramme66D670(frame, address, resources, Inputs((_, _) => throw new InvalidOperationException()));
        Assert.Equal(result, actual.Result);
        Assert.Equal((uint)(oldFrame + 1), Read(actual.Frame, address).ReadDword(48));
        Assert.Equal(frame.Memory.GeometryRegisters, actual.Frame.Memory.GeometryRegisters);
        Assert.Equal(frame.Memory.ScriptedColorRegisters, actual.Frame.Memory.ScriptedColorRegisters);
    }

    [Theory]
    [InlineData(10, true, true, 0)]
    [InlineData(11, true, false, 0)]
    [InlineData(14, true, false, 0)]
    [InlineData(15, false, false, 2)]
    public void Particle_terminal_branch_precedes_motion_and_acceleration_stops_at_new_twelve(int oldPhase, bool move, bool accelerate, int result)
    {
        var frame = Frame();
        (frame, uint address) = Append(frame, 0x2507698, 0x66D6D0);
        frame = Write(frame, address, Read(frame, address).WriteWord(12, 100).WriteWord(14, 200).WriteWord(16, 300).WriteWord(18, (ushort)oldPhase).WriteWord(20, 10).WriteWord(22, unchecked((ushort)-20)).WriteWord(24, 30).WriteWord(28, unchecked((ushort)-1)).WriteWord(30, 2).WriteWord(32, unchecked((ushort)-3)));
        int draws = 0;
        var actual = AdvanceParticle66D6D0(frame, address, Resources(), Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleSnipeLaser302DrawSprite180>(operation);
            Assert.Equal(0x1034480u, draw.SourceAddress);
            Assert.Equal((ushort)oldPhase, W(Context(current, draw.TemporaryAddress, 180), 4));
            Assert.Equal((ushort)oldPhase, BinaryPrimitives.ReadUInt16LittleEndian(current.Memory.ScriptedColorRegisters!.NormalInput!.Value.AsSpan().Slice(6)));
            Assert.Equal((short)3072, current.Memory.ModuleSpriteBillboard21DFED0!.ReadMatrix().Rotation[0]);
            draws++;
            return current;
        }));
        var task = Read(actual.Frame, address);
        Assert.Equal(1, draws);
        Assert.Equal(result, actual.Result);
        Assert.Equal(move ? new short[] { 110, 180, 330 } : new short[] { 100, 200, 300 }, new[] { Signed(task, 12), Signed(task, 14), Signed(task, 16) });
        Assert.Equal(accelerate ? new short[] { 9, -18, 27 } : new short[] { 10, -20, 30 }, new[] { Signed(task, 20), Signed(task, 22), Signed(task, 24) });
        Assert.Equal((ushort)(oldPhase + 1), task.ReadWord(18));
        Assert.Equal(frame.Random, actual.Frame.Random);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(15, 2)]
    public void Stationary_sprite_draws_old_phase_then_only_changes_phase(int phase, int result)
    {
        var frame = Frame();
        (frame, uint address) = Append(frame, 0x25076A8, 0x66D790);
        frame = Write(frame, address, Read(frame, address).WriteWord(12, 100).WriteWord(14, 200).WriteWord(16, 300).WriteWord(18, (ushort)phase));
        var actual = AdvanceSprite66D790(frame, address, Resources(), Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleSnipeLaser302DrawSprite180>(operation);
            Assert.Equal(0x1034270u, draw.SourceAddress);
            Assert.Equal((ushort)phase, W(Context(current, draw.TemporaryAddress, 180), 4));
            Assert.Equal((short)2800, current.Memory.ModuleSpriteBillboard21DFED0!.ReadMatrix().Rotation[0]);
            return current;
        }));
        var task = Read(actual.Frame, address);
        Assert.Equal(new ushort[] { 100, 200, 300, (ushort)(phase + 1) }, new[] { task.ReadWord(12), task.ReadWord(14), task.ReadWord(16), task.ReadWord(18) });
        Assert.Equal(result, actual.Result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Literal_alternate_Euler_selector_uses_existing_rotation_order(bool alternate)
    {
        var resources = Resources();
        var node = Node(alternate ? 0x40200u : 0x200u, new(512, 256, 128), new(4096, 4096, 4096));
        var actual = DrawProgrammeNode66D3C0(Frame(), resources.ProgrammeB, node, Caller(), resources, Inputs());
        var expected = alternate ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        Assert.Equal(expected.Rotation, NativeBattleModuleSpriteSetup.CaptureCurrentMatrix(actual.Memory.GeometryRegisters!).ReadMatrix().Rotation);
    }

    [Theory]
    [InlineData(false, 1448)]
    [InlineData(true, 1449)]
    public void Header100_rounds_diagonal_product_while_normal_scale_uses_integer_columns_and_both_keep_T(bool rounded, int first)
    {
        var resources = Resources();
        var node = Node(rounded ? 0x300u : 0x200u, new(0, 0, 512), new(2049, 4096, 4096));
        var actual = DrawProgrammeNode66D3C0(Frame(), resources.ProgrammeB, node, Caller(), resources, Inputs());
        var matrix = NativeBattleModuleSpriteSetup.CaptureCurrentMatrix(actual.Memory.GeometryRegisters!).ReadMatrix();
        Assert.Equal((short)first, matrix.Rotation[0]);
        Assert.Equal((short)-1449, matrix.Rotation[3]);
        Assert.Equal(new[] { 1100, 2200, 3300 }, new[] { matrix.X, matrix.Y, matrix.Z });
        Assert.Null(actual.Memory.GeometryRegisters!.Rotation20[18]);
    }

    [Theory]
    [InlineData(0, 100, false)]
    [InlineData(1024, 150, true)]
    [InlineData(4096, 300, false)]
    public void Programme_morph_only_writes_XYZ_and_preserves_current_scratch_W(int ratio, int expected, bool morph)
    {
        var resources = Resources();
        byte[] source = resources.ProgrammeD.Data.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(64), 28);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(72), 2);
        BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(76), 100);
        BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(84), 300);
        resources = resources with { ProgrammeD = resources.ProgrammeD with { Data = source.ToImmutableArray() } };
        byte[] data = Node(0x200, new(0, 0, 0), new(4096, 4096, 4096)).Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(38), (short)ratio);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(42), 1);
        var frame = Frame();
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(ScratchOffset, BitConverter.GetBytes((short)-777)).Write(ScratchOffset + 6, BitConverter.GetBytes((ushort)119)) } };
        uint selected = 0;
        var actual = DrawProgrammeNode66D3C0(frame, resources.ProgrammeD, new(0, 0, data.ToImmutableArray()), Caller(), resources, Inputs((current, operation) =>
        {
            selected = Assert.IsType<NativeBattleSnipeLaser302DrawMesh7043B0>(operation).VertexAddress;
            return current;
        }));
        Assert.Equal(morph ? ArenaAddress + ScratchOffset : resources.ProgrammeD.Address + (uint)(ratio == 4096 ? 84 : 76), selected);
        Assert.Equal((short)(morph ? expected : -777), BinaryPrimitives.ReadInt16LittleEndian(actual.Memory.Arena!.Read(ScratchOffset, 2)));
        Assert.Equal((ushort)119, W(actual.Memory.Arena!, ScratchOffset + 6));
    }

    private static NativeBattleSnipeLaser302Frame Frame()
    {
        var identity = Identity();
        var memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), identity.ReadMatrix(), true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), ModuleSpriteBillboard21DFED0 = identity };
        var effect = new NativeBattleOriginalEffectState(302, 301, new(302, Entry, Initializer), null, 0) { Pools = Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray() };
        return new(memory, effect, new(123), NativeBattleSnipeLaser302State.FromColdProcess().WriteDword(CasterGlobal, 0x1D97494)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }
    private static NativeBattleSnipeLaser302Resources Resources()
    {
        var resources = NativeBattleSnipeLaser302StateTests.Resources();
        byte[] d = resources.ProgrammeD.Data.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(d.AsSpan(0x100), 0);
        return resources with { ProgrammeD = resources.ProgrammeD with { Data = d.ToImmutableArray() } };
    }
    private static NativeBattleGfAnimationNode Node(uint header, NativeBattleVector rotation, NativeBattleVector scale)
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), header);
        NativeBattleVector[] vectors = [new(100, 200, 300), rotation, scale];
        for (int vector = 0; vector < 3; vector++)
        {
            int[] axes = [vectors[vector].X, vectors[vector].Y, vectors[vector].Z];
            for (int axis = 0; axis < 3; axis++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + vector * 8 + axis * 2), (short)axes[axis]);
            }
        }
        return new(0, 0, bytes.ToImmutableArray());
    }
    private static NativeBattleModulePoolTask Rotation(NativeBattleModulePoolTask task, int offset, NativeBattleActionMatrix matrix)
    {
        for (int index = 0; index < 9; index++)
        {
            task = task.WriteWord(offset + index * 2, unchecked((ushort)matrix.Rotation[index]));
        }
        return task;
    }
    private static NativeBattlePackedMatrix32 UnknownT(NativeBattlePackedMatrix32 matrix)
    {
        bool[] known = matrix.Known.ToArray();
        Array.Fill(known, false, 20, 12);
        return matrix with { Known = known.ToImmutableArray() };
    }
    private static NativeBattleEffectArena Point(short x, short y, short z, short w) => NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(x)).Write(2, BitConverter.GetBytes(y)).Write(4, BitConverter.GetBytes(z)).Write(6, BitConverter.GetBytes(w));
    private static short[] XYZ(NativeBattleEffectArena point) => Enumerable.Range(0, 3).Select(axis => BinaryPrimitives.ReadInt16LittleEndian(point.Read(axis * 2, 2))).ToArray();
    private static ushort W(NativeBattleEffectArena point, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(point.Read(offset, 2));
    private static NativeBattleEffectArena Caller() => PackedArena(NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 1000, Y = 2000, Z = 3000 }, null)).Write(32, BitConverter.GetBytes(-128)).Write(36, BitConverter.GetBytes(ArenaAddress + ScratchOffset));
    private static NativeBattlePackedMatrix32 Identity() => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
    private static NativeBattleSnipeLaser302Inputs Inputs(Func<NativeBattleSnipeLaser302Frame, NativeBattleSnipeLaser302Operation, NativeBattleSnipeLaser302Frame>? execute = null) => new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => Identity(), _ => new(null, 100, 160, 108), (_, _) => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), _ => NativeBattleActionPacketsState.Fresh(), execute ?? ((current, _) => current));
}
