using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleBladeShot12Controller;
using static Memoria.FF8.Runtime.NativeBattleBladeShot12Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBladeShot12ControllerTests
{
    [Fact]
    public void Entry_keeps_physical_model_pointers_and_resets_all_three_full_banks_without_clearing_packets()
    {
        var frame = Inactive();
        frame = frame with { State = frame.State.WriteDword(PacketBase + 17, 0x12345678).WriteWord(SourcePointGlobal + 6, 0xBEEF) with { RootBankStorage = NativeBattleEffectArena.Clear().Write(98, new byte[] { 1, 2 }), ChildABankStorage = NativeBattleEffectArena.Clear().Write(ChildABankLength - 2, new byte[] { 3, 4 }), ChildBBankStorage = NativeBattleEffectArena.Clear().Write(ChildBBankLength - 2, new byte[] { 5, 6 }) } };
        List<NativeBattleBladeShot12Operation> operations = [];
        List<(uint Model, int Bone)> bones = [];
        var inputs = Inputs(operations, bones) with { ReadAnchor571400 = (current, model) =>
        {
            Assert.Equal(0x1D972C0u, model);
            Assert.Equal(7, current.Memory.Active);
            return new(current with { Memory = current.Memory with { Active = 241 } }, PointView(new(900, 1000, 1100), 777));
        } };
        var actual = Initialize(frame, Resources(), inputs);
        Assert.Equal(0x1D97494u, actual.State.ReadDword(CasterGlobal));
        Assert.Equal(0x1D972C0u, actual.State.ReadDword(FirstTargetGlobal));
        Assert.Equal(0x1D99A78u, actual.State.ReadDword(ParametersGlobal));
        Assert.Equal(new[] { (0x1D97494u, 7) }, bones);
        Assert.Equal(new NativeBattleBladeShot12ActorMeshMask124(0x1D97494, 0x80, 0), Assert.Single(operations));
        Assert.Equal(241, actual.Memory.Active);
        Assert.Equal((ushort)128, Read(actual, RootBank).ReadWord(0x2A));
        Assert.Equal((ushort)0, Read(actual, RootBank).ReadWord(0x62));
        Assert.Equal((ushort)0, actual.Effect.Pools[1].Slots[^1].ReadWord(30));
        Assert.Equal((ushort)0, actual.Effect.Pools[2].Slots[^1].ReadWord(50));
        Assert.Equal(0x12345678u, actual.State.ReadDword(PacketBase + 17));
        Assert.Equal((ushort)0xBEEF, actual.State.ReadWord(SourcePointGlobal + 6));
        Assert.Null(actual.State.RootBankStorage);
        Assert.Null(actual.State.ChildABankStorage);
        Assert.Null(actual.State.ChildBBankStorage);
        Assert.Equal(frame.Random, actual.Random);
        Assert.Same(frame.Memory.Arena, actual.Memory.Arena);
    }

    [Fact]
    public void Entry_zero_horizontal_distance_reaches_native_division_without_a_fallback_direction()
    {
        var inputs = Inputs([], []) with { ReadAnchor571400 = (current, _) => new(current, PointView(new(100, 800, 300), 4096)) };
        Assert.Throws<DivideByZeroException>(() => Initialize(Inactive(), Resources(), inputs));
    }

    [Theory]
    [InlineData(32, 128)]
    [InlineData(30, 136)]
    public void Interpolation_copies_full_source_point_then_clears_accumulator_and_writes_truncated_WORD_rate(int duration, ushort rate)
    {
        var frame = Frame(65);
        var task = Read(frame, RootBank).WriteDword(0x1C, 0x12345678).WriteDword(0x20, 0xABCD9876);
        frame = Write(frame, RootBank, task);
        var actual = InitializeInterpolation6E8C60(frame, RootBank + 0x14, duration);
        task = Read(actual, RootBank);
        Assert.Equal(0x12345678u, task.ReadDword(0x14));
        Assert.Equal(0xABCD9876u, task.ReadDword(0x18));
        Assert.Equal((ushort)0, task.ReadWord(0x22));
        Assert.Equal(rate, task.ReadWord(0x2A));
        Assert.Equal(frame.Random, actual.Random);
    }

    [Theory]
    [InlineData(0u, PacketBase + 0x8000u, 1u)]
    [InlineData(1u, PacketBase, 0u)]
    [InlineData(0x80000000u, PacketBase, 0u)]
    public void Root_uses_actual_toggle_DWORD_and_private_packets_even_with_pause_flags(uint toggle, uint cursor, uint next)
    {
        var frame = Frame(65);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteDword(16, toggle));
        List<NativeBattleBladeShot12Operation> operations = [];
        var actual = AdvanceMain(frame, RootBank, Resources(), Inputs(operations, []) with { GraphicsFlags = 0x201 });
        Assert.Equal(cursor, actual.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal(next, Read(actual.Frame, RootBank).ReadDword(16));
        Assert.Equal(2, actual.Result);
        Assert.Equal((ushort)65, Read(actual.Frame, RootBank).ReadWord(12));
        Assert.Equal(new NativeBattleBladeShot12ActorMeshMask124(0x1D97494, 0, 0x80), Assert.Single(operations));
        Assert.Equal(frame.Random, actual.Frame.Random);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Old_thirty_two_uses_single_captured_contact_and_BYTE_latch_without_reading_adjacent_unknown_byte(bool fullChildren)
    {
        var frame = Frame(32);
        frame = Write(frame, RootBank, Read(frame, RootBank).MarkPayloadUnknown(0x61, 1));
        if (fullChildren)
        {
            var pool = frame.Effect.Pools[2];
            for (int index = 0; index < ChildBCount; index++)
            {
                (pool, int slot) = pool.Append(0x6E9B50);
                pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteDword(16, SpriteCAddress).WriteWord(0x1A, 0)) });
            }
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(2, pool) } };
        }
        List<NativeBattleBladeShot12Operation> operations = [];
        List<(uint Model, int Bone)> bones = [];
        List<uint> morphFlags = [];
        var packets = Packets();
        packets = packets with { Headers = packets.Headers.SetItem(1, packets.Headers[1] with { UnknownByteMask = 1u << 16 }) };
        var inputs = Inputs(operations, bones) with { Packets = packets, Execute = (current, operation) =>
        {
            if (operation is NativeBattleBladeShot12Contact506690 contact)
            {
                Assert.Equal(1, contact.ContactIndex);
                Assert.Equal((ushort)241, Read(current, RootBank).ReadWord(0x4C));
                Assert.Equal(new[] { (0x1D97494u, 7), (0x1D972C0u, 241) }, bones);
            }
            if (operation is NativeBattleBladeShot12DrawStatic88 draw && draw.SourceAddress is MeshAAddress or MeshBAddress)
            {
                morphFlags.Add(BitConverter.ToUInt32(Context(current, draw.TemporaryAddress, 88).Read(0x1C, 4)));
            }
            operations.Add(operation);
            return current;
        } };
        var actual = AdvanceMain(frame, RootBank, Resources(), inputs);
        Assert.Equal(0, actual.Result);
        Assert.Equal(new byte[] { 1 }, Read(actual.Frame, RootBank).ReadBytes(0x60, 1));
        Assert.Throws<NotSupportedException>(() => Read(actual.Frame, RootBank).ReadBytes(0x61, 1));
        Assert.Equal((ushort)33, Read(actual.Frame, RootBank).ReadWord(12));
        Assert.Single(operations.OfType<NativeBattleBladeShot12Contact506690>());
        Assert.Equal(new uint[] { 0xF0, 0xF0, 0xF0, 0xF0 }, morphFlags);
        Assert.Equal(Draws(frame.Random, fullChildren ? 12 : 22), actual.Frame.Random);
        Assert.Equal(NativeBattleTemporaryMemory.Address, BitConverter.ToUInt32(actual.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Theory]
    [InlineData((ushort)61, 0x80u, 0u)]
    [InlineData((ushort)62, 0u, 0x80u)]
    [InlineData((ushort)32767, 0u, 0x80u)]
    public void Mesh_mask_changes_at_signed_old_sixty_two(ushort age, uint clear, uint set)
    {
        List<NativeBattleBladeShot12Operation> operations = [];
        var actual = AdvanceMain(Frame(age), RootBank, Resources(), Inputs(operations, []));
        Assert.Equal(new NativeBattleBladeShot12ActorMeshMask124(0x1D97494, clear, set), Assert.Single(operations.OfType<NativeBattleBladeShot12ActorMeshMask124>()));
        Assert.Equal(age >= 65 ? 2 : 0, actual.Result);
    }

    [Fact]
    public void Actual_entry_and_complete_timeline_consume_136_CRT_draws_and_preserve_three_inactive_bank_owners()
    {
        var original = Inactive();
        List<NativeBattleBladeShot12Operation> operations = [];
        var inputs = Inputs(operations, []);
        var frame = Initialize(original, Resources(), inputs);
        int calls = 0;
        do
        {
            frame = Advance(frame, Resources(), inputs);
            calls++;
            Assert.True(calls <= 100);
        }
        while (frame.Effect.ReturnedDescriptor != 0);
        Assert.Equal(69, calls);
        Assert.Equal(Draws(original.Random, 136), frame.Random);
        Assert.Single(operations.OfType<NativeBattleBladeShot12Contact506690>());
        Assert.Equal(new uint[] { 0x61AB1, 0x61AAF }, operations.OfType<NativeBattleBladeShot12Sound5013A0>().Select(operation => operation.Packed));
        Assert.Empty(frame.Effect.Pools);
        Assert.NotNull(frame.State.RootBankStorage);
        Assert.NotNull(frame.State.ChildABankStorage);
        Assert.NotNull(frame.State.ChildBBankStorage);
        var restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleBladeShot12Frame>(NativeBattleTestSnapshots.Json(frame))!;
        var warm = Initialize(restored, Resources(), inputs);
        Assert.Equal((ushort)0, Read(warm, RootBank).ReadWord(12));
        Assert.Equal(frame.Random, warm.Random);
    }

    private static NativeCrtRandomState Draws(NativeCrtRandomState state, int count)
    {
        for (int index = 0; index < count; index++)
        {
            state = state.Draw(out _);
        }
        return state;
    }

    private static NativeBattleBladeShot12Frame Frame(ushort age)
    {
        var frame = Inactive();
        var root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootStride, RootCount);
        (root, _) = root.Append(RootCallback);
        frame = frame with { Effect = frame.Effect with { Pools = [root, NativeBattleEffectTaskPool.Initialize(ChildADescriptor, ChildABank, ChildAStride, ChildACount), NativeBattleEffectTaskPool.Initialize(ChildBDescriptor, ChildBBank, ChildBStride, ChildBCount)], ReturnedDescriptor = RootDescriptor }, State = frame.State.WriteDword(CasterGlobal, 0x1D97494).WriteDword(FirstTargetGlobal, 0x1D972C0).WriteDword(ParametersGlobal, 0x1D99A78) with { RootBankStorage = null, ChildABankStorage = null, ChildBBankStorage = null } };
        var task = Read(frame, RootBank).WriteWord(12, age).WriteWord(0x1C, 100).WriteWord(0x1E, 200).WriteWord(0x20, 300).WriteWord(0x24, 900).WriteWord(0x26, 1000).WriteWord(0x28, 1100).WriteWord(0x2A, 128).WriteWord(0x2C, 400).WriteWord(0x2E, 500).WriteWord(0x30, 600).WriteWord(0x5C, 4096);
        return Write(frame, RootBank, task);
    }

    private static NativeBattleBladeShot12Frame Inactive()
    {
        byte[] tim = new byte[32];
        BitConverter.GetBytes(12).CopyTo(tim, 8);
        BitConverter.GetBytes(12).CopyTo(tim, 20);
        var memory = NativeBattleBladeShot12MeshContext88Tests.Memory(2000) with { Arena = NativeBattleEffectArena.Clear().Write(0x30000, tim), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null) };
        NativeBattleOriginalEffectState effect = new(12, 11, new(12, Entry, Initializer), new(3, 0, 0x7F, 1, 0, 0), null) { ResourcePointers = [new(InitializerGlobal, 0x30000)] };
        return new(memory, effect, new(123), NativeBattleBladeShot12State.FromColdProcess().EnsureVertexSources(Resources())) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private static NativeBattleBladeShot12Inputs Inputs(List<NativeBattleBladeShot12Operation> operations, List<(uint Model, int Bone)> bones) => new(Packets(), NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null), _ => new(null, 100, 160, 108), (_, model) => new(checked((int)((model - 0x1D972C0) / 156)), 2) { Transform = new(new(0, 0, 0), new(100, 200, 300), 0, 777, 4096) }, (current, model, selector, distance) =>
    {
        Assert.Equal(0, distance);
        bones.Add((model, selector));
        return new(current with { Memory = current.Memory with { Active = selector } }, PointView(selector == 7 ? new(100, 200, 300) : new(241, 242, 243), 0x7777));
    }, (current, _) => new(current, PointView(new(900, 1000, 1100), 777)), (current, operation) =>
    {
        operations.Add(operation);
        return current;
    });

    private static NativeBattleActionPacketsState Packets()
    {
        var packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(3, 8, 15, -1, 32, 12, null));
        (packets, _) = packets.AppendContact(new(new(6, 4, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        (packets, _) = packets.Prepare(new(3, 8, 15, -1, 32, 12, null));
        (packets, _) = packets.AppendContact(new(new(0, 4, 0, 0, 0, 7, 0), new(4, 4, 0, 0, 0, 3, 0)));
        (packets, _) = packets.Publish(1);
        return packets;
    }

    private static NativeBattleBladeShot12Resources Resources()
    {
        byte[] camera = new byte[CameraLength];
        BitConverter.GetBytes((ushort)CameraLength).CopyTo(camera, 6);
        return new(Mesh(MeshAAddress, 0x5D0, 49, [0, 0, 24, 24, 0, 0, 0, 0]), Mesh(MeshBAddress, 0x5D0, 49, [0, 0, 24, 24, 0, 0, 0, 0]), Mesh(MeshCAddress, 0x828, 144, [0, 0, 16, 24, 0, 0, 0, 0]), Mesh(MeshDAddress, 0x718, 110, [0, 0, 0, 0, 16, 24, 0, 0]), Mesh(MeshEAddress, 0x6D0, 69, [0, 0, 0, 36, 0, 0, 0, 8]), Sprite(SpriteAAddress, 0x1BC, 5), Sprite(SpriteBAddress, 0x90, 5), Sprite(SpriteCAddress, 0x194, 15), Sprite(SpriteDAddress, 0x160, 13), Sprite(SpriteEAddress, 0x118, 8), Source(MorphSourceA0), Source(MorphSourceA1), Source(MorphSourceB0), Source(MorphSourceB1), new("FF8_EN:1365634", camera.ToImmutableArray()), 0x61AB1, 0x61AAF);
    }

    private static NativeBattleStaticMeshResource Mesh(uint address, int length, int count, int[] groups)
    {
        byte[] bytes = new byte[length];
        BitConverter.GetBytes(8 + count * 8).CopyTo(bytes, 0);
        BitConverter.GetBytes(count).CopyTo(bytes, 4);
        int cursor = 8 + count * 8;
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            BitConverter.GetBytes(groups[group]).CopyTo(bytes, cursor);
            cursor += 4 + groups[group] * strides[group];
        }
        return new(0, address, bytes.ToImmutableArray());
    }

    private static NativeBattleModuleResource Sprite(uint address, int length, int count)
    {
        byte[] bytes = new byte[length];
        BitConverter.GetBytes((ushort)count).CopyTo(bytes, 8);
        for (int phase = 0; phase <= count; phase++)
        {
            BitConverter.GetBytes((ushort)0x40).CopyTo(bytes, 10 + phase * 2);
        }
        return new(0, address, 0, 0, bytes.ToImmutableArray());
    }

    private static NativeBattleModuleResource Source(uint address)
    {
        byte[] bytes = new byte[MorphSourceLength];
        BitConverter.GetBytes(MorphSourceLength).CopyTo(bytes, 0);
        BitConverter.GetBytes(MutableVertexCount).CopyTo(bytes, 4);
        return new(0, address, 0, 0, bytes.ToImmutableArray());
    }
}
