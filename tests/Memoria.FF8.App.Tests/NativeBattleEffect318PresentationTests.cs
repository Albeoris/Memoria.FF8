namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect318PresentationTests
{
    [Fact]
    public void Archived_effect318_frames_record_the_body_renderer_and_consecutive_callbacks()
    {
        string? priorPath = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT318_OBSERVATION_PRIOR");
        string? currentPath = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT318_OBSERVATION_CURRENT");
        Assert.SkipUnless(priorPath is not null && currentPath is not null, "Select the consecutive archived Effect318 body frames.");
        var (_, previous, _) = NativeBattleFixtureInspection.ReadArchivedEffectFrame(priorPath!, 318);
        var (archive, current, primitive) = NativeBattleFixtureInspection.ReadArchivedEffectFrame(currentPath!, 318);
        Assert.Equal(previous + 1, current);
        var pools = archive.GetProperty("NativeGraphics").GetProperty("OriginalEffect").GetProperty("Pools");
        Assert.Contains(pools.EnumerateArray().SelectMany(pool => pool.GetProperty("Slots").EnumerateArray()), slot =>
            (slot.GetProperty("Flags").GetInt32() & 1) != 0 && slot.GetProperty("Callback").GetUInt32() == 0x65F2B0);
        Assert.Contains(primitive.LastCommands, command => command.Command.Opcode is >= 0x20 and <= 0x3F);
    }
}
