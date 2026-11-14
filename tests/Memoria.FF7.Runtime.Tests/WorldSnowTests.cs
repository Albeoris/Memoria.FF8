using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class WorldSnowTests
{
    public static void Run()
    {
        Check(Ff7WorldVisibility.ResolveSnowBlock(0, 0) == 0 && Ff7WorldVisibility.ResolveSnowBlock(35, 0) == 1 && Ff7WorldVisibility.ResolveSnowBlock(0, 27) == 2 && Ff7WorldVisibility.ResolveSnowBlock(35, 27) == 3, "Snow-world terrain must select its native four blocks across both world seams.");
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(game))
        {
            Console.WriteLine("SKIP original snow world: configure MEMORIA_FF7_GAME_DIR.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(game);
        using Ff7Session names = Ff7Session.NewGame(installation);
        byte[] persistent = new byte[1280];
        persistent[29] = 255;
        using Ff7WorldRuntime original = new(installation, persistent, 0x3C, new byte[] { 0, 1, 2 }, worldId: 3);
        original.ExpandDialogueToken = token => Ff7DialogueCursor.ExpandToken(token, names.Progress, [0, 1, 2]);
        for (int callback = 0; callback < 700; callback++)
        {
            if (original.Process.Dialogue is { AwaitingInput: true, Completed: false } dialogue)
            {
                original.CompleteDialogue(dialogue.RequestId, dialogue.Selected);
            }
            original.Advance();
            if (original.Process.MainState == 1 && original.MovementEnabled && original.Process.FadeDisplayedAlpha == 0)
            {
                break;
            }
        }
        Ff7WorldProcess ready = original.Process;
        Check(ready.WorldId == 3 && ready.MovementEnabled && ready.Entities.Single(entity => entity.Model == ready.ControlledModel).TerrainResolved && ready.Snow is not null, $"The original snow-world entry must initialize native terrain and wind owners: tick={ready.Tick}, movement={ready.MovementEnabled}, main={ready.MainState}, snow={ready.Snow}, player={ready.Entities.Single(entity => entity.Model == ready.ControlledModel)}.");
        using Ff7WorldRuntime cold = new(installation, original.Machine.CopyPersistent(), 0x3C, new byte[] { 0, 1, 2 }, worldId: 3);
        cold.ExpandDialogueToken = original.ExpandDialogueToken;
        cold.Restore(Cold(ready));
        int view = ready.ViewMode;
        original.SetButtons(0x21, 0x21);
        cold.SetButtons(0x21, 0x21);
        Advance(original, cold);
        Check(original.Process.Snow?.PoleIndex == 1 && original.Process.Entities.Any(entity => entity.Model == 21) && original.Process.ViewMode == view, "Confirm must plant the original snow marker while the camera toggle remains disabled.");
        original.SetButtons(0, 0);
        cold.SetButtons(0, 0);
        original.SetMovement(0, -1, true);
        cold.SetMovement(0, -1, true);
        for (int callback = 0; callback < 12; callback++)
        {
            Advance(original, cold);
        }
        Check(original.Process.Snow!.Countdown < ready.Snow!.Countdown && original.Process.Entities.Single(entity => entity.Model == original.ControlledModel).Z != ready.Entities.Single(entity => entity.Model == ready.ControlledModel).Z, "Snow-world ordinary movement must advance its native wind countdown and original terrain coordinates.");
        Ff7WorldProcess windy = ready with { Snow = new(1, 64, 64, 16, 283), Input = ready.Input! with { Vertical = -1 } };
        original.Restore(windy);
        cold.Restore(Cold(windy));
        int heading = windy.Input!.CameraHeadingTarget ?? windy.Camera!.Heading;
        Advance(original, cold);
        Check(original.Process.Snow is { Countdown: 63 } && original.Process.Input!.CameraHeadingTarget == (heading + 16 & 4095), "The original moving wind boundary must capture facing and turn the camera target by16.");
        Ff7WorldSnowLayerState firstLayer = original.Process.Snow!.Layers!.Single(layer => layer.Layer == 1);
        Check(firstLayer is { Brightness: 16, Delta: 16, Lifetime: 99, DisplayedBrightness: 0, DisplayedU: 0, DisplayedV: 0, Displayed: true }, "Starting snow must draw its pre-update zero brightness and UV while advancing its native fade and lifetime.");
        original.SetMovement(0, 0, true);
        cold.SetMovement(0, 0, true);
        original.SetButtons(32, 32);
        cold.SetButtons(32, 32);
        Advance(original, cold);
        Check(original.Process.Snow is { Countdown: 62, PoleIndex: 1 }, "The native storm must continue while idle and reject marker placement during wind.");
        Ff7WorldSnowLayerState nextLayer = original.Process.Snow!.Layers!.Single(layer => layer.Layer == 1);
        Check(nextLayer.Brightness == 32 && nextLayer.DisplayedBrightness == 16 && nextLayer.DisplayedU == firstLayer.U && nextLayer.DisplayedV == firstLayer.V, "Snow layer display must retain the original one-callback brightness and UV publication order.");
        original.SetButtons(0, 0);
        cold.SetButtons(0, 0);
        for (int callback = 0; callback < 98; callback++)
        {
            Advance(original, cold);
        }
        Check(original.Process.Snow!.Layers!.All(layer => layer is { Brightness: 128, Delta: -16, Lifetime: 0, DisplayedBrightness: 128, Displayed: true }), "The native100-callback snow lifetime must schedule its fade-out after drawing the current brightness.");
        for (int callback = 0; callback < 8; callback++)
        {
            Advance(original, cold);
        }
        Check(original.Process.Snow!.Layers!.All(layer => layer is { Brightness: 0, Delta: 0, Displayed: false }), "Native snow layers must stop drawing exactly when post-update brightness reaches zero.");
        Console.WriteLine("PASS original snow-world entry, four-block terrain, wind, marker input and cold continuation");
    }

    private static Ff7WorldProcess Cold(Ff7WorldProcess state)
    {
        return JsonSerializer.Deserialize<Ff7WorldProcess>(JsonSerializer.Serialize(state))!;
    }

    private static void Advance(Ff7WorldRuntime original, Ff7WorldRuntime cold)
    {
        original.Advance();
        cold.Advance();
        Check(JsonSerializer.Serialize(original.Process) == JsonSerializer.Serialize(cold.Process), "Snow-world callbacks must preserve exact cold wind, random, marker, terrain and script state.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
