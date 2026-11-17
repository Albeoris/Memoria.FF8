namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect319PresentationTests
{
    [Fact]
    public void Archived_effect319_frames_record_the_body_renderer_and_consecutive_callbacks()
    {
        string? priorPath = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT319_OBSERVATION_PRIOR");
        string? currentPath = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT319_OBSERVATION_CURRENT");
        Assert.SkipUnless(priorPath is not null && currentPath is not null, "Select the consecutive archived Effect319 body frames.");
        var (_, previous, _) = NativeBattleFixtureInspection.ReadArchivedEffectFrame(priorPath!, 319);
        var (archive, current, primitive) = NativeBattleFixtureInspection.ReadArchivedEffectFrame(currentPath!, 319);
        Assert.Equal(previous + 1, current);
        var pools = archive.GetProperty("NativeGraphics").GetProperty("OriginalEffect").GetProperty("Pools");
        Assert.Contains(pools.EnumerateArray().SelectMany(pool => pool.GetProperty("Slots").EnumerateArray()), slot =>
            (slot.GetProperty("Flags").GetInt32() & 1) != 0 && slot.GetProperty("Callback").GetUInt32() == 0x65F2B0);
        Assert.Contains(primitive.LastCommands, command => command.Command.Opcode is >= 0x20 and <= 0x3F);
    }
}
