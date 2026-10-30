using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect312Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect312LateTests
{
    private const uint TaskAddress = 0x223E1B8;
    private const uint Model = 0x1D9735C;
    private const uint Saved = 0x223EFE4;

    [Fact]
    public void Delayed_mesh_pauses_before_allocating_and_consumes_no_random()
    {
        NativeBattleEffect312Frame frame = Frame(0x5A03D0, 0);
        frame = Write(frame, TaskAddress, Read(frame, TaskAddress).WriteWord(14, 2));
        Harness harness = new() { Flags = 0x201 };
        NativeBattleEffect312Step paused = NativeBattleEffect312Late.Advance(frame, TaskAddress, null!, harness.Inputs());
        Assert.Equal(2, Read(paused.Frame, TaskAddress).ReadWord(14));
        Assert.Equal(frame.Random, paused.Frame.Random);
        Assert.Null(paused.Frame.Staging);
        harness.Flags = 0;
        NativeBattleEffect312Step delayed = NativeBattleEffect312Late.Advance(paused.Frame, TaskAddress, null!, harness.Inputs());
        Assert.Equal(1, Read(delayed.Frame, TaskAddress).ReadWord(14));
        Assert.Equal(0, Read(delayed.Frame, TaskAddress).ReadWord(12));
        Assert.Equal(frame.Random, delayed.Frame.Random);
        Assert.Empty(harness.Operations);
    }

    [Fact]
    public void Raise_restores_only_its_status_bit_and_actual_padded_transform_rows()
    {
        Harness harness = new();
        NativeBattleEffectArena original = harness.ModelBytes;
        NativeBattleEffect312Frame frame = Frame(0x5A0AA0, 0);
        frame = Write(frame, TaskAddress, Read(frame, TaskAddress).WriteWord(14, 1).WriteWord(32, 500));
        NativeBattleEffect312Step first = NativeBattleEffect312Late.Advance(frame, TaskAddress, null!, harness.Inputs());
        Assert.Equal(0, first.Result);
        Assert.Equal(0x1123, U16(harness.ModelBytes, 0));
        Assert.Equal(original.Read(12, 8), first.Frame.State.Read(Saved + 4, 8));
        Assert.Equal(original.Read(28, 8), first.Frame.State.Read(Saved + 12, 8));
        harness.ModelBytes = harness.ModelBytes.Write(0, BitConverter.GetBytes((ushort)0xB4A7)).Write(12, new byte[8]).Write(28, new byte[8]);
        frame = Write(first.Frame, TaskAddress, Read(first.Frame, TaskAddress).WriteWord(12, 8));
        NativeBattleEffect312Step completed = NativeBattleEffect312Late.Advance(frame, TaskAddress, null!, harness.Inputs());
        Assert.Equal(2, completed.Result);
        Assert.Equal(9, Read(completed.Frame, TaskAddress).ReadWord(12));
        Assert.Equal(0xA4A7, U16(harness.ModelBytes, 0));
        Assert.Equal(original.Read(12, 8), harness.ModelBytes.Read(12, 8));
        Assert.Equal(original.Read(28, 8), harness.ModelBytes.Read(28, 8));
        Assert.DoesNotContain(harness.Operations, operation => operation is NativeBattleEffect312Contact506690);
    }

    [Fact]
    public void Paused_raise_still_writes_the_actual_actor_but_does_not_advance()
    {
        Harness harness = new() { Flags = 0x201 };
        NativeBattleEffect312Frame frame = WithSaved(Frame(0x5A0AA0, 3), harness.ModelBytes);
        frame = Write(frame, TaskAddress, Read(frame, TaskAddress).WriteWord(14, 1).WriteWord(32, 500));
        NativeBattleEffect312Step step = NativeBattleEffect312Late.Advance(frame, TaskAddress, null!, harness.Inputs());
        Assert.Equal(0, step.Result);
        Assert.Equal(3, Read(step.Frame, TaskAddress).ReadWord(12));
        Assert.Equal(813, U16(harness.ModelBytes, 30));
        Assert.Equal(0x1123, U16(harness.ModelBytes, 0));
        Assert.All(harness.Operations.OfType<NativeBattleEffect312WriteModelBytes>(), write => Assert.Equal(Model, write.ModelAddress));
        Assert.Equal(frame.Random, step.Frame.Random);
    }

    [Fact]
    public void Throw_waits_for_the_actual_sequence_byte_then_contacts_its_physical_target_row()
    {
        Harness harness = new() { Sequence = 6 };
        NativeBattleEffectArena original = harness.ModelBytes;
        NativeBattleEffect312Frame frame = WithSaved(Frame(0x5A0BF0, 27), original);
        frame = Write(frame, TaskAddress, Read(frame, TaskAddress).WriteWord(14, 1).WriteWord(26, 2).WriteWord(32, unchecked((ushort)-3000)));
        frame = frame with { State = frame.State.WriteDword(0x2240EB8, 0x1D99A78), ReadPhysical = harness.Physical };
        NativeBattleEffect312Step waiting = NativeBattleEffect312Late.Advance(frame, TaskAddress, null!, harness.Inputs());
        Assert.Equal(0, waiting.Result);
        Assert.Equal(28, Read(waiting.Frame, TaskAddress).ReadWord(12));
        Assert.DoesNotContain(harness.Operations, operation => operation is NativeBattleEffect312Contact506690);
        harness.Sequence = 33;
        NativeBattleEffect312Step completed = NativeBattleEffect312Late.Advance(waiting.Frame, TaskAddress, null!, harness.Inputs());
        Assert.Equal(2, completed.Result);
        Assert.Equal(0x1D99C30u, Assert.Single(harness.Operations.OfType<NativeBattleEffect312Contact506690>()).TargetArgumentAddress);
        Assert.Equal(original.Read(12, 8), harness.ModelBytes.Read(12, 8));
        Assert.Equal(original.Read(28, 8), harness.ModelBytes.Read(28, 8));
    }

    [Fact]
    public void Paused_throw_does_not_publish_its_child_or_start_actor_animation()
    {
        Harness harness = new() { Flags = 0x201 };
        NativeBattleEffect312Frame frame = WithSaved(Frame(0x5A0BF0, 20), harness.ModelBytes);
        NativeBattleModulePoolTask task = Read(frame, TaskAddress).WriteWord(14, 1).WriteWord(24, 4096).WriteWord(26, 0).WriteWord(32, unchecked((ushort)-3000));
        task = task.WriteBytes(16, new byte[8]);
        frame = Write(frame, TaskAddress, task);
        NativeBattleEffect312Step result = NativeBattleEffect312Late.Advance(frame, TaskAddress, null!, harness.Inputs());
        Assert.Equal(0, result.Result);
        Assert.Equal(20, Read(result.Frame, TaskAddress).ReadWord(12));
        Assert.Single(result.Frame.Effect.Pools[0].Slots, slot => slot.Callback != 0);
        Assert.Equal(frame.Random, result.Frame.Random);
        Assert.DoesNotContain(harness.Operations, operation => operation is NativeBattleEffect312SetModelAnimation505C00);
    }

    [Fact]
    public void Throw_child_uses_one_current_random_draw_and_actor_horizontal_position()
    {
        Harness harness = new();
        NativeBattleEffect312Frame frame = WithSaved(Frame(0x5A0BF0, 20), harness.ModelBytes);
        NativeBattleModulePoolTask task = Read(frame, TaskAddress).WriteWord(14, 1).WriteWord(24, 4096).WriteWord(26, unchecked((ushort)-1)).WriteWord(32, unchecked((ushort)-3000)).WriteBytes(16, new byte[8]);
        frame = Write(frame, TaskAddress, task);
        NativeCrtRandomState expectedRandom = frame.Random.Draw(out ushort value);
        NativeBattleEffect312Step result = NativeBattleEffect312Late.Advance(frame, TaskAddress, null!, harness.Inputs());
        NativeBattleModulePoolTask child = Assert.Single(result.Frame.Effect.Pools[0].Slots, slot => slot.Callback == 0x5A0F50);
        Assert.Equal(expectedRandom, result.Frame.Random);
        Assert.Equal(0, child.ReadWord(12));
        Assert.Equal(0, child.ReadWord(18));
        Assert.Equal(U16(harness.ModelBytes, 28), child.ReadWord(16));
        Assert.Equal(U16(harness.ModelBytes, 32), child.ReadWord(20));
        Assert.Equal((value % 1024 + 3072) / 4, child.ReadWord(28));
        Assert.Equal(child.ReadWord(28), child.ReadWord(30));
        Assert.DoesNotContain(harness.Operations, operation => operation is NativeBattleEffect312SetModelAnimation505C00);
    }

    [Theory]
    [InlineData(0x59F7E0u, 14)]
    [InlineData(0x59FBD0u, 2)]
    public void Rejected_strips_publish_only_their_reached_tags_and_retain_the_second_packet_cursor(uint callback, int attemptCount)
    {
        Harness harness = new() { Flags = 0x201 };
        NativeBattleEffect312Frame frame = GraphicsFrame(callback, callback == 0x59F7E0 ? (ushort)1 : (ushort)0);
        frame = Write(frame, TaskAddress, Read(frame, TaskAddress).WriteWord(14, 0).WriteWord(24, 1).WriteWord(26, 2));
        frame = frame with { State = frame.State.WriteDword(0x223F248, 1).WriteDword(0x2240EDC, 0x20DFAB8) };
        NativeBattleEffect312Step result = NativeBattleEffect312Late.Advance(frame, TaskAddress, Resources(), GraphicsInputs(harness));
        NativeBattleGfGeometryBatch batch = Assert.Single(harness.Operations.OfType<NativeBattleEffect312DrawBatch>()).Batch;
        Assert.Equal(0, batch.AllocationBytes);
        Assert.Equal(attemptCount, batch.Attempts.Length);
        foreach (NativeBattleGfGeometryAttempt attempt in batch.Attempts)
        {
            Assert.Null(attempt.Primitive);
            Assert.Equal(0x08000000u, BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan()));
            Assert.All(attempt.WriteMask.Take(4), state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
            Assert.All(attempt.WriteMask.Skip(4), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
        }
        Assert.Equal(0x20DFADCu, result.Frame.State.ReadDword(0x2240EDC));
        Assert.Equal(frame.Random, result.Frame.Random);
        Assert.Equal(Read(frame, TaskAddress).ReadWord(12), Read(result.Frame, TaskAddress).ReadWord(12));
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(result.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void First_two_burst_frames_keep_the_retained_fade_bytes_unknown(ushort age)
    {
        Harness harness = new() { Flags = 0x201 };
        NativeBattleEffect312Frame frame = GraphicsFrame(0x5A0F50, age);
        frame = Write(frame, TaskAddress, Read(frame, TaskAddress).WriteWord(28, 1024).WriteWord(30, 1024));
        NativeBattleEffect312Step result = NativeBattleEffect312Late.Advance(frame, TaskAddress, Resources(), GraphicsInputs(harness));
        Assert.Throws<NotSupportedException>(() => result.Frame.Staging!.Read(NativeBattleTemporaryMemory.Address + 12, 4));
        Assert.Equal(0x33u, BinaryPrimitives.ReadUInt32LittleEndian(result.Frame.Staging!.Read(NativeBattleTemporaryMemory.Address + 28, 4)));
        Assert.Equal(age, Read(result.Frame, TaskAddress).ReadWord(12));
        Assert.Equal(frame.Random, result.Frame.Random);
    }

    [Fact]
    public void Scrolling_middle_frames_preserve_the_retained_fade_and_advance_only_after_render()
    {
        Harness harness = new() { Flags = 0x201 };
        NativeBattleEffect312Frame frame = GraphicsFrame(0x5A03D0, 4);
        frame = Write(frame, TaskAddress, Read(frame, TaskAddress).WriteWord(14, 0).WriteWord(24, 0).WriteWord(26, 0).WriteWord(28, 4096).WriteWord(32, 4096).WriteWord(34, 6));
        NativeBattleEffect312Step result = NativeBattleEffect312Late.Advance(frame, TaskAddress, Resources(), GraphicsInputs(harness));
        Assert.Throws<NotSupportedException>(() => result.Frame.Staging!.Read(NativeBattleTemporaryMemory.Address + 12, 4));
        Assert.Equal(24u, BinaryPrimitives.ReadUInt32LittleEndian(result.Frame.Staging!.Read(NativeBattleTemporaryMemory.Address + 20, 4)));
        Assert.Equal(4, Read(result.Frame, TaskAddress).ReadWord(12));
    }

    [Theory]
    [InlineData(0x59F7E0u, 28)]
    [InlineData(0x59FBD0u, 2)]
    public void Both_native_strip_layouts_capture_reached_corners_FIFO_and_keep_all_writes(uint callback, int packets)
    {
        NativeBattleEffect312Frame Prepare()
        {
            NativeBattleEffect312Frame frame = GraphicsFrame(callback, callback == 0x59F7E0 ? (ushort)1 : (ushort)0);
            frame = Write(frame, TaskAddress, Read(frame, TaskAddress).WriteWord(14, 0).WriteWord(24, 1).WriteWord(26, 2));
            frame = frame with { State = frame.State.WriteDword(0x2240EDC, 0x20DFAB8) };
            for (int point = 0; point < 48; point++)
            {
                frame = PutPoint(frame, 0x223F0C8u + (uint)(point * 8), point * 100, 100, 2000);
            }
            frame = frame with { State = frame.State.WriteDword(0x223F248, 1) };
            frame = PutPoint(frame, 0x223F24C, -100, -100, 2000);
            frame = PutPoint(frame, 0x223F254, -100, 100, 2000);
            return PutPoint(frame, 0x2240EC8, 100, -100, 2000);
        }
        Harness native = new() { Flags = 0x201 };
        NativeBattleEffect312Step expected = NativeBattleEffect312Late.Advance(Prepare(), TaskAddress, Resources(), GraphicsInputs(native));
        Harness captured = new() { Flags = 0x201 };
        NativeBattleEffect312Step actual = NativeBattleEffect312Late.Advance(Prepare(), TaskAddress, Resources(), GraphicsInputs(captured) with { CapturePresentation = true });
        Assert.Equal(NativeBattleTestSnapshots.Json(expected.Frame), NativeBattleTestSnapshots.Json(actual.Frame));
        NativeBattleGfGeometryBatch expectedBatch = Assert.Single(native.Operations.OfType<NativeBattleEffect312DrawBatch>()).Batch;
        NativeBattleGfGeometryBatch actualBatch = Assert.Single(captured.Operations.OfType<NativeBattleEffect312DrawBatch>()).Batch;
        Assert.Equal(NativeBattleTestSnapshots.Json(expectedBatch), NativeBattleTestSnapshots.Json(actualBatch));
        Assert.Equal(packets, actualBatch.Attempts.Length);
        HashSet<NativeBattlePilotVisualIdentity> identities = [];
        foreach (NativeBattleGfGeometryAttempt attempt in actualBatch.Attempts)
        {
            NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
            NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
            Assert.Equal((byte)4, geometry.CornerCount);
            Assert.True(geometry.UseSourceDepths);
            Assert.Equal(new System.Numerics.Vector4(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]), geometry.SourceDepths);
            Assert.NotEqual(0, geometry.Identity.Generation);
            Assert.NotEqual(0, geometry.Identity.ResourceGeneration);
            Assert.True(identities.Add(geometry.Identity));
        }
        string before = NativeBattleTestSnapshots.Json(actual.Frame);
        NativeBattlePilotVisualGeometry earlier = actualBatch.Attempts[0].Primitive!.PilotGeometry!;
        NativeBattlePilotVisualGeometry sampled = NativeBattlePilotVisualGeometry.Sample(earlier, earlier with { A = earlier.A + System.Numerics.Vector3.UnitX * 4 }, 0.5f);
        Assert.Equal(earlier.A + System.Numerics.Vector3.UnitX * 2, sampled.A);
        Assert.Equal(before, NativeBattleTestSnapshots.Json(actual.Frame));
    }

    [Fact]
    public void Strip_identity_keeps_both_actual_endpoint_births_and_material_passes_distinct()
    {
        NativeBattleEffect312Frame frame = GraphicsFrame(0x59FBD0, 0);
        const uint bank = 0x223F0C8;
        NativeBattlePilotVisualLifetimes.Allocated(frame.State.Storage, bank, 0);
        NativeBattlePilotVisualLifetimes.Allocated(frame.State.Storage, bank, 1);
        NativeBattlePilotVisualIdentity first = NativeBattleEffect312Presentation.Segment(frame, bank, 0, 1, 0, 0);
        Assert.Equal(first, NativeBattleEffect312Presentation.Segment(frame, bank, 0, 1, 0, 0));
        Assert.NotEqual(first, NativeBattleEffect312Presentation.Segment(frame, bank, 0, 1, 0, 1));
        NativeBattlePilotVisualLifetimes.Allocated(frame.State.Storage, bank, 1);
        NativeBattlePilotVisualIdentity second = NativeBattleEffect312Presentation.Segment(frame, bank, 0, 1, 0, 0);
        Assert.Equal(first.Generation, second.Generation);
        Assert.NotEqual(first.ResourceGeneration, second.ResourceGeneration);
        NativeBattlePilotVisualLifetimes.Allocated(frame.State.Storage, bank, 0);
        NativeBattlePilotVisualIdentity third = NativeBattleEffect312Presentation.Segment(frame, bank, 0, 1, 0, 0);
        Assert.NotEqual(first.Generation, third.Generation);
        Assert.Equal(second.ResourceGeneration, third.ResourceGeneration);
        NativeBattlePilotVisualGeometry geometry = new(first, default, default, default, default, 4);
        Assert.Same(geometry, NativeBattlePilotVisualGeometry.Sample(geometry, geometry with { Identity = second, A = System.Numerics.Vector3.One }, 0.5f));
    }

    private static NativeBattleEffect312Frame PutPoint(NativeBattleEffect312Frame frame, uint address, int x, int y, int z)
    {
        NativeBattleEffectArena point = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(unchecked((short)x))).Write(2, BitConverter.GetBytes(unchecked((short)y))).Write(4, BitConverter.GetBytes(unchecked((short)z))).Write(6, new byte[2]);
        return WriteBytes(frame, address, point, 8);
    }

    private static NativeBattleEffect312Inputs GraphicsInputs(Harness harness)
    {
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        return harness.Inputs() with { ReadView = _ => NativeBattlePackedMatrix32.FromMatrix(identity, null), ReadProjection = _ => new(identity, 256, 0, 0), ReadCameraRoll = _ => 0 };
    }

    private static NativeBattleEffect312Frame GraphicsFrame(uint callback, ushort age)
    {
        NativeBattleEffect312Frame frame = Frame(callback, age);
        return frame with { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()), Memory = frame.Memory with { Arena = NativeBattleEffectArena.Clear(), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() } };
    }

    private static NativeBattleEffect312Resources Resources()
    {
        byte[] mesh = new byte[64];
        BinaryPrimitives.WriteUInt32LittleEndian(mesh, 32);
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(4), 3);
        ImmutableDictionary<uint, NativeBattleStaticMeshResource> meshes = new uint[] { 0xCDE73C, 0xCD9624 }.ToImmutableDictionary(address => address, address => new NativeBattleStaticMeshResource(0, address, mesh.ToImmutableArray()));
        byte[] axis = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(axis, 4096);
        return new(null!, null!, meshes, ImmutableDictionary<uint, NativeBattleModuleResource>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(0xCDF660, axis.ToImmutableArray()).Add(0xCDF670, new byte[48].ToImmutableArray()));
    }

    private static NativeBattleEffect312Frame Frame(uint callback, ushort age)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x223E1A8, TaskAddress, 36, 100);
        (pool, int slot) = pool.Append(callback);
        pool = pool.WriteSlot(slot, pool.Slots[slot].WriteWord(12, age));
        NativeBattleOriginalEffectState effect = new(312, 311, new(312, 0x59CB90, 0x59CB70), new(3, 0, 1, 0, 0, 0), null) { Pools = [pool] };
        return new(NativeBattleEffectMemory.FromColdProcess(), effect, new(83), NativeBattleEffect312State.FromColdProcess().DetachBank(1));
    }

    private static NativeBattleEffect312Frame WithSaved(NativeBattleEffect312Frame frame, NativeBattleEffectArena model)
    {
        NativeBattleEffectArena saved = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((ushort)(U16(model, 0) & 0x1000)));
        saved = CopyBytes(model, 12, saved, 4, 8);
        saved = CopyBytes(model, 28, saved, 12, 8);
        return WriteBytes(frame, Saved, saved, 20);
    }

    private static ushort U16(NativeBattleEffectArena bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.Read(offset, 2));

    private sealed class Harness
    {
        public uint Flags { get; set; }
        public byte Sequence { get; set; }
        public List<NativeBattleEffect312Operation> Operations { get; } = [];
        public NativeBattleEffectArena ModelBytes { get; set; } = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((ushort)0x0123)).Write(12, new byte[] { 1, 0, 2, 0, 3, 0, 0xA7, 0x5D }).Write(28, new byte[] { 200, 0, 0xE8, 3, 44, 1, 0x6C, 0xD2 });

        public NativeBattleEffectArena Physical(uint address, int length)
        {
            uint value = address == 0x1D99A7C ? 0x1D99B00u : address == 0x1D99B08 ? 0x1D99C00u : throw new InvalidOperationException("Unexpected physical pointer read.");
            Assert.Equal(4, length);
            return NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(value));
        }

        public NativeBattleEffect312Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => Flags, _ => throw new InvalidOperationException(), (_, address, length) => Physical(address, length), (_, model) => model == Model ? ModelBytes : throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException(), (_, model) => model == Model ? Sequence : throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), Execute);

        private NativeBattleEffect312Frame Execute(NativeBattleEffect312Frame frame, NativeBattleEffect312Operation operation)
        {
            Operations.Add(operation);
            if (operation is NativeBattleEffect312WriteModelBytes write)
            {
                Assert.Equal(Model, write.ModelAddress);
                ModelBytes = CopyBytes(write.Data, 0, ModelBytes, write.Offset, write.Length);
            }
            else if (operation is NativeBattleEffect312DrawBatch draw)
            {
                frame = frame with { State = frame.State.WriteDword(draw.CursorGlobal, frame.State.ReadDword(draw.CursorGlobal) + (uint)draw.Batch.AllocationBytes) };
            }
            return frame;
        }
    }
}
