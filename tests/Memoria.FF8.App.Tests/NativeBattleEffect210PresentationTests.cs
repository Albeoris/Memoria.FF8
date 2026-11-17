namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect210PresentationTests
{
    [Fact]
    public void Archived_effect210_frame_records_its_selected_effect_and_mesh_bank()
    {
        string? priorPath = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT210_OBSERVATION_PRIOR");
        string? currentPath = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT210_OBSERVATION_CURRENT");
        Assert.SkipUnless(priorPath is not null && currentPath is not null, "Select the adjacent archived Effect210 frames.");
        var (_, previous, _) = NativeBattleFixtureInspection.ReadArchivedEffectFrame(priorPath!, 210);
        var (_, current, primitive) = NativeBattleFixtureInspection.ReadArchivedEffectFrame(currentPath!, 210);
        Assert.Equal(previous + 1, current);
        Assert.Contains(primitive.LastCommands, command => command.Command.Packet.Bank == "off_B6D070" && command.Command.Opcode is >= 0x20 and <= 0x3F);
    }
}
