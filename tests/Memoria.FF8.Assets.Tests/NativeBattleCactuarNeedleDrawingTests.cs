using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleCactuarNeedleDrawingTests
{
    [Fact]
    public void Original_material_commands_emit_crossed_common_packets_with_independent_cold_next_draw()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleCactuar199Resources resources = game.ReadNativeCactuar199Resources();
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleActionMatrix view = identity with { Z = 4096 };
        NativeBattleCameraProjection projection = new(view, 216, 160, 108);
        NativeBattlePrimitiveProcess process = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400();
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        ImmutableArray<byte> commands = resources.EarlyColors.Data.AddRange(resources.LateColors.Data);
        Assert.Equal(40, commands.Length);
        for (int offset = 0; offset < commands.Length; offset += 4)
        {
            uint color = BinaryPrimitives.ReadUInt32LittleEndian(commands.AsSpan().Slice(offset));
            Assert.Equal(0x2Eu, color >> 24);
            NativeBattleCactuarNeedleDraw draw = NativeBattleCactuarNeedleDrawing.Emit5A9C10(identity, new(100, -20, 300), color, view, projection);
            Assert.Equal(80, draw.Packets.AllocationBytes);
            Assert.All(draw.Packets.Attempts, attempt => Assert.Equal(color, attempt.Primitive!.Words[1]));
            NativeBattlePrimitiveProcess coldProcess = NativeBattleSnapshotJson.Deserialize<NativeBattlePrimitiveProcess>(NativeBattleSnapshotJson.Serialize(process))!;
            NativeBattleLogicalOrdering coldOrdering = NativeBattleSnapshotJson.Deserialize<NativeBattleLogicalOrdering>(NativeBattleSnapshotJson.Serialize(ordering))!;
            NativeBattleCommonPacketWrite next = NativeBattleCommonPacketArena.Append(process, ordering, draw.Packets);
            Assert.Equal(NativeBattleSnapshotJson.Serialize(next), NativeBattleSnapshotJson.Serialize(NativeBattleCommonPacketArena.Append(coldProcess, coldOrdering, draw.Packets)));
            process = next.Process;
            ordering = next.Ordering;
        }
        Assert.Equal(800, process.ByteCursor);
        Assert.Equal(20, ordering.Proxies.Length);
    }
}
