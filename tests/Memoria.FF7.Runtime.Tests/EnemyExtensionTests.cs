using Memoria.FF7.Runtime;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using System.Text.Json;

internal static class EnemyExtensionTests
{
    public static void Run()
    {
        Ff7EnemyExtensionDocument scan = Ff7EnemyExtensionDocument.Parse("""{"schemaVersion":1,"documentId":"scan","enemies":[{"enemyId":22,"scanDescription":"An iron sentinel watches the reactor's heart."}]}""");
        Ff7EnemyExtensionDocument growth = Ff7EnemyExtensionDocument.Parse("""{"schemaVersion":1,"documentId":"growth","enemies":[{"enemyId":22,"growth":{"hp":[1000000,1000000,0,0]},"drawTiers":[{"minimumDelta":0,"slots":[{"magicId":7,"resistance":0}]}]}]}""");
        Ff7EnemyExtensionCatalog catalog = new([scan, growth]);
        Ff7EnemyExtensionDocument level = Ff7EnemyExtensionDocument.Parse("""{"schemaVersion":1,"documentId":"level","enemies":[{"enemyId":22,"scaleLevel":false}]}""");
        Check(new Ff7EnemyExtensionCatalog([scan, growth, level]).Find(22)!.ScaleLevel == false, "Explicit level policy must compose independently of growth and Draw.");
        Throws(() => new Ff7EnemyExtensionCatalog([level, level with { DocumentId = "other-level" }]), "Competing level policies must reject.");
        Check(catalog.Count == 1 && catalog.Find(22)?.ScanDescription == scan.Enemies[0].ScanDescription, "Disjoint ID-bound properties must compose.");
        growth.Enemies[0].Growth!["hp"][1] = 999;
        Ff7EnemyExtensionDelta first = catalog.Find(22)!;
        first.Growth!["hp"][1] = 10;
        first.DrawTiers![0].Slots[0] = new(1, 99);
        Check(catalog.Find(22)!.Growth!["hp"][1] == 1000000 && catalog.Find(22)!.DrawTiers![0].Slots[0].MagicId == 7, "Published catalog must not retain mutable input or leak output arrays.");
        Throws(() => new Ff7EnemyExtensionCatalog([scan, scan with { DocumentId = "other" }]), "Overlapping Scan properties must reject rather than silently overwrite.");
        Throws(() => Ff7EnemyExtensionDocument.Parse("""{"schemaVersion":1,"documentId":"a","documentId":"b","enemies":[]}"""), "Duplicate JSON properties must reject.");
        Throws(() => Ff7EnemyExtensionDocument.Parse("""{"schemaVersion":1,"documentId":"a","enemies":[{"enemyId":1,"level":99}]}"""), "A native replacement field must not masquerade as a delta.");
        Throws(() => Ff7EnemyExtensionDocument.Parse("""{"schemaVersion":1,"documentId":"a","enemies":[{"enemyId":1,"drawTiers":[{"minimumDelta":0,"slots":[{"magicId":64,"resistance":0}]}]}]}"""), "GF identities must not enter stocked magic pools.");
        int[] row = [1000000, 1000000, 0, 0];
        Check(Ff7EnemyExtensionCatalog.Scale(77, 5, 20, row, 255) == 77 && Ff7EnemyExtensionCatalog.Scale(77, 20, 20, row, 255) == 77, "Baseline and lower party levels must be exact.");
        Check(Ff7EnemyExtensionCatalog.Scale(77, 100, 20, row, 255) == 154 && Ff7EnemyExtensionCatalog.Scale(0, 100, 20, row, 255) == 0, "Growth must retain its anchor and intentional zero.");
        Check(Ff7EnemyExtensionCatalog.Scale(200, 100, 20, row, 255) == 255, "Native storage projection must clamp without wrapping.");
        Check(Ff7EnemyExtensionCatalog.Scale(3, 98, 97, row, 255) == 4 && Ff7EnemyExtensionCatalog.Scale(3, 98, 97, [1000000, 0, 9000000, 0], 255) == 6, "Fractional growth intervals must evaluate one exact rational expression before flooring.");
        Check(Ff7EnemyExtensionCatalog.ScaleUnsigned(uint.MaxValue, 100, 20, null, int.MaxValue) == uint.MaxValue && Ff7EnemyExtensionCatalog.ScaleUnsigned(uint.MaxValue, 20, 20, row, int.MaxValue) == uint.MaxValue, "Absent rows and baseline anchors must retain unsigned source values without narrowing.");
        Check(Ff7EnemyExtensionCatalog.ScaleUnsigned(3_000_000_000, 100, 20, row, int.MaxValue) == 3_000_000_000 && Ff7EnemyExtensionCatalog.ScaleUnsigned(3_000_000_000, 100, 20, row, uint.MaxValue) == uint.MaxValue, "A projection ceiling must never lower its native anchor or wrap unsigned rewards.");
        Check(!new Ff7EnemyIntegrationContext().Effective && new Ff7EnemyIntegrationContext(EnteredFromFf8: true).Effective, "Automatic must use explicit entry provenance.");
        Check(!new Ff7EnemyIntegrationContext(Ff7EnemyIntegrationPreference.Disabled, true).Effective && new Ff7EnemyIntegrationContext(Ff7EnemyIntegrationPreference.Enabled).Effective, "Explicit preferences must override provenance.");
        InstalledInitialization();
        Console.WriteLine("FFVII enemy extension composition, arithmetic, stock identity and context checks passed.");
    }

