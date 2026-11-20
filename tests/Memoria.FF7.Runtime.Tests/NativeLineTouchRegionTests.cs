using Memoria.FF7.Runtime;

internal static class NativeLineTouchRegionTests
{
    internal static void Run()
    {
        Ff7Actor player = new() { Slot = 0, Entity = 0, Model = 0, PlayerControlled = true, X4096 = 50 << 12, Y4096 = -11 << 12, CollisionRadius = 10 };
        Ff7Line line = new(0, 1, 0, 0, 0, 100, 0, 0);
        Ff7Actor inside = player with { Y4096 = -9 << 12 };
        int previews = 0;
        Ff7AutoplayInput? ready = Ff7AutoplayDriver.ChooseNativeLineTouchInput(inside, line, _ =>
        {
            previews++;
            throw new InvalidOperationException("A real contact must wait for native publication without searching a sampled catalog.");
        }, (_, _) => false, true);
        Check(ready == new Ff7AutoplayInput() && previews == 0 && !line.Touch, "Current geometric contact synthesized native flags or required a sampled destination.");
        Ff7NativeMoveResult Preview(Ff7AutoplayInput input) => new(input is { Horizontal: 0, Vertical: -1 } ? inside : player with { Y4096 = -12 << 12 }, false, true, []);
        Ff7AutoplayInput? selected = Ff7AutoplayDriver.ChooseNativeLineTouchInput(player, line, Preview, (_, _) => true, true);
        Check(selected is { Horizontal: 0, Vertical: -1, Run: true }, "The actual native contact region lost to an overshooting sampled point.");
        Check(Ff7AutoplayDriver.ChooseNativeLineTouchInput(player, line, Preview, (_, _) => false, true) is null, "A protected unrelated native boundary was bypassed to reach LINE contact.");
        foreach (Ff7NativeMoveResult rejected in new Ff7NativeMoveResult[]
        {
            new(inside, false, false, []),
            new(player with { X4096 = 110 << 12, Y4096 = 0 }, false, true, []),
            new(player with { Y4096 = -10 << 12 }, false, true, []),
            new(inside with { Z4096 = 20 << 12 }, false, true, [])
        })
        {
            Check(Ff7AutoplayDriver.ChooseNativeLineTouchInput(player, line, _ => rejected, (_, _) => true, false) is null, "An unmoved, endpoint-exterior, radius-boundary or wrong-height point became native LINE contact.");
        }
        Check(Ff7AutoplayDriver.ChooseNativeLineTouchInput(inside, line with { Enabled = 0 }, _ => throw new InvalidOperationException(), (_, _) => true, true) is null, "A disabled native LINE gained contact authority.");
        Console.WriteLine("PASS native LINE touch region, actual contact without catalog, collision and protected-boundary admission");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
