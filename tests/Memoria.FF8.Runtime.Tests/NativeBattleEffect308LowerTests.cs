using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect308Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect308Lower;
using static Memoria.FF8.Runtime.NativeBattleEffect308Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect308LowerTests
{
    [Fact]
    public void Zero_span_preserves_local_generator_and_negative_span_uses_arithmetic_shift()
    {
        Fixture fixture = new();
        NativeBattleEffect308RandomStep zero = Range668D30(fixture.Frame, 0, 42);
        Assert.Equal(42, zero.Value);
        Assert.Equal(1u, zero.Frame.State.ReadDword(InternalRandomGlobal));
        NativeBattleEffect308RandomStep nonzero = Range668D30(zero.Frame, -128, 42);
        Assert.Equal(139u, nonzero.Frame.State.ReadDword(InternalRandomGlobal));
        Assert.Equal(41, nonzero.Value);
        Assert.Equal(fixture.Frame.Random, nonzero.Frame.Random);
    }

    [Theory]
    [InlineData(false, 180)]
    [InlineData(true, 0)]
    public void Act_birth_consumes_six_words_and_restores_pose_before_derivative_initialization(bool forceYzero, int expectedY)
    {
        Fixture fixture = new();
        fixture.Operands(1, 2048, 0, 1024, 0, -256);
        (NativeBattleEffect308Frame frame, uint next) = BirthAct6684C0(fixture.Frame, Fixture.Pc, Fixture.Model, Fixture.Rows, forceYzero, fixture.Resources, fixture.Inputs());
        Assert.Equal(Fixture.Pc + 12, next);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == ActDescriptor);
        NativeBattleModulePoolTask task = pool.Slots[pool.Head!.Value];
        Assert.Equal(110, Signed(task, 12));
        Assert.Equal(expectedY, Signed(task, 14));
        Assert.Equal(5030, Signed(task, 16));
        Assert.Equal(1024, Signed(task, 0x12));
        Assert.Equal(-256, Signed(task, 0x16));
        Assert.Equal(0x1000000u, task.ReadDword(0x18));
        Assert.Equal(0u, task.ReadDword(0x1C));
        Assert.Collection(fixture.Operations, operation => Assert.IsType<NativeBattleEffect308ApplyOffsetPose662C00>(operation), operation => Assert.IsType<NativeBattleEffect308RestorePose508C90>(operation));
        Assert.Equal((1, 2048), Assert.Single(fixture.AnchorCalls));
    }

    [Fact]
    public void Exhausted_particle_pool_skips_pose_and_anchor_but_returns_full_payload_end()
    {
        Fixture fixture = new();
        fixture.Fill(ParticleDescriptor);
        fixture.Operands(0, 1, 2048, 3, 2048, -1, 2, -3, 4, -5, 6);
        (NativeBattleEffect308Frame frame, uint next) = BirthParticle668970(fixture.Frame, Fixture.Pc, Fixture.Model, Fixture.Rows, fixture.Resources, fixture.Inputs());
        Assert.Equal(Fixture.Pc + 22, next);
        Assert.Empty(fixture.Operations);
        Assert.Empty(fixture.AnchorCalls);
        Assert.Equal(fixture.Frame.State.ReadDword(InternalRandomGlobal), frame.State.ReadDword(InternalRandomGlobal));
    }

    [Fact]
    public void Particle_birth_uses_low_bytes_and_keeps_applied_pose_current()
    {
        Fixture fixture = new();
        fixture.Operands(0, 1, 4096, 0x103, 2048, -1, 2, -3, 4, -5, 6);
        (NativeBattleEffect308Frame frame, uint next) = BirthParticle668970(fixture.Frame, Fixture.Pc, Fixture.Model, Fixture.Rows, fixture.Resources, fixture.Inputs());
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == ParticleDescriptor);
        NativeBattleModulePoolTask task = pool.Slots[pool.Head!.Value];
        Assert.Equal(Fixture.Pc + 22, next);
        Assert.Equal(new byte[] { 0, 3, 255, 2, 253, 4, 251, 6 }, task.ReadBytes(0x14, 8));
        Assert.Equal(110, Signed(task, 12));
        Assert.IsType<NativeBattleEffect308ApplyOffsetPose662C00>(Assert.Single(fixture.Operations));
    }

    [Theory]
    [InlineData(0, 12)]
    [InlineData(1, 16)]
    [InlineData(3, 13)]
    public void Particle_final_draw_moves_old_signed_velocities_updates_only_X_Y_and_then_completes(int selector, int phases)
    {
        Fixture fixture = new();
        (fixture.Frame, uint address) = Append(fixture.Frame, ParticleDescriptor, 0x668A50);
        NativeBattleModulePoolTask task = Read(fixture.Frame, address).WriteWord(12, 100).WriteWord(14, 200).WriteWord(16, 5000).WriteWord(0x12, 4096).WriteBytes(0x14, [unchecked((byte)(phases - 1)), (byte)selector, 255, 2, 253, 4, 251, 99]);
        fixture.Frame = Write(fixture.Frame, address, task);
        NativeBattleEffect308Step result = Advance(fixture.Frame, address, fixture.Resources, fixture.Inputs());
        task = Read(result.Frame, address);
        Assert.Equal(2, result.Result);
        Assert.Equal(96, Signed(task, 12));
        Assert.Equal(208, Signed(task, 14));
        Assert.Equal(4988, Signed(task, 16));
        Assert.Equal(new byte[] { 3, 248, 253, 4, 251, 99 }, task.ReadBytes(0x16, 6));
        Assert.Equal(phases, task.ReadBytes(0x14, 1)[0]);
        NativeBattleEffectArena retained = NativeBattleTemporaryMemory.ReadView(result.Frame.Staging!, NativeBattleTemporaryMemory.Address, 180);
        Assert.Equal(phases - 1, Word308(retained, 4));
        Assert.Single(fixture.Deliveries);
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(result.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Burst_zero_count_or_full_pool_still_anchors_without_random_samples(int count)
    {
        Fixture fixture = new();
        if (count != 0)
        {
            fixture.Fill(ParticleDescriptor);
        }
        fixture.Operands(0, 1, 0, 0, 2048, (short)count, 100, 0, 100, 0, 100, 0, 10, 0, 10, 0, 10, 0, 4);
        (NativeBattleEffect308Frame frame, uint next) = BirthBurst668B20(fixture.Frame, Fixture.Pc, Fixture.Model, Fixture.Rows, fixture.Resources, fixture.Inputs());
        Assert.Equal(Fixture.Pc + 38, next);
        Assert.Single(fixture.AnchorCalls);
        Assert.IsType<NativeBattleEffect308ApplyOffsetPose662C00>(Assert.Single(fixture.Operations));
        Assert.Equal(1u, frame.State.ReadDword(InternalRandomGlobal));
    }

    [Fact]
    public void Burst_random_order_skips_zero_spans_and_acceleration_shifts_truncated_signed_bytes()
    {
        Fixture fixture = new();
        fixture.Operands(0, 1, 0, 1, 2048, 1, 0, -2, 0, 3, 0, -4, 1, -1, 0, -128, 0, 127, 8);
        (NativeBattleEffect308Frame frame, uint next) = BirthBurst668B20(fixture.Frame, Fixture.Pc, Fixture.Model, Fixture.Rows, fixture.Resources, fixture.Inputs());
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == ParticleDescriptor);
        NativeBattleModulePoolTask task = pool.Slots[pool.Head!.Value];
        Assert.Equal(Fixture.Pc + 38, next);
        Assert.Equal(139u, frame.State.ReadDword(InternalRandomGlobal));
        Assert.Equal(new byte[] { 255, 128, 127, 255, 255, 0 }, task.ReadBytes(0x16, 6));
        Assert.Equal(108, Signed(task, 12));
        Assert.Equal(183, Signed(task, 14));
        Assert.Equal(5026, Signed(task, 16));
    }

    [Theory]
    [InlineData(0, 0x1026038)]
    [InlineData(1, 0x1025D20)]
    public void Smoke_uses_reverse_directory_and_draws_before_final_life_decay_and_motion(int selector, uint source)
    {
        Fixture fixture = new();
        uint row = Fixture.Main + 0x890;
        NativeBattleEffectArena data = PutWord308(PutWord308(PutWord308(PutWord308(NativeBattleEffectArena.Clear(), 0, 100), 2, 200), 4, 5000), 6, 1).Write(8, [255, 2, 253, (byte)selector]);
        fixture.Frame = WriteBytes(fixture.Frame, row, data, 12);
        NativeBattleEffect308Frame frame = DrawSmoke669620(fixture.Frame, Fixture.Main, fixture.Resources, fixture.Inputs());
        NativeBattleEffectArena after = ReadBytes(frame, row, 12);
        Assert.Equal(0, Word308(after, 6));
        Assert.Equal(96, Word308(after, 0));
        Assert.Equal(208, Word308(after, 2));
        Assert.Equal(4988, Word308(after, 4));
        NativeBattleEffect308DrawBatch draw = Assert.Single(fixture.Deliveries);
        Assert.Equal(40, draw.Batch.AllocationBytes);
        Assert.Equal(19, Assert.Single(draw.Batch.Attempts).Primitive!.Bucket);
        NativeBattleEffectArena retained = NativeBattleTemporaryMemory.ReadView(frame.Staging!, NativeBattleTemporaryMemory.Address, 180);
        Assert.Equal(source, Dword308(retained, 0));
        Assert.Equal(source + 44, Dword308(retained, 0x2C));
        Assert.Equal(1u, Dword308(retained, 0x30));
    }

    [Fact]
    public void Background_consumes_three_WORD_low_bytes_and_emits_actual_flat_quad()
    {
        Fixture fixture = new();
        fixture.Operands(0x1234, 0x5678, -1);
        (_, uint next) = SetBackground668D60(fixture.Frame, Fixture.Pc, fixture.Inputs());
        Assert.Equal(Fixture.Pc + 6, next);
        NativeBattleEffect308DrawBatch draw = Assert.Single(fixture.Deliveries);
        Assert.Equal(24, draw.Batch.AllocationBytes);
        NativeBattleGeometryPrimitive primitive = Assert.Single(draw.Batch.Attempts).Primitive!;
        Assert.Equal(8, primitive.Bucket);
        Assert.Equal(0x2AFF7834u, primitive.Words[1]);
        Assert.Equal(0x00D80140u, primitive.Words[5]);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(7)]
    public void Act_node_reuses_actual_selected_groups_and_retains_cue_zero_FAR_and_context_bytes(int group)
    {
        Fixture fixture = new();
        NativeBattleStaticMeshResource mesh = Mesh(0x1000010, group);
        byte[] source = new byte[16 + mesh.Bytes.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(8), 16);
        mesh.Bytes.AsSpan().CopyTo(source.AsSpan(16));
        fixture.Resources = fixture.Resources with { Geometry = fixture.Resources.Geometry.Add(mesh.Address, mesh) };
        byte[] node = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(node.AsSpan(4), 0x200);
        for (int axis = 0; axis < 3; axis++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(24 + axis * 2), 4096);
        }
        NativeBattleEffectArena caller = PackedArena(NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null));
        caller = Put308(Put308(Put308(Put308(Put308(Put308(caller, 0x20, 4096), 0x24, 4096), 0x28, 4096), 0x2C, 1), 0x30, unchecked((uint)-256)), 0x34, Fixture.Arena + MorphOffset);
        fixture.Frame = fixture.Frame with { Staging = fixture.Frame.Staging!.Write(NativeBattleTemporaryMemory.Address + 8, [1, 2, 3]).MarkUnknown(NativeBattleTemporaryMemory.Address + 0x38, 1) };
        NativeBattleEffect308Frame frame = NativeBattleEffect308Programme.DrawNode(fixture.Frame, new(0x1000000, source.ToImmutableArray()), new(0, 0, node.ToImmutableArray()), caller, fixture.Resources, fixture.Inputs());
        NativeBattleEffect308DrawBatch draw = Assert.Single(fixture.Deliveries);
        Assert.True(draw.Batch.AllocationBytes > 0);
        Assert.Equal((uint?)16, frame.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        Assert.Equal((uint?)32, frame.Memory.PrimitiveProcess.ModelRendererRegisters.FarGreen);
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(frame.Staging!, NativeBattleTemporaryMemory.Address, 88);
        Assert.Equal(new byte[] { 1, 2, 3 }, temporary.Read(8, 3));
        Assert.False(temporary.ReadSnapshot(0x38, 1).Known[0]);
        Assert.Equal(mesh.Address + (uint)mesh.Bytes.Length, Dword308(temporary, 0x20));
    }

    [Fact]
    public void Act_callback_advances_its_physical_cache_and_finishes_when_remaining_becomes_zero()
    {
        Fixture fixture = new();
        fixture.Operands(1, 0, 0, 1024, 0, -256);
        (fixture.Frame, _) = BirthAct6684C0(fixture.Frame, Fixture.Pc, Fixture.Model, Fixture.Rows, false, fixture.Resources, fixture.Inputs());
        NativeBattleEffectTaskPool pool = fixture.Frame.Effect.Pools.Single(pool => pool.DescriptorAddress == ActDescriptor);
        uint address = pool.BankAddress + (uint)(pool.Head!.Value * pool.Stride);
        NativeBattleEffect308Step result = Advance(fixture.Frame, address, fixture.Resources, fixture.Inputs());
        Assert.Equal(2, result.Result);
        Assert.Equal(1u, Read(result.Frame, address).ReadDword(0x1C));
        Assert.Empty(fixture.Deliveries);
    }

    [Fact]
    public void Morph_writes_only_XYZ_and_projects_current_staging_W_knownness()
    {
        Fixture fixture = new();
        NativeBattleStaticMeshResource original = Mesh(0x1000010, 7);
        int polygon = BinaryPrimitives.ReadInt32LittleEndian(original.Bytes.AsSpan());
        byte[] bytes = new byte[original.Bytes.Length + 32];
        original.Bytes.AsSpan().Slice(0, polygon).CopyTo(bytes);
        original.Bytes.AsSpan().Slice(12, 32).CopyTo(bytes.AsSpan(polygon));
        original.Bytes.AsSpan().Slice(polygon).CopyTo(bytes.AsSpan(polygon + 32));
        BinaryPrimitives.WriteInt32LittleEndian(bytes, polygon + 32);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 2);
        for (int index = 0; index < 4; index++)
        {
            short x = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(polygon + index * 8));
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(polygon + index * 8), (short)(x + 100));
            fixture.Frame = WriteBytes(fixture.Frame, Fixture.Arena + MorphOffset + (uint)(index * 8), NativeBattleEffectArena.Clear().Write(6, [0xA5]).MarkUnknown(7, 1), 8);
        }
        NativeBattleStaticMeshResource mesh = original with { Bytes = bytes.ToImmutableArray() };
        fixture.Resources = fixture.Resources with { Geometry = fixture.Resources.Geometry.Add(mesh.Address, mesh) };
        byte[] source = new byte[16 + bytes.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(8), 16);
        bytes.AsSpan().CopyTo(source.AsSpan(16));
        byte[] node = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(node.AsSpan(4), 0x200);
        for (int axis = 0; axis < 3; axis++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(24 + axis * 2), 4096);
        }
        BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(38), 2048);
        BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(42), 1);
        NativeBattleEffectArena caller = PackedArena(NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null));
        caller = Put308(Put308(Put308(caller, 0x2C, 0), 0x30, 0), 0x34, Fixture.Arena + MorphOffset);
        NativeBattleEffect308Frame frame = NativeBattleEffect308Programme.DrawNode(fixture.Frame, new(0x1000000, source.ToImmutableArray()), new(0, 0, node.ToImmutableArray()), caller, fixture.Resources, fixture.Inputs());
        NativeBattleEffectArena row = ReadBytes(frame, Fixture.Arena + MorphOffset, 8);
        Assert.Equal(-150, Word308(row, 0));
        Assert.Equal(-200, Word308(row, 2));
        Assert.Equal(5000, Word308(row, 4));
        Assert.Equal(0xA5, row.Read(6, 1)[0]);
        Assert.False(row.ReadSnapshot(7, 1).Known[0]);
        Assert.False(frame.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[7]);
        Assert.Single(fixture.Deliveries);
    }

    private sealed class Fixture
    {
        public const uint Arena = 0x20DFAB8;
        public const uint Main = Arena + 0x1B3C;
        public const uint Rows = Main + 0x20;
        public const uint Model = 0x1D97494;
        public const uint Pc = 0x102E5A2;
        public NativeBattleEffect308Frame Frame;
        public NativeBattleEffect308Resources Resources;
        public List<NativeBattleEffect308Operation> Operations = [];
        public List<(int Bone, int Anchor)> AnchorCalls = [];
        public List<NativeBattleEffect308DrawBatch> Deliveries = [];

        public Fixture()
        {
            ImmutableArray<NativeBattleEffectTaskPool> pools = CommonPools.Select(shape => NativeBattleEffectTaskPool.Initialize(shape.Descriptor, Arena + shape.Bank, shape.Stride, shape.Capacity)).ToImmutableArray();
            NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess(), GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), identity, true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(identity, null) };
            memory = NativeBattleEffectTaskMemory.RevokeArenaOwnership(memory, pools);
            NativeBattleEffect308State state = NativeBattleEffect308State.FromColdProcess().WriteDword(ArenaGlobal, Arena).WriteDword(CursorGlobal, Arena + FirstPacketOffset).WriteDword(InternalRandomGlobal, 1);
            state = state with { ScriptSourcesBound = true, ScriptStorage = ScriptRanges.ToImmutableDictionary(range => range.Address, _ => NativeBattleEffectArena.Clear()) };
            Frame = new(memory, new(308, 307, new(308, Entry, Initializer), null, null) { Pools = pools }, new(42), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(NativeBattleTemporaryMemory.Address, new byte[532]) };
            Frame = WriteBytes(Frame, Rows + 16, PutWord308(PutWord308(PutWord308(NativeBattleEffectArena.Clear(), 0, 10), 2, -20), 4, 30), 6);
            byte[] programme = new byte[32];
            BinaryPrimitives.WriteUInt32LittleEndian(programme.AsSpan(4), 16);
            BinaryPrimitives.WriteUInt32LittleEndian(programme.AsSpan(20), 1);
            byte[] directory = new byte[8];
            BinaryPrimitives.WriteUInt32LittleEndian(directory, 0x1000000);
            BinaryPrimitives.WriteUInt32LittleEndian(directory.AsSpan(4), 32);
            ImmutableDictionary<uint, NativeBattleModuleResource> sprites = ImmutableDictionary<uint, NativeBattleModuleResource>.Empty;
            ImmutableDictionary<byte, uint> pointers = ImmutableDictionary<byte, uint>.Empty;
            foreach ((byte selector, uint address, int count) in new (byte, uint, int)[] { (0, 0x1025D20, 12), (1, 0x1025E8C, 16), (3, 0x1026198, 13) })
            {
                byte[] data = new byte[48];
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(8), (ushort)count);
                for (int index = 0; index <= count; index++)
                {
                    BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(10 + index * 2), 44);
                }
                sprites = sprites.Add(address, new(0, address, 0, 0, data.ToImmutableArray()));
                pointers = pointers.Add(selector, address);
            }
            ImmutableDictionary<uint, NativeBattleModuleResource> smoke = ImmutableDictionary<uint, NativeBattleModuleResource>.Empty.Add(0x1026038, Smoke(0x1026038, 13)).Add(0x1025D20, Smoke(0x1025D20, 12));
            Resources = new(null!, null!, null!, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty, ImmutableDictionary<int, NativeBattleGfAnimationSource>.Empty.Add(0, new(0x1000000, programme.ToImmutableArray())), ImmutableDictionary<int, int>.Empty.Add(0, 32), ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, sprites, pointers, smoke, 0, ImmutableDictionary<int, NativeBattleEffectFileAsset>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(0x102A400, directory.ToImmutableArray()));
        }

        public void Operands(params short[] values)
        {
            byte[] data = new byte[values.Length * 2];
            for (int index = 0; index < values.Length; index++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(index * 2), values[index]);
            }
            Frame = WriteBytes(Frame, Pc, NativeBattleEffectArena.Clear().Write(0, data), data.Length);
        }

        public void Fill(uint descriptor)
        {
            NativeBattleEffectTaskPool pool = Frame.Effect.Pools.Single(pool => pool.DescriptorAddress == descriptor);
            for (int index = 0; index < pool.Slots.Length; index++)
            {
                (Frame, _) = Append(Frame, descriptor, 0x668A50);
            }
        }

        public NativeBattleEffect308Inputs Inputs()
        {
            return new(NativeBattleGteColorInterpolation.CodeConstants45F270, null!, null!, null!, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 512, 160, 108), _ => 0, (frame, model, bone, anchor, retained) =>
            {
                AnchorCalls.Add((bone, anchor));
                return new(frame, PutPoint(retained, 0, new(100, 200, 5000)));
            }, null!, null!, null!, (frame, operation) =>
            {
                if (operation is NativeBattleEffect308DrawBatch draw)
                {
                    Deliveries.Add(draw);
                }
                else
                {
                    Operations.Add(operation);
                }
                return frame;
            });
        }
    }

    private static NativeBattleModuleResource Smoke(uint address, int phases)
    {
        byte[] data = new byte[64];
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(8), (ushort)phases);
        for (int index = 1; index <= phases; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(8 + index * 2), 40);
        }
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(40), 1);
        data[44] = 10;
        data[46] = 12;
        data[47] = 0x2E;
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(60), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(62), 4096);
        return new(0, address, 0, 0, data.ToImmutableArray());
    }

    private static NativeBattleStaticMeshResource Mesh(uint address, int group)
    {
        int count = (group & 1) == 0 ? 3 : 4;
        int polygon = 12 + count * 8;
        int stride = group switch { 2 or 4 => 20, 3 or 5 => 24, _ => 36 };
        byte[] data = new byte[polygon + 32 + stride];
        BinaryPrimitives.WriteInt32LittleEndian(data, polygon);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), count);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(8), 1);
        short[] xy = [-200, -200, 200, -200, -200, 200, 200, 200];
        for (int index = 0; index < count; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(12 + index * 8), xy[index * 2]);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(14 + index * 8), xy[index * 2 + 1]);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(16 + index * 8), 5000);
        }
        int face = polygon + group * 4 + 4;
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(face - 4), 1);
        uint command = group switch { 2 => 0x26u, 3 => 0x2Eu, 4 => 0x32u, 5 => 0x3Au, _ => 0x3Eu };
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(face), command << 24 | 0x808080);
        for (int index = 0; index < count; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(face + 4 + index * 2), (ushort)(index * 2));
        }
        return new(0, address, data.ToImmutableArray());
    }
}
