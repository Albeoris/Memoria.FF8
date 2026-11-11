using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleJunk44Controller;
using static Memoria.FF8.Runtime.NativeBattleJunk44Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleJunk44CleanupTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cleanup_preserves_both_TIM_buffers_until_deferred_upload_with_original_double_poll(bool restoreJson)
    {
        NativeBattleJunk44Resources resources = NativeBattleJunk44StateTests.Resources() with { Files = [new(360, "MA8DEF_0.TIM", Tim(704, 0x1234)), new(361, "MA8DEF_1.TIM", Tim(768, 0x5678))] };
        Assert.All(resources.Files, file => Assert.True(file.Data.Length <= PacketBankBytes));
        Assert.Equal(ScratchOffset, PacketOffset + 2 * PacketBankBytes);
        Assert.True(Pools.Skip(1).Max(pool => pool.Bank + (uint)(pool.Stride * pool.Capacity)) <= ArenaAddress + PacketOffset);
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
        (root, _) = root.Append(RootCallback);
        NativeBattleOriginalEffectState effect = new(44, 43, new(44, Entry, Initializer), new(3, 0, 0x7F, 1, 0, 0), null) { Pools = [root] };
        NativeBattleJunk44State state = NativeBattleJunk44State.FromColdProcess().WriteDword(ArenaGlobal, ArenaAddress) with { RootBankStorage = null };
        NativeBattleJunk44Frame frame = new(NativeBattleEffectIo.Initialize534110(NativeBattleEffectMemory.FromColdProcess().BeginLoad(44), 0x128C), effect, new(123), state);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(0x0C, 115).WriteByte(0x0F, 1));
        List<short> requested = [];
        int advanceCalls = 0;
        NativeBattleJunk44Inputs inputs = new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), Poll, Execute);
        frame = AdvanceTask(frame, RootBank, resources, inputs).Frame;
        Assert.Equal(new short[] { 360 }, requested);
        frame = frame with { Memory = NativeBattleEffectIo.Complete534150(frame.Memory).Memory };
        Assert.Equal(1, frame.Memory.Textures!.Count);
        frame = Restore(frame);
        int priorCalls = advanceCalls;
        frame = AdvanceTask(frame, RootBank, resources, inputs).Frame;
        Assert.Equal(priorCalls + 2, advanceCalls);
        Assert.Equal(new short[] { 360, 361 }, requested);
        Assert.Equal(1, frame.Memory.Textures!.Count);
        frame = Restore(frame);
        NativeBattleEffectTextureDrain first = frame.Memory.Textures!.Drain505D20(frame.Memory.Arena);
        AssertRectangle(first.State.ReadVram(), 704, 0x1234);
        frame = frame with { Memory = frame.Memory with { Textures = first.State } };
        frame = frame with { Memory = NativeBattleEffectIo.Complete534150(frame.Memory).Memory };
        frame = Restore(frame);
        NativeBattleEffectTextureDrain second = frame.Memory.Textures!.Drain505D20(frame.Memory.Arena);
        AssertRectangle(second.State.ReadVram(), 704, 0x1234);
        AssertRectangle(second.State.ReadVram(), 768, 0x5678);
        frame = frame with { Memory = frame.Memory with { Textures = second.State } };
        Assert.Equal(2, AdvanceTask(frame, RootBank, resources, inputs).Result);

        NativeBattleJunk44Frame Restore(NativeBattleJunk44Frame current)
        {
            return restoreJson ? NativeBattleTestSnapshots.RestoreJson<NativeBattleJunk44Frame>(NativeBattleTestSnapshots.Json(current)) : current;
        }

        NativeBattleJunk44Frame Queue(NativeBattleJunk44Frame current, NativeBattleEffectFileRequest? request)
        {
            if (request is null)
            {
                return current;
            }
            requested.Add(request.FileId);
            NativeBattleEffectFileAsset file = resources.Files.Single(file => file.FileId == request.FileId);
            return current with { Memory = current.Memory with { Arena = current.Memory.Arena!.Write(checked((int)(request.DestinationAddress - ArenaAddress)), file.Data.AsSpan()) } };
        }

        NativeBattleJunk44Frame Execute(NativeBattleJunk44Frame current, NativeBattleJunk44Operation operation)
        {
            Assert.IsType<NativeBattleJunk44AdvanceIo534210>(operation);
            advanceCalls++;
            NativeBattleEffectIoAdvance advance = NativeBattleEffectIo.Advance534210(current.Memory);
            return Queue(current with { Memory = advance.Memory }, advance.Request);
        }

        NativeBattleJunk44IoStep Poll(NativeBattleJunk44Frame current)
        {
            advanceCalls++;
            NativeBattleEffectIoPoll poll = NativeBattleEffectIo.Select5342C0(current.Memory, 1);
            return new(Queue(current with { Memory = poll.Memory }, poll.Request), !poll.Busy);
        }
    }

    private static void AssertRectangle(byte[] vram, int x, ushort value)
    {
        for (int row = 256; row < 320; row++)
        {
            for (int column = x; column < x + 64; column++)
            {
                Assert.Equal(value, BinaryPrimitives.ReadUInt16LittleEndian(vram.AsSpan(2 * (row * 1024 + column), 2)));
            }
        }
    }

    private static ImmutableArray<byte> Tim(ushort x, ushort value)
    {
        byte[] bytes = new byte[33824];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 16);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 8);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 44);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(18), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(52), 12 + 64 * 64 * 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(56), x);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(58), 256);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(60), 64);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(62), 64);
        for (int offset = 64; offset < 64 + 64 * 64 * 2; offset += 2)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
        }
        return bytes.ToImmutableArray();
    }
}
