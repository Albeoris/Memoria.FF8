using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGuardianRibbonVisualTests
{
    [Fact]
    public void Named_completed_GF_ribbon_frame_changes_actual_pixels_when_only_its_common_GT4_packets_are_removed()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_RIBBON_VISUAL_SNAPSHOT");
        Assert.SkipUnless(path is not null, "A concrete completed native GF ribbon frame is required.");
        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(path!))!;
        Assert.Equal((ushort)116, snapshot.NativeGraphics!.OriginalEffect!.Binding.Effect);
        NativeBattlePrimitiveFrame frame = snapshot.NativeGraphics.PrimitiveFrame!;
        bool Ribbon(NativeBattleLogicalResolvedCommand command) => command.Command.Packet.Bank is "off_B6D070" or "off_B6D094" && (command.Command.Opcode & 0xFC) == 0x3C;
        int ribbonCommands = frame.LastCommands.Count(command => command.Pass > 0 && Ribbon(command));
        Assert.True(ribbonCommands > 0);
        BattleSession original = BattleSession.Restore(snapshot);
        BattleSnapshot without = snapshot with { NativeGraphics = snapshot.NativeGraphics with { PrimitiveFrame = frame with { LastCommands = frame.LastCommands.Where(command => !Ribbon(command)).ToImmutableArray() } } };
        BattleSession control = BattleSession.Restore(without);
        using BattleAssets assets = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        BattleScenePresentation scene = new(assets, snapshot.Definition, true);
        byte[] full = scene.Render(original, 320, 224).ToArray();
        byte[] removed = scene.Render(control, 320, 224);
        int changed = Enumerable.Range(0, 320 * 224).Count(index => !full.AsSpan(4 * index, 3).SequenceEqual(removed.AsSpan(4 * index, 3)));
        Assert.True(changed > 0, "The reached native ribbon packets must make a measurable visible contribution in the named frame.");
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(original.Capture()));
        Assert.Equal(JsonSerializer.Serialize(without), JsonSerializer.Serialize(control.Capture()));
        Console.WriteLine($"Native ribbon visual control at frame{snapshot.Frame}/callback{snapshot.NativeGraphics.Callback}: removed{ribbonCommands} resolved common GT4 draws, changed{changed} RGB pixels; neither simulation advanced.");
    }
}
