using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTextureAnimationTests
{
    [Fact]
    public void Opcode9FConsumesOnlyItsTerminatedByteListAndSchedulesTheSharedAuxiliaryTaskInline()
    {
        NativeBattleGraphicsResource resource = new([1, 0, 4, 0, 0x9F, 2, 5, 255, 0xA1]) { AnimationFrames = [1] };
        NativeBattleGraphicsModel model = Model(1) with { Flags0 = 0x403 };
        NativeBattleAuxiliaryProgram? scheduled = null;
        List<string> calls = [];
        NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(model, resource, new(null, null)
        {
            ScheduleTextureAnimation = (program, sequence) =>
            {
                scheduled = program;
                Assert.Equal(1, sequence);
                calls.Add("schedule");
            },
            ApplyTexture = (selector, variant) => calls.Add($"copy:{selector}:{variant}")
        });
        Assert.Equal(["schedule", "copy:0:0"], calls);
        Assert.Equal(5, scheduled!.Offset);
        Assert.Equal((ushort)5, step.Model.SequenceState!.Offset46);
        Assert.Equal(0, step.Model.Flags0 & 0x400);
        Assert.Empty(step.Cues);
        NativeBattleGraphicsResource unterminated = new([1, 0, 4, 0, 0x9F, 2, 3]) { AnimationFrames = [1] };
        Assert.Throws<InvalidDataException>(() => NativeBattleSequenceMachine.Advance(model, unterminated, new(null, null)
        {
            ScheduleTextureAnimation = (_, _) =>
            {
            },
            ApplyTexture = (_, _) =>
            {
            }
        }));
    }

    [Fact]
    public void DeadlineCounterIsCumulativeAndProcessesAtMostOneBytePerCallbackAcrossRestore()
    {
        NativeBattleAuxiliaryState state = NativeBattleAuxiliaryState.Clear().AppendTextureAnimation(0, 11, new([2, 2, 0, 255], 0));
        NativeBattleGraphicsModel model = Model(11);
        List<byte> variants = [];
        NativeBattleAuxiliaryInputs inputs = new()
        {
            ReadModel = _ => model,
            ApplyTexture = (_, selector, variant) =>
            {
                Assert.Equal(0, selector);
                variants.Add(variant);
            },
            RestoreTextureBlink = _ => model = model with { Flags0 = (ushort)(model.Flags0 | 0x400) }
        };
        for (int frame = 1; frame <= 5; frame++)
        {
            state = state.Advance(inputs).State;
            state = JsonSerializer.Deserialize<NativeBattleAuxiliaryState>(JsonSerializer.Serialize(state))!;
            state.Validate();
            Assert.Equal(Math.Min(3, Math.Max(0, frame - 1)), variants.Count);
        }
        Assert.Equal(new byte[] { 1, 0, 1 }, variants);
        Assert.Equal((ushort)4, BinaryPrimitives.ReadUInt16LittleEndian(state.Slots[0].Payload.AsSpan().Slice(10)));
        model = model with { Sequence = 12 };
        state = state.Advance(inputs).State;
        Assert.Equal(new byte[] { 1, 0, 1, 0 }, variants);
        Assert.Equal(0x400, model.Flags0 & 0x400);
        Assert.Null(state.Head);
        Assert.Equal((ushort)0, state.Slots[0].Flags);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(255)]
    public void BDBEApplyUnsignedTextureOperandsAndResetInlineBeforeFollowingAction(int blink)
    {
        NativeBattleGraphicsResource resource = new([1, 0, 4, 0, 0xBE, (byte)blink, 0xBD, 128, 255, 0xAA, 0xA1]) { AnimationFrames = [1] };
        List<string> order = [];
        NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(Model(1) with { Flags0 = 0x403 }, resource, new(null, null)
        {
            ApplyTexture = (selector, variant) => order.Add($"copy:{selector}:{variant}"),
            ApplyActionContacts = model =>
            {
                order.Add("action");
                Assert.Equal(blink != 0, (model.Flags0 & 0x400) != 0);
                return model;
            }
        });
        Assert.Equal(["copy:0:0", "copy:128:255", "action"], order);
        Assert.Equal((ushort)7, step.Model.SequenceState!.Offset46);
        Assert.Empty(step.Cues);
    }

    [Fact]
    public void TextureDeadlineCounterWrapsItsUnsignedWordAndDoesNotAdvanceOnTheTerminator()
    {
        NativeBattleAuxiliaryState state = NativeBattleAuxiliaryState.Clear().AppendTextureAnimation(0, 11, new([1, 255], 0));
        byte[] payload = state.Slots[0].Payload.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(10), ushort.MaxValue);
        state = state with { Slots = state.Slots.SetItem(0, state.Slots[0] with { Payload = payload.ToImmutableArray() }) };
        int copies = 0;
        NativeBattleAuxiliaryInputs inputs = new() { ReadModel = _ => Model(11), ApplyTexture = (_, _, _) => copies++ };
        state = state.Advance(inputs).State;
        Assert.Equal(0, copies);
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(state.Slots[0].Payload.AsSpan().Slice(10)));
        state = state.Advance(inputs).State;
        Assert.Equal(1, copies);
        state = state.Advance(inputs).State;
        Assert.Equal(1, copies);
        Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(state.Slots[0].Payload.AsSpan().Slice(10)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CallbackAppliesTheReachedDeadlineBeforeItsLoadedAndSequenceCompletionRelease(bool removed)
    {
        NativeBattleAuxiliaryState state = NativeBattleAuxiliaryState.Clear().AppendTextureAnimation(0, 11, new([0, 255], 0));
        NativeBattleGraphicsModel model = Model(11) with { Flags0 = removed ? (ushort)0 : (ushort)3, ResourceFlags44 = removed ? (ushort)0 : (ushort)0x40 };
        List<byte> variants = [];
        bool restored = false;
        NativeBattleAuxiliaryStep result = state.Advance(new() { ReadModel = _ => model, ApplyTexture = (_, _, variant) => variants.Add(variant), RestoreTextureBlink = _ => restored = true });
        Assert.Equal(new byte[] { 1, 0 }, variants);
        Assert.True(restored);
        Assert.Null(result.State.Head);
    }

    [Fact]
    public void TextureMotionUsesTheOriginalAllocatedOrdinalAndByteRectangleOperands()
    {
        ImmutableArray<byte> table = [2, 0, 1, 2, 3, 4, 5, 0, 0, 0, 10, 20, 30, 40];
        Assert.Equal(new NativeBattleTextureCopy(798, 168, 4, 5, 770, 131), NativeBattleTextureMotion.Plan50C780(table, 0x30, 0, 1));
        Assert.Null(NativeBattleTextureMotion.Plan50C780([], null, 0, 0));
        Assert.Null(NativeBattleTextureMotion.Plan50C780([0, 0], null, 0, 0));
        Assert.Throws<NotSupportedException>(() => NativeBattleTextureMotion.Plan50C780(null, 3, 0, 0));
        Assert.Throws<NotSupportedException>(() => NativeBattleTextureMotion.Plan50C780(table, null, 0, 0));
        Assert.Throws<InvalidDataException>(() => NativeBattleTextureMotion.Plan50C780(table, 3, 0, 2));
    }

    [Fact]
    public void PackedModelTimsRelocateIntoThePlayerPhysicalSlotsWithoutChangingTheirOriginal()
    {
        byte[] bytes = new byte[144];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 2);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 12);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 78);
        for (int index = 0; index < 2; index++)
        {
            int offset = 12 + index * 66;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), 16);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset + 4), 8);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset + 8), 44);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 14), (short)(224 + index));
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 16), 16);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 18), 1);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 22), 31);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset + 52), 14);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 60), 1);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 62), 1);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 64), 0x1111);
        }
        NativeBattleEffectHeapAllocation source = new("body", bytes.ToImmutableArray());
        NativeBattleModelTextureLoad load = NativeBattleModelTextures.Prepare507400(source, unchecked((ushort)~0x30));
        Assert.Equal((ushort)0x30, load.AllocatedSlots);
        Assert.Equal((byte?)4, load.Binding.PaletteRows[0]);
        Assert.Equal((byte?)5, load.Binding.PaletteRows[1]);
        Assert.Equal((short)224, BinaryPrimitives.ReadInt16LittleEndian(source.Data.AsSpan().Slice(26)));
        NativeBattleEffectTextureDrain drain = NativeBattleModelTextures.Enqueue507400(NativeBattleEffectTextureState.FromColdProcess(), load).Drain505D20();
        Assert.Equal(new short[] { 228, 0, 229, 128 }, drain.Uploads.Select(upload => upload.Y));
        Assert.Equal(new short[] { 0, 768, 0, 768 }, drain.Uploads.Select(upload => upload.X));
    }

    private static NativeBattleGraphicsModel Model(byte sequence) => new(0, 3) { Flags2 = 4, Flags8 = 0, Kind5 = 0, Sequence = sequence, ResourceFlags44 = 1, SequenceState = new() { Pose = 0, PoseFrame114 = 0, PoseLimit115 = 1 } };
}
