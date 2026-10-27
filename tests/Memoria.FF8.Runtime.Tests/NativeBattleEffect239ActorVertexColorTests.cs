using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect239ActorVertexColorTests
{
    private static readonly uint Base = NativeBattleEffectNodeMemory.ArenaAddress;
    private static readonly uint State = Base + 0x1000;
    private static readonly uint Distances = Base + 0x1100;
    private static readonly uint Palette = Base + 0x1200;
    private static readonly uint Colors = Base + 0x2000;

    [Fact]
    public void Workspace_update_preserves_active_high_unknowns_and_clears_inactive_dwords()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(2);
        frame = PutWord(frame, Distances, 0);
        frame = PutWord(frame, Distances + 2, 100);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x2003, 1).MarkUnknown(0x2007, 1) };
        NativeBattleEffectArena before = NativeBattleStorageSnapshots.Capture(frame.Arena);
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicActorVertexColor20.UpdateColors(frame, State, Colors, 100, 128, 0, 0, new(0, 0, 0));
        Assert.Equal(new byte[] { 16, 32, 64 }, result.Arena.Read(0x2000, 3));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x2003, 1));
        Assert.Equal(new byte[4], result.Arena.Read(0x2004, 4));
        Assert.Throws<NotSupportedException>(() => before.Read(0x2007, 1));
        Assert.Same(frame.Arena, result.Arena);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame.Core)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result.Core)));
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame.TemporaryFrame)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result.TemporaryFrame)));
        Assert.Equal(frame.Random, result.Random);
    }

    [Fact]
    public void Object_color_selection_uses_actual_object_index_despite_a_skipped_prior_object()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(3);
        frame = PutWord(frame, State + 0x10, 0);
        frame = PutWord(frame, State + 0x12, 4);
        frame = PutDword(frame, Colors, 0x00112233);
        frame = PutDword(frame, Colors + 8, 0xAABBCCDD);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x200B, 1) };
        NativeBattleCinematicVertexColors selected = NativeBattleCinematicActorVertexColor20.ReadObjectColors(frame, State, Colors, 1, 1);
        Assert.Equal(0xAABBCCDDu, Assert.Single(selected.Colors));
        Assert.False(Assert.Single(selected.HighKnown));
        Assert.Equal(0x00112233u, Assert.Single(NativeBattleCinematicActorVertexColor20.ReadObjectColors(frame, State, Colors, 0, 1).Colors));
    }

    [Fact]
    public void Current_workspace_knownness_reaches_actual_glow_packet_masks_and_ordering()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(3);
        frame = PutWord(frame, State + 0x10, 0);
        for (int index = 0; index < 3; index++)
        {
            frame = frame with { Arena = frame.Arena.MarkUnknown(0x2003 + index * 4, 1) };
        }
        frame = NativeBattleCinematicActorVertexColor20.UpdateColors(frame, State, Colors, 100, 128, 0, 0, new(0, 0, 0));
        NativeBattleCinematicVertexColors colors = NativeBattleCinematicActorVertexColor20.ReadObjectColors(frame, State, Colors, 0, 3);
        NativeBattleGfGeometryBatch batch = NativeBattleCinematicMonsterPackets.Glow(Triangle(), new NativeBattleProjectedVertex[] { new(0, 100), new(10, 100), new(10u << 16, 100) }, colors.Colors, 7, colors.HighKnown);
        Assert.Equal(40, batch.AllocationBytes);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Equal((byte)9, attempt.Data[3]);
        Assert.Equal((byte)0x34, attempt.Data[7]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[7]);
        Assert.Equal(NativeBattleGfPacketByteState.Unknown, attempt.WriteMask[19]);
        Assert.Equal(NativeBattleGfPacketByteState.Unknown, attempt.WriteMask[31]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[38]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[39]);
        Assert.Equal(13, attempt.Primitive!.Bucket);
    }

    [Fact]
    public void Empty_distance_workspace_does_not_require_unread_palette_or_distance_payloads()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x1100, 2).MarkUnknown(0x1200, 1024) };
        Assert.Same(frame, NativeBattleCinematicActorVertexColor20.UpdateColors(frame, State, Colors, 0, 0, 0, 0, new(0, 0, 0)));
    }

    [Fact]
    public void Inactive_vertices_do_not_consume_any_unknown_palette_rgb()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(2);
        frame = PutWord(frame, Distances, 100);
        frame = PutWord(frame, Distances + 2, 101);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x1200, 1024) };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicActorVertexColor20.UpdateColors(frame, State, Colors, 100, 128, 0, 0, new(0, 0, 0));
        Assert.Equal(new byte[8], result.Arena.Read(0x2000, 8));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x1200, 3));
    }

    [Fact]
    public void Active_vertex_consumes_only_the_selected_palette_rgb()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(1);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x1200, 1024).Write(0x1200 + 17 * 4, new byte[] { 32, 64, 128 }) };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicActorVertexColor20.UpdateColors(frame, State, Colors, 100, 128, 17, 0, new(0, 0, 0));
        Assert.Equal(new byte[] { 16, 32, 64 }, result.Arena.Read(0x2000, 3));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x1200, 3));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x1200 + 17 * 4 + 3, 1));
    }

    [Fact]
    public void Unknown_selected_palette_rgb_rolls_back_earlier_updates_in_the_caller_transaction()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(2);
        frame = PutWord(frame, Distances, 100);
        frame = frame with { Arena = frame.Arena.Write(0x2000, new byte[] { 1, 2, 3, 4 }).MarkUnknown(0x1201, 1) };
        string before = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame));
        using (NativeBattleEffectArenaTransaction transaction = new(frame.Arena))
        {
            Assert.Throws<NotSupportedException>(() => NativeBattleCinematicActorVertexColor20.UpdateColors(frame with { Arena = transaction.View }, State, Colors, 100, 128, 0, 0, new(0, 0, 0)));
        }
        Assert.Equal(before, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame)));
    }

    [Fact]
    public void JSON_next_preserves_workspace_aliases_and_high_byte_knownness()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(2);
        frame = PutWord(frame, State + 0x10, 0);
        frame = PutWord(frame, Distances + 2, 100);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x2003, 1) };
        frame = NativeBattleCinematicActorVertexColor20.UpdateColors(frame, State, Colors, 100, 128, 0, 0, new(0, 0, 0));
        string json = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame));
        NativeBattleWaterExecutionFrame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(json)!);
        Assert.Equal(json, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(restored)));
        NativeBattleWaterExecutionFrame direct = NativeBattleCinematicActorVertexColor20.UpdateColors(frame, State, Colors, 101, 64, 0, 0, new(0, 0, 0));
        NativeBattleWaterExecutionFrame cold = NativeBattleCinematicActorVertexColor20.UpdateColors(restored, State, Colors, 101, 64, 0, 0, new(0, 0, 0));
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(direct)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(cold)));
        Assert.False(NativeBattleCinematicActorVertexColor20.ReadObjectColors(cold, State, Colors, 0, 2).HighKnown[0]);
        Assert.True(NativeBattleCinematicActorVertexColor20.ReadObjectColors(cold, State, Colors, 0, 2).HighKnown[1]);
    }

    private static NativeBattleWaterExecutionFrame Fixture(ushort count)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = PutWord(frame, State, count);
        frame = PutDword(frame, State + 4, Distances);
        frame = PutDword(frame, State + 12, Palette);
        for (int index = 0; index < 256; index++)
        {
            int offset = 0x1200 + index * 4;
            frame = frame with { Arena = frame.Arena.Write(offset, new byte[] { 32, 64, 128 }).MarkUnknown(offset + 3, 1) };
        }
        return frame;
    }

    private static NativeBattleRawMesh Triangle()
    {
        byte[] face = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(4), 2);
        return new([new(new(0, 0, 0), 0), new(new(1, 0, 0), 0), new(new(0, 1, 0), 0)], [new(false, face.ToImmutableArray())]);
    }

    private static NativeBattleWaterExecutionFrame PutWord(NativeBattleWaterExecutionFrame frame, uint address, ushort value) => frame with { Arena = frame.Arena.Write(checked((int)(address - Base)), BitConverter.GetBytes(value)) };
    private static NativeBattleWaterExecutionFrame PutDword(NativeBattleWaterExecutionFrame frame, uint address, uint value) => frame with { Arena = frame.Arena.Write(checked((int)(address - Base)), BitConverter.GetBytes(value)) };
}
