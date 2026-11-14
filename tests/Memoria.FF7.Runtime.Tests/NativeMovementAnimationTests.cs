using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeMovementAnimationTests
{
    public static void Run()
    {
        Ff7FieldProcess legacy = JsonSerializer.Deserialize<Ff7FieldProcess>("{}")!;
        legacy.Validate();
        Check(legacy.NativeIdleAnimation == 0 && legacy.NativeWalkAnimation == 1 && legacy.NativeRunAnimation == 2, "Older saves must recover the native field-initial animation indices without replay or migration.");
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP original DC movement animations: MEMORIA_FF7_GAME_DIR is not configured.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        Ff7FieldResources field = Ff7FieldResources.Load(installation, "ujunon3");
        Check(field.Scripts.Data.Span.Slice(0x0D7E, 12).SequenceEqual(new byte[] { 0xDC, 3, 1, 0, 0xDC, 6, 1, 1, 0xDC, 7, 1, 2 }), "The reached original dolphin initializer's three DC operands changed.");
        Ff7FieldHost host = new(field, (_, _) => 10, fieldId: 430);
        for (byte selector = 0; selector < 3; selector++)
        {
            byte animation = selector == 0 ? (byte)3 : selector == 1 ? (byte)6 : (byte)7;
            Check(host.Execute(new(selector + 1, 18, 6, 0x0D7E + selector * 4, new FieldMovementAnimationCommand(animation, selector))).Completed, "An unbound entity's DC command did not complete immediately.");
        }
        Check(host.Process.Actors.Length == 0 && host.Process.NativeIdleAnimation == 3 && host.Process.NativeWalkAnimation == 6 && host.Process.NativeRunAnimation == 7, "DC must update field-global words independently of a model owner.");
        string beforeIgnored = JsonSerializer.Serialize(host.Process);
        Check(host.Execute(new(4, 18, 6, 0, new FieldMovementAnimationCommand(255, 255))).Completed && JsonSerializer.Serialize(host.Process) == beforeIgnored, "An unknown selector must consume the command without writing a field animation index.");
        int model = Enumerable.Range(0, field.Models.Models.Count).First(index => field.Models.Models[index].Animations.Count is > 7 and < 127);
        Ff7Actor player = new() { Slot = 0, Entity = 0, Model = model, PlayerControlled = true, Animation = 1 };
        Check(host.SelectPlayerMovementAnimation(player, false, false).Animation == 3 && host.SelectPlayerMovementAnimation(player, true, false).Animation == 6 && host.SelectPlayerMovementAnimation(player, true, true).Animation == 7, "The automatic player idle/walk/run consumer did not use the selected native indices.");
        host.Execute(new(5, 18, 6, 0, new FieldMovementAnimationCommand(255, 1)));
        host.Execute(new(6, 18, 6, 0, new FieldMovementAnimationCommand(255, 2)));
        Check(host.SelectPlayerMovementAnimation(player, true, false).Animation == 0 && host.SelectPlayerMovementAnimation(player, true, true).Animation == 0, "Invalid walking/running indices must select native clip0 before the model-binding check.");
        host.Execute(new(7, 18, 6, 0, new FieldMovementAnimationCommand(127, 0)));
        Ff7Actor rejectedIdle = host.SelectPlayerMovementAnimation(player, false, false);
        Check(rejectedIdle.Animation == player.Animation && rejectedIdle.RequestedAnimation == 127, "An invalid idle request must preserve its previous bound model clip through the existing native binding contract.");
        Ff7FieldProcess cold = JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!;
        Ff7FieldHost restored = new(field, (_, _) => 10, cold, 430);
        Check(restored.SelectPlayerMovementAnimation(player, false, false) == rejectedIdle && restored.Process.NativeWalkAnimation == 255 && restored.Process.NativeRunAnimation == 255, "Cold native animation words or rejected binding changed.");
        host.Execute(new(8, 18, 6, 0, new FieldMovementAnimationCommand(255, 0)));
        Check(host.Process.NativeIdleAnimation == 255, "The global DC word must retain unsigned255 until its signed native consumer executes.");
        try
        {
            host.SelectPlayerMovementAnimation(player, false, false);
            throw new InvalidOperationException("A native negative animation-table index was silently replaced.");
        }
        catch (NotSupportedException)
        {
        }
        Ff7FieldProcess next = host.CreateNextFieldProcess(430);
        Check(next.NativeIdleAnimation == 0 && next.NativeWalkAnimation == 1 && next.NativeRunAnimation == 2, "Entering a new field must reset the native animation words to0/1/2.");
        RunCheckpoint(installation);
        Console.WriteLine("PASS native DC global indices, automatic player selection, rejected binding, field reset and old/cold save defaults");
    }

    private static void RunCheckpoint(Ff7Installation installation)
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_DOLPHIN_SAVE");
        if (string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP reached dolphin initializer: MEMORIA_FF7_DOLPHIN_SAVE is not configured.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream input = new(source);
        Ff7SessionSave save = CombinedSessionCodec.Read(input).Ff7 ?? throw new InvalidDataException("The dolphin fixture has no FFVII continuation.");
        Check(save.FieldName == "ujunon3" && save.Vm.Tick == 0, "The configured dolphin fixture must retain the reached field initializer.");
        using Ff7Session live = Ff7Session.Restore(installation, save);
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(save))!);
        live.AdvanceTick(advanceClock: false);
        cold.AdvanceTick(advanceClock: false);
        Check(live.Machine.Capture().Tick == 1 && JsonSerializer.Serialize(live.Capture()) == JsonSerializer.Serialize(cold.Capture()), "The original dolphin initializer diverged from its independent one-callback cold continuation.");
        Check(live.Host.Process.NativeIdleAnimation == 3 && live.Host.Process.NativeWalkAnimation == 6 && live.Host.Process.NativeRunAnimation == 7, "The original unbound initializer did not apply its native movement animation indices.");
        Check(source.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "The dolphin standalone changed during read-only continuation.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
