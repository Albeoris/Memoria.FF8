using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTonberry90ScreenSpriteTests
{
    [Fact]
    public void Direct_screen_matrix_scales_identity_and_task_position_without_camera_view_transform()
    {
        NativeBattleModulePoolTask task = new(1, 0, null, 0x766FB0, new byte[0x7C - 12].ToImmutableArray());
        task = task.WriteWord(0x1C, unchecked((ushort)-56)).WriteWord(0x1E, unchecked((ushort)-88)).WriteWord(0x20, 1000).WriteWord(0x56, 0xFF00);
        NativeBattleActionMatrix matrix = NativeBattleTonberry90ScreenSprites.Matrix766830(task);
        Assert.Equal(new short[] { 256, 0, 0, 0, 256, 0, 0, 0, 256 }, matrix.Rotation);
        Assert.Equal(-56, matrix.X);
        Assert.Equal(-88, matrix.Y);
        Assert.Equal(1000, matrix.Z);
        ImmutableArray<byte> source = NativeBattleCuragaProjectedSpritesTests.Resource(false, NativeBattleCuragaProjectedSpritesTests.Quad());
        NativeBattleCameraProjection projection = new(null, 1000, 160, 108);
        NativeBattleGfGeometryBatch expected = NativeBattleTonberry90ScreenSprites.Draw766830(task, source, projection, 0);
        Assert.NotNull(Assert.Single(expected.Attempts).Primitive);
        NativeBattleActionMatrix unrelated = new([0, 0, 4096, 0, 4096, 0, -4096, 0, 0], 70000, -90000, -12000, 4096);
        NativeBattleGfGeometryBatch changedView = NativeBattleTonberry90ScreenSprites.Draw766830(task, source, projection with { View = unrelated }, 0);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(changedView));
        Assert.Empty(NativeBattleTonberry90ScreenSprites.Draw766830(task.WriteByte(0x26, 4), [], projection, 0).Attempts);
    }

    [Fact]
    public void First_screen_sprite_reads_actual_programme_focal_moves_with_signed_velocity_then_waits_for_phase_seven()
    {
        var fixture = Fixture(0x766FB0);
        uint camera = fixture.Frame.Memory.Tonberry90!.CameraAddress1547510!.Value;
        uint director = fixture.Frame.Memory.Tonberry90.DirectorAddress1547168!.Value;
        NativeBattleTonberry90Frame frame = fixture.Frame with { Memory = fixture.Frame.Memory with { Tonberry90 = fixture.Frame.Memory.Tonberry90!.Write(camera + 0xE0, BitConverter.GetBytes((ushort)900)).Write(director + 0x40, BitConverter.GetBytes((ushort)6)) } };
        NativeBattleTonberry90TaskStep step = NativeBattleTonberry90Creature.AdvanceScreenSprite(frame, fixture.Address, (current, _) => current);
        NativeBattleModulePoolTask task = Read(step.Frame, fixture.Address);
        Assert.Equal((ushort)900, task.ReadWord(0x20));
        Assert.Equal((ushort)0xFF00, task.ReadWord(0x56));
        Assert.Equal(0x1547258u, task.ReadDword(0x4C));
        Assert.Equal((byte)4, (byte)(task.ReadByte(0x26) & 4));
        step = NativeBattleTonberry90Creature.AdvanceScreenSprite(step.Frame, fixture.Address, (current, _) => current);
        Assert.Equal((byte)2, Read(step.Frame, fixture.Address).ReadByte(0x29));
        step = NativeBattleTonberry90Creature.AdvanceScreenSprite(step.Frame, fixture.Address, (current, _) => current);
        Assert.Equal(unchecked((ushort)-87), Read(step.Frame, fixture.Address).ReadWord(0x1E));
        Assert.False(step.Complete);
        frame = step.Frame with { Memory = step.Frame.Memory with { Tonberry90 = step.Frame.Memory.Tonberry90!.Write(director + 0x40, BitConverter.GetBytes((ushort)7)) } };
        step = NativeBattleTonberry90Creature.AdvanceScreenSprite(frame, fixture.Address, (current, _) => current);
        Assert.True(step.Complete);
        Assert.Equal(unchecked((ushort)-86), Read(step.Frame, fixture.Address).ReadWord(0x1E));
    }

    [Fact]
    public void Second_screen_sprite_repeats_frames_three_four_four_times_before_finishing_its_last_frame()
    {
        var fixture = Fixture(0x7667B0);
        NativeBattleModulePoolTask task = Read(fixture.Frame, fixture.Address).WriteByte(0x29, 2).WriteWord(0x50, 4).WriteWord(0x52, 5).WriteWord(0x74, 4);
        NativeBattleTonberry90Frame frame = Write(fixture.Frame, fixture.Address, task);
        for (int tick = 0; tick < 7; tick++)
        {
            NativeBattleTonberry90TaskStep step = NativeBattleTonberry90Creature.AdvanceScreenSprite(frame, fixture.Address, (current, _) => current);
            Assert.False(step.Complete);
            frame = step.Frame;
        }
        Assert.Equal((byte)3, Read(frame, fixture.Address).ReadByte(0x29));
        Assert.Equal((ushort)0, Read(frame, fixture.Address).ReadWord(0x74));
        for (int tick = 0; tick < 3; tick++)
        {
            NativeBattleTonberry90TaskStep step = NativeBattleTonberry90Creature.AdvanceScreenSprite(frame, fixture.Address, (current, _) => current);
            Assert.Equal(tick == 2, step.Complete);
            frame = step.Frame;
        }
        Assert.Equal((byte)4, Read(frame, fixture.Address).ReadByte(0x29));
        Assert.Equal((ushort)5, Read(frame, fixture.Address).ReadWord(0x50));
    }

    private static (NativeBattleTonberry90Frame Frame, uint Address) Fixture(uint callback)
    {
        var fixture = NativeBattleTonberry90ControllerTests.Fixture();
        int pool = Enumerable.Range(0, fixture.Frame.Effect.Pools.Length).Single(index => fixture.Frame.Effect.Pools[index].DescriptorAddress == 0x259F098);
        NativeBattleEffectHierarchyAppend appended = new NativeBattleEffectHierarchy(fixture.Frame.Effect.Pools).Append8DC540(pool, callback, 0x7C, NativeBattleTonberry90Controller.MainBank, (_, _, _) => new(0, 3));
        return (fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = appended.Hierarchy.Pools } }, appended.Address);
    }

    private static NativeBattleModulePoolTask Read(NativeBattleTonberry90Frame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
    private static NativeBattleTonberry90Frame Write(NativeBattleTonberry90Frame frame, uint address, NativeBattleModulePoolTask task) => frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
}
