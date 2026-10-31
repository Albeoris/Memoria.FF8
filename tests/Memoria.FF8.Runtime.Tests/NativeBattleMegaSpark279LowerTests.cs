using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleMegaSpark279Controller;
using static Memoria.FF8.Runtime.NativeBattleMegaSpark279Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMegaSpark279LowerTests
{
    [Fact]
    public void Captured279_linked_strip_keeps_actual_prewrite_world_centers_offsets_CRT_and_all_native_row_updates()
    {
        NativeBattleMegaSpark279Frame Build(out uint address)
        {
            var frame = Frame();
            frame = frame with { Memory = frame.Memory with { GeometryRegisters = frame.Memory.GeometryRegisters! with { Sz = [null, null, 303u, 404u] } } };
            var a = PointView(new(-100, 0, 2000), 0);
            var b = PointView(new(100, 0, 2000), 0);
            var zero = PointView(new(0, 0, 0), 0);
            var first = BirthRow67FA80(frame, a, a, zero, zero, 0, 4096, 512, 1, 2, Resources(), Inputs());
            var second = BirthRow67FA80(first.Frame, b, b, zero, zero, 0, 4096, 512, 1, 2, Resources(), Inputs());
            address = Pools[4].Bank;
            frame = WriteBytes(second.Frame, first.RowAddress + 36, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(second.RowAddress)), 4);
            return Write(frame, address, Read(frame, address).WriteWord(12, 2).WriteDword(20, second.RowAddress));
        }
        var nativeFrame = Build(out uint nativeAddress);
        var capturedFrame = Build(out uint capturedAddress);
        NativeBattleGfGeometryBatch? nativeBatch = null;
        NativeBattleGfGeometryBatch? capturedBatch = null;
        var projection = new NativeBattleCameraProjection(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108);
        var native = AdvanceRows67FC30(nativeFrame, nativeAddress, Resources(), Inputs((current, operation) =>
        {
            nativeBatch = Assert.IsType<NativeBattleMegaSpark279DeliverBatch>(operation).Batch;
            return current;
        }) with { ReadProjection = _ => projection });
        var captured = AdvanceRows67FC30(capturedFrame, capturedAddress, Resources(), Inputs((current, operation) =>
        {
            capturedBatch = Assert.IsType<NativeBattleMegaSpark279DeliverBatch>(operation).Batch;
            return current;
        }) with { ReadProjection = _ => projection, CapturePresentation = true });
        Assert.NotNull(nativeBatch);
        Assert.NotNull(capturedBatch);
        Assert.Equal(new[] { 303, 404, 2000, 2000 }, Assert.Single(nativeBatch!.Attempts).Primitive!.VertexPayload4);
        Assert.Equal(new[] { 303, 404, 2000, 2000 }, Assert.Single(capturedBatch!.Attempts).Primitive!.VertexPayload4);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(nativeBatch), System.Text.Json.JsonSerializer.Serialize(capturedBatch));
        Assert.Equal(native.Result, captured.Result);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(native.Frame)), System.Text.Json.JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(captured.Frame)));
        var visual = Assert.IsType<NativeBattlePilotVisualGeometry>(Assert.Single(capturedBatch!.Attempts).Primitive!.PilotGeometry);
        Assert.Equal(new System.Numerics.Vector3(-100, 0, 2000), visual.A);
        Assert.Equal(new System.Numerics.Vector3(100, 0, 2000), visual.C);
        Assert.Equal(4, visual.ScreenOffsets.Length);
        Assert.False(visual.ScreenSpace);
        Assert.True(visual.Continuous);
        Assert.True(visual.UseSourceDepths);
    }

    [Fact]
    public void Captured279_sprite_forwards_actual_physical_task_birth_and_resource_before_temporary_release()
    {
        NativeBattleMegaSpark279Frame Build(out uint address)
        {
            var frame = Frame();
            (frame, address) = Append(frame, 0x2508080, 0x6800F0);
            return Write(frame, address, Read(frame, address).WriteWord(12, 100).WriteWord(14, 200).WriteWord(16, 300).WriteWord(18, 0));
        }
        var nativeFrame = Build(out uint nativeAddress);
        var capturedFrame = Build(out uint capturedAddress);
        NativeBattlePilotVisualIdentity? owner = null;
        var native = AdvanceSprite6800F0(nativeFrame, nativeAddress, Resources(), Inputs());
        var captured = AdvanceSprite6800F0(capturedFrame, capturedAddress, Resources(), Inputs((current, operation) =>
        {
            owner = Assert.IsType<NativeBattleMegaSpark279DrawSprite180>(operation).PilotIdentity;
            Assert.NotNull(owner);
            return current;
        }) with { CapturePresentation = true });
        Assert.Equal(native.Result, captured.Result);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(native.Frame)), System.Text.Json.JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(captured.Frame)));
        Assert.Equal((ushort)279, owner!.Value.Effect);
        Assert.Equal(0x2508080u, owner.Value.Bank);
        Assert.True(owner.Value.Generation > 0);
        Assert.Equal(NativeBattleSelectedProgrammeVisuals.Resource(NativeBattlePilotVisualLifetimes.Identity(279, Read(capturedFrame, capturedAddress)), Resources().Sprite.Address).ResourceGeneration, owner.Value.ResourceGeneration);
    }

    [Fact]
    public void Known_packed_W_is_preserved_and_a_second_row_skips_the_live_first_sentinel()
    {
        var frame = Frame();
        var start = PointView(new(100, 200, 300), 0x1234);
        var point = PointView(new(400, 500, 600), 0x4567);
        var direction = PointView(new(10, 20, 30), 0);
        var resources = Resources();
        var first = BirthRow67FA80(frame, start, point, direction, null, 0, 0, 512, 1, 2, resources, Inputs());
        var second = BirthRow67FA80(first.Frame, start, point, direction, null, 0, 0, 512, 1, 2, resources, Inputs());
        Assert.Equal(ArenaAddress + RowsOffset, first.RowAddress);
        Assert.Equal(first.RowAddress + 40, second.RowAddress);
        Assert.Equal(start.Read(0, 8), ReadBytes(second.Frame, first.RowAddress, 8).Read(0, 8));
        Assert.Equal((ushort)0x1234, W(ReadBytes(second.Frame, first.RowAddress, 8), 6));
        Assert.Equal(frame.Memory.SharedEffectRandom, second.Frame.Memory.SharedEffectRandom);
        Assert.Equal(frame.Random, second.Frame.Random);
        int seed = 0;
        for (int index = 0; index < 6; index++)
        {
            seed = (seed * 125 + 14) & 32767;
        }
        Assert.Equal((uint)seed, second.Frame.State.ReadDword(SeedGlobal));
    }

    [Fact]
    public void Unknown_sentinel_is_not_replaced_by_an_in_use_default()
    {
        var point = PointView(new(100, 200, 300));
        var born = BirthRow67FA80(Frame(), point, point, point, null, 0, 0, 512, 1, 2, Resources(), Inputs());
        Assert.False(ReadBytes(born.Frame, born.RowAddress, 8).ReadSnapshot(6, 2).Known[0]);
        Assert.Throws<NotSupportedException>(() => BirthRow67FA80(born.Frame, point, point, point, null, 0, 0, 512, 1, 2, Resources(), Inputs()));
    }

    [Fact]
    public void A_single_live_row_is_not_retired_merely_because_it_cannot_form_a_quad()
    {
        var point = PointView(new(100, 200, 300), 0x1234);
        var born = BirthRow67FA80(Frame(), point, point, point, null, 0, 4096, 512, 1, 2, Resources(), Inputs());
        uint address = Pools[4].Bank;
        var inputs = Inputs((_, _) => throw new InvalidOperationException()) with { ReadRowPointW = _ => throw new InvalidOperationException("Finished progress must not read unwritten GPF scratch.") };
        var actual = AdvanceRows67FC30(born.Frame, address, Resources(), inputs);
        Assert.Equal(0, actual.Result);
        Assert.Equal((ushort)1, Read(actual.Frame, address).ReadWord(12));
        Assert.Equal((short)1280, BinaryPrimitives.ReadInt16LittleEndian(ReadBytes(actual.Frame, born.RowAddress + 32, 2).Read(0, 2)));
    }

    [Theory]
    [InlineData(0u, 130, 220, 290, true)]
    [InlineData(0x8000u, 130, 220, 290, true)]
    [InlineData(0x200u, 110, 220, 330, false)]
    [InlineData(0x8200u, 110, 220, 330, false)]
    public void Node_header200_chooses_parent_space_and_8000_does_not_skip_rotation(uint header, int x, int y, int z, bool parent)
    {
        var resources = Resources();
        var matrix = NativeBattleGfMatrices.Euler701310(new(0, 1024, 0)) with { X = 100, Y = 200, Z = 300 };
        var caller = PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, null)).Write(32, BitConverter.GetBytes(-128)).Write(36, BitConverter.GetBytes(ArenaAddress + ScratchOffset));
        var actual = DrawProgrammeNode67F7E0(Frame(), resources.ProgrammeA, Node(header), caller, resources, Inputs());
        var current = NativeBattleModuleSpriteSetup.CaptureCurrentMatrix(actual.Memory.GeometryRegisters!).ReadMatrix();
        Assert.Equal(new[] { x, y, z }, new[] { current.X, current.Y, current.Z });
        Assert.Equal(parent ? matrix.Rotation : NativeBattleCuragaMatrices.Identity8DD770().Rotation, current.Rotation);
        Assert.Null(actual.Memory.GeometryRegisters!.Rotation20[18]);
    }

    [Fact]
    public void Invisible_node_does_not_consume_caller_or_allocate_temporary()
    {
        byte[] bytes = Node(0).Data.ToArray();
        Array.Clear(bytes, 24, 6);
        var frame = Frame();
        var resources = Resources();
        var actual = DrawProgrammeNode67F7E0(frame, resources.ProgrammeA, new(0, 0, bytes.ToImmutableArray()), NativeBattleEffectArena.Clear().MarkUnknown(0, 40), resources, Inputs());
        Assert.Same(frame, actual);
    }

    [Theory]
    [InlineData(9, 40, 0, 0, 40)]
    [InlineData(10, 40, 0, 0, 0)]
    [InlineData(10, 39, 0, 2, 0)]
    public void Attachment_draws_eighteen_process_randoms_then_fades_with_exact_zero_counted(int age, int brightness, int weight, int result, int expectedBrightness)
    {
        var frame = Frame();
        (frame, uint address) = Append(frame, 0x25080C0, 0x680520);
        var task = Read(frame, address).WriteDword(24, SourcePointsAddress).WriteDword(28, 0x12340000);
        task = CopyTask(task, 12, NativeBattleEffectArena.Clear().Write(0, new byte[] { (byte)age, 1, 14, 7 }), 4);
        task = CopyTask(task, 16, NativeBattleEffectArena.Clear().Write(0, Resources().ExtraPoint8.AsSpan()), 8);
        for (int index = 0; index < 8; index++)
        {
            task = task.WriteWord(32 + index * 4, (ushort)brightness).WriteWord(34 + index * 4, 0);
            frame = WriteBytes(frame, ArenaAddress + GeneratedSineOffset + (uint)(index * 2), NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)weight)), 2);
        }
        frame = Write(frame, address, task);
        (frame, uint temporary) = Allocate(frame, 128);
        var retained = NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 128).ToArray());
        frame = PublishContext(frame, temporary, retained, 128);
        frame = Release(frame, 128);
        int calls = 0;
        var actual = AdvanceAttachment680520(frame, address, Resources(), Inputs((current, operation) =>
        {
            var batch = Assert.IsType<NativeBattleMegaSpark279DeliverBatch>(operation);
            Assert.Equal(0x1D8E054u, batch.CursorGlobal);
            Assert.Equal((ushort)0, batch.Flags);
            Assert.Equal(7 * 52, batch.Batch.AllocationBytes);
            Assert.Equal(7, batch.Batch.Attempts.Length);
            foreach (var packet in batch.Batch.Attempts)
            {
                Assert.Equal((byte)0x3E, packet.Data[7]);
                var primitive = Assert.IsType<NativeBattleGeometryPrimitive>(packet.Primitive);
                Assert.Equal(13, primitive.Words.Length);
                Assert.Equal(0x0C000000u, primitive.Words[0]);
                Assert.Equal(12u, primitive.Words[0] >> 24);
                Assert.Equal(0x3Eu, primitive.Words[1] >> 24);
                Assert.Equal(Enumerable.Range(0, 13).Select(word => BinaryPrimitives.ReadUInt32LittleEndian(packet.Data.AsSpan().Slice(word * 4))), primitive.Words);
                Assert.Equal(new[] { 2000, 2000, 2000, 2000 }, primitive.VertexPayload4);
                Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[38]);
                Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[39]);
                Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[50]);
                Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[51]);
                Assert.Equal(0x0C000000u, BinaryPrimitives.ReadUInt32LittleEndian(packet.Data.AsSpan()));
            }
            var rows = Context(current, temporary, 128);
            for (int index = 1; index < 7; index++)
            {
                Assert.Equal((ushort)0xA5A5, W(rows, index * 16 + 6));
                Assert.Equal((ushort)brightness, W(rows, index * 16 + 14));
            }
            calls++;
            return current;
        }));
        Assert.Equal(1, calls);
        Assert.Equal(result, actual.Result);
        Assert.Equal((byte)(actual.Result == 2 ? age : age + 1), TaskView(Read(actual.Frame, address), 12, 1).Read(0, 1)[0]);
        for (int index = 0; index < 8; index++)
        {
            Assert.Equal((ushort)expectedBrightness, Read(actual.Frame, address).ReadWord(32 + index * 4));
        }
        var expected = frame.Memory.SharedEffectRandom!;
        for (int index = 0; index < 18; index++)
        {
            expected = expected.Draw7059E0(out _);
        }
        Assert.Equal(expected, actual.Frame.Memory.SharedEffectRandom);
        Assert.Equal(frame.State.ReadDword(SeedGlobal), actual.Frame.State.ReadDword(SeedGlobal));
        Assert.Equal(frame.Random, actual.Frame.Random);
        Assert.Equal(BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)), BinaryPrimitives.ReadUInt32LittleEndian(actual.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Theory]
    [InlineData(6, 0)]
    [InlineData(7, 2)]
    public void Sprite_retires_at_new_phase_eight(int phase, int result)
    {
        var frame = Frame();
        (frame, uint address) = Append(frame, 0x2508080, 0x6800F0);
        frame = Write(frame, address, Read(frame, address).WriteWord(12, 1).WriteWord(14, 2).WriteWord(16, 3).WriteWord(18, (ushort)phase));
        var actual = AdvanceSprite6800F0(frame, address, Resources(), Inputs((current, operation) =>
        {
            var sprite = Assert.IsType<NativeBattleMegaSpark279DrawSprite180>(operation);
            Assert.Equal((ushort)phase, W(Context(current, sprite.TemporaryAddress, 180), 4));
            return current;
        }));
        Assert.Equal(result, actual.Result);
        Assert.Equal((ushort)(phase + 1), Read(actual.Frame, address).ReadWord(18));
    }

    [Fact]
    public void Triangle_sampling_publishes_OP_SQR_and_LZ_before_the_endpoint_matrix_query()
    {
        var resources = Resources();
        byte[] triangle = resources.AttachmentTables.ToArray();
        short[] vertices = [-100, -100, 0, 17, 100, -100, 0, 18, 0, 100, 0, 21];
        for (int index = 0; index < vertices.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(triangle.AsSpan(index * 2), vertices[index]);
        }
        resources = resources with { AttachmentTables = triangle.ToImmutableArray() };
        var frame = Frame();
        (frame, _) = BirthAttachment6804B0(frame, 0x12340000, AttachmentTablesAddress, ExtraPointAddress, 0, 12, 7, resources, Inputs());
        uint address = Pools[5].Bank;
        int query = 0;
        var inputs = Inputs() with { ReadCurrentBoneMatrix = (current, row, bone) =>
        {
            Assert.Equal(0x12340000u, row);
            int[] expectedBones = [17, 18, 21, 10];
            Assert.Equal(expectedBones[query], bone);
            if (query == 3)
            {
                Assert.Equal(new int?[] { 0, 0, 100000000 }, current.Memory.ScriptedColorRegisters!.Mac);
                Assert.Equal(100000000u, current.Memory.GeometryRegisters!.Lzcs);
                Assert.Equal(5u, current.Memory.GeometryRegisters.Lzcr);
                Assert.Equal(-200, BinaryPrimitives.ReadInt32LittleEndian(current.Memory.GeometryRegisters.Rotation20.Take(4).Select(value => value!.Value).ToArray()));
            }
            query++;
            return Identity();
        } };
        _ = AdvanceAttachment680520(frame, address, resources, inputs);
        Assert.Equal(4, query);
    }

    private static NativeBattleGfAnimationNode Node(uint flags)
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), flags);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), 10);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10), 20);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12), 30);
        for (int axis = 0; axis < 3; axis++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(24 + axis * 2), 4096);
        }
        return new(0, 0, bytes.ToImmutableArray());
    }
    private static NativeBattleMegaSpark279Resources Resources() => NativeBattleMegaSpark279StateTests.Resources();
    private static NativeBattlePackedMatrix32 Identity(int z = 0) => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = z }, null);
    private static ushort W(NativeBattleEffectArena view, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(view.Read(offset, 2));
    private static NativeBattleMegaSpark279Frame Frame()
    {
        var memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), Identity().ReadMatrix(), true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), SharedEffectRandom = new(123), ModuleSpriteBillboard21DFED0 = Identity() };
        var effect = new NativeBattleOriginalEffectState(279, 278, new(279, Entry, Initializer), null, 0) { Pools = Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray() };
        var frame = new NativeBattleMegaSpark279Frame(memory, effect, new(123), NativeBattleMegaSpark279State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        for (int index = 0; index < RowCount; index++)
        {
            frame = WriteBytes(frame, ArenaAddress + RowsOffset + (uint)(index * RowStride + 6), NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)-1)), 2);
        }
        return frame;
    }
    private static NativeBattleMegaSpark279Inputs Inputs(Func<NativeBattleMegaSpark279Frame, NativeBattleMegaSpark279Operation, NativeBattleMegaSpark279Frame>? execute = null) => new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, _ => Identity(2000), _ => new(null, 100, 160, 108), (_, _) => throw new InvalidOperationException(), (_, _) => 46, (_, row, _) => row == 0x12340000 ? Identity() : throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), _ => NativeBattleActionPacketsState.Fresh(), execute ?? ((current, _) => current));
}
