using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class FlashbackPartyTests
{
    public static void Run()
    {
        Check(Enumerable.Range(0, 9).All(id => Ff7NativeCharacterIdentity.RecordId(id) == id) && Ff7NativeCharacterIdentity.RecordId(9) == 6 && Ff7NativeCharacterIdentity.RecordId(10) == 7, "Native temporary identities must map to their retained physical records.");
        try
        {
            Ff7NativeCharacterIdentity.RecordId(11);
            throw new InvalidOperationException("An unsupported party identity was accepted.");
        }
        catch (InvalidDataException)
        {
        }
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP installed flashback party: MEMORIA_FF7_GAME_DIR is unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using Ff7Session initial = Ff7Session.NewGame(installation);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        Ff7Progress original = initial.Progress;
        Check(original.Characters.Single(character => character.Id == 6).NativeRecord[0] == 9 && original.Characters.Single(character => character.Id == 7).NativeRecord[0] == 10, "Original initial records lost their temporary native character identities.");
        foreach (int[] party in new int[][] { [9, 255, 255], [9, 10, 255], [0, 255, 255] })
        {
            Ff7Progress progress = original with { Party = party };
            progress.Validate();
            Ff7Progress cold = JsonSerializer.Deserialize<Ff7Progress>(JsonSerializer.Serialize(progress))!;
            cold.Validate();
            Check(JsonSerializer.Serialize(progress) == JsonSerializer.Serialize(cold), "Temporary party changed records, progress or identities through JSON.");
            byte[] variables = initial.Capture().Vm.PersistentVariables.ToArray();
            for (int slot = 0; slot < 3; slot++)
            {
                variables[0x109 + slot] = checked((byte)party[slot]);
            }
            Ff7NativeSaveSlot native = Ff7NativeSaveCodec.ExportField(progress, variables, 277, -62, 57, 5, 0);
            Ff7Progress imported = Ff7NativeSaveCodec.ImportProgress(native, original.InitialKernelData);
            Check(imported.Party.SequenceEqual(party) && imported.Characters.Select(character => character.NativeRecord).Zip(progress.Characters.Select(character => character.NativeRecord)).All(pair => pair.First.SequenceEqual(pair.Second)), "Native save projection changed temporary party bytes or physical records.");
        }
        Ff7Progress selected = original with { Party = [9, 10, 255] };
        Ff7Progress recovered = Ff7NativePartyRecovery.Publish(selected, Ff7MateriaResources.ReadRecords(kernel), executable.NativeMateriaStatModifiers).Progress;
        Ff7Progress changed = Ff7NativePartyRecovery.AddHitPoints(recovered, 9, -10, Ff7MateriaResources.ReadRecords(kernel), executable.NativeMateriaStatModifiers).Progress;
        Check(changed.Characters.Single(character => character.Id == 6).HitPoints == recovered.Characters.Single(character => character.Id == 6).HitPoints - 10 && changed.Characters.Single(character => character.Id == 0) == original.Characters.Single(character => character.Id == 0), "Young Cloud recovery must update physical record6 and preserve adult Cloud.");
        Check(Ff7BattleAnimationResources.ModelPrefix(9, false) == Ff7BattleAnimationResources.ModelPrefix(0, false), "Original temporary Cloud must retain the CLOUD.DAT model identity.");
        TestBattleInitialization(selected, kernel, executable, installation);
        Console.WriteLine("Original temporary party identities, physical records, JSON/native saves and recovery passed.");
    }

    private static void TestBattleInitialization(Ff7Progress progress, IReadOnlyList<KernelSection> kernel, Ff7PcExecutableData executable, Ff7Installation installation)
    {
        Ff7CharacterState young = progress.Characters.Single(character => character.Id == 6);
        Ff7CharacterState sephiroth = progress.Characters.Single(character => character.Id == 7);
        Ff7BattlePlayerStats youngEquipped = Ff7NativeBattlePlayerStats.Create(young, kernel, executable.NativeMateriaStatModifiers);
        byte[] youngBare = young.NativeRecord.ToArray();
        youngBare[30] = byte.MaxValue;
        Ff7BattlePlayerStats youngUnequipped = Ff7NativeBattlePlayerStats.Create(young with { NativeRecord = youngBare }, kernel, executable.NativeMateriaStatModifiers);
        Check(youngEquipped.Actor.Attack == youngUnequipped.Actor.Attack + 10, "Original Power Wrist must add ten Strength through the native stat selector.");
        byte[] unsupportedAccessories = kernel[7].Data.ToArray();
        unsupportedAccessories[5] = 0;
        Check(unsupportedAccessories[12] == byte.MaxValue, "Special accessory regression must distinguish its native byte5 from byte12.");
        KernelSection[] unsupportedKernel = kernel.ToArray();
        unsupportedKernel[7] = kernel[7] with { Data = unsupportedAccessories };
        try
        {
            Ff7NativeBattlePlayerStats.Create(young, unsupportedKernel, executable.NativeMateriaStatModifiers);
            throw new InvalidOperationException("A native accessory special-effect producer was silently admitted.");
        }
        catch (NotSupportedException)
        {
        }
        Ff7BattlePlayerStats sephirothEquipped = Ff7NativeBattlePlayerStats.Create(sephiroth, kernel, executable.NativeMateriaStatModifiers);
        byte[] sephirothBare = sephiroth.NativeRecord.ToArray();
        sephirothBare[30] = byte.MaxValue;
        Ff7BattlePlayerStats sephirothUnequipped = Ff7NativeBattlePlayerStats.Create(sephiroth with { NativeRecord = sephirothBare }, kernel, executable.NativeMateriaStatModifiers);
        Check(sephirothEquipped.Actor.Defense == sephirothUnequipped.Actor.Defense + 50 && sephirothEquipped.Actor.MagicDefense == sephirothUnequipped.Actor.MagicDefense + 50, "Original Touph Ring must add fifty Vitality and Spirit through both native stat selectors.");
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(formation => formation.FormationId == 292);
        Ff7BattleSession battle = new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1);
        Check(battle.Actors.Where(actor => actor.Actor.Slot < 3).Select(actor => actor.Identity).SequenceEqual([9, 10]), "Original flashback battle must retain its logical actor identities.");
        Ff7BattleSnapshot snapshot = JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!;
        Ff7BattleSession cold = new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: snapshot);
        for (int callback = 0; callback < 8; callback++)
        {
            battle.Advance();
            cold.Advance();
        }
        Check(JsonSerializer.Serialize(battle.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Original battle292 initialization changed through JSON continuation.");
        Ff7BattleSnapshot injured = snapshot with { Actors = snapshot.Actors.Select(actor => actor.Identity == 9 ? actor with { Actor = actor.Actor with { HitPoints = actor.Actor.HitPoints - 5 } } : actor).ToArray() };
        Ff7BattleSession changed = new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: injured);
        Ff7Progress settled = changed.PublishVitals(progress);
        Check(settled.Characters.Single(character => character.Id == 6).HitPoints == young.HitPoints - 5 && settled.Characters.Single(character => character.Id == 0) == progress.Characters.Single(character => character.Id == 0), "Temporary battle vitals must publish to physical record6 and preserve adult Cloud.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
