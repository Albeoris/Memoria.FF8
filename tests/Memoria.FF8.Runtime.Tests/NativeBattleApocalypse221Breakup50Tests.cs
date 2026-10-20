using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleApocalypse221Breakup50Tests
{
    private const uint Basis = NativeBattleEffectNodeMemory.ArenaAddress;
    private const uint State = Basis + 0x4000;
    private const uint Record = State + 20;

    [Theory]
    [InlineData(1, 0, 0, 112, 12, 209, 4076)]
    [InlineData(1, 2, 0, 100, 10, 204, 4096)]
    [InlineData(3, 0, 0, 112, 12, 209, 4096)]
    [InlineData(3, 0, 2, 100, 10, 204, 4096)]
    public void Face_integration_retains_signed_limits_and_wrapping_rotation(int flags, int age, int secondAge, int radius, int speed, int phase, int scale)
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        frame = PutWord(frame, Record, flags);
        frame = PutWord(frame, Record + 2, age);
        frame = PutWord(frame, Record + 0xA, secondAge);
        NativeBattleWaterExecutionFrame result = NativeBattleApocalypse221Breakup50.Integrate(frame, Record);
        Assert.Equal(radius, Word(result, Record + 0xC));
        Assert.Equal(speed, Word(result, Record + 0xE));
        Assert.Equal(phase, Word(result, Record + 0x12));
        Assert.Equal(scale, Word(result, Record + 0x20));
        Assert.Equal(age + 1, Word(result, Record + 2));
        Assert.Equal((byte)4, result.Arena.Read(Offset(Record + 0x18), 1)[0]);
        Assert.Equal(frame.Random, result.Random);
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(result))!;
        Assert.Equal(JsonSerializer.Serialize(NativeBattleApocalypse221Breakup50.Integrate(result, Record)), JsonSerializer.Serialize(NativeBattleApocalypse221Breakup50.Integrate(restored, Record)));
    }

    [Fact]
    public void Callback_pause_retains_every_face_owner()
    {
        NativeBattleWaterExecutionFrame frame = Fixture().WriteTemporaryByte(0x145, 1);
        Assert.Same(frame, NativeBattleApocalypse221Breakup50.Integrate(frame, Record));
    }

    [Fact]
    public void Activation_preserves_original_sequential_reuse_of_first_angular_band_bounds()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        frame = PutWord(frame, State + 0xA, 2);
        frame = PutWord(frame, Record, 0);
        frame = PutWord(frame, Record + 0x12, 1024);
        frame = PutWord(frame, Record + 36, 0);
        frame = PutWord(frame, Record + 36 + 0xC, 100);
        frame = PutWord(frame, Record + 36 + 0x12, 1000);
        frame = frame with { Arena = frame.Arena.Write(0x204A, new byte[] { 2 }) };
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x90, 100);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x8C, 0);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x94, 500);
        frame = frame.WriteTemporaryDword(0x80, 5).WriteTemporaryDword(0x88, Basis + 0x2100);
        NativeBattleWaterExecutionFrame result = NativeBattleApocalypse221Breakup50.Activate(frame, State, 101);
        Assert.Equal(3, Word(result, Record));
        Assert.Equal(1, Word(result, Record + 36));
        Assert.Equal(0, Word(result, Record + 0xA));
        Assert.Equal(80, Word(result, Record + 0xE));
        Assert.Equal(0, Word(result, Record + 0x10));
        NativeCrtRandomState expected = frame.Random.Draw(out _);
        Assert.Equal(expected, result.Random);
    }

    [Fact]
    public void First_draw_allocates_actual_records_and_world_vertices_once_and_consumes_five_random_calls_per_face()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x74, State);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xBC, 0);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xB8, Basis + 0x3000);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x96, 4096);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x8E, 0);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x94, 300);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x8C, 0);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x98, 0);
        short[] parameters = [-7, -2, 0, 0, 10, 5, 0, 0, 0];
        for (int index = 0; index < parameters.Length; index++)
        {
            frame = PutWord(frame, Basis + 0x3000u + (uint)(index * 2), parameters[index]);
        }
        frame = PutDword(frame, Basis + 0x3808, 0x40);
        frame = PutWord(frame, Basis + 0x3842, 3);
        frame = PutDword(frame, Basis + 0x3814, 0x100);
        for (int index = 0; index < 3; index++)
        {
            uint face = Basis + 0x3844u + (uint)(index * 20);
            frame = PutWord(frame, face + 0xA, 0);
            frame = PutWord(frame, face + 0xC, 8);
            frame = PutWord(frame, face + 0xE, 16);
        }
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x4000, 0x98) };
        int transformations = 0;
        int draws = 0;
        NativeBattleApocalypse221Breakup50Services services = new((owner, selector) =>
        {
            Assert.Equal(-7, selector);
            return Basis + 0x2100;
        }, (owner, shape) =>
        {
            Assert.Equal(-2, shape);
            return Basis + 0x3800;
        }, (owner, source, destination, count) =>
        {
            transformations++;
            Assert.Equal(Basis + 0x3900, source);
            Assert.Equal(Basis + 0x4080, destination);
            Assert.Equal(3, count);
            for (uint index = 0; index < 3; index++)
            {
                owner = PutWord(owner, destination + index * 8, (int)(3 + index * 3));
                owner = PutWord(owner, destination + index * 8 + 2, (int)(6 + index * 3));
                owner = PutWord(owner, destination + index * 8 + 4, 0);
            }
            return owner;
        }, (owner, state, vertices) =>
        {
            draws++;
            Assert.Equal(State, state);
            Assert.Equal(Basis + 0x4080, vertices);
            Assert.Equal(3, Word(owner, state + 0xA));
            Assert.Equal(6, Word(owner, Record + 4));
            Assert.Equal(9, Word(owner, Record + 6));
            Assert.Equal(6, Word(owner, Record + 0xC));
            Assert.Equal(58, Word(owner, Record + 0xE));
            Assert.Equal(unchecked((ushort)-5), Word(owner, Record + 0x10));
            Assert.Throws<NotSupportedException>(() => owner.Arena.Read(Offset(Record + 0xA), 2));
            return owner;
        });
        NativeBattleWaterExecutionFrame first = NativeBattleApocalypse221Breakup50.Draw(frame, services);
        NativeCrtRandomState expected = frame.Random;
        for (int index = 0; index < 15; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, first.Random);
        Assert.Equal(Basis + 0x4098, WaterSchedulerFixture.ReadCoreDword(first, 0x2797690 + 0x74));
        NativeBattleWaterExecutionFrame next = NativeBattleApocalypse221Breakup50.Draw(JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(first))!, services);
        Assert.Equal(first.Random, next.Random);
        Assert.Equal(1, transformations);
        Assert.Equal(2, draws);
    }

    private static NativeBattleWaterExecutionFrame Fixture()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryByte(0x145, 0).WriteTemporaryDword(0x7C, 3).WriteTemporaryDword(0x80, 2).WriteTemporaryDword(0x90, 1);
        frame = PutWord(frame, State + 0xA, 1);
        frame = PutWord(frame, Record, 1);
        frame = PutWord(frame, Record + 0xC, 100);
        frame = PutWord(frame, Record + 0xE, 10);
        frame = PutWord(frame, Record + 0x10, 2);
        frame = PutWord(frame, Record + 0x12, 200);
        frame = PutWord(frame, Record + 0x14, 4);
        frame = PutWord(frame, Record + 0x16, 5);
        frame = PutWord(frame, Record + 0x20, 4096);
        frame = PutWord(frame, Record + 0x22, -20);
        return frame with { Arena = frame.Arena.Write(Offset(Record + 0x18), new byte[] { 250, 0, 0, 10, 0, 0 }) };
    }

    private static int Offset(uint address) => checked((int)(address - Basis));
    private static ushort Word(NativeBattleWaterExecutionFrame frame, uint address) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(Offset(address), 2));
    private static NativeBattleWaterExecutionFrame PutWord(NativeBattleWaterExecutionFrame frame, uint address, int value) => frame with { Arena = frame.Arena.Write(Offset(address), BitConverter.GetBytes(unchecked((ushort)value))) };
    private static NativeBattleWaterExecutionFrame PutDword(NativeBattleWaterExecutionFrame frame, uint address, uint value) => frame with { Arena = frame.Arena.Write(Offset(address), BitConverter.GetBytes(value)) };
}
