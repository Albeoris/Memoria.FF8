using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;

internal static class NativeBattleDestroyedTests
{
    public static void Run()
    {
        Ff7BattleDestroyedState state = Ff7BattleDestroyedEffect.Start(0x10);
        byte[] kernel = new byte[3988];
        Ff7BattleRng random = new(kernel, new(new byte[8], CrtSeed: 1));
        int calls = 0;
        int packets = 0;
        List<int> impacts = [];
        for (int frame = 0; !state.Completed && frame < 40; frame++)
        {
            Ff7BattleDestroyedState saved = JsonSerializer.Deserialize<Ff7BattleDestroyedState>(JsonSerializer.Serialize(state))!;
            Ff7BattleRng coldRandom = new(kernel, random.Capture());
            Ff7BattleDestroyedUpdate update = Ff7BattleDestroyedEffect.Advance(state, _ => 5, Position, NextRandom);
            Ff7BattleDestroyedUpdate cold = Ff7BattleDestroyedEffect.Advance(saved, _ => 5, Position, coldRandom.NextCrt);
            Check(JsonSerializer.Serialize(update) == JsonSerializer.Serialize(cold) && JsonSerializer.Serialize(random.Capture()) == JsonSerializer.Serialize(coldRandom.Capture()), "Destroyed changed its native callbacks or CRT continuation after restore.");
            state = update.State;
            packets += state.Sprites?.Count ?? 0;
            if (update.ImpactTargets != 0)
            {
                Check(update.ImpactTargets == 0x10, "Destroyed changed its physical impact target.");
                impacts.Add(frame);
            }
            if (frame == 0)
            {
                Check(state.Sprites is [{ Position: { X: -33, Y: 114, Z: -400 }, Scale: 8929, Variant: 0 }], "Destroyed's first original random bone and sprite fields changed.");
            }
            if (frame == 1)
            {
                Check(state.Sprites![1] is { Position: { X: 258, Y: -98, Z: -436 }, Scale: 7753, Variant: 1 }, "Destroyed's second original sprite fields changed.");
            }
            if (frame == 21)
            {
                Check(state.Sprites![^1] is { Position: { X: 174, Y: -169, Z: -448 }, Scale: 8918, Variant: 0 }, "Destroyed's final original sprite fields changed.");
            }
            Check(state.ScreenBlue == (frame < 30 ? (frame & 1) * 32 + 32 : 0), "Destroyed's native full-screen pulse receipt changed.");
        }
        Check(state.Frame == 33 && calls == 132 && packets == 264 && impacts.SequenceEqual(new[] { 29 }), "Destroyed changed its original retirement, impact, packet count or random consumption.");

        int NextRandom()
        {
            calls++;
            return random.NextCrt();
        }

        static Vector3 Position(int slot, int bone)
        {
            Check(slot == 4, "Destroyed sampled another target actor.");
            return new(bone * 100, -bone * 20, -300);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
