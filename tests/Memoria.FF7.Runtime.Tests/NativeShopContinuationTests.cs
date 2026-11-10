using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeShopContinuationTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_SHOP_CHECKPOINT");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP original physical shop continuation: configure original resources and a shop checkpoint.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream input = new(source);
        CombinedSessionSave envelope = CombinedSessionCodec.Read(input);
        Ff7SessionSave saved = envelope.Ff7 ?? throw new InvalidDataException("The shop fixture has no FFVII continuation.");
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session original = Ff7Session.Restore(installation, saved);
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(saved))!);
        Check(original.Field.Name == "mrkt2" && original.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) == 188, "The physical shop fixture must retain the reviewed market visit.");
        Ff7AutoplayDriver driver = new(original);
        Ff7AutoplayDriver coldDriver = new(cold);
        int callbacks = 0;
        for (; callbacks < 1600 && original.Shop is null; callbacks++)
        {
            AdvanceField(original, driver, true);
            AdvanceField(cold, coldDriver, true);
            Equal(original, cold);
        }
        Ff7Actor? stopped = original.Host.Process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        Check(original.Field.Name == "mkt_s3" && original.Shop is { ShopId: 8, Phase: Ff7ShopPhase.Opening }, $"The original counter did not enter its native shop within the physical bound: {original.Field.Name}/{original.Machine.Tick}, lock={original.Host.Process.PlayerLocked}, player=({stopped?.X4096 >> 12},{stopped?.Y4096 >> 12}), triangle={stopped?.Triangle}, lines={JsonSerializer.Serialize(original.Host.Process.Lines)}.");
        FieldHostRequest owner = original.Machine.Capture().PendingRequests!.Single(request => request.Command is FieldMenuCommand { Mode: 8, Parameter: 8 });
        Check(owner.Entity == 7 && owner.ProgramCounter == 0x04EA, "The shop must come from its exact original counter owner, not a synthetic host request.");
        string? outputPath = Environment.GetEnvironmentVariable("MEMORIA_FF7_SHOP_OUTPUT");
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            Check(!StringComparer.OrdinalIgnoreCase.Equals(Path.GetFullPath(path), Path.GetFullPath(outputPath)), "A shop presentation fixture cannot overwrite its original source.");
            using FileStream output = new(outputPath, FileMode.CreateNew, FileAccess.Write);
            CombinedSessionCodec.Write(output, envelope with { Ff7 = original.Capture() });
            Console.WriteLine($"WROTE selected shop fixture at {original.FieldGeneration}:{original.Machine.Tick}, native owner {owner.Entity}/{owner.Priority}/{owner.ProgramCounter:X4}");
        }
        string frozen = JsonSerializer.Serialize(original.Machine.Capture());
        int shopCallbacks = 0;
        for (; original.Shop!.Phase != Ff7ShopPhase.Completed && shopCallbacks < 60; shopCallbacks++)
        {
            bool cancel = original.Shop.Phase == Ff7ShopPhase.Interactive;
            original.AdvanceShop(cancel);
            cold.AdvanceShop(cancel);
            original.AdvanceClock(TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60));
            cold.AdvanceClock(TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60));
            Check(frozen == JsonSerializer.Serialize(original.Machine.Capture()), "The native shop module advanced its frozen field VM.");
            Equal(original, cold);
        }
        Check(shopCallbacks == 55, "Native shop opening, semantic cancellation and closing changed their callback timing.");
        bool returned = false;
        original.NativeTickCompleted += observation => returned |= observation.ScriptReturns.Any(receipt => receipt.Entity == 7 && receipt.ProgramCounter == 0x04F5);
        for (int callback = 0; callback < 100 && !returned; callback++)
        {
            AdvanceField(original, driver, false);
            AdvanceField(cold, coldDriver, false);
            Equal(original, cold);
        }
        Check(returned && !original.Host.Process.PlayerLocked && !original.Host.Process.MenuDisabled, "The original counter did not reach its native return after cancellation.");
        Check(original.Progress.Gil == saved.Progress.Gil && JsonSerializer.Serialize(original.Progress.Inventory) == JsonSerializer.Serialize(saved.Progress.Inventory) && JsonSerializer.Serialize(original.Progress.Materia) == JsonSerializer.Serialize(saved.Progress.Materia), "Entering and cancelling the original shop changed inventory or Gil.");
        Check(SHA256.HashData(source).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "The original physical shop source was modified.");
        Console.WriteLine($"PASS original physical shop entry/cancel/return and cold continuation ({callbacks} field callbacks, {shopCallbacks} menu callbacks)");
    }

    private static void AdvanceField(Ff7Session session, Ff7AutoplayDriver driver, bool navigate)
    {
        foreach (Ff7Dialogue dialogue in session.Host.Process.Dialogues.Where(dialogue => !dialogue.Completed && dialogue.AwaitingInput && session.Host.CanConfirmDialogue(dialogue.RequestId)).ToArray())
        {
            session.Host.UpdateDialogue(Ff7DialogueCursor.Confirm(dialogue, FieldText.Read(session.Field.Scripts.Texts[dialogue.Text].Span)));
        }
        Ff7AutoplayInput action = default;
        if (navigate && !session.Host.Process.PlayerLocked)
        {
            if (session.Field.Name == "mrkt2")
            {
                action = driver.Gateway(203);
            }
            else if (session.Field.Name == "mkt_s3" && session.Host.Process.Lines.SingleOrDefault(line => line.Entity == 2) is { } counter)
            {
                action = driver.Approach(new Vector3((counter.X1 + counter.X2) / 2f, (counter.Y1 + counter.Y2) / 2f, counter.Z1));
                if (counter.OnLine)
                {
                    action = driver.Steer(new Vector3((counter.X1 + counter.X2) / 2f, (counter.Y1 + counter.Y2) / 2f, counter.Z1), run: false);
                    action = action with { Talk = session.Host.Process.Tick % 16 == 0 };
                }
            }
        }
        driver.Apply(action);
        session.AdvanceTick();
    }

    private static void Equal(Ff7Session original, Ff7Session cold)
    {
        Check(JsonSerializer.Serialize(original.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Cold original shop continuation diverged.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
