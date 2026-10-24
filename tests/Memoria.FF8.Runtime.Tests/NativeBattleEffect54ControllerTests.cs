using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect54Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect54Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect54ControllerTests
{
    private const uint Arena = 0x20DFAB8;

    [Fact]
    public void Initialize_binds_cells_once_and_warm_entry_preserves_relocations()
    {
        Fixture f = new();
        NativeBattleEffect54Frame first = Initialize(f.Inactive, f.Resources, f.Inputs);
        Assert.False(first.State.ResourceSourcePending);
        Assert.Equal(new uint[] { CameraAddress, Arena }, f.Operations.Select(operation => operation switch { NativeBattleEffect54RegisterCamera5099A0 camera => camera.Source, NativeBattleEffect54UploadTim505E30 tim => tim.Source, _ => 0u }).ToArray());
        first = NativeBattleEffect54Lower.Relocate80D880(first, SourceAddress, f.Resources);
        ImmutableArray<byte> relocated = f.Resources.ReadBank(first.Staging!);
        first = Write(first, RootBank, Read(first, RootBank).WriteByte(0x29, 9));
        NativeBattleEffect54Frame retired = Advance(first, f.Resources, f.Inputs);
        Assert.Empty(retired.Effect.Pools);
        NativeBattleEffect54Frame warm = Initialize(retired, f.Resources, f.Inputs);
        Assert.Equal(relocated, f.Resources.ReadBank(warm.Staging!));
        Assert.Equal(1u, BitConverter.ToUInt32(warm.Staging!.Read(SourceAddress, 4)));
        Assert.All(warm.State.FixedBanks.Values, bank => Assert.Null(bank));
    }

    [Fact]
    public void Root_alternates_actual_packet_bases_before_phase_and_preserves_view_padding()
    {
        Fixture f = new();
        NativeBattleEffect54Frame frame = Initialize(f.Inactive, f.Resources, f.Inputs);
        frame = Advance(frame, f.Resources, f.Inputs);
        Assert.Equal(Arena + 0x8000, frame.State.ReadDword(CursorGlobal));
        Assert.Equal(Arena + 0x10000, frame.State.ReadDword(SecondaryCursorGlobal));
        Assert.Equal(f.View.Bytes, frame.Memory.SharedEffectView2793E58!.Bytes);
        Assert.Equal(f.View.Known, frame.Memory.SharedEffectView2793E58.Known);
        frame = Advance(frame, f.Resources, f.Inputs);
        Assert.Equal(Arena, frame.State.ReadDword(CursorGlobal));
        Assert.Equal(Arena + 0x8000, frame.State.ReadDword(SecondaryCursorGlobal));
        Assert.Equal(2, Read(frame, RootBank).ReadWord(0x5C));
    }

    [Fact]
    public void Root_phase_two_clears_exact_auxiliary_regions_and_runs_new_fade_same_pass()
    {
        Fixture f = new();
        NativeBattleEffect54Frame frame = Initialize(f.Inactive, f.Resources, f.Inputs);
        uint first = Arena + 0x10000;
        frame = WriteBytes(frame, first - 1, NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 0xAA2A).ToArray()), 0xAA2A);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteByte(0x29, 2));
        f.Operations.Clear();
        frame = Advance(frame, f.Resources, f.Inputs);
        Assert.Equal(first, frame.State.ReadDword(EmitterRecordsGlobal));
        Assert.Equal(first + 0xCA8, frame.State.ReadDword(DrawRecordsGlobal));
        Assert.Equal(first + 0xAA28, frame.State.ReadDword(AllocationCursorGlobal));
        Assert.Equal(new byte[0xAA28], ReadBytes(frame, first, 0xAA28).Read(0, 0xAA28));
        Assert.Equal(0xA5, ReadBytes(frame, first - 1, 1).Read(0, 1)[0]);
        Assert.Equal(0xA5, ReadBytes(frame, first + 0xAA28, 1).Read(0, 1)[0]);
        Assert.Equal(4, f.Operations.OfType<NativeBattleEffect54StageTransition>().Count());
        Assert.All(f.Operations.OfType<NativeBattleEffect54StageTransition>(), transition => Assert.True(transition.ClearRgb));
        Assert.Equal(1, Read(frame, FadeBank).ReadWord(0x24));
        Assert.Equal(1, Read(frame, RootBank).ReadByte(0x28));
    }

    [Theory]
    [InlineData(false, 256, 512, 0)]
    [InlineData(false, 768, 1024, 2)]
    [InlineData(true, 768, 512, 0)]
    [InlineData(true, 256, 0, 2)]
    public void Fade_uses_WORD_scale_and_marks_terminal_without_reclearing_RGB(bool outgoing, int oldScale, int expected, int result)
    {
        Fixture f = new();
        NativeBattleEffect54Frame frame = Initialize(f.Inactive, f.Resources, f.Inputs);
        (frame, uint fade) = AppendHierarchy(frame, 3, outgoing ? FadeOutCallback : FadeInCallback, 64, RootBank, f.Inputs);
        frame = Write(frame, fade, Read(frame, fade).WriteByte(0x29, 1).WriteWord(0x1C, (ushort)oldScale));
        f.Operations.Clear();
        NativeBattleEffect54Step step = AdvanceTask(frame, fade, f.Resources, f.Inputs);
        Assert.Equal(result, step.Result);
        Assert.Equal(expected, Read(step.Frame, fade).ReadWord(0x1C));
        Assert.Equal(4, f.Operations.Count);
        Assert.All(f.Operations.OfType<NativeBattleEffect54StageTransition>(), value => Assert.Equal(new NativeBattleEffect54StageTransition(value.Group, (ushort)expected, false), value));
    }

    [Fact]
    public void Driver_releases_root_at_thirty_then_delivers_current_contact_on_next_visit()
    {
        Fixture f = new();
        NativeBattleEffect54Frame frame = Initialize(f.Inactive, f.Resources, f.Inputs);
        (frame, uint driver) = AppendHierarchy(frame, 1, DriverCallback, 88, RootBank, f.Inputs);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteByte(0x63, 1));
        frame = Write(frame, driver, Read(frame, driver).WriteByte(0x29, 1).WriteWord(0x24, 30));
        f.Operations.Clear();
        NativeBattleEffect54Step first = AdvanceTask(frame, driver, f.Resources, f.Inputs);
        Assert.Equal(0, first.Result);
        Assert.Equal(0, Read(first.Frame, RootBank).ReadByte(0x63));
        Assert.DoesNotContain(f.Operations, operation => operation is NativeBattleEffect54Contact506690);
        NativeBattleEffect54Step contact = AdvanceTask(first.Frame, driver, f.Resources, f.Inputs);
        Assert.Equal(2, contact.Result);
        Assert.Equal(new NativeBattleEffect54Contact506690(0), Assert.Single(f.Operations.OfType<NativeBattleEffect54Contact506690>()));
        Assert.Equal(0, Read(contact.Frame, RootBank).ReadByte(0x28));
    }

    [Fact]
    public void Draw_allocator_uses_hint_fifty_eight_and_wraps_at_fifty_nine()
    {
        Fixture f = new();
        NativeBattleEffect54Frame frame = f.Active();
        frame = frame with { State = frame.State.WriteWord(DrawHintGlobal, 58) };
        uint emitter = frame.State.ReadDword(EmitterRecordsGlobal);
        frame = WriteBytes(frame, emitter + 0x6B, NativeBattleEffectArena.Clear().Write(0, [2]), 1);
        (frame, uint address) = AllocateDraw810EE0(frame, emitter, 1);
        Assert.Equal(frame.State.ReadDword(DrawRecordsGlobal) + 58u * DrawStride, address);
        Assert.Equal(0, frame.State.ReadWord(DrawHintGlobal));
        Assert.Equal(1, Word54(ReadBytes(frame, ModelBank + 0x34 + 0x16, 2), 0));
        Assert.Equal(emitter, Dword54(ReadBytes(frame, address + 0x1BC, 4), 0));
        Assert.Equal(2, ReadBytes(frame, address + 0x1D9, 1).Read(0, 1)[0]);
    }

    [Fact]
    public void Full_draw_allocator_preserves_COMMON_and_CRT_and_returns_native_null()
    {
        Fixture f = new();
        NativeBattleEffect54Frame frame = f.Active();
        uint start = frame.State.ReadDword(DrawRecordsGlobal);
        for (int index = 0; index < DrawCount; index++)
        {
            frame = WriteBytes(frame, start + (uint)(index * DrawStride) + 0x1D7, NativeBattleEffectArena.Clear().Write(0, [1]), 1);
        }
        NativeBattleEffectArena original = frame.Memory.Arena!;
        (NativeBattleEffect54Frame result, uint address) = AllocateDraw810EE0(frame, frame.State.ReadDword(EmitterRecordsGlobal), 0);
        Assert.Equal(0u, address);
        Assert.Same(original, result.Memory.Arena);
        Assert.Equal(frame.Random, result.Random);
        Assert.Equal(2, result.State.ReadWord(DrawHintGlobal));
    }

    [Fact]
    public void Unlink_keeps_retained_node_links_and_updates_actual_head_and_tail()
    {
        Fixture f = new();
        NativeBattleEffect54Frame frame = f.Active();
        uint first = frame.State.ReadDword(EmitterRecordsGlobal);
        uint second = first + EmitterStride;
        frame = Link811790(frame, first, 0);
        frame = Link811790(frame, second, 1);
        frame = Unlink8117C0(frame, first);
        Assert.Equal(second, Dword54(ReadBytes(frame, ModelBank + 0x34 + 0x2C, 4), 0));
        Assert.Equal(second, Dword54(ReadBytes(frame, ModelBank + 0x34 + 0x30, 4), 0));
        Assert.Equal(second, Dword54(ReadBytes(frame, first + 4, 4), 0));
        Assert.Equal(0u, Dword54(ReadBytes(frame, second, 4), 0));
    }

    [Fact]
    public void Type_zero_initializes_from_current_target_F1_band_and_retains_vector_W_unknownness()
    {
        Fixture f = new();
        NativeBattleEffect54Frame frame = f.Active();
        uint context = ModelBank + 0x34;
        frame = WriteBytes(frame, context + 0xD4, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(123 << 16)).Write(4, BitConverter.GetBytes(-45 << 16)).Write(8, BitConverter.GetBytes(678 << 16)).Write(12, [7, 8, 9, 10]), 16);
        (frame, uint emitter) = NativeBattleEffect54Lower.AllocateEmitter810560(frame, 0, 0, 0, f.Resources, f.Inputs);
        NativeCrtRandomState random = frame.Random;
        frame = Emit810600(frame, emitter, f.Resources, f.Inputs);
        Assert.Equal(new byte[] { 7, 8, 9, 10 }, ReadBytes(frame, emitter + 0x58, 4).Read(0, 4));
        Assert.Equal(123 << 16, BitConverter.ToInt32(ReadBytes(frame, emitter + 0x4C, 4).Read(0, 4)));
        Assert.Equal(-45 << 16, BitConverter.ToInt32(ReadBytes(frame, emitter + 0x50, 4).Read(0, 4)));
        Assert.Equal(random, frame.Random);
        Assert.Equal(new[] { true, true, true, true, true, true, false, false }, frame.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value.ToArray());
        Assert.Equal(26, Word54(ReadBytes(frame, emitter + 0x64, 2), 0));
    }

    [Fact]
    public void Matrix_rotation_preserves_padding_and_translation_and_publishes_GTE_without_T_load()
    {
        Fixture f = new();
        NativeBattleEffect54Frame frame = f.Active();
        uint address = Arena + 0x4000;
        NativeBattleEffectArena matrix = PackedArena(NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 11, Y = 22, Z = 33 }, null)).Write(18, [0x7B]).MarkUnknown(19, 1);
        frame = WriteBytes(frame, address, matrix, 32);
        NativeBattleGeometryRegisters geometry = frame.Memory.GeometryRegisters! with { Translation = [101, 202, 303] };
        frame = frame with { Memory = frame.Memory with { GeometryRegisters = geometry } };
        frame = Rotate8DD(frame, address, 2, 1024);
        Assert.Equal(new int?[] { 101, 202, 303 }, frame.Memory.GeometryRegisters!.Translation.ToArray());
        Assert.Equal(new[] { true, false }, ReadBytes(frame, address + 18, 2).ReadSnapshot(0, 2).Known);
        Assert.Null(frame.Memory.ScriptedColorRegisters!.NormalInputKnown);
        Assert.Equal(new byte[] { 11, 0, 0, 0, 22, 0, 0, 0, 33, 0, 0, 0 }, ReadBytes(frame, address + 20, 12).Read(0, 12));
        Assert.Equal(-4096, Word54(ReadBytes(frame, address + 2, 2), 0));
    }

    [Fact]
    public void Selected_birth_consumes_seven_CRT_draws_links_child_after_row_and_restores_shared_scratch_pointer()
    {
        Fixture f = new(true);
        NativeBattleEffect54Frame frame = f.Active();
        uint context = ModelBank + 0x34;
        uint scratch = 0x2793E08;
        frame = WriteBytes(frame, scratch, NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 80).ToArray()).MarkUnknown(0x26, 1), 80);
        (frame, uint emitter) = NativeBattleEffect54Lower.AllocateEmitter810560(frame, 0, 0, 0, f.Resources, f.Inputs);
        NativeCrtRandomState expected = frame.Random;
        for (int index = 0; index < 7; index++)
        {
            expected = expected.Draw(out _);
        }
        frame = Emit810600(frame, emitter, f.Resources, f.Inputs);
        uint row = frame.State.ReadDword(DrawRecordsGlobal);
        uint child = Dword54(ReadBytes(frame, row + 4, 4), 0);
        Assert.Equal(expected, frame.Random);
        Assert.Equal(0x2793E58u, frame.State.ReadDword(ScratchPointerGlobal));
        Assert.Equal(row, Dword54(ReadBytes(frame, emitter + 4, 4), 0));
        Assert.Equal(1, Word54(ReadBytes(frame, row + 8, 2), 0));
        Assert.Equal(0, Word54(ReadBytes(frame, child + 8, 2), 0));
        Assert.Equal(row, Dword54(ReadBytes(frame, child + 0x5C, 4), 0));
        Assert.Equal(2, Word54(ReadBytes(frame, context + 0x14, 2), 0));
        Assert.Equal(1, Word54(ReadBytes(frame, context + 0x16, 2), 0));
        Assert.Equal(new[] { false, true }, ReadBytes(frame, scratch + 0x26, 2).ReadSnapshot(0, 2).Known);
        Assert.Equal(0xA5, ReadBytes(frame, scratch + 0x27, 1).Read(0, 1)[0]);
        Assert.Equal(new[] { false, true }, frame.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value.Skip(6).ToArray());
    }

    private sealed class Fixture
    {
        public NativeBattleEffect54Resources Resources { get; }
        public NativeBattleEffect54Frame Inactive { get; }
        public NativeBattleEffect54Inputs Inputs { get; }
        public NativeBattlePackedMatrix32 View { get; } = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
        public List<NativeBattleEffect54Operation> Operations { get; } = [];

        public Fixture(bool spawn = false)
        {
            Resources = SyntheticResources(spawn);
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), SharedEffectView2793E58 = View };
            NativeBattleOriginalEffectState effect = new(54, 54, new(54, Entry, Initializer), new(3, 0, 1, 0, 0, 0), null) { ResourcePointers = [new(InitializerGlobal, 0)] };
            Inactive = new(memory, effect, new(1), NativeBattleEffect54State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[640], new byte[1176]);
            (packets, _) = packets.Prepare(new(3, 8, 11, 0, 223, 54, null));
            (packets, _) = packets.AppendContact(new(new(0, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
            (packets, _) = packets.Publish(1);
            Inputs = new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => packets, (_, _) => NativeBattleEffectArena.Clear(), _ => View, _ => new(null, 100, 160, 108), _ => 0, (frame, _, _, _, retained) => new(frame, PutPoint(retained, 0, new(0, 0, 2000))), (frame, operation) =>
            {
                Operations.Add(operation);
                return frame;
            });
        }

        public NativeBattleEffect54Frame Active()
        {
            NativeBattleEffect54Frame frame = Initialize(Inactive, Resources, Inputs);
            (frame, uint model) = AppendHierarchy(frame, 2, ModelCallback, 676, RootBank, Inputs);
            frame = frame with { State = frame.State.WriteDword(CurrentContextGlobal, model + 0x34).WriteDword(EmitterRecordsGlobal, Arena + 0x10000).WriteDword(DrawRecordsGlobal, Arena + 0x10CA8).WriteDword(ScratchPointerGlobal, 0x2793E58) };
            frame = NativeBattleEffect54Lower.Relocate80D880(frame, SourceAddress, Resources);
            frame = WriteBytes(frame, model + 0x34 + 0x224, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(SourceAddress + 4)), 4);
            frame = WriteBytes(frame, model + 0x34 + 0x228, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(SourceAddress + 0x44)), 4);
            return frame;
        }
    }

    private static NativeBattleEffect54Resources SyntheticResources(bool spawn)
    {
        byte[] bank = new byte[NativeBattleEffect54Resources.SourceLength];
        ImmutableDictionary<uint, NativeBattleModuleResource>.Builder sprites = ImmutableDictionary.CreateBuilder<uint, NativeBattleModuleResource>();
        for (int index = 0; index < 2; index++)
        {
            uint descriptor = NativeBattleEffect54Resources.DescriptorAddresses[index];
            int at = (int)(descriptor - SourceAddress);
            Put32(bank, 4 + index * 4, descriptor - SourceAddress);
            Put32(bank, 0x44 + index * 4, NativeBattleEffect54Resources.SpriteAddresses[index] - SourceAddress);
            bank[at + 0x11] = index == 0 ? (byte)0 : (byte)3;
            bank[at + 0x12] = index == 0 ? (byte)27 : (byte)14;
            bank[at + 0x22] = (byte)index;
            bank[at + 0x25] = 1;
            bank[at + 0x36] = index == 0 ? (byte)1 : (byte)4;
            Put32(bank, at + 0xC8, (uint)(0x700 + index * 0x20));
            if (spawn && index == 0)
            {
                bank[at + 0x34] = 1;
                bank[at + 0x28] = 1;
                bank[at + 0x2C] = 1;
                bank[0x700] = 1;
                BinaryPrimitives.WriteInt16LittleEndian(bank.AsSpan(at + 0x154), 4084);
                for (int lane = 0; lane < 3; lane++)
                {
                    Put32(bank, at + 0xCC + lane * 4, (uint)(0x740 + lane * 0x40));
                    for (int age = 0; age < 27; age++)
                    {
                        BinaryPrimitives.WriteInt16LittleEndian(bank.AsSpan(0x740 + lane * 0x40 + age * 2), (short)(lane + 1));
                    }
                }
            }
            int length = NativeBattleEffect54Resources.SpriteLengths[index];
            int frames = NativeBattleEffect54Resources.SpriteFrameCounts[index];
            byte[] sprite = new byte[length];
            sprite[0] = 36;
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(8), (ushort)frames);
            for (int frame = 0; frame < frames; frame++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(10 + frame * 2), 44);
            }
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(10 + frames * 2), ushort.MaxValue);
            Put32(sprite, 44, 1);
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(62), 0x400);
            sprite.CopyTo(bank, (int)(NativeBattleEffect54Resources.SpriteAddresses[index] - SourceAddress));
            sprites.Add(NativeBattleEffect54Resources.SpriteAddresses[index], new(54, NativeBattleEffect54Resources.SpriteAddresses[index], checked((uint)length), 0, sprite.ToImmutableArray()));
        }
        byte[] tim = new byte[165408];
        Put32(tim, 0, 16);
        Put32(tim, 4, 9);
        Put32(tim, 8, 12);
        Put32(tim, 20, 165388);
        BinaryPrimitives.WriteUInt16LittleEndian(tim.AsSpan(28), 256);
        BinaryPrimitives.WriteUInt16LittleEndian(tim.AsSpan(30), 323);
        byte[] camera = new byte[176];
        BinaryPrimitives.WriteUInt16LittleEndian(camera, 2);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(2), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(4), 40);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(6), 176);
        return new(new("mag053.tim", tim.ToImmutableArray()), new("test-camera", camera.ToImmutableArray()), new("test-bank", bank.ToImmutableArray()), sprites.ToImmutable(), ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(SoundAddress, new byte[4].ToImmutableArray()));
    }

    private static void Put32(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
}
