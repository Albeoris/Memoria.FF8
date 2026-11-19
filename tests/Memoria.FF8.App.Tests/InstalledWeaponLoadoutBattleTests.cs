using System.Collections.Immutable;
using System.Security.Cryptography;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

/// <summary>Installed native host checks from an externally selected standalone field save.</summary>
public sealed class InstalledWeaponLoadoutBattleTests
{
    public static IEnumerable<object[]> StylePairs()
    {
        foreach (int recipient in new[] { 0, 6 })
        {
            foreach (int donor in new[] { 0, 1, 2, 3, 4, 5, 6, 8, 9, 10 })
            {
                yield return [recipient, donor];
            }
        }
        yield return [4, 1];
        yield return [10, 9];
    }

    [Theory]
    [MemberData(nameof(StylePairs))]
    public void Every_installed_weapon_style_enters_and_attacks_without_changing_recipient_body(int recipient, int donor)
    {
        Run(recipient, donor);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Native_no_override_baseline_enters_attacks_and_returns(int recipient)
    {
        Run(recipient, recipient, assignOverride: false);
    }

    private static void Run(int recipient, int donor, bool assignOverride = true)
    {
        string directory = TestResources.GameDirectory;
        string source = TestResources.FixturePath("weapon-limit-field-source");
        byte[] sourceHash = SHA256.HashData(File.ReadAllBytes(source));
        using GameResourceHost selected = new(directory, watch: false);
        using GameAssets game = new(directory, resources: selected.Resources, modelOverrides: selected);
        GameState loaded = PlayerStateLoader.Load(source, directory, sharedAssets: game, resources: selected.Resources);
        Assert.Equal(SceneKind.Field, loaded.Scene);
        Assert.Null(loaded.Battle);
        Assert.Null(loaded.PendingBattleEncounter);
        GameSession session = new(loaded);
        using GameWorld world = new(session, directory, resources: selected.Resources, sharedAssets: game, modelOverrides: selected, readSceneMods: () => selected.SceneMods, readGameplayPolicy: () => selected.GameplayPolicy, validateWorldPresentation: false);
        world.EnsureField();
        Assert.False(session.CharacterVitals!.DreamEnabled);
        NativeCharacterVitals vitals = session.CharacterVitals.WithExtendedRoster(true);
        NativeCharacterVital[] records = vitals.Records.ToArray();
        records[recipient] = records[recipient] with { MemberFlags = (ushort)(records[recipient].MemberFlags | 1), Status = 0 };
        session.SetCharacterVitals((vitals with { Records = records, CombatParty = [checked((byte)recipient), 255, 255], AdditionalCombatParty = null, AdditionalCombatPartyMinimumCapacities = null }).RebuildParty().Rest());
        world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { BattleAllyCount = 3, AdditionalEnemies = 0 });
        BattleAssets resources = world.BattleResources;
        int weapon = assignOverride ? resources.WeaponDefinitions.First(definition => definition.CharacterId == donor).Id : session.CharacterVitals.GetBattleWeapon(recipient);
        donor = resources.WeaponDefinitions[weapon].CharacterId;
        BattleLoadoutDraft draft = session.CreateBattleLoadoutDraft([new(recipient, session.CharacterVitals.GetBattleWeapon(recipient), BattleLimitFamilyRules.NativeFamilies(recipient))]);
        if (assignOverride)
        {
            draft.AssignWeapon(recipient, weapon);
            session.ApplyBattleLoadoutDraft(draft);
        }
        BattleLoadoutOverrides? assignments = session.State.BattleLoadouts;
        List<long[]> damageTimings = [];
        int sourceField = session.State.FieldId;

        for (int encounter = 0; encounter < 2; encounter++)
        {
            RequestEncounter(world, session);
            if (encounter == 0 && Environment.GetEnvironmentVariable("MEMORIA_FF8_LOADOUT_PREPARE_DIRECTORY") is { Length: > 0 } preparedDirectory)
            {
                Directory.CreateDirectory(preparedDirectory);
                string preparedPath = Path.Combine(preparedDirectory, $"recipient-{recipient:00}-style-{donor:00}{(assignOverride ? "" : "-native")}.ff8save");
                if (!File.Exists(preparedPath))
                {
                    session.Save(preparedPath, compact: true);
                    File.WriteAllText(Path.ChangeExtension(preparedPath, ".json"), System.Text.Json.JsonSerializer.Serialize(new { SourceResource = "weapon-limit-field-source", SourceSha256 = Convert.ToHexString(sourceHash), StandalonePath = Path.GetFullPath(preparedPath), Recipient = recipient, Donor = donor, WeaponId = weapon, NativeBaseline = !assignOverride, Field = session.State.FieldId, FieldFrame = world.Events.Frame, Boundary = "Standalone prepared field with one pending ordinary native encounter; StartEncounter is the next control command.", Resume = $"Memoria.FF8.exe --state \"{Path.GetFullPath(preparedPath)}\" --game-dir \"{directory}\"" }));
                }
            }
            world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: 4));
            BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
            BattleCombatant actor = Assert.Single(battle.Combatants, candidate => candidate.IsPlayer);
            Assert.Equal(recipient, actor.ModelId);
            BattleCombatantDefinition definition = battle.GetCombatantDefinition(actor.Id);
            Assert.Equal(weapon, definition.WeaponId);
            Assert.Equal(donor, definition.WeaponProfile!.ResourceOwner);
            Assert.Equal(recipient, definition.NativePlayerStatRules!.CharacterId);
            BattleModel body = resources.LoadPlayer(recipient, definition.Costume);
            BattleModel composed = resources.LoadPlayerForWeapon(recipient, definition.Costume, weapon);
            Assert.Equal(body.Name, composed.Name);
            Assert.Equal(body.Bones, composed.Bones);
            NativeBattleGraphicsResource compiled = resources.ReadNativeGraphicsResource(definition);
            NativeBattleGraphicsResource donorResource = resources.ReadNativeGraphicsResource(definition with { ModelId = donor, Costume = 0 });
            Assert.Equal(donorResource.SequenceSection.ToArray(), compiled.SequenceSection.ToArray());
            Assert.Equal(body.NativePoseSource!.Skeleton.ToArray(), compiled.PoseSource!.Skeleton.ToArray());
            compiled.Validate();
            battle.ConfigureDiagnosticScheduling(holdEnemyCommands: true, readinessActorId: actor.Id);
            TickUntil(world, () => battle.NativeEntryReady, $"recipient {recipient}, donor {donor}, weapon {weapon}: native entry");
            actor.Atb = BattleSession.AtbMaximum;
            TickUntil(world, () => battle.CanChoosePlayerCommand(actor.Id), $"recipient {recipient}, donor {donor}, weapon {weapon}: player readiness");
            BattleCombatant target = battle.Combatants.First(candidate => !candidate.IsPlayer && candidate.IsAlive && candidate.IsActive && !candidate.Untargetable);
            int eventStart = battle.Events.Count;
            long commandFrame = battle.Frame;
            int targetHp = target.CurrentHp;
            world.ApplyBattleInput(new(BattleInputKind.Attack, ActorId: actor.Id, TargetId: target.Id) { ForcePhysical = true });
            TickUntil(world, () => battle.Events.Skip(eventStart).Any(entry => entry.Kind == "ActionComplete" && entry.ActorId == actor.Id) && battle.ActiveAction is null && battle.Presentation?.HasActiveAction != true, $"recipient {recipient}, donor {donor}, weapon {weapon}: native attack completion");
            BattleEvent[] damage = battle.Events.Skip(eventStart).Where(entry => entry.Kind == "Damage" && entry.ActorId == actor.Id && entry.TargetId == target.Id).ToArray();
            Assert.NotEmpty(damage);
            Assert.Contains(damage, entry => entry.Amount > 0);
            BattleEvent completion = battle.Events.Skip(eventStart).First(entry => entry.Kind == "ActionComplete" && entry.ActorId == actor.Id);
            Assert.All(damage, entry => Assert.InRange(entry.Frame, commandFrame, completion.Frame));
            damageTimings.Add(damage.Select(entry => entry.Frame - commandFrame).ToArray());
            Assert.True(target.CurrentHp < targetHp);
            Assert.Equal(recipient, actor.ModelId);
            Assert.Equal(weapon, battle.GetCombatantDefinition(actor.Id).WeaponId);
            Assert.Equal(assignments?.Get(recipient)?.WeaponId, session.State.BattleLoadouts?.Get(recipient)?.WeaponId);
            // The action above is ordinary native combat. Explicit debug victory only bounds lifecycle cleanup.
            if (!battle.CanReturnToField)
            {
                ExpandedBattleInstalledEntry.CompleteDebugVictory(world);
            }
            TickUntil(world, () => battle.CanReturnToField, $"recipient {recipient}, donor {donor}, weapon {weapon}: battle return");
            world.ApplyBattleInput(new(BattleInputKind.Return));
            Assert.Null(world.Battle);
            Assert.Equal(SceneKind.Field, session.State.Scene);
            Assert.Equal(sourceField, session.State.FieldId);
            Assert.Equal(assignments?.Get(recipient)?.WeaponId, session.State.BattleLoadouts?.Get(recipient)?.WeaponId);
        }
        Assert.Equal(damageTimings[0], damageTimings[1]);
        Assert.Equal(sourceHash, SHA256.HashData(File.ReadAllBytes(source)));
    }

    private static void RequestEncounter(GameWorld world, GameSession session)
    {
        string program = world.Events.Capture().GetProperty("ProgramHash").GetString()!;
        NativeRandomEncounterState random = session.State.RandomEncounters?.Native ?? NativeRandomEncounterState.FromColdProcess();
        session.SetRandomEncounters(new(true, random, new(SceneKind.Field, session.State.FieldId, world.Events.Frame, 94, program)));
    }

    private static void TickUntil(GameWorld world, Func<bool> complete, string boundary)
    {
        for (int callback = 0; callback < 1800 && !complete(); callback++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
        }
        Assert.True(complete(), "Timed out at " + boundary + ": " + string.Join(", ", world.Battle?.Events.TakeLast(8).Select(entry => entry.Kind + "/" + entry.ActorId) ?? []));
    }
}
