using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeBattleSaveWindowTests
{
    internal static void Run()
    {
        CheckOperands();
        CheckInstalledBattle();
    }

    private static void CheckOperands()
    {
        byte[] kernel = new byte[3988];
        Ff7BattleRng random = new(kernel, new(new byte[8]));
        SceneBattleData formation = new(1, [], [], [], null);
        Ff7BattleAiActor[] actors = [new(0, 0, 8, 0, 0, 10, 10), new(4, 0, 8, 0, 0, 10, 10)];
        byte[] globals = new byte[64];
        globals[3] = 0xB8;
        byte[] bank = new byte[Ff7BattleAi.NativeSaveBankSize];
        bank[0xC4] = 0x12;
        bank[0x1C4] = 0x3D;
        bank[0x1C5] = 0xFC;
        bank[0x26C] = 0xE1;
        bank[0x26D] = 0xE2;
        bank[0x4FF] = 0xA5;
        Ff7BattleAiContext context = new(4, actors, 0x11, 0, GlobalMemory: globals, SaveBank1: bank);
        byte[] script =
        [
            0x60, 0, 0x61, 0xC4, 0x01, 0x95,
            0x11, 0, 0, 0x01, 0x10, 0x20, 0x60, 0x3C, 0x44, 0x90,
            0x60, 2, 0x62, 0xFF, 0xFF, 0xFF, 0x95,
            0x60, 1, 0x61, 0x6C, 0x02, 0x95,
            0x60, 0, 0x62, 0xFF, 0x04, 0, 0x95,
            0x11, 8, 0, 0x01, 0x10, 0x20, 0x90,
            0x60, 0, 0x61, 0x6C, 0x02, 0x95,
            0x73
        ];
        Ff7BattleAiResult result = Ff7BattleAi.Run(script, context, [], formation, random);
        Check(result.LocalMemory[0] == 1 && result.LocalMemory[1] == 0xA5, "Extended operands must preserve the native unsigned byte comparison and the last mapped field byte.");
        Check(result.GlobalMemory[2] == 0x3D && result.GlobalMemory[3] == 0xB8 && result.SaveBank1Writes.Count == 1 && result.SaveBank1Writes[0x26C] == 0x3D, "Native save exchange must publish one byte and see the latest staged value within the callback.");
        Check(bank[0x26C] == 0xE1 && bank[0x26D] == 0xE2 && globals[2] == 0, "AI projection must preserve its caller's bytes and neighboring persistent storage.");
        try
        {
            Ff7BattleAi.Run([0x60, 0, 0x61, 0, 5, 0x95, 0x73], context, [], formation, random);
            throw new InvalidOperationException("An unowned persistent address was admitted.");
        }
        catch (NotSupportedException)
        {
        }
    }

    private static void CheckInstalledBattle()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP installed roster-level battle: MEMORIA_FF7_GAME_DIR unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using Ff7Session initial = Ff7Session.NewGame(installation);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(entry => entry.FormationId == 908);
        IReadOnlyList<short> modifiers = Ff7PcExecutableData.Load(installation).NativeMateriaStatModifiers;
        Ff7BattleSystemMessages messages = Ff7BattleSystemMessages.Load(installation);
        FieldVariables variables = new();
        variables.WriteByte(FieldBank.Persistent0Byte, 0xC4, 99);
        variables.WriteByte(FieldBank.Persistent1Byte, 0xC4, 34);
        variables.WriteByte(FieldBank.Persistent2Byte, 0x6C, 9);
        variables.WriteByte(FieldBank.Persistent2Byte, 0x6D, 23);
        Ff7BattleSession Create(Ff7BattleSnapshot? snapshot = null) => new(formation, initial.Progress, kernel, modifiers, 1, snapshot: snapshot, initialSaveBank1: Ff7BattleSession.CaptureNativeSaveBank(variables), systemMessages: messages);

        Ff7BattleSnapshot entry = Create().Capture();
        Check(entry.Actors.Single(actor => actor.Actor.Slot == 4).AiMemory[0x20] == 1 && entry.AiSaveBank1![0x26C] == 0 && entry.AiSaveBankExtendedDirty is [0x26C], "The installed initialization must use the sub-35 roster comparison and stage its original upper-bank write.");
        variables.WriteByte(FieldBank.Persistent1Byte, 0xC4, 35);
        Ff7BattleSnapshot threshold = Create().Capture();
        Check(threshold.Actors.Single(actor => actor.Actor.Slot == 4).AiMemory[0x20] == 0, "The installed initialization must compare roster level 35 strictly and never read its low-address alias.");

        Ff7BattleSnapshot queued = entry with { Queue = Ff7BattleCommandQueue.StageReady(entry.Queue, 4, 0xFF) };
        Ff7BattleSession uninterrupted = Create(queued);
        Ff7BattleSession fork = Create(uninterrupted.Capture());
        uninterrupted.Advance(waitForCommand: true);
        fork.Advance(waitForCommand: true);
        Ff7BattleSnapshot next = uninterrupted.Capture();
        Check(next.Queue.Slots.Any(slot => slot?.Command is { ActorSlot: 4, CommandId: 0x20 }) && JsonSerializer.Serialize(next) == JsonSerializer.Serialize(fork.Capture()), "The installed enemy's native turn did not queue the same attack after independent private restoration.");

        fork.PublishNativeSaveWrites(variables);
        Check(variables.ReadByte(FieldBank.Persistent2Byte, 0x6C) == 0 && variables.ReadByte(FieldBank.Persistent2Byte, 0x6D) == 23 && variables.ReadByte(FieldBank.Persistent1Byte, 0xC4) == 35 && fork.Capture().AiSaveBankExtendedDirty is null, "Publishing a retained upper-bank write must preserve unrelated field bytes and consume the write once.");
        variables.WriteByte(FieldBank.Persistent2Byte, 0x6C, 10);
        Create(fork.Capture()).PublishNativeSaveWrites(variables);
        Check(variables.ReadByte(FieldBank.Persistent2Byte, 0x6C) == 10, "Restoring a consumed write must never republish it.");

        Ff7BattleSnapshot legacy = Create(entry with { AiSaveBank1 = entry.AiSaveBank1![..256], AiSaveBankExtendedDirty = null }).Capture();
        Check(legacy.AiSaveBank1![0x1C4] == 35 && legacy.AiSaveBank1[0x26C] == 10 && legacy.AiSaveBankExtendedDirty is null && JsonSerializer.Serialize(legacy.Random) == JsonSerializer.Serialize(entry.Random), "Legacy low-only images must recover the unmapped suffix from field facts without replaying initialization or consuming randomness.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
