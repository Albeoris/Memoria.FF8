using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeShopAutoPlayTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_SHOP_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP native shop autoplay: configure original resources and an opening-shop standalone fixture.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream input = new(source);
        Ff7SessionSave saved = CombinedSessionCodec.Read(input).Ff7!;
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session original = Ff7Session.Restore(installation, saved);
        Check(original.Shop is { Phase: Ff7ShopPhase.Opening, Tick: 0 }, "The fixture must contain the genuine native shop opening boundary.");
        string vm = JsonSerializer.Serialize(original.Machine.Capture());
        string progress = JsonSerializer.Serialize(original.Progress with { Clock = null });
        long fieldTick = original.Host.Process.Tick;
        Ff7ClockState expected = original.Clock;
        bool countUp = (original.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 95) & 2) != 0;
        Ff7AutoPlayController controller = Create(original);
        controller.Start();
        for (int callback = 0; callback < 55; callback++)
        {
            using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(original.Capture()))!);
            Ff7AutoPlayController coldController = Create(cold);
            coldController.Start();
            controller.Advance(1);
            coldController.Advance(1);
            Check(controller.Failure is null && coldController.Failure is null, "Native shop autoplay failed before its normal completion.");
            Check(JsonSerializer.Serialize(original.Capture()) == JsonSerializer.Serialize(cold.Capture()), "A cold shop callback changed native phase, input, sound or clock ownership.");
            Check(JsonSerializer.Serialize(original.Machine.Capture()) == vm && original.Host.Process.Tick == fieldTick, "A native UI callback must not poll or advance the suspended field VM/process.");
            expected = Ff7NativeClock.Advance(expected, TimeSpan.FromSeconds(1d / 60), countUp);
        }
        Check(original.Shop is { Phase: Ff7ShopPhase.Completed, Tick: 55 } && controller.Steps == 55, "Shop autoplay must retain27opening, cancel,27closing callbacks without instant completion.");
        Check(original.Clock == expected && JsonSerializer.Serialize(original.Progress with { Clock = null }) == progress, "Headless UI time must use the existing clock accumulator without changing transactions or other progress.");
        using Ff7Session external = Ff7Session.Restore(installation, saved);
        Ff7ClockState beforeExternal = external.Clock;
        Ff7AutoPlayController hosted = new(external, _ => throw new Exception("Shop invoked field navigation."), advanceTick: () => throw new Exception("Shop advanced the field VM."), advanceShop: cancel => external.AdvanceShop(cancel));
        hosted.Start();
        for (int callback = 0; callback < 55; callback++)
        {
            hosted.Advance(1);
        }
        Check(hosted.Failure is null && external.Shop?.Phase == Ff7ShopPhase.Completed && external.Clock == beforeExternal, "An App-owned shop callback must not double-advance its independently owned real clock.");
        Check(SHA256.HashData(source).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "The native shop fixture was modified.");
    }

    private static Ff7AutoPlayController Create(Ff7Session session) => new(session, _ => throw new Exception("Shop invoked field navigation."), advanceTick: () => throw new Exception("Shop advanced the field VM."));

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
