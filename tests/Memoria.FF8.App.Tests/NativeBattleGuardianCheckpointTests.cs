using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGuardianCheckpointTests
{
    [Fact]
    public void Resume_the_named_guardian_checkpoint_for_one_graphical_callback()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_PREFIX_SNAPSHOT");
        Assert.SkipUnless(path is not null, "A concrete standalone GF checkpoint is required.");
        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllText(path!))!;
        BattleSession battle = BattleSession.Restore(snapshot);
        Assert.Equal((ushort)116, battle.NativeGraphics!.OriginalEffect!.Binding.Effect);
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(battle.Capture()));
        BattleSession clone = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
        string first = Advance(battle);
        string second = Advance(clone);
        Assert.Equal(first, second);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Capture()));
        Console.WriteLine($"Saved frame={snapshot.Frame}; callback={snapshot.NativeGraphics!.Callback}; one-callback result={first}");
    }

    private static string Advance(BattleSession battle)
    {
        try
        {
            battle.AdvanceNativeGraphicsPresentation(TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30));
            return "continued";
        }
        catch (NotSupportedException exception)
        {
            //A named diagnostic may intentionally precede a strict unsupported contract. Report it; never replay earlier frames.
            return exception.Message;
        }
    }
}
