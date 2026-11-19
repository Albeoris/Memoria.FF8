using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Mods;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GuardianForceSummonDamageTests
{
    private const int Eden = 15;
    private const int LimitBreak = 115;
    private const string Caster = "party-3";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Installed_Eden_resolves_the_learned_ability_on_its_hosted_damage_contact(bool learned)
    {
        using IsolatedFieldGame game = new();
        string directory = game.GameDirectory;
        string root = TestResources.ArtifactDirectory;
        string mods = Path.Combine(root, Guid.NewGuid().ToString("N"));
        string package = Path.Combine(mods, "Memoria", "Summon limit");
        string payload = Path.Combine(package, GuardianForceRules.PayloadPath);
        Directory.CreateDirectory(Path.GetDirectoryName(payload)!);
        File.WriteAllText(Path.Combine(package, "mod.json"), """{"id":"test-summon-limit","name":"Summon limit","package":"Memoria","description":{"en":"Sets the summon damage limit."}}""");
        File.WriteAllText(payload, """{"schemaVersion":1,"defaultSummonDamageLimit":9999,"abilities":[{"id":115,"summonDamageLimit":"unlimited"}]}""");
        using GameResourceHost host = new(directory, Path.Combine(mods, "Application"), watch: false, modsDirectory: mods);
        Assert.Equal(ModEffectiveStatus.Active, Assert.Single(host.Mods.Catalog.Packages).Status);
        using GameAssets assets = new(directory, resources: host.Resources, modelOverrides: host);
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, assets.ReadColdProcessNativeBattleMemory()) with { GlobalRandom = new(917) });
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory, resources: host.Resources, sharedAssets: assets, modelOverrides: host);
        ExpandedBattleInstalledEntry.Prepare(world, session, current => PrepareParty(current, learned), additionalEnemies: 0);
        BattleSession battle = ExpandedBattleInstalledEntry.Start(world);
        BattleActionDefinition action = Assert.Single(battle.ActionDefinitions, action => action.Ability == BattleAbility.GuardianForce && action.Id == Eden);
        BattleNativeAction native = Assert.IsType<BattleNativeAction>(action.NativeAction);
        Assert.Equal(8, native.AttackFlags & 8);
        Assert.Equal(learned ? int.MaxValue : 9999, action.GuardianForceSummonDamageLimit);
        Assert.Equal(int.MaxValue, Assert.Single(battle.ActionDefinitions, action => action.Ability == BattleAbility.GuardianForce && action.Id == 0).GuardianForceSummonDamageLimit);
        BattleCombatant caster = battle.Combatants.Single(actor => actor.Id == Caster);
        BattleCombatant target = battle.Combatants.First(actor => BattleSession.IsValidSelectedActionTarget(caster, action, actor));
        caster.Atb = BattleSession.AtbMaximum;
        world.ApplyBattleInput(new(BattleInputKind.Action, ActorId: caster.Id, TargetId: target.Id, Ability: BattleAbility.GuardianForce, ActionId: Eden));

        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        BattleModelOwner casterOwner = battle.ReadPresentationModelOwner(caster.Id);
        BattleModelOwner recipient = battle.ReadPresentationModelOwner(target.Id);
        long? summonToken = null;
        for (int callback = 0; callback < 3200; callback++)
        {
            if (battle.GuardianForceCasts.Count != 0 && battle.ActiveAction is null && battle.QueuedActions.Count == 0)
            {
                world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
            }
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            if (summonToken is null && process.ActionPackets.PublishedHeaders().Any(header => header.Slot.Command == 254 && header.Slot.Action == native.ActionId && header.Slot.Source == casterOwner))
            {
                summonToken = Assert.IsType<long>(process.ActiveActionToken);
            }
            if (summonToken is long token && process.LastStepActionToken == token && process.LastFrameServices!.ReadOrderedReceipts().OfType<NativeBattlePresentationContactReceived>().FirstOrDefault(contact => contact.Packet.Primary.Recipient == recipient && contact.Packet.Primary.PresentedAmount > 0) is { } contact)
            {
                int presented = contact.Packet.Primary.PresentedAmount;
                if (learned)
                {
                    Assert.True(presented > 9999, $"Eden's uncapped summon dealt {presented} damage.");
                }
                else
                {
                    Assert.Equal(9999, presented);
                }
                return;
            }
        }
        Assert.Fail("Eden did not deliver its hosted damage contact.");
    }

    private static void PrepareParty(GameSession session, bool learned)
    {
        ExpandedBattleGuardianTests.PrepareParty(session, learnedBoost: false, guardianId: Eden);
        NativeCharacterVitals vitals = session.State.CharacterVitals!;
        NativeGuardianForceVital eden = vitals.GuardianForces[Eden];
        NativeGuardianForceVital other = vitals.GuardianForces[0];
        ulong cap = 1UL << (LimitBreak - 64);
        ulong damageAbilities = 0;
        for (int bit = 19; bit < 28; bit++)
        {
            NativeStatModifier modifier = vitals.Definitions.GuardianForceModifiers[bit - 19];
            if (modifier.Selector == 0 && modifier.Percentage > 0)
            {
                damageAbilities |= 1UL << bit;
            }
        }
        uint experience = NativeStatDefinitions.ExperienceForLevel(GameplayLevelRules.DefaultMaximumLevel, vitals.Definitions.GuardianForces[Eden]);
        vitals.GuardianForces[Eden] = eden with { Experience = experience, LearnedBank = eden.LearnedBank with { High = ((eden.LearnedBank.High | damageAbilities) & ~cap) | (learned ? cap : 0) } };
        vitals.GuardianForces[0] = other with { LearnedBank = other.LearnedBank with { High = other.LearnedBank.High | cap } };
        session.SetCharacterVitals(vitals);
    }
}
