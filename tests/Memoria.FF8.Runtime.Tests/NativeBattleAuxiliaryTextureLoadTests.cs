using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAuxiliaryTextureLoadTests
{
    private const uint Destination = 0x20DFAB8 + 0x2E50;
    private const uint Completion = 0x25217A8;

    [Fact]
    public void Successful_allocation_writes_only_phase_and_two_pointers_then_clears_completion()
    {
        NativeBattleAuxiliaryState state = NativeBattleAuxiliaryState.Clear();
        NativeBattleAuxiliaryTask prior = state.Slots[0] with { Flags = 0xA500, Payload = Enumerable.Repeat((byte)0xCC, 32).ToImmutableArray(), UnknownByteMask = uint.MaxValue };
        state = state with { Slots = state.Slots.SetItem(0, prior) };
        List<(uint Address, byte Value)> writes = [];
        (NativeBattleAuxiliaryState result, bool allocated) = state.AppendCommonTextureLoad508630(Destination, Completion, (address, value) => writes.Add((address, value)));
        Assert.True(allocated);
        Assert.Equal(new[] { (Completion, (byte)0) }, writes);
        NativeBattleAuxiliaryTask task = result.Slots[0];
        Assert.Equal((ushort)0xA501, task.Flags);
        Assert.Equal((ushort)0, task.State);
        Assert.Equal(NativeBattleAuxiliaryKind.CommonTextureLoad508660, task.Kind);
        Assert.Equal((byte)0, NativeBattleStorageSnapshots.Capture(task).Payload[0]);
        Assert.Equal(Destination, BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleStorageSnapshots.Capture(task).Payload.AsSpan().Slice(4)));
        Assert.Equal(Completion, BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleStorageSnapshots.Capture(task).Payload.AsSpan().Slice(8)));
        Assert.Equal(uint.MaxValue & ~0xFF1u, task.UnknownByteMask);
        Assert.All(new[] { 1, 2, 3 }.Concat(Enumerable.Range(12, 20)), index => Assert.Equal((byte)0xCC, NativeBattleStorageSnapshots.Capture(task).Payload[index]));
        result.Validate();
    }

    [Fact]
    public void Exhausted_shared_pool_does_not_clear_completion_or_replace_any_task()
    {
        NativeBattleAuxiliaryState state = NativeBattleAuxiliaryState.Clear();
        for (int index = 0; index < 16; index++)
        {
            state = state.AppendCameraBlend(10);
        }
        (NativeBattleAuxiliaryState result, bool allocated) = state.AppendCommonTextureLoad508630(Destination, Completion, (_, _) => throw new InvalidOperationException("No write is native on allocation failure."));
        Assert.False(allocated);
        Assert.Same(state, result);
    }

    [Theory]
    [InlineData(int.MinValue, false)]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(int.MaxValue, true)]
    public void Phase1_uses_shared_signed_result_and_does_not_complete_when_only_queuing_TIM(int result, bool ready)
    {
        NativeBattleAuxiliaryState state = Append();
        List<(ushort File, uint Destination)> requests = [];
        state = state.Advance(new() { RequestFile5084B0 = (file, address) => requests.Add((file, address)) }).State;
        Assert.Equal(new[] { ((ushort)0xA3, Destination) }, requests);
        Assert.Equal((byte)1, state.Slots[0].Payload[0]);
        int enqueued = 0;
        NativeBattleAuxiliaryStep step = state.Advance(new()
        {
            ReadEffectIoResult508470 = () => result,
            EnqueueTexture505E30 = address =>
            {
                Assert.Equal(Destination, address);
                enqueued++;
            },
            WriteCompletionByte = (_, _) => throw new InvalidOperationException("Phase1 cannot complete.")
        });
        Assert.Equal(ready ? 1 : 0, enqueued);
        Assert.Equal(ready ? (byte)2 : (byte)1, step.State.Slots[0].Payload[0]);
        Assert.Equal(0, step.State.Head);
        string json = JsonSerializer.Serialize(step.State);
        Assert.Equal(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleAuxiliaryState>(json)));
    }

    [Fact]
    public void Current_mutable_TIM_is_queued_before_completion_on_a_later_auxiliary_pass()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(115) with { QuezacotlCompletion17A8 = null };
        NativeBattleAuxiliaryState priorPass = NativeBattleAuxiliaryState.Clear().Advance().State;
        (NativeBattleAuxiliaryState state, _) = priorPass.AppendCommonTextureLoad508630(Destination, Completion, (_, value) => memory = memory with { QuezacotlCompletion17A8 = value });
        int signedResult = -99;
        int requests = 0;
        byte[] old = Tim(0x1111);
        NativeBattleAuxiliaryInputs inputs = new()
        {
            RequestFile5084B0 = (file, address) =>
            {
                Assert.Equal((ushort)0xA3, file);
                Assert.Equal(Destination, address);
                requests++;
                signedResult = -1;
                memory = memory with { Arena = memory.Arena!.Write(0x2E50, old) };
            },
            ReadEffectIoResult508470 = () => signedResult,
            EnqueueTexture505E30 = address =>
            {
                Assert.Equal(Destination, address);
                (NativeBattleEffectTextureState textures, _) = memory.Textures!.EnqueueArenaTim505E30(memory.Arena!, 0x2E50);
                memory = memory with { Textures = textures };
            },
            WriteCompletionByte = (address, value) =>
            {
                Assert.Equal(Completion, address);
                memory = memory with { QuezacotlCompletion17A8 = value };
            }
        };
        Assert.Equal(0, requests);
        state = state.Advance(inputs).State;
        Assert.Equal(1, requests);
        Assert.Equal(-1, signedResult);
        state = state.Advance(inputs).State;
        Assert.Equal((byte)1, state.Slots[0].Payload[0]);
        memory = memory with { Arena = memory.Arena!.Write(0x2E50, Tim(0x3210)) };
        signedResult = 0;
        state = state.Advance(inputs).State;
        Assert.Equal((byte)0, memory.QuezacotlCompletion17A8);
        Assert.Equal(0x2E50, memory.Textures!.Slots[0]!.ArenaOffset);
        NativeBattleEffectTextureDrain drain = memory.Textures.Drain505D20(memory.Arena);
        Assert.Equal(new byte[] { 0x10, 0x32 }, drain.Uploads[1].Bytes.ToArray());
        state = JsonSerializer.Deserialize<NativeBattleAuxiliaryState>(JsonSerializer.Serialize(state))!;
        state = state.Advance(inputs).State;
        Assert.Equal((byte)255, memory.QuezacotlCompletion17A8);
        Assert.Null(state.Head);
        Assert.Null(state.Tail);
        Assert.Equal((ushort)0, state.Slots[0].Flags);
        Assert.Equal((byte)2, state.Slots[0].Payload[0]);
        Assert.Equal(1, requests);
    }

    [Fact]
    public void Unhandled_phase_is_retained_and_required_current_callbacks_stay_strict()
    {
        NativeBattleAuxiliaryState state = Append();
        Assert.Throws<NotSupportedException>(() => state.Advance());
        NativeBattleAuxiliaryState invalid = state with { Slots = state.Slots.SetItem(0, state.Slots[0] with { Payload = state.Slots[0].Payload.SetItem(0, 255) }) };
        Assert.Equal(JsonSerializer.Serialize(invalid), JsonSerializer.Serialize(invalid.Advance().State));
        NativeBattleAuxiliaryState unknown = state with { Slots = state.Slots.SetItem(0, state.Slots[0] with { UnknownByteMask = 0x10 }) };
        Assert.Throws<InvalidDataException>(unknown.Validate);
    }

    private static NativeBattleAuxiliaryState Append()
    {
        return NativeBattleAuxiliaryState.Clear().AppendCommonTextureLoad508630(Destination, Completion, (_, _) =>
        {
        }).State;
    }

    private static byte[] Tim(ushort pixel)
    {
        byte[] data = new byte[66];
        BinaryPrimitives.WriteUInt32LittleEndian(data, 16);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), 8);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(8), 44);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(16), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(18), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(52), 14);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(56), 64);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(58), 256);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(60), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(62), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(64), pixel);
        return data;
    }
}
