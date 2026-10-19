using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleUltraWaves154State State, NativeBattleGraphicsState Graphics, int Cursor) CaptureNativeUltraWaves154(NativeBattleUltraWaves154State state, NativeBattleGraphicsState graphics, NativeBattleUltraWaves154Resources resources, int cursor)
    {
        NativeBattlePrimitiveProcess process = NativeEffectMemory!.PrimitiveProcess ?? throw new NotSupportedException("Native Ultra Waves capture requires its primitive process.");
        byte buffer = process.Buffer80 ?? throw new NotSupportedException("Native Ultra Waves capture requires its display buffer.");
        NativeSoftwareDrawingEnvironmentBuffers environments = process.Software?.EnvironmentBuffers ?? throw new NotSupportedException("Native Ultra Waves capture requires its retained DRAWENV records.");
        ImmutableArray<byte> alternate = buffer == 0 ? environments.Buffer1 : environments.Buffer0;
        ImmutableArray<byte> restore = NativeBattleDrawEnvironmentPackets.Restore45C0F0(state.Packets.Read(cursor, 64).ToImmutableArray(), alternate);
        Append(NativeBattleUltraWaves154Drawing.Environment(restore, 64, alternate[0x18] != 0 ? 36 : 24, 0x101B));
        NativeBattleCameraProjection projection = NativeCamera!.Projection!;
        ImmutableArray<NativeBattleHoly175CaptureDirectoryEntry> directory = Enumerable.Range(0, 4).Select(index => new NativeBattleHoly175CaptureDirectoryEntry(resources.StageDraw[index * 2], checked((byte)resources.StageDraw[index * 2 + 1]))).ToImmutableArray();
        NativeBattleHoly175CaptureStage drawn = NativeBattleHoly175CaptureGeometry.Draw5D29E0(NativeStage ?? throw new NotSupportedException("Native Ultra Waves capture requires its stage."), definition.NativeStageSource?.Geometry ?? throw new NotSupportedException("Native Ultra Waves capture requires its actual stage geometry."), directory, projection.View!, projection, definition.NativeGraphics!.Rules.SummonColors!, 108, PublishNativeBodyProjectionScratch);
        NativeStage = drawn.Stage;
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocation = 0;
        foreach (NativeBattleGeometryPrimitive primitive in drawn.Packets)
        {
            byte[] data = new byte[primitive.Words.Length * 4];
            for (int word = 0; word < primitive.Words.Length; word++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(word * 4), primitive.Words[word]);
            }
            attempts.Add(new(allocation, data.ToImmutableArray(), primitive.PhysicalWriteMask ?? throw new NotSupportedException("Native Ultra Waves stage capture requires its actual write mask."), primitive));
            allocation += data.Length;
        }
        NativeBattleCommonPacketWrite common = NativeBattleCommonPacketArena.Append(process, graphics.PrimitiveFrame!.Pending, new(attempts.ToImmutable(), allocation));
        NativeEffectMemory = NativeEffectMemory! with { PrimitiveProcess = common.Process };
        graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = common.Ordering } };
        int staticOffset = buffer * 24;
        byte[] packet = resources.CapturePackets.AsSpan().Slice(staticOffset, 24).ToArray();
        ImmutableArray<uint> words = Enumerable.Range(0, 6).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(index * 4))).ToImmutableArray();
        NativeBattleLogicalOrdering.Builder links = new(graphics.PrimitiveFrame.Pending);
        NativeBattlePrimitiveReference reference = new("DBAEE0", 0, staticOffset);
        links.InvalidatePacketRange(reference, 24);
        bool linked = links.ProxyCount < NativeBattlePrimitiveOrdering.ProxyCapacity;
        links.Prepend(0x1121, reference, words, 1, [0, 0, 0, 0]);
        if (linked)
        {
            NativeBattleLogicalOrdering.LinkBytes(links.LastHeaderLink).CopyTo(packet, 0);
        }
        state = state with { CapturePackets = state.CapturePackets.Write(staticOffset, packet) };
        graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = links.ToImmutable() } };
        Append(NativeBattleUltraWaves154Drawing.Environment(NativeBattleDrawEnvironmentPackets.Area45C940(state.Packets.Read(cursor, 12).ToImmutableArray(), 384, 256, 320, 216), 12, 12, 0x1121));
        Append(NativeBattleUltraWaves154Drawing.Environment(NativeBattleDrawEnvironmentPackets.Offset45C9B0(state.Packets.Read(cursor, 8).ToImmutableArray(), 384, 256), 12, 8, 0x1121));
        return (state, graphics, cursor);

        void Append(NativeBattleGfGeometryBatch batch)
        {
            NativeBattleRegionalPacketWrite written = NativeBattleRegionalPacketArena.Append(state.Packets, "24224D4", cursor, 0x18000, graphics.PrimitiveFrame!.Pending, batch);
            state = state with { Packets = written.Arena };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
            cursor = written.Cursor;
        }
    }
}
