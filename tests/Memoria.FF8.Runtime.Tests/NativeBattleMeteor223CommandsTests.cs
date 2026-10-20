using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMeteor223CommandsTests
{
    private const uint Arena = NativeBattleEffectNodeMemory.ArenaAddress;

    [Theory]
    [InlineData(0x8035, true)]
    [InlineData(0x80BD, false)]
    public void Relative_rectangle_uses_signed_slot_preserves_physical_ring_and_restores_next_command(ushort opcode, bool readback)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, opcode);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, Arena + 0x1000);
        frame = frame with { Core = frame.Core.Write(0x2798BF4, [255]), Arena = frame.Arena.Write(0x1000, Words(opcode, 16, unchecked((ushort)-1))).Write(0x1010, Words(3, 4, 2, 1)) };
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        int calls = 0;
        NativeBattleMeteor223TextureServices services = new(Arena + 0x6000, (current, rectangle, pointer, capture) =>
        {
            calls++;
            Assert.Equal(readback, capture);
            Assert.Equal(new NativeBattleEffectTextureRectangle(3, 4, 2, 1), rectangle);
            Assert.Equal(Arena + 0x5FF0, pointer);
            Assert.Equal(new byte[] { 0 }, current.Core.Read(0x2798BF4, 1));
            Assert.Equal(Words(3, 4, 2, 1), current.Core.Read(0x27978E8 + 15 * 8, 8));
            return current;
        });
        NativeBattleWaterExecutionFrame result = NativeBattleMeteor223Commands.Execute(frame, new(0x1000, 0x100, Arena + 0x1000), NativeBattleCinematicTextureMetadataTests.Resources(), services);
        NativeBattleWaterExecutionFrame cold = NativeBattleMeteor223Commands.Execute(restored, new(0x1000, 0x100, Arena + 0x1000), NativeBattleCinematicTextureMetadataTests.Resources(), services);
        Assert.Equal(2, calls);
        Assert.Equal(Arena + 0x1006, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(cold));
    }

    [Fact]
    public void Alternative_texture_backward_copy_preserves_first_DWORD_and_uses_actual_current_source()
    {
        NativeBattleCinematicResources resources = NativeBattleCinematicTextureMetadataTests.Resources();
        byte[] data = resources.StaticData.Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(0x20 + 22 * 8 + 4), 4);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(0x20 + 22 * 8 + 6), 1);
        resources = resources with { StaticData = resources.StaticData with { Data = [.. data] } };
        NativeBattleWaterExecutionFrame frame = NativeBattleCinematicTextureMetadataTests.Frame().WriteTemporaryWord(0x14A, 0xBB);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, Arena + 0x1000);
        frame = frame with { Arena = frame.Arena.Write(0x1000, Words(0xBB, 22, 0)).Write(0x5060, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }).Write(0x6000, new byte[] { 9, 10, 11, 12, 13, 14, 15, 16 }) };
        NativeBattleWaterExecutionFrame result = NativeBattleMeteor223Commands.Execute(frame, new(0x1000, 0x100, Arena + 0x1000), resources, new(Arena + 0x6000, (_, _, _, _) => throw new InvalidOperationException()));
        Assert.Equal(new byte[] { 9, 10, 11, 12, 5, 6, 7, 8 }, result.Arena.Read(0x6000, 8));
        Assert.Equal(Arena + 0x1006, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    [Fact]
    public void Spread_results_applies_one_per_boundary_preserves_callback_changes_and_stops_after_last()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xB8, Arena + 0x3000);
        frame = frame with { Arena = frame.Arena.Write(0x3000, Words(8)) };
        List<int> contacts = [];
        NativeBattleWaterExecutionFrame Apply(NativeBattleWaterExecutionFrame current, int index)
        {
            contacts.Add(index);
            return current with { Arena = current.Arena.Write(0x4000 + index, new byte[] { 77 }) };
        }
        frame = NativeBattleMeteor223Commands.SpreadResults(frame, 3, Apply);
        Assert.Empty(contacts);
        for (int age = 1; age <= 6; age++)
        {
            frame = NativeBattleMeteor223Commands.SpreadResults(frame, 255, Apply);
        }
        Assert.Equal(new[] { 0, 1, 2 }, contacts);
        Assert.Equal(new byte[] { 77, 77, 77 }, frame.Arena.Read(0x4000, 3));
        Assert.Equal(7u, WaterSchedulerFixture.ReadSlotDword(frame, 0, 0xBC));
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(JsonSerializer.Serialize(frame), JsonSerializer.Serialize(NativeBattleMeteor223Commands.SpreadResults(restored, 0, Apply)));
    }

    [Fact]
    public void Result_callback_changes_to_current_node_are_reread_before_native_increments()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xBC, 10);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xC0, 2);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xC4, 5);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xB8, 8);
        frame = NativeBattleMeteor223Commands.SpreadResults(frame, 0, (current, index) =>
        {
            Assert.Equal(2, index);
            current = WaterSchedulerFixture.SlotDword(current, 0, 0xC0, 4);
            return WaterSchedulerFixture.SlotDword(current, 0, 0xBC, 20);
        });
        Assert.Equal(5u, WaterSchedulerFixture.ReadSlotDword(frame, 0, 0xC0));
        Assert.Equal(21u, WaterSchedulerFixture.ReadSlotDword(frame, 0, 0xBC));
    }

    private static byte[] Words(params ushort[] values)
    {
        byte[] bytes = new byte[2 * values.Length];
        for (int index = 0; index < values.Length; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2 * index), values[index]);
        }
        return bytes;
    }
}
