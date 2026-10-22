using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleMeltEye158Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMeltEye158DrawingTests
{
    [Theory]
    [InlineData(false, -32768, 2)]
    [InlineData(false, -1, 2)]
    [InlineData(false, 0, 0)]
    [InlineData(false, 1, 0)]
    [InlineData(false, 32767, 0)]
    [InlineData(true, -32768, 2)]
    [InlineData(true, -1, 2)]
    [InlineData(true, 0, 0)]
    [InlineData(true, 1, 0)]
    [InlineData(true, 32767, 0)]
    public void Impact_sprite_draws_before_pause_and_reads_feedback_from_released_physical_bytes(bool paused, short feedback, int completeNativeReturn)
    {
        Fixture f = new(paused ? 1u : 0u);
        NativeBattleMeltEye158Frame frame = f.Set(f.Frame, 0x1C, 80);
        frame = NativeBattleMeltEye158Controller.Write(frame, ChildBank, f.Task(frame).MarkPayloadUnknown(0x16, 2));
        f.Feedback = feedback;
        var step = NativeBattleMeltEye158Controller.AdvanceImpactSprite(frame, ChildBank, f.Resources, f.Inputs);
        NativeBattleMeltEye158DrawSprite180 draw = Assert.Single(f.Draws.OfType<NativeBattleMeltEye158DrawSprite180>());
        Assert.Null(draw.W);
        Assert.Equal((short)80, draw.Scale);
        Assert.Equal((short)-10, draw.Depth);
        uint eax = feedback < 0 ? 1u : 0u;
        eax = unchecked(eax - 1);
        eax = (eax & 0xFFFFFF00u) | ((eax & 0xFFu) & 0xFEu);
        eax = unchecked(eax + 2);
        Assert.Equal(completeNativeReturn, unchecked((int)eax));
        Assert.Equal(paused ? 0 : completeNativeReturn, step.Result);
        Assert.Equal((ushort)(paused ? 0 : 1), f.Task(step.Frame).ReadWord(12));
        Assert.Equal(NativeBattleTemporaryMemory.Address, U32(step.Frame.Staging!, NativeBattleTemporaryMemory.Cursor));
        Assert.Equal(feedback, BinaryPrimitives.ReadInt16LittleEndian(step.Frame.Staging!.Read(draw.Temporary180 + 0x28, 2)));
    }

    [Theory]
    [InlineData(true, 7, 0x33u, 30, -1)]
    [InlineData(true, 8, 0xF3u, 30, 0)]
    [InlineData(true, 9, 0xF3u, 30, 682)]
    [InlineData(false, 6, 0xF3u, 2, 0)]
    public void Static_callers_keep_partial_cue_stores_native_shift_and_draw_before_pause(bool head, int age, uint flags, int shift, int cue)
    {
        Fixture f = new(1);
        NativeBattleMeltEye158Frame frame = f.Set(f.Set(f.Frame, 12, (ushort)age), 0x1C, 4096);
        frame = frame with { Staging = frame.Staging!.Write(NativeBattleTemporaryMemory.Address + 12, BitConverter.GetBytes(0x12345678)) };
        var step = head ? NativeBattleMeltEye158Controller.AdvanceHeadMesh(frame, ChildBank, f.Resources, f.Inputs) : NativeBattleMeltEye158Controller.AdvanceImpactMesh(frame, ChildBank, f.Resources, f.Inputs);
        NativeBattleMeltEye158DrawStatic88 draw = Assert.Single(f.Draws.OfType<NativeBattleMeltEye158DrawStatic88>());
        Assert.Equal((byte)shift, draw.DepthShift);
        Assert.Equal(flags, U32(step.Frame.Staging!, draw.Temporary88 + 0x1C));
        Assert.Equal(cue < 0 ? 0x12345678 : cue, unchecked((int)U32(step.Frame.Staging!, draw.Temporary88 + 12)));
        Assert.Equal((ushort)age, f.Task(step.Frame).ReadWord(12));
        Assert.Equal(0, step.Result);
        Assert.Equal(NativeBattleTemporaryMemory.Address, U32(step.Frame.Staging!, NativeBattleTemporaryMemory.Cursor));
        Assert.All(step.Frame.Memory.GeometryRegisters!.Rotation20.Skip(18), value => Assert.Null(value));
    }

    [Fact]
    public void Shared_loop_temporary_keeps_extended_flag_between_draws_and_feedback_updates_live_heads()
    {
        Fixture f = new();
        NativeBattleMeltEye158Frame frame = f.Set(f.Frame, 12, 17);
        frame = f.Set(frame, 14, 1);
        for (int index = 0; index < 2; index++)
        {
            uint row = SlotBankA + (uint)(index * 24);
            frame = frame with { State = frame.State.WriteDword(row, 1).WriteWord(row + 6, 500) };
        }
        f.Feedback = -1;
        var step = NativeBattleMeltEye158Controller.AdvanceForkEmitter(frame, ChildBank, f.Resources, f.Inputs);
        Assert.Equal(new ushort[] { 0, 0x100 }, f.Flags);
        Assert.Equal(2, f.Draws.Count);
        Assert.Equal(0u, step.Frame.State.ReadDword(SlotBankA));
        Assert.Equal(0u, step.Frame.State.ReadDword(SlotBankA + 24));
        Assert.Equal(2, step.Result);
        Assert.Equal(NativeBattleTemporaryMemory.Address, U32(step.Frame.Staging!, NativeBattleTemporaryMemory.Cursor));
    }

    [Fact]
    public void Fork_birth_consumes_five_draws_per_child_and_keeps_CPU_position_W_unknown()
    {
        Fixture f = new();
        NativeBattleMeltEye158Frame frame = f.Set(f.Set(f.Frame, 14, 1), 0x18, 7);
        NativeCrtRandomState expected = frame.Random;
        for (int index = 0; index < 10; index++)
        {
            expected = expected.Draw(out _);
        }
        var step = NativeBattleMeltEye158Controller.AdvanceForkEmitter(frame, ChildBank, f.Resources, f.Inputs);
        Assert.Equal(expected, step.Frame.Random);
        Assert.Equal(1u, step.Frame.State.ReadDword(SlotBankA));
        Assert.Equal(1u, step.Frame.State.ReadDword(SlotBankA + 24));
        Assert.Throws<NotSupportedException>(() => step.Frame.State.ReadWord(SlotBankA + 14));
        Assert.Equal(0, step.Result);
    }

    [Fact]
    public void Stream_updates_current_slots_after_draw_and_preserves_JSON_next()
    {
        Fixture f = new();
        NativeBattleMeltEye158Frame frame = f.Set(f.Set(f.Frame, 0x18, ushort.MaxValue), 0x1A, 5);
        frame = frame with { State = frame.State.WriteDword(SlotBankB, 1).WriteWord(SlotBankB + 4, 4).WriteWord(SlotBankB + 6, 500) };
        NativeBattleMeltEye158Frame restored = JsonSerializer.Deserialize<NativeBattleMeltEye158Frame>(JsonSerializer.Serialize(frame))!;
        var first = NativeBattleMeltEye158Controller.AdvanceStream(frame, ChildBank, f.Resources, f.Inputs);
        var second = NativeBattleMeltEye158Controller.AdvanceStream(restored, ChildBank, f.Resources, f.Inputs);
        Assert.Equal(JsonSerializer.Serialize(first.Frame), JsonSerializer.Serialize(second.Frame));
        Assert.Equal((ushort)6, first.Frame.State.ReadWord(SlotBankB + 4));
        Assert.Equal((ushort)4, f.Task(first.Frame).ReadWord(0x18));
    }

    private static uint U32(NativeBattleFileStaging staging, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(address, 4));

    private sealed class Fixture
    {
        public NativeBattleMeltEye158Frame Frame { get; }
        public NativeBattleMeltEye158Resources Resources { get; }
        public NativeBattleMeltEye158Inputs Inputs { get; }
        public List<NativeBattleMeltEye158Operation> Draws { get; } = [];
        public List<ushort> Flags { get; } = [];
        public short Feedback { get; set; }
        public Fixture(uint flags = 0)
        {
            NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootStride, RootCount);
            NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, ChildStride, ChildCount);
            (children, int slot) = children.Append(ImpactSpriteCallback);
            children = children with { Slots = children.Slots.SetItem(slot, children.Slots[slot].WriteBytes(12, new byte[24])) };
            NativeBattleOriginalEffectState effect = new(158, 157, new(158, Entry, Initializer), null, null) { Pools = [root, children] };
            Frame = new(NativeBattleEffectMemory.FromColdProcess(), effect, new(123), NativeBattleMeltEye158State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
            ImmutableArray<NativeBattleModuleAnchor> vectors = Enumerable.Range(0, 11).Select(index => new NativeBattleModuleAnchor(0, 0, (short)(index * 100), 0)).ToImmutableArray();
            Resources = new(new("FF8_EN:DAFBF8", []), new(0, SpriteAAddress, 0, 0, []), new(0, SpriteBAddress, 0, 0, []), new(0, SpriteCAddress, 0, 0, []), new(0, MeshAAddress, []), new(0, MeshBAddress, []), vectors, 0x61AC0);
            NativeBattlePackedMatrix32 identity = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
            Inputs = new(flags, NativeBattleActionPacketsState.Fresh(), (frame, _, _, _) => new(frame, new(0, 0, 0)), (frame, _) => new(frame, new(0, 0, 0)), (_, _) => identity, _ => identity, (frame, op) =>
            {
                Draws.Add(op);
                if (op is NativeBattleMeltEye158DrawSprite180 sprite)
                {
                    ushort current = BinaryPrimitives.ReadUInt16LittleEndian(frame.Staging!.Read(sprite.Temporary180 + 0x24, 2));
                    Flags.Add(current);
                    return frame with { Staging = frame.Staging.Write(sprite.Temporary180 + 0x24, BitConverter.GetBytes((ushort)0x100)).Write(sprite.Temporary180 + 0x28, BitConverter.GetBytes(Feedback)) };
                }
                return frame;
            });
        }
        public NativeBattleModulePoolTask Task(NativeBattleMeltEye158Frame frame) => NativeBattleMeltEye158Controller.Read(frame, ChildBank);
        public NativeBattleMeltEye158Frame Set(NativeBattleMeltEye158Frame frame, int offset, ushort value) => NativeBattleMeltEye158Controller.Write(frame, ChildBank, Task(frame).WriteWord(offset, value));
    }
}
