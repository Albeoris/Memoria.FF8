using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect209Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private static NativeBattleEffect209Frame DeliverNativeEffect209Batch(ref NativeBattleGraphicsState graphics, NativeBattleEffect209Frame frame, NativeBattleEffect209DrawBatch draw)
    {
        NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect209 requires its current ordering table.");
        if (draw.CursorGlobal == 0x1D8E054)
        {
            NativeBattleCommonPacketWrite common = NativeBattleCommonPacketDelivery.Deliver(frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect209 requires its ordinary packet process."), primitive.Pending, draw.Batch, draw.Flags);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = common.Ordering } };
            return frame with { Memory = frame.Memory with { PrimitiveProcess = common.Process } };
        }
        if (draw.CursorGlobal != CursorGlobal)
        {
            throw new NotSupportedException("Effect209 selected an unsupported packet cursor.");
        }
        int cursor = NativeBattleWaterExecutionFields.ArenaOffset(frame.State.ReadDword(CursorGlobal), 0);
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect209 requires its current effect arena.");
        NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.ResolveWithKnownness(draw.Batch, cursor, NativeBattleEffectArena.Length, address =>
        {
            (byte[] bytes, bool[] known) = arena.ReadSnapshot(address, 1);
            return (bytes[0], known[0]);
        });
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(frame.Memory, cursor, primitive.Pending, resolved, draw.Flags);
        graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
        return frame with { Memory = written.Memory, State = frame.State.WriteDword(CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
    }

    private static NativeBattleEffect209Frame DeliverNativeEffect209Template(ref NativeBattleGraphicsState graphics, NativeBattleEffect209Frame frame, NativeBattleEffect209DrawAddressedTemplate draw)
    {
        int offset = checked((int)(draw.Address - 0x209FAB8));
        NativeBattleGuardianWorkBuffer work = frame.Memory.GuardianWorkBuffer ?? throw new NotSupportedException("Effect209 requires its current screen template bank.");
        (byte[] bytes, _) = draw.Data40.ReadSnapshot(0, 40);
        NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[40];
        for (int index = 4; index < 8; index++)
        {
            mask[index] = NativeBattleGfPacketByteState.Known;
        }
        for (int corner = 0; corner < 4; corner++)
        {
            for (int index = 8 + corner * 8; index < 12 + corner * 8; index++)
            {
                mask[index] = NativeBattleGfPacketByteState.Known;
            }
        }
        ImmutableArray<uint> words = Enumerable.Range(0, 10).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToImmutableArray();
        NativeBattleGeometryPrimitive linked = new(draw.Bucket, words, []);
        NativeBattleGfGeometryBatch batch = new([new(0, bytes.ToImmutableArray(), mask.ToImmutableArray(), linked)], 40);
        NativeBattleEffectArena retained = new(work.Regions);
        batch = NativeBattlePacketBatchWords.ResolveWithKnownness(batch, offset, NativeBattleGuardianWorkBuffer.Length, address =>
        {
            (byte[] value, bool[] known) = retained.ReadSnapshot(address, 1);
            return (value[0], known[0]);
        });
        NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect209 requires its current ordering table.");
        NativeBattleEffectPacketWrite written = NativeBattleGuardianWorkPacketArena.Append(frame.Memory, offset, primitive.Pending, batch, draw.Flags);
        graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
        return frame with { Memory = written.Memory };
    }
}
