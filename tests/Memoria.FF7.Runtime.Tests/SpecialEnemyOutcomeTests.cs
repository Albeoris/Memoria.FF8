using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class SpecialEnemyOutcomeTests
{
    public static void Run()
    {
        FieldVariables variables = new();
        variables.WriteByte(FieldBank.Persistent0Byte, 2, 9);
        variables.WriteByte(FieldBank.Persistent0Byte, 84, 6);
        variables.WriteByte(FieldBank.Persistent1Byte, 2, 77);
        byte[] initialBank = Ff7BattleSession.CaptureNativeSaveBank(variables);
        Check(initialBank.Length == 256 && initialBank[2] == 9 && initialBank[84] == 6, "The native battle bridge must read the first persistent bank.");
        initialBank[2] = 0;
        Check(variables.ReadByte(FieldBank.Persistent0Byte, 2) == 9, "Capturing the native bank must not alias field-owned storage.");

        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP installed special outcome persistence: MEMORIA_FF7_GAME_DIR unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using Ff7Session session = Ff7Session.NewGame(installation);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(formation => formation.FormationId == 300);
        IReadOnlyList<short> modifiers = Ff7PcExecutableData.Load(installation).NativeMateriaStatModifiers;
        Ff7BattleSession Create(Ff7BattleSnapshot? snapshot = null) => new(formation, session.Progress, kernel, modifiers, 1, snapshot: snapshot, initialSaveBank1: Ff7BattleSession.CaptureNativeSaveBank(variables));

        Ff7BattleSnapshot initial = Create().Capture();
        Check(initial.AiSaveBank1 is { Length: 256 } && initial.AiSaveBank1[2] == 9 && initial.AiSaveBank1[84] == 6, "Battle initialization must project the field bank before native callbacks.");
        Ff7BattleSession ordinary = Create(initial);
        Check(ordinary.ResolveNativeFieldResult(4) == 4, "An unchanged pre-battle result byte must not replace the current terminal result.");

        byte[] changedBank = initial.AiSaveBank1!.ToArray();
        changedBank[2] = 17;
        changedBank[84] = 1;
        Ff7BattleSnapshot staged = RoundTrip(initial with { AiSaveBank1 = changedBank, AiSaveBankDirty = [2, 84] });
        Ff7BattleSession restored = Create(staged);
        Check(JsonSerializer.Serialize(staged) == JsonSerializer.Serialize(restored.Capture()), "Restoring pending native outcomes must not replay callbacks or change script PCs, progress or RNG.");
        Check(restored.ResolveNativeFieldResult(0) == 17 && restored.ResolveNativeFieldResult(4) == 17, "An explicit native result must survive either ordinary victory or escape publication.");

        variables.WriteByte(FieldBank.Persistent0Byte, 91, 123);
        restored.PublishNativeSaveWrites(variables);
        Check(variables.ReadByte(FieldBank.Persistent0Byte, 2) == 17 && variables.ReadByte(FieldBank.Persistent0Byte, 84) == 1, "Pending native result and capture outputs must reach the actual field owner.");
        Check(variables.ReadByte(FieldBank.Persistent0Byte, 91) == 123 && variables.ReadByte(FieldBank.Persistent1Byte, 2) == 77, "Publishing native writes must preserve unrelated field changes and other persistent banks.");
        Check(restored.Capture().AiSaveBankDirty is { Length: 0 }, "Published native writes must be consumed exactly once.");
        variables.WriteByte(FieldBank.Persistent0Byte, 84, 8);
        Ff7BattleSession afterPublish = Create(RoundTrip(restored.Capture()));
        afterPublish.PublishNativeSaveWrites(variables);
        Check(variables.ReadByte(FieldBank.Persistent0Byte, 84) == 8, "Cold continuation must not replay a consumed native capture output.");

        changedBank[2] = 0;
        Ff7BattleSession timeout = Create(RoundTrip(initial with { AiSaveBank1 = changedBank, AiSaveBankDirty = [2] }));
        Check(timeout.ResolveNativeFieldResult(4) == 0, "An explicit native timeout result of zero must remain distinct from an absent result write.");
        Ff7BattleSession legacy = Create(initial with { AiSaveBank1 = null, AiSaveBankDirty = null });
        Check(legacy.ResolveNativeFieldResult(1) == 1 && legacy.Capture().AiSaveBank1?[84] == 8, "An older snapshot must recover a missing nonprogress bank from its field owner without inventing pending writes.");
        Check(JsonSerializer.Serialize(legacy.Capture().Random) == JsonSerializer.Serialize(initial.Random), "Recovering an older snapshot bank must not consume battle randomness.");
        Console.WriteLine("PASS native special outcomes, field bank ownership, JSON continuation and consumed-write persistence.");
    }

    private static Ff7BattleSnapshot RoundTrip(Ff7BattleSnapshot snapshot)
    {
        return JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(snapshot)) ?? throw new InvalidDataException("Battle snapshot round-trip returned no state.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
