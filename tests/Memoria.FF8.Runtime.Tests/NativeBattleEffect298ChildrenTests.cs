using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect298Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect298Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect298ChildrenTests
{
    [Theory]
    [InlineData(0u, false)]
    [InlineData(0x8000u, false)]
    [InlineData(0x200u, true)]
    [InlineData(0x8200u, true)]
    public void Programme_caller_scale_precedes_parent_space_and_header8000_does_not_skip_parent_rotation(uint header, bool direct)
    {
        var frame = Frame();
        var resources = NativeBattleEffect298StateTests.Resources();
        var matrix = new NativeBattleActionMatrix([0, 0, 4096, 0, 4096, 0, -4096, 0, 0], 100, 200, 300, 4096);
        var caller = PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, null)).Write(32, BitConverter.GetBytes(8192)).Write(36, BitConverter.GetBytes(4096)).Write(40, BitConverter.GetBytes(2048)).Write(44, BitConverter.GetBytes(1)).Write(48, BitConverter.GetBytes(-256)).Write(52, BitConverter.GetBytes(ArenaAddress + ScratchOffset));
        int calls = 0;
        var actual = DrawProgrammeNode671B50(frame, resources.ProgrammeA, Node(header), caller, resources, Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleEffect298DrawMesh7043B0>(operation);
            var currentMatrix = NativeBattleModuleSpriteSetup.CaptureCurrentMatrix(current.Memory.GeometryRegisters!).ReadMatrix();
            Assert.Equal(direct ? new[] { 120, 220, 315 } : new[] { 115, 220, 280 }, new[] { currentMatrix.X, currentMatrix.Y, currentMatrix.Z });
            Assert.Equal(direct ? new short[] { 8192, 0, 0, 0, 4096, 0, 0, 0, 2048 } : new short[] { 0, 0, 2048, 0, 4096, 0, -8192, 0, 0 }, currentMatrix.Rotation);
            var temporary = Context(current, draw.TemporaryAddress, 88);
            Assert.Equal(-256, BinaryPrimitives.ReadInt32LittleEndian(temporary.Read(24, 4)));
            Assert.Equal(0x2030u, BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(28, 4)));
            Assert.Equal(resources.ProgrammeA.Address + 76, draw.VertexAddress);
            calls++;
            return current;
        }));
        Assert.Equal(1, calls);
        Assert.Equal(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4), actual.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
    }

    [Fact]
    public void Invisible_programme_does_not_read_unknown_caller_or_allocate()
    {
        var frame = Frame();
        var resources = NativeBattleEffect298StateTests.Resources();
        byte[] node = Node(0).Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(36), 4096);
        var actual = DrawProgrammeNode671B50(frame, resources.ProgrammeA, new(0, 0, node.ToImmutableArray()), NativeBattleEffectArena.Clear().MarkUnknown(0, 56), resources, Inputs((_, _) => throw new InvalidOperationException()));
        Assert.Same(frame, actual);
    }

    [Fact]
    public void Failed_programme_append_still_advances_six_WORDs_without_pose_or_bone_reads()
    {
        var frame = Frame();
        for (int index = 0; index < 8; index++)
        {
            (frame, _) = Append(frame, Pools[3].Descriptor, 0x671AB0);
        }
        var resources = Script(0, 2048, 2, 256, 1024, -128);
        var actual = BirthProgramme671990(frame, ScriptAddress, 0x1D972C0, ArenaAddress + MainOffset + 28, false, resources, Inputs((_, _) => throw new InvalidOperationException()));
        Assert.Equal(ScriptAddress + 12, actual.Next);
        Assert.Same(frame, actual.Frame);
    }

    [Fact]
    public void Partial_sprite_batch_consumes_only_successful_nonzero_range_draws_and_retains_custom_pose()
    {
        var frame = Frame();
        for (int index = 0; index < 63; index++)
        {
            (frame, _) = Append(frame, Pools[2].Descriptor, 0x671F20);
        }
        short[] payload = new short[24];
        payload[1] = 1;
        payload[2] = 2048;
        payload[3] = 2;
        payload[4] = 256;
        payload[5] = 2;
        payload[6] = 512;
        payload[7] = -256;
        List<Type> operations = [];
        var inputs = Inputs((current, operation) =>
        {
            operations.Add(operation.GetType());
            return current;
        }) with { ReadBone502170 = (current, model, bone, distance) =>
        {
            Assert.Equal(0x1D972C0u, model);
            Assert.Equal(1, bone);
            Assert.Equal(2048, distance);
            return new(current, PointView(new(100, 200, 300)));
        } };
        var actual = BirthSpriteBatch671FF0(frame, ScriptAddress, 0x1D972C0, ArenaAddress + MainOffset + 28, Script(payload), inputs);
        Assert.Equal(ScriptAddress + 48, actual.Next);
        Assert.Equal(new[] { typeof(NativeBattleEffect298ApplyOffsetPose662C00) }, operations);
        Assert.Equal(14u, actual.Frame.State.ReadDword(SeedGlobal));
        Assert.Equal(frame.Memory.SharedEffectRandom, actual.Frame.Memory.SharedEffectRandom);
        var task = Read(actual.Frame, Pools[2].Bank + 63 * 28);
        Assert.Equal(-156, Signed(task, 12));
        Assert.Equal(200, Signed(task, 14));
        Assert.Equal(300, Signed(task, 16));
        Assert.Equal(256, Signed(task, 18));
        Assert.Equal(new byte[] { 0, 2, 0, 0, 0, 0, 0, 0 }, TaskView(task, 20, 8).Read(0, 8));
    }

    [Fact]
    public void Sprite_draws_old_phase_then_updates_signed_BYTE_motion_and_wraps_before_retirement()
    {
        var frame = Frame();
        (frame, uint address) = Append(frame, Pools[2].Descriptor, 0x671F20);
        var task = Read(frame, address).WriteWord(12, 100).WriteWord(14, 200).WriteWord(16, 300).WriteWord(18, 128);
        task = CopyTask(task, 20, NativeBattleEffectArena.Clear().Write(0, new byte[] { 15, 1, 127, 128, 2, 2, 3, 0x77 }), 8);
        frame = Write(frame, address, task);
        int calls = 0;
        var actual = AdvanceSprite671F20(frame, address, NativeBattleEffect298StateTests.Resources(), Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleEffect298DrawSprite180>(operation);
            Assert.Equal(SpriteAddresses[1], draw.SourceAddress);
            Assert.Equal((ushort)15, BinaryPrimitives.ReadUInt16LittleEndian(Context(current, draw.TemporaryAddress, 180).Read(4, 2)));
            ImmutableArray<byte> input = current.Memory.ScriptedColorRegisters!.NormalInput ?? throw new InvalidOperationException();
            Assert.Equal(new byte[] { 128, 0 }, input.AsSpan().Slice(6, 2).ToArray());
            Assert.All(current.Memory.ScriptedColorRegisters.NormalInputKnown!.Value, value => Assert.True(value));
            calls++;
            return current;
        }));
        Assert.Equal(1, calls);
        Assert.Equal(2, actual.Result);
        task = Read(actual.Frame, address);
        Assert.Equal(new[] { 608, -312, 308 }, new[] { (int)Signed(task, 12), Signed(task, 14), Signed(task, 16) });
        Assert.Equal(new byte[] { 16, 1, 129, 134, 2, 2, 3, 0x77 }, TaskView(task, 20, 8).Read(0, 8));
        Assert.Equal(frame.State.ReadDword(SeedGlobal), actual.Frame.State.ReadDword(SeedGlobal));
        Assert.Equal(frame.Memory.SharedEffectRandom, actual.Frame.Memory.SharedEffectRandom);
    }

    [Fact]
    public void Fragment_birth_keeps_twelve_material_rows_and_separates_OP_random_owners()
    {
        var frame = Frame();
        uint main = ArenaAddress + MainOffset;
        uint chunk = main + 0x2EC;
        frame = WriteBytes(frame, chunk, NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 172).ToArray()).Write(6, new byte[2]), 172);
        frame = WriteBytes(frame, main + 0x89EC, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(chunk)), 4);
        frame = WriteBytes(frame, main + 0x89F0, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(main + 0x83EC)), 4);
        byte[] geometry = new byte[44 + 12 * 16];
        BinaryPrimitives.WriteInt32LittleEndian(geometry, 1);
        BinaryPrimitives.WriteInt32LittleEndian(geometry.AsSpan(4), 8);
        BinaryPrimitives.WriteInt16LittleEndian(geometry.AsSpan(8), 1);
        BinaryPrimitives.WriteInt16LittleEndian(geometry.AsSpan(12), 3);
        BinaryPrimitives.WriteInt16LittleEndian(geometry.AsSpan(20), 100);
        BinaryPrimitives.WriteInt16LittleEndian(geometry.AsSpan(28), 100);
        BinaryPrimitives.WriteInt16LittleEndian(geometry.AsSpan(32), 12);
        for (int face = 0; face < 12; face++)
        {
            int at = 44 + face * 16;
            BinaryPrimitives.WriteInt16LittleEndian(geometry.AsSpan(at + 2), 1);
            BinaryPrimitives.WriteInt16LittleEndian(geometry.AsSpan(at + 4), 2);
            BinaryPrimitives.WriteUInt16LittleEndian(geometry.AsSpan(at + 6), 0x8078);
            BinaryPrimitives.WriteUInt32LittleEndian(geometry.AsSpan(at + 8), 0x3D948048);
            BinaryPrimitives.WriteUInt32LittleEndian(geometry.AsSpan(at + 12), 0x00B68078);
        }
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        var pose = new NativeBattlePoseSource(skeleton.ToImmutableArray(), [1]);
        var identity = NativeBattlePackedMatrix32.FromMatrix(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096), null);
        var body = new NativeBattleEffect298BodySource(pose, new(new(0, 0, 0), [new(new(0, 0, 0), new(1024, 1024, 1024))], false), [identity], 0x1000, (address, length) => NativeBattleEffectArena.Clear().Write(0, geometry.AsSpan().Slice(checked((int)(address - 0x1000)), length))) { VertexScratchAddress = 0x1A00000, VertexScratchLength = 24 };
        List<Type> operations = [];
        var inputs = Inputs((current, operation) =>
        {
            operations.Add(operation.GetType());
            return current;
        }) with
        {
            ReadModel = (_, _) => new(0, 3) { Color40 = 0x123456, GeometryWorldMatrix = identity.ReadMatrix(), SequenceState = new() { MeshMask124 = 1 } },
            ReadBody = (_, _) => body
        };
        var actual = BirthFragments6710E0(frame, ScriptAddress, 0x1D972C0, main + 28, Script(0, -1, 1, 100, 5000, 32767), inputs);
        Assert.Equal(ScriptAddress + 12, actual.Next);
        Assert.Equal(new[] { typeof(NativeBattleEffect298ApplyOffsetPose662C00), typeof(NativeBattleEffect298RestorePose508C90) }, operations);
        Assert.Equal(12044u, actual.Frame.State.ReadDword(SeedGlobal));
        Assert.Equal(3305u, actual.Frame.Memory.SharedEffectRandom!.State);
        var result = ReadBytes(actual.Frame, chunk, 172);
        Assert.Equal((short)21, BinaryPrimitives.ReadInt16LittleEndian(result.Read(6, 2)));
        Assert.Equal(new short[] { 0, -200, -122 }, Enumerable.Range(0, 3).Select(axis => BinaryPrimitives.ReadInt16LittleEndian(result.Read(8 + axis * 2, 2))));
        Assert.Equal((short)5504, BinaryPrimitives.ReadInt16LittleEndian(result.Read(14, 2)));
        Assert.Equal(new short[] { 1764, 23906, -313, -182 }, Enumerable.Range(0, 4).Select(index => BinaryPrimitives.ReadInt16LittleEndian(result.Read(16 + index * 2, 2))));
        Assert.Equal(MeshAddresses[0], BinaryPrimitives.ReadUInt32LittleEndian(result.Read(24, 4)));
        for (int row = 0; row < 12; row++)
        {
            Assert.Equal(geometry.AsSpan(52, 8).ToArray().Concat(geometry.AsSpan(50, 2).ToArray()), result.Read(28 + row * 12, 10));
            Assert.Equal(new byte[] { 0xA5, 0xA5 }, result.Read(38 + row * 12, 2));
        }
        var smoke = ReadBytes(actual.Frame, main + 0x83EC, 12);
        Assert.Equal((short)12, BinaryPrimitives.ReadInt16LittleEndian(smoke.Read(6, 2)));
        Assert.Equal(new byte[] { 0, 0, 225, 0 }, smoke.Read(8, 4));
        Assert.Equal(5u, actual.Frame.Memory.GeometryRegisters!.Lzcr);
        Assert.Equal(100000000u, actual.Frame.Memory.GeometryRegisters.Lzcs);
    }

    private static NativeBattleEffect298Resources Script(params short[] words)
    {
        var resources = NativeBattleEffect298StateTests.Resources();
        byte[] bytes = resources.ScriptBytes.ToArray();
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 2), words[index]);
        }
        return resources with { ScriptBytes = bytes.ToImmutableArray() };
    }

    private static NativeBattleGfAnimationNode Node(uint header)
    {
        byte[] data = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), header);
        for (int axis = 0; axis < 3; axis++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(8 + axis * 2), (short)((axis + 1) * 10));
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(24 + axis * 2), 4096);
        }
        return new(0, 0, data.ToImmutableArray());
    }

    private static NativeBattleEffect298Frame Frame()
    {
        var identity = NativeBattlePackedMatrix32.FromMatrix(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096), null);
        var memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), identity.ReadMatrix(), true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), SharedEffectRandom = new(123), ModuleSpriteBillboard21DFED0 = identity };
        var effect = new NativeBattleOriginalEffectState(298, 297, new(298, Entry, Initializer), null, 0) { Pools = Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray() };
        return new(memory, effect, NativeBattleEffect298State.FromColdProcess().WriteDword(ArenaGlobal, ArenaAddress) with { RootBankStorage = null }) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private static NativeBattleEffect298Inputs Inputs(Func<NativeBattleEffect298Frame, NativeBattleEffect298Operation, NativeBattleEffect298Frame> execute) => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => NativeBattlePackedMatrix32.FromMatrix(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 2000, 4096), null), _ => new(null, 100, 160, 108), (_, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), _ => NativeBattleActionPacketsState.Fresh(), (_, _) => throw new InvalidOperationException(), execute, _ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException());
}
