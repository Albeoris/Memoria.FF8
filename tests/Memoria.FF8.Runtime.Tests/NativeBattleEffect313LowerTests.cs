using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect313Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect313Lower;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect313LowerTests
{
    [Theory]
    [InlineData(0x59BCA0)]
    [InlineData(0x59C3E0)]
    public void Positive_delay_defers_all_drawing_and_pause_defers_decrement(uint callback)
    {
        Fixture fixture = new(callback);
        fixture.Task(t => t.WriteWord(14, 2));
        NativeBattleEffect313Step paused = Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs(0x201));
        Assert.Same(fixture.Frame, paused.Frame);
        NativeBattleEffect313Step running = Advance(paused.Frame, fixture.Address, fixture.Resources, fixture.Inputs());
        Assert.Equal(1, Signed(Read(running.Frame, fixture.Address), 14));
        Assert.Equal(0, Signed(Read(running.Frame, fixture.Address), 12));
        Assert.Empty(fixture.Operations);
    }

    [Theory]
    [InlineData(0x59B9F0)]
    [InlineData(0x59BB60)]
    [InlineData(0x59C2F0)]
    [InlineData(0x59C250)]
    public void Paused_leaf_still_draws_and_releases_before_holding_age(uint callback)
    {
        Fixture fixture = new(callback);
        fixture.Task(t => t.WriteWord(14, 20).WriteWord(28, 4096).WriteWord(32, 4096));
        NativeBattleEffect313Step result = Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs(0x201));
        Assert.Equal(0, result.Result);
        Assert.Equal(0, Signed(Read(result.Frame, fixture.Address), 12));
        NativeBattleEffect313DrawBatch draw = Assert.IsType<NativeBattleEffect313DrawBatch>(Assert.Single(fixture.Operations));
        Assert.Equal(0x223CC9Cu, draw.CursorGlobal);
        Assert.NotEqual(fixture.Frame.Memory.GeometryRegisters, result.Frame.Memory.GeometryRegisters);
        Assert.Equal(fixture.Frame.Random, result.Frame.Random);
    }

    [Fact]
    public void Debris_draws_last_eight_fade_then_integrates_signed_division_and_WORD_spin()
    {
        Fixture fixture = new(0x59B9F0);
        fixture.Task(t => t.WriteWord(12, 12).WriteWord(14, 20).WriteWord(16, 10).WriteWord(18, 20).WriteWord(20, 2000).WriteWord(22, unchecked((ushort)-10)).WriteWord(24, 1).WriteWord(26, 4096).WriteWord(28, 32760).WriteWord(30, 20).WriteWord(32, 32760).WriteWord(34, 20));
        NativeBattleEffect313Step result = Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs());
        NativeBattleModulePoolTask task = Read(result.Frame, fixture.Address);
        Assert.Equal(10, Signed(task, 18));
        Assert.Equal(-9, Signed(task, 22));
        Assert.Equal(unchecked((short)32780), Signed(task, 28));
        Assert.Equal(unchecked((short)32780), Signed(task, 32));
        NativeBattleEffectArena context = fixture.Temporary(result.Frame, 88);
        Assert.Equal(0xC3u, BinaryPrimitives.ReadUInt32LittleEndian(context.Read(28, 4)));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(context.Read(12, 4)));
        Assert.Equal(0xCD76C8u, BinaryPrimitives.ReadUInt32LittleEndian(context.Read(0, 4)));
    }

    [Fact]
    public void Ring_updates_scale_with_old_velocity_and_arithmetic_half_then_retires()
    {
        Fixture fixture = new(0x59BB60);
        fixture.Task(t => t.WriteWord(12, 23).WriteWord(28, 4096).WriteWord(32, 100).WriteWord(34, unchecked((ushort)-3)));
        NativeBattleEffect313Step result = Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs());
        Assert.Equal(2, result.Result);
        Assert.Equal(97, Signed(Read(result.Frame, fixture.Address), 32));
        Assert.Equal(-1, Signed(Read(result.Frame, fixture.Address), 34));
        NativeBattleEffectArena context = fixture.Temporary(result.Frame, 88);
        Assert.Equal(13 * 292, BinaryPrimitives.ReadInt32LittleEndian(context.Read(12, 4)));
    }

    [Theory]
    [InlineData(4, 96)]
    [InlineData(7, 110)]
    public void Sparkle_tint_uses_integer_quotient_then_signed_BYTE_multiply(short life, byte expected)
    {
        Fixture fixture = new(0x59BCA0);
        fixture.Task(t => t.WriteWord(12, 1).WriteWord(14, 0).WriteWord(28, 4096).WriteWord(32, unchecked((ushort)life)));
        NativeBattleEffect313Step result = Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs(0x201));
        NativeBattleEffectArena context = fixture.Temporary(result.Frame, 180);
        Assert.Equal(new[] { expected, expected, expected }, context.Read(28, 3));
        Assert.Equal(12, Word313(context, 36));
        Assert.Equal(0, Word313(context, 4));
    }

    [Theory]
    [InlineData(15, 256, 0xC0)]
    [InlineData(16, -1, 0)]
    [InlineData(30, 0, 0xC0)]
    public void Expansion_cue_middle_interval_leaves_unconsumed_cue_bytes(short age, int cue, uint flags)
    {
        Fixture fixture = new(0x59C2F0);
        fixture.Task(t => t.WriteWord(12, unchecked((ushort)age)).WriteWord(28, 4096));
        fixture.Frame = fixture.Frame with { Staging = fixture.Frame.Staging!.Write(NativeBattleTemporaryMemory.Address + 12, BitConverter.GetBytes(-1)) };
        NativeBattleEffect313Step result = Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs(0x201));
        NativeBattleEffectArena context = fixture.Temporary(result.Frame, 88);
        Assert.Equal(cue, BinaryPrimitives.ReadInt32LittleEndian(context.Read(12, 4)));
        Assert.Equal(flags, BinaryPrimitives.ReadUInt32LittleEndian(context.Read(28, 4)));
    }

    [Fact]
    public void Rotating_sprite_uses_terminal_output_to_restart_phase_after_release()
    {
        Fixture fixture = new(0x59C3E0);
        fixture.Task(t => t.WriteWord(12, 10).WriteWord(14, 0).WriteWord(28, 4096).WriteWord(32, 15));
        NativeBattleEffect313Step result = Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs());
        Assert.Equal(0, Signed(Read(result.Frame, fixture.Address), 32));
        Assert.Equal(11, Signed(Read(result.Frame, fixture.Address), 12));
        Assert.Equal(1, Word313(fixture.Temporary(result.Frame, 180), 36));
        Assert.Equal(-1, Word313(fixture.Temporary(result.Frame, 180), 40));
    }

    [Fact]
    public void Beam_shape_preserves_anchor_W_and_has_zero_initial_longitudinal_scale()
    {
        NativeBattleEffectArena anchor = Point(10, 20, 30).Write(6, [0xA5]);
        NativeBattleEffect313BeamShape result = BuildBeam313(anchor, Point(2000, -4000, -6000), Point(10, 20, -970), 0, 4096, 0);
        Assert.Equal(1000, result.Length);
        Assert.Equal(10, Word313(result.Emitter8, 0));
        Assert.Equal(20, Word313(result.Emitter8, 2));
        Assert.Equal(-970, Word313(result.Emitter8, 4));
        Assert.Equal(0xA5, result.Emitter8.ReadSnapshot(6, 2).Data[0]);
        Assert.False(result.Emitter8.ReadSnapshot(6, 2).Known[1]);
        Assert.Equal(0, result.Matrix.Rotation[2]);
        Assert.Equal(0, result.Matrix.Rotation[5]);
        Assert.Equal(0, result.Matrix.Rotation[8]);
        Assert.Equal(10, result.Matrix.X);
        Assert.Equal(20, result.Matrix.Y);
        Assert.Equal(30, result.Matrix.Z);
    }

    [Fact]
    public void Beam_births_consume_count_then_five_CRT_each_in_actual_native_order()
    {
        Fixture fixture = new(0x59BDF0);
        fixture.Task(t => t.WriteWord(12, 2).WriteWord(16, 0).WriteWord(18, 0).WriteWord(20, 1000).WriteWord(24, 70).WriteWord(26, 160).WriteWord(32, 4096));
        NativeCrtRandomState expected = fixture.Frame.Random.Draw(out ushort countDraw);
        int count = countDraw % 5 + 3;
        List<(int Life, int X, int Z, int Velocity, int Scale)> values = [];
        for (int i = 0; i < count; i++)
        {
            expected = expected.Draw(out ushort life);
            expected = expected.Draw(out ushort x);
            expected = expected.Draw(out ushort z);
            expected = expected.Draw(out ushort velocity);
            expected = expected.Draw(out ushort scale);
            values.Add((life % 8 + 8, x % 1000 - 500, z % 900 - 400, -10 - velocity % 20, scale % 1792 + 3072));
        }
        NativeBattleEffect313BeamShape shape = BuildBeam313(fixture.Frame.State.ReadView(0x223CC90, 8), Point(2000, -4000, -6000), TaskView(Read(fixture.Frame, fixture.Address), 16, 8), 70, 4096, 2);
        NativeBattleEffect313Step result = Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs());
        Assert.Equal(expected, result.Frame.Random);
        NativeBattleModulePoolTask[] children = result.Frame.Effect.Pools[0].Slots.Where(t => t.Callback == 0x59C250 && (t.Flags & 1) != 0).ToArray();
        Assert.Equal(count, children.Length);
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(values[i].Life, Signed(children[i], 14));
            Assert.Equal(unchecked((short)(Word313(shape.Emitter8, 0) + values[i].X)), Signed(children[i], 16));
            Assert.Equal(0, Signed(children[i], 18));
            Assert.Equal(unchecked((short)(Word313(shape.Emitter8, 4) + values[i].Z)), Signed(children[i], 20));
            Assert.Equal(values[i].Velocity, Signed(children[i], 22));
            Assert.Equal(values[i].Scale, Signed(children[i], 28));
        }
    }

    [Fact]
    public void Mandatory_beam_allocation_failure_does_not_skip_child_or_reset_stream()
    {
        Fixture fixture = new(0x59BDF0, 1);
        fixture.Task(t => t.WriteWord(12, 2).WriteWord(20, 1000).WriteWord(32, 4096));
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs()));
        Assert.Contains("mandatory", error.Message);
        Assert.IsType<NativeBattleEffect313DrawBatch>(Assert.Single(fixture.Operations));
    }

    [Fact]
    public void Actor_sequence_wait_keeps_motion_then_restores_exact_current_saved_bytes_before_contact()
    {
        Fixture fixture = new(0x59C4F0);
        NativeBattleEffect313Step first = Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs(0x201));
        Assert.Equal(0, Signed(Read(first.Frame, fixture.Address), 12));
        Assert.Equal(1, fixture.AnchorCalls);
        Assert.False(first.Frame.State.ReadView(0x223CC3C + 6, 1).ReadSnapshot(0, 1).Known[0]);
        fixture.Frame = first.Frame;
        fixture.Task(t => t.WriteWord(12, 31).WriteWord(26, 2));
        fixture.Sequence = 6;
        NativeBattleEffect313Step wait = Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs());
        Assert.Equal(0, wait.Result);
        Assert.Equal(32, Signed(Read(wait.Frame, fixture.Address), 12));
        Assert.DoesNotContain(fixture.Operations, op => op is NativeBattleEffect313Contact506690);
        fixture.Sequence = 0;
        NativeBattleEffect313Step done = Advance(wait.Frame, fixture.Address, fixture.Resources, fixture.Inputs());
        Assert.Equal(2, done.Result);
        Assert.Equal(33, Signed(Read(done.Frame, fixture.Address), 12));
        Assert.IsType<NativeBattleEffect313Contact506690>(fixture.Operations[^1]);
        Assert.Equal(2, ((NativeBattleEffect313Contact506690)fixture.Operations[^1]).HeaderRelativeIndex);
        Assert.Equal(100, Word313(fixture.Model, 28));
        Assert.Equal(200, Word313(fixture.Model, 30));
        Assert.Equal(300, Word313(fixture.Model, 32));
        Assert.False(fixture.Model.ReadSnapshot(18, 1).Known[0]);
        Assert.False(fixture.Model.ReadSnapshot(34, 1).Known[0]);
    }

    [Fact]
    public void Actor_full_record_bank_consumes_no_CRT_and_sequence_still_starts_after_attempts()
    {
        Fixture fixture = new(0x59C4F0);
        for (int i = 0; i < 80; i++)
        {
            fixture.Frame = fixture.Frame with { State = fixture.Frame.State.WriteDword(0x223B698u + (uint)i * 24, 1) };
        }
        fixture.Task(t => t.WriteWord(12, 24).WriteWord(26, 0));
        NativeBattleEffect313Step result = Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs());
        Assert.Equal(fixture.Frame.Random, result.Frame.Random);
        Assert.IsType<NativeBattleEffect313SelectSequence505C00>(fixture.Operations[^1]);
    }

    [Fact]
    public void Cleanup_transports_actual_P_plus_10000_and_reads_completion_after_service()
    {
        Fixture fixture = new(0x59C9E0);
        fixture.Complete = true;
        NativeBattleEffect313Step result = Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs(0x201));
        NativeBattleEffect313AppendTextureLoad508630 op = Assert.IsType<NativeBattleEffect313AppendTextureLoad508630>(Assert.Single(fixture.Operations));
        Assert.Equal(0x20EFAB8u, op.SourceAddress);
        Assert.Equal(fixture.Address + 14, op.CompletionAddress);
        Assert.Equal(2, result.Result);
        Assert.Equal(1, Signed(Read(result.Frame, fixture.Address), 12));
    }

    [Fact]
    public void Actual_debris_capture_keeps_loaded_body_FIFO_task_owner_and_native_update()
    {
        Fixture Prepare()
        {
            Fixture fixture = new(0x59B9F0);
            fixture.Task(task => task.WriteWord(12, 0).WriteWord(14, 20).WriteWord(24, 0).WriteWord(26, 4096).WriteWord(28, 0).WriteWord(30, 0).WriteWord(32, 0).WriteWord(34, 0));
            NativeBattleStaticMeshResource mesh = CaptureMesh(6) with { Address = 0xCD7410 };
            fixture.Resources = fixture.Resources with { Meshes = fixture.Resources.Meshes.SetItem(mesh.Address, mesh) };
            return fixture;
        }
        Fixture native = Prepare();
        NativeBattleEffect313Step expected = Advance(native.Frame, native.Address, native.Resources, native.Inputs() with { ReadProjection = _ => new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108) });
        Fixture captured = Prepare();
        NativeBattlePilotVisualIdentity owner = NativeBattlePilotVisualLifetimes.Identity(313, Read(captured.Frame, captured.Address));
        NativeBattleEffect313Step actual = Advance(captured.Frame, captured.Address, captured.Resources, captured.Inputs() with { CapturePresentation = true, ReadProjection = _ => new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108) });
        Assert.Equal(NativeBattleTestSnapshots.Json(expected.Frame), NativeBattleTestSnapshots.Json(actual.Frame));
        NativeBattleGfGeometryBatch expectedBatch = Assert.Single(native.Operations.OfType<NativeBattleEffect313DrawBatch>()).Batch;
        NativeBattleGfGeometryBatch actualBatch = Assert.Single(captured.Operations.OfType<NativeBattleEffect313DrawBatch>()).Batch;
        Assert.Equal(NativeBattleTestSnapshots.Json(expectedBatch), NativeBattleTestSnapshots.Json(actualBatch));
        NativeBattleGeometryPrimitive body = Assert.Single(actualBatch.Attempts).Primitive!;
        NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(body.PilotGeometry);
        Assert.Equal(owner.Bank, geometry.Identity.Bank);
        Assert.Equal(owner.Slot, geometry.Identity.Slot);
        Assert.Equal(owner.Generation, geometry.Identity.Generation);
        Assert.Equal(0xCD7410, geometry.Identity.ResourceGeneration);
        Assert.True(geometry.UseSourceDepths);
        Assert.Equal(new System.Numerics.Vector4(body.VertexPayload4[0], body.VertexPayload4[1], body.VertexPayload4[2], body.VertexPayload4[3]), geometry.SourceDepths);
        Assert.Equal(new System.Numerics.Vector3(-100, -100, 1000), geometry.A);
        Assert.Equal(1, Read(actual.Frame, captured.Address).ReadWord(12));
        Assert.Null(Assert.Single(expectedBatch.Attempts).Primitive!.PilotGeometry);
        string before = NativeBattleTestSnapshots.Json(actual.Frame);
        NativeBattlePilotVisualGeometry sampled = NativeBattlePilotVisualGeometry.Sample(geometry, geometry with { A = geometry.A + System.Numerics.Vector3.UnitX * 4 }, 0.5f);
        Assert.Equal(geometry.A + System.Numerics.Vector3.UnitX * 2, sampled.A);
        Assert.Equal(before, NativeBattleTestSnapshots.Json(actual.Frame));
    }

    private static NativeBattleStaticMeshResource CaptureMesh(int group, bool degenerate = false)
    {
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        byte[] bytes = new byte[8 + 4 * 8 + 8 * 4 + strides[group]];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 4);
        short[] xs = [-100, 100, -100, 100];
        short[] ys = [-100, -100, 100, 100];
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 8), xs[index]);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10 + index * 8), ys[index]);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(14 + index * 8), (short)(index + 3));
        }
        int cursor = 40;
        for (int section = 0; section < 8; section++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(cursor), section == group ? 1 : 0);
            cursor += 4;
            if (section != group)
            {
                continue;
            }
            byte command = group switch { 3 => 0x2C, 4 => 0x30, 5 => 0x38, 6 => 0x34, _ => 0x20 };
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor), (uint)command << 24 | 0x808080u);
            for (int corner = 0; corner < ((group & 1) == 0 ? 3 : 4); corner++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor + 4 + corner * 2), (ushort)(degenerate ? 0 : corner * 2));
            }
            if ((group & 2) != 0)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor + 10), 0x0605);
                if ((group & 1) != 0)
                {
                    BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor + 10), 6);
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor + 20), 0x08070605);
                }
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor + 12), 0x12340201);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor + 16), 0x56780403);
            }
            if ((group & 4) != 0)
            {
                int start = (group & 2) != 0 ? (group & 1) != 0 ? 24 : 20 : 12;
                for (int corner = 1; corner < ((group & 1) == 0 ? 3 : 4); corner++)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor + start + (corner - 1) * 4), 0x00808080);
                }
            }
            cursor += strides[group];
        }
        return new(0, 0x1000, bytes.ToImmutableArray());
    }

    private static NativeBattleEffectArena Point(short x, short y, short z) => PutWord313(PutWord313(PutWord313(NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, x), 2, y), 4, z);

    private sealed class Fixture
    {
        public uint Address = 0x223BE28;
        public NativeBattleEffect313Frame Frame;
        public NativeBattleEffect313Resources Resources;
        public NativeBattleEffectArena Model = PutWord313(PutWord313(PutWord313(PutWord313(PutWord313(PutWord313(PutWord313(NativeBattleEffectArena.Clear().MarkUnknown(18, 2).MarkUnknown(34, 2), 0, 0x0202), 12, 10), 14, 20), 16, 30), 28, 100), 30, 200), 32, 300);
        public List<NativeBattleEffect313Operation> Operations = [];
        public byte Sequence;
        public bool Complete;
        public int AnchorCalls;
        public Fixture(uint callback, ushort capacity = 100)
        {
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x223BE18, Address, 36, capacity);
            (pool, int slot) = pool.Append(callback);
            pool = pool.WriteSlot(slot, pool.Slots[slot].WriteBytes(12, new byte[24]).WriteWord(14, 0).WriteWord(20, 1000).WriteWord(26, ushort.MaxValue).WriteWord(28, 4096).WriteWord(32, 4));
            NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(identity, null) };
            memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(memory, [pool]);
            NativeBattleEffect313State state = NativeBattleEffect313State.FromColdProcess().WriteDword(0x223CC98, 0x20DFAB8).WriteView(0x223CC90, Point(0, 0, 2000), 8);
            state = state with { FixedBanks = state.FixedBanks.SetItem(Address, null) };
            Frame = new(memory, new(313, 313, new(313, 0x59B0B0, 0x59B0D0), null, null) { Pools = [pool] }, new(1), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(NativeBattleTemporaryMemory.Address, new byte[532]) };
            byte[] mesh = new byte[40];
            BinaryPrimitives.WriteInt32LittleEndian(mesh, 8);
            ImmutableDictionary<uint, NativeBattleStaticMeshResource> meshes = ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty;
            foreach (uint source in NativeBattleEffect313Resources.MeshAddresses)
            {
                meshes = meshes.Add(source, new(0, source, mesh.ToImmutableArray()));
            }
            byte[] sprite = new byte[48];
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(8), 16);
            for (int index = 0; index < 16; index++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(10 + index * 2), 44);
            }
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(42), ushort.MaxValue);
            ImmutableDictionary<uint, NativeBattleModuleResource> sprites = ImmutableDictionary<uint, NativeBattleModuleResource>.Empty;
            foreach (uint source in NativeBattleEffect313Resources.SpriteAddresses)
            {
                sprites = sprites.Add(source, new(5, source, 48, 0, sprite.ToImmutableArray()));
            }
            Resources = new(null!, null!, meshes, sprites, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(0xCD8D40, BitConverter.GetBytes(0xCD7410u).Concat(BitConverter.GetBytes(0xCD76C8u)).ToImmutableArray()));
        }
        public void Task(Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> change) => Frame = Write(Frame, Address, change(Read(Frame, Address)));
        public NativeBattleEffectArena Temporary(NativeBattleEffect313Frame frame, int length) => NativeBattleTemporaryMemory.ReadView(frame.Staging!, NativeBattleTemporaryMemory.Address, length);
        public NativeBattleEffect313Inputs Inputs(uint flags = 0) => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => flags, _ => null!, (_, _) => Model, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 100, 160, 108), _ => 0, _ => Point(2000, -4000, -6000), null!, (frame, _, retained) =>
        {
            AnchorCalls++;
            return new(frame, PutWord313(PutWord313(PutWord313(retained, 0, 10), 2, -100), 4, 200));
        }, null!, (_, _) => Sequence, (frame, operation) =>
        {
            Operations.Add(operation);
            if (operation is NativeBattleEffect313WriteModelBytes write)
            {
                Model = CopyBytes(write.Data, 0, Model, write.Offset, write.Length);
            }
            if (operation is NativeBattleEffect313SelectSequence505C00 select)
            {
                Sequence = select.Sequence;
            }
            if (operation is NativeBattleEffect313AppendTextureLoad508630 load && Complete)
            {
                frame = WriteBytes(frame, load.CompletionAddress, NativeBattleEffectArena.Clear().Write(0, [1]), 1);
            }
            return frame;
        });
    }
}
