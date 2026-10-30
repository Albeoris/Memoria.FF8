using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect209Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private static NativeBattleEffect209Frame DeliverNativeEffect209Batch(ref NativeBattleGraphicsState graphics, NativeBattleEffect209Frame frame, NativeBattleEffect209DrawBatch draw)
    {
        NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect209 requires its current ordering table.");
        if (draw.CursorGlobal != 0x1D8E054)
        {
            throw new NotSupportedException("Effect209 selected an unsupported packet cursor.");
        }
        draw = draw with { Batch = NativeBattleCinematicCapturePresentation.Depths(draw.Batch) };
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess!;
        frame = frame with { Effect = frame.Effect with { PresentationPackets = NativeBattleEffectPresentationPackets.Record(frame.Effect.PresentationPackets, process.PacketBank!, process.PacketBuffer, process.ByteCursor!.Value, draw.Batch) } };
        NativeBattleCommonPacketWrite common = NativeBattleCommonPacketDelivery.Deliver(frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect209 requires its ordinary packet process."), primitive.Pending, draw.Batch, draw.Flags);
        graphics = graphics with { PrimitiveFrame = primitive with { Pending = common.Ordering } };
        return frame with { Memory = frame.Memory with { PrimitiveProcess = common.Process } };
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
        NativeBattleEffectArena retained = work.Arena;
        batch = NativeBattlePacketBatchWords.ResolveWithKnownness(batch, offset, NativeBattleGuardianWorkBuffer.Length, address =>
        {
            (byte[] value, bool[] known) = retained.ReadSnapshot(address, 1);
            return (value[0], known[0]);
        });
        NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect209 requires its current ordering table.");
        batch = NativeBattleCinematicCapturePresentation.Discrete(batch, frame.PresentationIdentity is { } identity ? identity with { ResourceGeneration = draw.Address } : null);
        frame = frame with { Effect = frame.Effect with { PresentationPackets = NativeBattleEffectPresentationPackets.Record(frame.Effect.PresentationPackets, "209FAB8", 0, offset, batch) } };
        NativeBattleEffectPacketWrite written = NativeBattleGuardianWorkPacketArena.Append(frame.Memory, offset, primitive.Pending, batch, draw.Flags);
        graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
        return frame with { Memory = written.Memory };
    }
}
