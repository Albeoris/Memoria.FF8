using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect54Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect54Lower;
using static Memoria.FF8.Runtime.NativeBattleEffect54Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect54LowerTests
{
    [Fact]
    public void Relocation_updates_live_nullable_tables_and_all_channels_once()
    {
        Fixture f = new(false);
        NativeBattleEffect54Frame result = Relocate80D880(f.Frame, SourceAddress, f.Resources);
        Assert.Equal(1u, BitConverter.ToUInt32(f.Resources.ReadResource(result.Staging!, SourceAddress, 4)));
        Assert.Equal(NativeBattleEffect54Resources.DescriptorAddresses[0], BitConverter.ToUInt32(f.Resources.ReadResource(result.Staging!, SourceAddress + 4, 4)));
        Assert.Equal(0u, BitConverter.ToUInt32(f.Resources.ReadResource(result.Staging!, SourceAddress + 12, 4)));
        Assert.Equal(SourceAddress, BitConverter.ToUInt32(f.Resources.ReadResource(result.Staging!, NativeBattleEffect54Resources.DescriptorAddresses[0] + 0x144, 4)));
        Assert.All(result.Staging!.ReadSnapshot(SourceAddress + 12, 4).Known, value => Assert.False(value));
        f.Frame = result;
        f.Task(task => task.WriteDword(0x10, RootBank).WriteDword(0x30, SourceAddress).WriteByte(0x2C, 3));
        NativeBattleEffect54Frame warm = Initialize80D090(f.Frame, ModelBank, f.Resources, f.Inputs);
        Assert.Equal(f.Resources.ReadBank(result.Staging!), f.Resources.ReadBank(warm.Staging!));
        Assert.Equal(new[] { 241, 241, 240, 241, 241, 240 }, f.PointCalls);
        Assert.Equal(0xA5A5A5A5u, BitConverter.ToUInt32(ReadBytes(warm, ModelBank + 0x34 + 0x1C4 + 12, 4).Read(0, 4)));
        Assert.Equal(30 << 16, BitConverter.ToInt32(ReadBytes(warm, ModelBank + 0x34 + 0x1F4, 4).Read(0, 4)));
        Assert.Equal(40 << 16, BitConverter.ToInt32(ReadBytes(warm, ModelBank + 0x34 + 0x204 + 4, 4).Read(0, 4)));
        Assert.Equal(100u, BitConverter.ToUInt32(ReadBytes(warm, ModelBank + 0x34, 4).Read(0, 4)));
    }

    [Fact]
    public void Context_first_and_completed_states_do_not_simulate_or_consume_random()
    {
        Fixture f = new();
        f.Word(ModelBank + 0x34 + 0x20, 0);
        NativeBattleEffect54Step first = Advance80DAE0(f.Frame, ModelBank, f.Resources, f.Inputs);
        Assert.Equal(1, Word54(ReadBytes(first.Frame, ModelBank + 0x34 + 0x20, 2), 0));
        Assert.Equal(f.Frame.Random, first.Frame.Random);
        f.Frame = first.Frame;
        f.Word(ModelBank + 0x34 + 0x20, 2);
        NativeBattleEffect54Step terminal = Advance80DAE0(f.Frame, ModelBank, f.Resources, f.Inputs);
        Assert.Equal(1, terminal.Result);
        Assert.Empty(f.Draws);
    }

    [Fact]
    public void Full_emitter_scan_wraps_at_twenty_nine_and_preserves_all_bytes_and_CRT()
    {
        Fixture f = new();
        for (int index = 0; index < EmitterCount; index++)
        {
            f.Byte(f.Emitters + (uint)(index * EmitterStride) + 0x69, 1);
        }
        f.Frame = f.Frame with { State = f.Frame.State.WriteWord(EmitterHintGlobal, 28) };
        NativeBattleEffectArena original = f.Frame.Memory.Arena!;
        (NativeBattleEffect54Frame frame, uint address) = AllocateEmitter810560(f.Frame, 0, 0, 0, f.Resources, f.Inputs);
        Assert.Equal(0u, address);
        Assert.Equal(1, frame.State.ReadWord(EmitterHintGlobal));
        Assert.Same(original, frame.Memory.Arena);
        Assert.Equal(f.Frame.Random, frame.Random);
    }

    [Fact]
    public void Allocator_clears_only_selected_row_and_links_actual_context()
    {
        Fixture f = new();
        f.Dword(ModelBank + 0x34 + 0x2C, 0);
        f.Dword(ModelBank + 0x34 + 0x30, 0);
        f.Frame = f.Frame with { State = f.Frame.State.WriteWord(EmitterHintGlobal, 28) };
        (NativeBattleEffect54Frame frame, uint address) = AllocateEmitter810560(f.Frame, 0x12345678, 1, 2, f.Resources, f.Inputs);
        Assert.Equal(f.Emitters + 28u * EmitterStride, address);
        Assert.Equal(0, frame.State.ReadWord(EmitterHintGlobal));
        Assert.Equal(0x12345678u, Dword54(ReadBytes(frame, address + 0x5C, 4), 0));
        Assert.Equal(new byte[] { 1, 1, 2 }, ReadBytes(frame, address + 0x69, 3).Read(0, 3));
        Assert.Equal(address, Dword54(ReadBytes(frame, ModelBank + 0x34 + 0x2C, 4), 0));
        Assert.Equal(1, Word54(ReadBytes(frame, ModelBank + 0x34 + 0x14, 2), 0));
    }

    [Theory]
    [InlineData(13, 14, 0)]
    [InlineData(14, 0, 1)]
    [InlineData(127, -128, 0)]
    public void Sprite_cycle_uses_signed_BYTE_increment_and_completion(int phase, int next, int done)
    {
        Fixture f = new();
        f.Byte(f.Node + 0x1D0, unchecked((byte)phase));
        f.Byte(f.Node + 0x1D1, 14);
        NativeBattleEffect54Frame frame = Cycle80FB40(f.Frame, f.Node, f.Descriptor);
        Assert.Equal(next, unchecked((sbyte)ReadBytes(frame, f.Node + 0x1D0, 1).Read(0, 1)[0]));
        Assert.Equal(done, ReadBytes(frame, f.Node + 0x1D2, 1).Read(0, 1)[0]);
    }

    [Fact]
    public void Terminal_drawable_clears_resource_before_draw_and_unlinks_on_following_visit()
    {
        Fixture f = new();
        f.Byte(f.Node + 0x1D0, 14);
        f.Byte(f.Node + 0x1D1, 14);
        NativeBattleEffect54Frame end = AdvanceDrawable80F370(f.Frame, f.Node, f.Resources, f.Inputs);
        Assert.Equal(0u, Dword54(ReadBytes(end, f.Node + 0x170, 4), 0));
        Assert.Equal(1, Word54(ReadBytes(end, f.Node + 0x1CC, 2), 0));
        Assert.Equal(0, Word54(ReadBytes(end, f.Node + 0x1CA, 2), 0));
        Assert.Equal(1, ReadBytes(end, f.Node + 0x1D7, 1).Read(0, 1)[0]);
        end = DrawSprites80F1F0(end, ModelBank, f.Resources, f.Inputs);
        Assert.Empty(f.Draws);
        NativeBattleEffect54Frame retired = AdvanceDrawable80F370(end, f.Node, f.Resources, f.Inputs);
        Assert.Equal(0u, Dword54(ReadBytes(retired, ModelBank + 0x34 + 0x2C, 4), 0));
        Assert.Equal(0, Word54(ReadBytes(retired, ModelBank + 0x34 + 0x16, 2), 0));
        Assert.Equal(0, ReadBytes(retired, f.Node + 0x1D7, 1).Read(0, 1)[0]);
    }

    [Fact]
    public void Motion_uses_new_velocity_signed_damping_and_transports_unknown_fourth_lane()
    {
        Fixture f = new();
        f.Dword(f.Node + 0x220, 10);
        f.Dword(f.Node + 0x260, 3);
        f.Dword(f.Node + 0x1E0, 20);
        f.Dword(f.Node + 0x12C, 100);
        f.Frame = WriteBytes(f.Frame, f.Node + 0x138, NativeBattleEffectArena.Clear().Write(0, [1, 2, 3, 4]).MarkUnknown(1, 1), 4);
        NativeBattleEffect54Frame frame = Motion80F400(f.Frame, f.Node, f.Descriptor, f.Resources);
        Assert.Equal(13u, Dword54(ReadBytes(frame, f.Node + 0x220, 4), 0));
        Assert.Equal(33u, Dword54(ReadBytes(frame, f.Node + 0x1E0, 4), 0));
        Assert.Equal(133u, Dword54(ReadBytes(frame, f.Node + 0xDC, 4), 0));
        Assert.Equal(new[] { true, false, true, true }, ReadBytes(frame, f.Node + 0xE8, 4).ReadSnapshot(0, 4).Known);
    }

    [Theory]
    [InlineData(-1, -513, -513)]
    [InlineData(256, -513, -511)]
    [InlineData(65536, -513, -1)]
    [InlineData(100000, 513, 1)]
    public void Damping_preserves_two_signed_divisions_and_ratio_clamp(int ratio, int value, int expected)
    {
        Assert.Equal(expected, Damp80FA60(ratio, value));
    }

    [Fact]
    public void Preparation_keeps_matrix_padding_and_publishes_current_full_MAC_and_sign_Z_W()
    {
        Fixture f = new();
        f.Dword(f.Node + 0xDC, unchecked((uint)-65537));
        f.Dword(f.Node + 0xE0, 2 * 65536);
        f.Dword(f.Node + 0xE4, unchecked((uint)-65537));
        f.Word(f.Node + 0x9E, unchecked((short)0xCAFE));
        f.Word(f.Node + 0xBE, unchecked((short)0xBEEF));
        NativeBattleEffect54Frame frame = Prepare80FC10(f.Frame, f.Node, f.Descriptor);
        Assert.Equal(-1, unchecked((int)Dword54(ReadBytes(frame, f.Node + 0xA0, 4), 0)));
        Assert.Equal(-1, frame.Memory.ScriptedColorRegisters!.Mac[0]);
        Assert.Equal(2, frame.Memory.ScriptedColorRegisters.Mac[1]);
        Assert.Equal(-1, frame.Memory.ScriptedColorRegisters.Mac[2]);
        Assert.Equal(unchecked((short)0xCAFE), Word54(ReadBytes(frame, f.Node + 0x9E, 2), 0));
        Assert.Equal(unchecked((short)0xBEEF), Word54(ReadBytes(frame, f.Node + 0xBE, 2), 0));
        Assert.Equal(Enumerable.Repeat((byte)255, 2), frame.Memory.ScriptedColorRegisters.NormalInput!.Value.AsSpan().Slice(6, 2).ToArray());
    }

    [Fact]
    public void Sprite_draw_uses_canonical_180_scratch_current_SZ_and_private_delivery()
    {
        Fixture f = new();
        NativeBattleEffect54Frame frame = DrawSprite80F2B0(f.Frame, ModelBank, f.Node, f.Resources, f.Inputs);
        NativeBattleEffect54DrawBatch draw = Assert.Single(f.Draws);
        Assert.Equal(PrimaryCursorGlobal, draw.CursorGlobal);
        NativeBattleGeometryPrimitive primitive = Assert.Single(draw.Batch.Attempts).Primitive!;
        Assert.Equal(40, draw.Batch.AllocationBytes);
        Assert.Equal(new[] { 2000, 2000, 2000, 2000 }, primitive.VertexPayload4);
        Assert.Equal(f.Frame.State.ReadDword(ScratchPointerGlobal), frame.State.ReadDword(ScratchPointerGlobal));
        NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Capture(frame.Memory);
        Assert.Equal(FirstSpriteAddress, Dword54(temporary, 0));
        Assert.Equal(0, Word54(temporary, 0x24));
        Assert.Equal(0xA5, temporary.ReadSnapshot(0x96, 1).Data[0]);
        Assert.False(temporary.ReadSnapshot(0x96, 1).Known[0]);
        Assert.Equal(f.Frame.Random, frame.Random);
    }

    private sealed class Fixture
    {
        public uint Emitters = 0x20DFAB8;
        public uint Node = 0x20DFAB8 + 30 * 108;
        public NativeBattleEffect54Frame Frame;
        public NativeBattleEffect54Resources Resources;
        public byte[] Descriptor;
        public List<int> PointCalls = [];
        public List<NativeBattleEffect54DrawBatch> Draws = [];
        public NativeBattleEffect54Inputs Inputs;

        public Fixture(bool relocate = true)
        {
            NativeBattleEffectTaskPool models = NativeBattleEffectTaskPool.Initialize(ModelDescriptor, ModelBank, 676, 3);
            (models, int model) = models.Append(ModelCallback);
            models = models with { Slots = models.Slots.SetItem(model, models.Slots[model].WriteBytes(12, Enumerable.Repeat((byte)0xA5, 664).ToArray()).WriteDword(0x10, RootBank).WriteDword(0x30, SourceAddress).WriteWord(0x29A, 0).WriteWord(0x29C, 0).WriteWord(0x29E, 0).WriteByte(0x2A, 0).WriteByte(0x2C, 3)) };
            NativeBattleEffectTaskPool roots = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 100, 2);
            (roots, int root) = roots.Append(RootCallback);
            roots = roots with { Slots = roots.Slots.SetItem(root, roots.Slots[root].WriteWord(0x5A, 1)) };
            NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), SharedEffectView2793E58 = NativeBattlePackedMatrix32.FromMatrix(identity, 0x1234) };
            memory = NativeBattleEffectNodeContextMemory.Publish(memory, NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 180).ToArray()));
            byte[] bank = new byte[NativeBattleEffect54Resources.SourceLength];
            for (int index = 0; index < 2; index++)
            {
                Put32(bank, 4 + index * 4, NativeBattleEffect54Resources.DescriptorAddresses[index] - SourceAddress);
                Put32(bank, 0x44 + index * 4, NativeBattleEffect54Resources.SpriteAddresses[index] - SourceAddress);
                for (int axis = 0; axis < 3; axis++)
                {
                    Put32(bank, checked((int)(NativeBattleEffect54Resources.DescriptorAddresses[index] - SourceAddress)) + 0xD8 + axis * 4, 0x700);
                }
            }
            byte[] sprite = new byte[524];
            sprite[0] = 36;
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(8), 15);
            for (int phase = 0; phase < 15; phase++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(10 + phase * 2), 44);
            }
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(40), ushort.MaxValue);
            Put32(sprite, 44, 1);
            sprite[48] = 4;
            sprite[49] = 128;
            sprite[50] = 4;
            sprite[51] = 0x2E;
            sprite[52] = 10;
            sprite[53] = 20;
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(62), 0x400);
            sprite.CopyTo(bank, checked((int)(FirstSpriteAddress - SourceAddress)));
            Resources = new(null!, null!, new("test-source", bank.ToImmutableArray()), ImmutableDictionary<uint, NativeBattleModuleResource>.Empty.Add(FirstSpriteAddress, new(54, FirstSpriteAddress, checked((uint)sprite.Length), 0, sprite.ToImmutableArray())), ImmutableDictionary<uint, ImmutableArray<byte>>.Empty);
            NativeBattleEffect54State state = NativeBattleEffect54State.FromColdProcess().WriteDword(CurrentContextGlobal, ModelBank + 0x34).WriteDword(EmitterRecordsGlobal, Emitters).WriteDword(DrawRecordsGlobal, Node).WriteDword(ScratchPointerGlobal, 0x2793E58);
            NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
            (state, staging) = state.BindSources(staging, Resources);
            state = state.DetachBank(ModelBank).DetachBank(RootBank);
            Frame = new(memory, new(54, 54, new(54, Initializer, Entry), new(3, 0, 1, 0, 0, 0), null) { Pools = [models, roots] }, new(1), state) { Staging = staging };
            if (relocate)
            {
                Frame = Relocate80D880(Frame, SourceAddress, Resources);
            }
            Word(ModelBank + 0x34 + 0x14, 0);
            Word(ModelBank + 0x34 + 0x16, 1);
            Dword(ModelBank + 0x34 + 0x224, SourceAddress + 4);
            Dword(ModelBank + 0x34 + 0x2C, Node);
            Dword(ModelBank + 0x34 + 0x30, Node);
            Word(Node + 8, 1);
            Byte(Node + 0x1D7, 1);
            Byte(Node + 0x1D8, 1);
            Dword(Node + 0x170, FirstSpriteAddress);
            Matrix(Node + 0xC, identity);
            Matrix(Node + 0xAC, identity with { Z = 2000 });
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[640], new byte[1176]);
            (packets, _) = packets.Prepare(new(3, 8, 11, 0, 223, 54, null));
            (packets, _) = packets.AppendContact(new(new(0, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
            (packets, _) = packets.Publish(1);
            Inputs = new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => packets, (_, physical) =>
            {
                int actor = checked((int)((physical - 0x1D972C0) / 156));
                return NativeBattleEffectArena.Clear().Write(0x1C, BitConverter.GetBytes((short)(actor == 3 ? 100 : 0))).Write(0x24, BitConverter.GetBytes((short)7)).Write(0x3C, BitConverter.GetBytes((short)40));
            }, frame => frame.Memory.SharedEffectView2793E58!, _ => new(null, 100, 160, 108), _ => 0, (frame, _, selector, _, retained) =>
            {
                PointCalls.Add(selector);
                return new(frame, PutPoint(retained, 0, new(selector == 240 ? (short)30 : (short)10, 20, 2000)));
            }, (frame, operation) =>
            {
                if (operation is NativeBattleEffect54DrawBatch draw)
                {
                    Draws.Add(draw);
                }
                return frame;
            });
            Descriptor = Resources.ReadResource(Frame.Staging!, NativeBattleEffect54Resources.DescriptorAddresses[0], NativeBattleEffect54Resources.DescriptorLength);
        }

        public void Task(Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> change) => Frame = Write(Frame, ModelBank, change(Read(Frame, ModelBank)));
        public void Byte(uint address, byte value) => Frame = WriteBytes(Frame, address, NativeBattleEffectArena.Clear().Write(0, [value]), 1);
        public void Word(uint address, short value) => Frame = WriteBytes(Frame, address, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(value)), 2);
        public void Dword(uint address, uint value) => Frame = WriteBytes(Frame, address, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(value)), 4);
        public void Matrix(uint address, NativeBattleActionMatrix matrix) => Frame = WriteBytes(Frame, address, PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, null)), 32);
        private static void Put32(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
    }
}
