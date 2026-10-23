using System.Buffers.Binary;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleSigh46Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSigh46ParticleTests
{
    [Theory]
    [InlineData(7, 1)]
    [InlineData(8, 2)]
    public void Producer_waits_for_signed_old_age_eight_before_resetting_count_index(int age, int phase)
    {
        var fixture = Producer(1);
        var frame = Write(fixture.Frame, fixture.Address, Read(fixture.Frame, fixture.Address).WriteWord(0x24, (ushort)age).WriteWord(0x88, 123));
        var actual = AdvanceParticles835E90(frame, fixture.Address, fixture.Resources, NativeBattleSigh46LowerTests.Inputs([]));
        Assert.Equal((byte)phase, Read(actual.Frame, fixture.Address).ReadByte(0x29));
        Assert.Equal((ushort)(phase == 2 ? 0 : 123), Read(actual.Frame, fixture.Address).ReadWord(0x88));
        Assert.Equal(frame.Random, actual.Frame.Random);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(7, 1)]
    public void Each_birth_consumes_four_ordered_CRT_values_and_copies_full_packed_position(int countIndex, int count)
    {
        var fixture = Producer(2);
        var frame = Write(fixture.Frame, fixture.Address, Read(fixture.Frame, fixture.Address).WriteWord(0x88, (ushort)countIndex));
        var expected = frame.Random;
        ushort first = 0;
        ushort speed = 0;
        ushort vertical = 0;
        ushort gravity = 0;
        for (int instance = 0; instance < count; instance++)
        {
            expected = expected.Draw(out ushort a);
            expected = expected.Draw(out ushort b);
            expected = expected.Draw(out ushort c);
            expected = expected.Draw(out ushort d);
            if (instance == 0)
            {
                first = a;
                speed = b;
                vertical = c;
                gravity = d;
            }
        }
        var actual = AdvanceParticles835E90(frame, fixture.Address, fixture.Resources, NativeBattleSigh46LowerTests.Inputs([]));
        Assert.Equal(expected, actual.Frame.Random);
        var children = actual.Frame.Effect.Pools[4].Slots.Where(task => task.Callback == 0x836090 && (task.Flags & 1) != 0).ToArray();
        Assert.Equal(count, children.Length);
        var child = children[0];
        NativeBattleActionMatrix rotation = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), (short)((first % 1024 - 512) & 0xFFF));
        NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(rotation, new(0, 0, -3072));
        Assert.Equal(unchecked((short)(100 + offset.X)), Signed(child, 0x1C));
        Assert.Equal(unchecked((short)(200 + offset.Y)), Signed(child, 0x1E));
        Assert.Equal(unchecked((short)(300 + offset.Z)), Signed(child, 0x20));
        Assert.Equal((ushort)0xBEEF, child.ReadWord(0x22));
        Assert.Equal(unchecked((short)(offset.X * ((speed & 0x7FF) + 1024) / 4096)), Signed(child, 0x78));
        Assert.Equal(unchecked((short)(offset.Z * ((speed & 0x7FF) + 1024) / 4096)), Signed(child, 0x7C));
        Assert.Equal(-(vertical & 0x3FF), Signed(child, 0x7A));
        Assert.Equal(-16 - (gravity & 15), Signed(child, 0x82));
        Assert.Equal((byte)count, Read(actual.Frame, fixture.Address).ReadByte(0x28));
        Assert.Equal((ushort)(countIndex + 1), Read(actual.Frame, fixture.Address).ReadWord(0x88));
        Assert.Equal(countIndex == 7 ? (byte)3 : (byte)2, Read(actual.Frame, fixture.Address).ReadByte(0x29));
    }

    [Fact]
    public void All_eight_count_rows_emit_twenty_four_children_and_wait_for_real_child_drain()
    {
        var fixture = Producer(2);
        var frame = fixture.Frame;
        var expected = frame.Random;
        for (int index = 0; index < 8; index++)
        {
            frame = AdvanceParticles835E90(frame, fixture.Address, fixture.Resources, NativeBattleSigh46LowerTests.Inputs([])).Frame;
        }
        for (int draw = 0; draw < 24 * 4; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, frame.Random);
        Assert.Equal((byte)24, Read(frame, fixture.Address).ReadByte(0x28));
        Assert.Equal((byte)3, Read(frame, fixture.Address).ReadByte(0x29));
        Assert.Equal((ushort)8, Read(frame, fixture.Address).ReadWord(0x88));
        var waiting = AdvanceParticles835E90(frame, fixture.Address, fixture.Resources, NativeBattleSigh46LowerTests.Inputs([]));
        Assert.Equal(0, waiting.Result);
        frame = Write(waiting.Frame, fixture.Address, Read(waiting.Frame, fixture.Address).WriteByte(0x28, 0));
        var drained = AdvanceParticles835E90(frame, fixture.Address, fixture.Resources, NativeBattleSigh46LowerTests.Inputs([]));
        Assert.Equal(2, drained.Result);
        Assert.Equal(expected, drained.Frame.Random);
    }

    [Fact]
    public void Particle_initialization_consumes_one_extra_draw_and_renders_frame_zero_from_ordinary_allocator()
    {
        var fixture = NativeBattleSigh46LowerTests.Fixture(4, 0x836090, 0);
        var task = Read(fixture.Frame, fixture.Address).WriteWord(0x1C, 100).WriteWord(0x1E, 200).WriteWord(0x20, 300).MarkPayloadUnknown(0x22, 2);
        var frame = Write(fixture.Frame, fixture.Address, task);
        var expected = frame.Random.Draw(out ushort value);
        List<NativeBattleSigh46Operation> operations = [];
        var actual = AdvanceSprite836090(frame, fixture.Address, fixture.Resources, NativeBattleSigh46LowerTests.Inputs(operations));
        Assert.Equal(expected, actual.Frame.Random);
        var draw = Assert.IsType<NativeBattleSigh46DrawSprite180>(Assert.Single(operations));
        uint[] sprites = [0x15B82A4, 0x15B8438, 0x15B8BA4];
        Assert.Equal(sprites[value % 3], draw.SourceAddress);
        Assert.Equal(NativeBattleTemporaryMemory.Address, draw.TemporaryAddress);
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(Context(actual.Frame, draw.TemporaryAddress, 180).Read(4, 2)));
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(Context(actual.Frame, draw.TemporaryAddress, 180).Read(0x24, 2)));
        Assert.False(actual.Frame.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
        Assert.Equal(0x2793E58u, actual.Frame.State.ReadDword(0x26527A0));
    }

    [Theory]
    [InlineData(13, 0, 1)]
    [InlineData(14, 2, 0)]
    public void Velocity_uses_truncating_division_and_frame_fifteen_retires_after_integrating(int oldFrame, int result, int draws)
    {
        var fixture = NativeBattleSigh46LowerTests.Fixture(4, 0x836090, 1);
        var task = Read(fixture.Frame, fixture.Address).WriteWord(0x1C, 100).WriteWord(0x1E, 200).WriteWord(0x20, 300).WriteDword(0x4C, 0x15B82A4).WriteWord(0x50, (ushort)oldFrame).WriteWord(0x52, 14).WriteWord(0x78, unchecked((ushort)-17)).WriteWord(0x7A, unchecked((ushort)-15)).WriteWord(0x7C, 33).WriteWord(0x82, unchecked((ushort)-3));
        var frame = Write(fixture.Frame, fixture.Address, task);
        List<NativeBattleSigh46Operation> operations = [];
        var actual = AdvanceSprite836090(frame, fixture.Address, fixture.Resources, NativeBattleSigh46LowerTests.Inputs(operations));
        task = Read(actual.Frame, fixture.Address);
        Assert.Equal(result, actual.Result);
        Assert.Equal(draws, operations.Count);
        Assert.Equal((short)-16, Signed(task, 0x78));
        Assert.Equal((short)-18, Signed(task, 0x7A));
        Assert.Equal((short)31, Signed(task, 0x7C));
        Assert.Equal((short)99, Signed(task, 0x1C));
        Assert.Equal((short)199, Signed(task, 0x1E));
        Assert.Equal((short)301, Signed(task, 0x20));
        Assert.Equal((ushort)14, task.ReadWord(0x50));
        Assert.Equal(frame.Random, actual.Frame.Random);
    }

    [Fact]
    public void Particle_snapshot_restores_same_next_negative_velocity_and_final_frame_transition()
    {
        var fixture = NativeBattleSigh46LowerTests.Fixture(4, 0x836090, 1);
        var frame = Write(fixture.Frame, fixture.Address, Read(fixture.Frame, fixture.Address).WriteWord(0x1C, 100).WriteWord(0x1E, 200).WriteWord(0x20, 300).WriteDword(0x4C, 0x15B82A4).WriteWord(0x50, 14).WriteWord(0x52, 14).WriteWord(0x78, unchecked((ushort)-17)));
        var restored = JsonSerializer.Deserialize<NativeBattleSigh46Frame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(JsonSerializer.Serialize(AdvanceSprite836090(frame, fixture.Address, fixture.Resources, NativeBattleSigh46LowerTests.Inputs([])).Frame), JsonSerializer.Serialize(AdvanceSprite836090(restored, fixture.Address, fixture.Resources, NativeBattleSigh46LowerTests.Inputs([])).Frame));
    }

    private static (NativeBattleSigh46Frame Frame, uint Address, NativeBattleSigh46Resources Resources) Producer(byte phase)
    {
        var fixture = NativeBattleSigh46LowerTests.Fixture(4, 0x835E90, phase);
        var frame = fixture.Frame;
        var task = Read(frame, fixture.Address).WriteWord(0x1C, 100).WriteWord(0x1E, 200).WriteWord(0x20, 300).WriteWord(0x22, 0xBEEF);
        task = CopyTask(task, 0x58, PackedArena(NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), 0xABCD)), 32);
        return (Write(frame, fixture.Address, task), fixture.Address, fixture.Resources);
    }
}
