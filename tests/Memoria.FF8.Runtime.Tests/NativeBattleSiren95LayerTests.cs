using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSiren95LayerTests
{
    [Fact]
    public void Fade_layer_uses_real_701970_remaining_and_applies_the_two_distinct_fade_orders()
    {
        var fixture = Fixture(0x73EF60, 300);
        var frame = fixture.Frame;
        var first = NativeBattleSiren95LayerProgrammes.Advance(frame, fixture.Address, fixture.Source, Draw);
        Assert.Equal((ushort)127, Read(first.Frame, fixture.Address).ReadWord(0x8C));
        Assert.Equal(1, Frame(first.Frame, fixture.Address));
        frame = Put(first.Frame, fixture.Address, Read(first.Frame, fixture.Address).WriteByte(0x29, 2).WriteDword(0x7C, 0x7F0));
        uint director = frame.Memory.Siren95!.DirectorAddress1533010!.Value;
        frame = frame with { Memory = frame.Memory with { Siren95 = frame.Memory.Siren95.Write(director + 0x40, BitConverter.GetBytes((short)8)) } };
        int fadeAtDraw = -1;
        NativeBattleSiren95ProgramAdvance Observe(NativeBattleSiren95Frame current, uint pointer)
        {
            fadeAtDraw = unchecked((int)Read(current, pointer).ReadDword(0x7C));
            return Draw(current, pointer);
        }
        var result = NativeBattleSiren95LayerProgrammes.Advance(frame, fixture.Address, fixture.Source, Observe);
        Assert.Equal(2048, fadeAtDraw);
        Assert.Equal((byte)3, Read(result.Frame, fixture.Address).ReadByte(0x29));
        result = NativeBattleSiren95LayerProgrammes.Advance(result.Frame, fixture.Address, fixture.Source, Observe);
        Assert.Equal(2048, fadeAtDraw);
        Assert.Equal(2304u, Read(result.Frame, fixture.Address).ReadDword(0x7C));
        var shortFixture = Fixture(0x73EF60, 2);
        frame = NativeBattleSiren95LayerProgrammes.Advance(shortFixture.Frame, shortFixture.Address, shortFixture.Source, Draw).Frame;
        Assert.Equal(1, Frame(frame, shortFixture.Address));
        var ended = NativeBattleSiren95LayerProgrammes.Advance(frame, shortFixture.Address, shortFixture.Source, Draw);
        Assert.True(ended.Complete);
        Assert.Equal(2, Frame(ended.Frame, shortFixture.Address));
    }

    [Fact]
    public void Restarting_layer_uses_signed_task_clock_and_reinitializes_at_137_without_drawing_that_tick()
    {
        var fixture = Fixture(0x73F6A0, 2);
        var frame = NativeBattleSiren95LayerProgrammes.Advance(fixture.Frame, fixture.Address, fixture.Source, (_, _) => throw new InvalidOperationException()).Frame;
        frame = Put(frame, fixture.Address, Read(frame, fixture.Address).WriteByte(0x29, 3).WriteWord(0x24, 136));
        frame = NativeBattleSiren95LayerProgrammes.Advance(frame, fixture.Address, fixture.Source, (_, _) => throw new InvalidOperationException()).Frame;
        Assert.Equal((byte)3, Read(frame, fixture.Address).ReadByte(0x29));
        string saved = JsonSerializer.Serialize(frame);
        var result = NativeBattleSiren95LayerProgrammes.Advance(frame, fixture.Address, fixture.Source, (_, _) => throw new InvalidOperationException());
        Assert.Equal((byte)4, Read(result.Frame, fixture.Address).ReadByte(0x29));
        Assert.Equal(0, Frame(result.Frame, fixture.Address));
        var restored = NativeBattleSiren95LayerProgrammes.Advance(JsonSerializer.Deserialize<NativeBattleSiren95Frame>(saved)!, fixture.Address, fixture.Source, (_, _) => throw new InvalidOperationException());
        Assert.Equal(JsonSerializer.Serialize(result.Frame), JsonSerializer.Serialize(restored.Frame));
    }

    [Theory]
    [InlineData(0x73F250u, 50, 1536, 0xFC00)]
    [InlineData(0x73F370u, 80, 1536, 0xFC00)]
    [InlineData(0x73F500u, 130, 4096, 0xF800)]
    public void Camera_facing_layers_initialize_exact_scales_and_wait_signed_distinct_delays(uint callback, short delay, uint scale, ushort y)
    {
        var fixture = Fixture(callback, 300);
        NativeBattleSiren95Frame frame = NativeBattleSiren95LayerProgrammes.Advance(fixture.Frame, fixture.Address, fixture.Source, (_, _) => throw new InvalidOperationException()).Frame;
        Assert.Equal(scale, Read(frame, fixture.Address).ReadDword(0x50));
        Assert.Equal(y, Read(frame, fixture.Address).ReadWord(0x1E));
        Assert.Equal((ushort)0xCC0, Read(frame, fixture.Address).ReadWord(0x20));
        Assert.Equal(unchecked((ushort)-128), Read(frame, fixture.Address).ReadWord(0x88));
        frame = Put(frame, fixture.Address, Read(frame, fixture.Address).WriteWord(0x24, unchecked((ushort)(delay - 1))));
        frame = NativeBattleSiren95LayerProgrammes.Advance(frame, fixture.Address, fixture.Source, (_, _) => throw new InvalidOperationException()).Frame;
        Assert.Equal((byte)1, Read(frame, fixture.Address).ReadByte(0x29));
        var step = NativeBattleSiren95LayerProgrammes.Advance(frame, fixture.Address, fixture.Source, (_, _) => throw new InvalidOperationException());
        Assert.Equal((byte)2, Read(step.Frame, fixture.Address).ReadByte(0x29));
    }

    [Theory]
    [InlineData(0x73F250u, 5, 0, 1536, 0xFC00)]
    [InlineData(0x73F370u, 5, 1, 4096, 0xF800)]
    [InlineData(0x73F500u, 6, 1, 8192, 0xFA00)]
    public void Facing_transition_preserves_native_draw_order_and_raw_director_padding(uint callback, short stage, int expectedDraws, uint scale, ushort y)
    {
        var fixture = Fixture(callback, 300);
        var frame = NativeBattleSiren95LayerProgrammes.Advance(fixture.Frame, fixture.Address, fixture.Source, (_, _) => throw new InvalidOperationException()).Frame;
        uint director = frame.Memory.Siren95!.DirectorAddress1533010!.Value;
        frame = frame with { Memory = frame.Memory with { Siren95 = frame.Memory.Siren95!.Write(director + 0x40, BitConverter.GetBytes(stage)).WriteDword(director + 8, 0x12340056).WriteDword(director + 12, 0xABCD0789) } };
        frame = Put(frame, fixture.Address, Read(frame, fixture.Address).WriteByte(0x29, 2).WriteWord(0x62, 1234));
        int draws = 0;
        var step = NativeBattleSiren95LayerProgrammes.Advance(frame, fixture.Address, fixture.Source, (current, address) =>
        {
            var task = Read(current, address);
            Assert.Equal(scale, task.ReadDword(0x50));
            Assert.Equal(y, task.ReadWord(0x1E));
            if (callback == 0x73F500)
            {
                Assert.Equal((ushort)0x56, task.ReadWord(0x1C));
                Assert.Equal(0xABCD0789u, task.ReadDword(0x20));
            }
            draws++;
            return new(current, 0);
        }, NativeBattleCuragaMatrices.Identity8DD770);
        Assert.Equal(expectedDraws, draws);
        Assert.Equal((ushort)0, Read(step.Frame, fixture.Address).ReadWord(0x62));
        Assert.Equal(callback == 0x73F250, step.Complete);
        Assert.Equal((byte)3, Read(step.Frame, fixture.Address).ReadByte(0x29));
    }

    [Fact]
    public void Final_target_layer_reads_its_F1_anchor_once_and_draws_the_completion_tick()
    {
        var fixture = Fixture(0x73F800, 2);
        var frame = Put(fixture.Frame, fixture.Address, Read(fixture.Frame, fixture.Address).WriteWord(0x22, 0xABCD));
        int anchors = 0;
        int draws = 0;
        NativeBattleVector Anchor(byte actor)
        {
            Assert.Equal((byte)3, actor);
            anchors++;
            return new(-123, 456, -789);
        }
        NativeBattleSiren95ProgramAdvance Render(NativeBattleSiren95Frame current, uint address)
        {
            var task = Read(current, address);
            Assert.Equal(unchecked((ushort)-123), task.ReadWord(0x1C));
            Assert.Equal((ushort)456, task.ReadWord(0x1E));
            Assert.Equal(unchecked((ushort)-789), task.ReadWord(0x20));
            Assert.Equal((ushort)0xABCD, task.ReadWord(0x22));
            Assert.Equal(4096u, task.ReadDword(0x50));
            draws++;
            return new(current, draws == 1 ? 1 : 0);
        }
        var step = NativeBattleSiren95LayerProgrammes.Advance(frame, fixture.Address, fixture.Source, Render, readTargetAnchor: Anchor);
        Assert.False(step.Complete);
        Assert.Equal(1, draws);
        string saved = JsonSerializer.Serialize(step.Frame);
        step = NativeBattleSiren95LayerProgrammes.Advance(step.Frame, fixture.Address, fixture.Source, Render, readTargetAnchor: Anchor);
        Assert.True(step.Complete);
        Assert.Equal(2, draws);
        Assert.Equal(1, anchors);
        draws = 1;
        var restored = NativeBattleSiren95LayerProgrammes.Advance(JsonSerializer.Deserialize<NativeBattleSiren95Frame>(saved)!, fixture.Address, fixture.Source, Render, readTargetAnchor: Anchor);
        Assert.Equal(JsonSerializer.Serialize(step), JsonSerializer.Serialize(restored));
    }

    private static NativeBattleSiren95ProgramAdvance Draw(NativeBattleSiren95Frame current, uint address)
    {
        var task = Read(current, address);
        var source = Source(task.ReadDword(0x74), task.ReadDword(0x74) == 0x2000 ? 2 : 300);
        return NativeBattleSiren95ProgramDrawing.AdvanceWithResult73C280(current, address, source, NativeBattleCuragaMatrices.Identity8DD770(), new(null, 216, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270, new((frame, _, _) => frame));
    }
    private static (NativeBattleSiren95Frame Frame, uint Address, NativeBattleGfAnimationSource Source) Fixture(uint callback, int duration)
    {
        var fixture = NativeBattleSiren95ControllerTests.Fixture();
        var appended = NativeBattleSiren95Actors.AppendTask(fixture.Frame, 0x258A010, callback, 0x534, NativeBattleSiren95Controller.MainBank, fixture.Inputs.Packets);
        var task = Read(appended.Frame, appended.Address).WriteDword(0x74, duration == 2 ? 0x2000u : 0x1000u).WriteDword(0x78, 8);
        return (Put(appended.Frame, appended.Address, task), appended.Address, Source(duration == 2 ? 0x2000u : 0x1000u, duration));
    }
    private static NativeBattleGfAnimationSource Source(uint address, int duration)
    {
        byte[] bytes = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 8);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), duration);
        return new(address, bytes.ToImmutableArray());
    }
    private static int Frame(NativeBattleSiren95Frame frame, uint address) => BinaryPrimitives.ReadInt32LittleEndian(Read(frame, address).Payload.AsSpan().Slice(0x98 - 12));
    private static NativeBattleModulePoolTask Read(NativeBattleSiren95Frame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
    private static NativeBattleSiren95Frame Put(NativeBattleSiren95Frame frame, uint address, NativeBattleModulePoolTask task) => frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
}