    private static void InstalledInitialization()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP installed enemy extension initialization: MEMORIA_FF7_GAME_DIR unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using Ff7Session original = Ff7Session.NewGame(installation);
        Ff7SessionSave savedWorld = original.Capture();
        WorldSwitch travel = new(new() { ActiveGame = SessionGame.Ff8, Ff8Payload = [1], Ff8PayloadFormat = "test" });
        Throws(() => travel.ToFf7(new(true), [1], "test", () => savedWorld, _ => throw new InvalidDataException("Rejected destination.")), "A failed preparation must reject the transition.");
        Check(travel.State.ActiveGame == SessionGame.Ff8 && travel.State.EnemyIntegration is null, "Rejected travel must not publish provenance.");
        travel.ToFf7(new(true), [1], "test", () => savedWorld, _ => { });
        Check(travel.State.EnemyIntegration?.Effective == true, "Successful FFVIII entry must resolve Automatic.");
        CombinedSessionSave resumed = JsonSerializer.Deserialize<CombinedSessionSave>(JsonSerializer.Serialize(travel.State))!;
        Check(resumed.EnemyIntegration?.Effective == true, "Direct resume must retain the segment activation context.");
        WorldSwitch disabledTravel = new(resumed with { EnemyIntegration = resumed.EnemyIntegration! with { Preference = Ff7EnemyIntegrationPreference.Disabled } });
        disabledTravel.ToFf8(new(true), savedWorld, (_, _) => { });
        disabledTravel.ToFf7(new(true), [1], "test", () => savedWorld, _ => { });
        Check(disabledTravel.State.EnemyIntegration is { Preference: Ff7EnemyIntegrationPreference.Disabled, Effective: false }, "An explicit disable must persist across round-trip travel.");
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        SceneFormation native = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[300];
        uint originalExperience = native.EnemyRewards[0].Experience;
        SceneFormation formation = native with { EnemyRewards = native.EnemyRewards.Select(r => r with { Experience = 17, Gil = 23, AbilityPoints = 3, Items = [new(0, 63)] }).ToArray() };
        Ff7EnemyExtensionDelta[] deltas = formation.EnemyRewards.Where(e => e.EnemyId >= 0).Select(r => new Ff7EnemyExtensionDelta { EnemyId = r.EnemyId, ScanDescription = "A watchman in the city's iron veins.", Growth = new() { ["hp"] = [1000000, 1000000, 0, 0] }, RewardGrowth = new() { ["exp"] = [1000000, 1000000, 0, 0] }, DrawTiers = [new(0, [new(1, 0)]), new(1, [new(2, 0)])], ItemReplacements = [new(1, "drop", 0, 1), new(10, "drop", 0, 2)] }).ToArray();
        Ff7EnemyExtensionCatalog catalog = new([new(1, "test", deltas)]);
        Ff7BattleSession disabled = new(formation, original.Progress, kernel, executable.NativeMateriaStatModifiers, 1);
        byte baseLevel = disabled.Data.Enemies[0].Level;
        Ff7BattleSession baseline = new(formation, original.Progress, kernel, executable.NativeMateriaStatModifiers, 1, enemyExtensions: catalog, enemyExtensionPartyLevels: [baseLevel]);
        Check(baseline.Data.Enemies[0].MaxHitPoints == disabled.Data.Enemies[0].MaxHitPoints && baseline.Data.Enemies[0].Rewards.Experience == 17 && baseline.Data.Enemies[0].Rewards.Items[0].ItemId == 0, "An enabled baseline encounter must retain native stats, rewards and item results.");
        Ff7BattleSession high = new(formation, original.Progress, kernel, executable.NativeMateriaStatModifiers, 1, enemyExtensions: catalog, enemyExtensionPartyLevels: [20, 80]);
        Check(high.EnemyExtension(4)!.Level == Math.Max((int)baseLevel, 50), "Party reference must be the floor mean of the supplied actual party, not maximum.");
        Check(high.Data.Enemies[0].MaxHitPoints > disabled.Data.Enemies[0].MaxHitPoints && high.Data.Enemies[0].Rewards.Items[0].ItemId == 2 && high.Data.Enemies[0].Rewards.Items[0].Rate == 63, "Projection must grow HP and select highest delta against the original item without modifying probability.");
        Check(formation.EnemyRewards[0].Items[0].ItemId == 0 && native.EnemyRewards[0].Experience == originalExperience, "Applying extension must not mutate source definitions.");
        Ff7BattleSnapshot snapshot = JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(high.Capture()))!;
        Ff7BattleSession restored = new(formation, original.Progress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: snapshot, enemyExtensions: Ff7EnemyExtensionCatalog.Empty, enemyExtensionPartyLevels: [1]);
        Check(JsonSerializer.Serialize(restored.Capture()) == JsonSerializer.Serialize(high.Capture()) && restored.Data.Enemies[0].MaxHitPoints == high.Data.Enemies[0].MaxHitPoints && restored.Data.Enemies[0].Rewards.Items[0].ItemId == 2, "Restoration must preserve resolved generation and RNG without consulting the changed catalog or party.");
        Ff7BattleSnapshot old = disabled.Capture() with { EnemyExtensions = null };
        Ff7BattleSession oldRestored = new(formation, original.Progress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: old, enemyExtensions: catalog, enemyExtensionPartyLevels: [100]);
        Check(oldRestored.EnemyExtension(4) is null && oldRestored.Data.Enemies[0].MaxHitPoints == disabled.Data.Enemies[0].MaxHitPoints, "Old snapshots must not gain newly enabled scaling during restore.");
        SceneFormation unsignedFormation = formation with { EnemyRewards = formation.EnemyRewards.Select(reward =>
        {
            byte[] raw = reward.Raw.ToArray();
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(164, 4), uint.MaxValue);
            return reward with { Experience = uint.MaxValue, Gil = uint.MaxValue, Raw = raw };
        }).ToArray() };
        Ff7EnemyExtensionCatalog scanOnly = new([new(1, "unsigned-scan", deltas.Select(d => new Ff7EnemyExtensionDelta { EnemyId = d.EnemyId, ScanDescription = "An iron watchman." }).ToArray())]);
        Ff7BattleSession unsignedScan = new(unsignedFormation, original.Progress, kernel, executable.NativeMateriaStatModifiers, 1, enemyExtensions: scanOnly, enemyExtensionPartyLevels: [100]);
        Check(unsignedScan.Data.Enemies[0].MaxHitPoints == uint.MaxValue && unsignedScan.Data.Enemies[0].Rewards.Experience == uint.MaxValue && unsignedScan.Data.Enemies[0].Rewards.Gil == uint.MaxValue, "A Scan-only delta must preserve unsigned native HP and reward amounts.");
        Ff7BattleSession unsignedBaseline = new(unsignedFormation, original.Progress, kernel, executable.NativeMateriaStatModifiers, 1, enemyExtensions: catalog, enemyExtensionPartyLevels: [baseLevel]);
        Check(unsignedBaseline.Data.Enemies[0].MaxHitPoints == uint.MaxValue && unsignedBaseline.Data.Enemies[0].Rewards.Experience == uint.MaxValue, "Declared growth must leave its unsigned baseline intact before its positive delta.");
        Console.WriteLine("Installed FFVII enemy initialization, delta item/reward anchors and JSON continuation passed.");
    }

    private static void Check(bool valid, string message)
    {
        if (!valid)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void Throws(Action action, string message)
    {
        try
        {
            action();
        }
        catch (Exception exception) when (exception is InvalidDataException or System.Text.Json.JsonException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }
}
