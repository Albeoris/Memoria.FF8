using System.Buffers.Binary;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

// Synthetic BATTLE owners exercise host transaction boundaries, not an original field journey.
public sealed class HostedAutomaticBattleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CurrentPolicyCanFinishAPendingOrActiveBattleImmediatelyWithoutAdvancingCombat(bool alreadyStarted)
    {
        (GameSession session, _) = Fixture();
        using GameWorld world = new(session, LaunchOptions.DefaultGameDirectory);
        world.ApplyBattleInput(new BattleInput(BattleInputKind.ConfigurePlaythrough, FullCombat: true));
        // Formation 34 is a supported early world encounter; this synthetic owner isolates policy timing.
        world.Events.LoadField(999, Program(34), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        world.Events.Step();
        world.Events.Step();
        Assert.NotNull(world.Events.PendingInteractiveBattle);
        if (alreadyStarted)
        {
            world.ApplyBattleInput(new BattleInput(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: 7));
        }
        world.ApplyBattleInput(new BattleInput(BattleInputKind.ConfigureEncounters, RandomEncounters: false));
        Assert.NotNull(world.Events.PendingInteractiveBattle);
        Assert.False(session.State.RandomEncounters!.Enabled);
        long fieldFrame = world.Events.Frame;
        world.ApplyBattleInput(new BattleInput(BattleInputKind.ConfigurePlaythrough, FullCombat: false));
        Assert.False(session.State.FullBattlePlaythrough == true);
        Assert.Equal(BattleOutcome.Victory, world.Battle!.Outcome);
        Assert.Equal(0, world.Battle.Frame);
        Assert.Equal(fieldFrame, world.Events.Frame);
        Assert.Empty(session.State.AppliedBattleResultIds);
        GameState snapshot = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!;
        GameSession restored = new(snapshot);
        Assert.Equal(BattleOutcome.Victory, restored.State.Battle!.Outcome);
        Assert.False(restored.State.FullBattlePlaythrough == true);
        Assert.NotNull(restored.State.PendingBattleEncounter);
        world.ApplyBattleInput(new BattleInput(BattleInputKind.Return));
        Assert.Null(world.Battle);
        Assert.Equal(42, session.State.Variables[800]);
        Assert.Single(session.State.AppliedBattleResultIds);
    }

    [Fact]
    public void HeldAutomaticBattleWaitsForHostAndOperationCompletionFollowsPublication()
    {
        (GameSession session, _) = Fixture();
        EventEngine engine = new(session) { HostAutomaticBattles = true };
        engine.LoadField(999, Program(), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        engine.Step();
        engine.Step();
        Assert.Equal(NativeBattleBackend.AutomaticVictory, engine.PendingHostedBattle!.Backend);
        Assert.Null(engine.PendingInteractiveBattle);
        long frame = engine.Frame;
        engine.Step();
        Assert.Equal(frame, engine.Frame);
        Assert.False(session.State.Variables.ContainsKey(800));
        Assert.Throws<InvalidOperationException>(() => engine.ReturnFromHostedBattle(1));
        bool published = false;
        bool completedAfterPublication = false;
        engine.OperationCompleted += () => completedAfterPublication = published;
        engine.ReturnFromHostedBattle(0, () => published = true);
        Assert.True(published);
        Assert.True(completedAfterPublication);
        Assert.Equal(42, session.State.Variables[800]);
        NativeBattleCompletion result = Assert.Single(engine.Scene.NativeBattleCompletions!);
        Assert.Equal((byte)4, result.Result);
        Assert.Equal(0, result.EmulatedCombatCallbacks);
        Assert.Null(engine.PendingHostedBattle);
    }

    [Fact]
    public void ResearchHostPublishesCurrentInstalledApWithoutSimulatedCombat()
    {
        (GameSession session, int learningIndex) = Fixture();
        using BattleAssets assets = new(LaunchOptions.DefaultGameDirectory);
        int expectedAp = assets.LoadEncounter(512).Enemies.Sum(enemy => assets.ReadEnemyAbilityPoints(enemy.ModelId));
        NativeCharacterVitals before = session.State.CharacterVitals!.DeepCopy();
        string items = JsonSerializer.Serialize(session.State.Economy!.Items);
        using GameWorld world = new(session, LaunchOptions.DefaultGameDirectory);
        world.Events.LoadField(999, Program(), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        world.Events.Step();
        world.Events.Step();
        Assert.Null(world.Events.PendingHostedBattle);
        Assert.Null(world.Battle);
        Assert.Null(session.State.Battle);
        Assert.Null(session.State.PendingBattleEncounter);
        Assert.Single(session.State.AppliedBattleResultIds);
        Assert.Equal(expectedAp, session.State.CharacterVitals!.GuardianForces[0].AbilityProgress[learningIndex]);
        Assert.Equal(before.Records[0].HitPoints, session.State.CharacterVitals.Records[0].HitPoints);
        Assert.Equal(before.Records[0].Status, session.State.CharacterVitals.Records[0].Status);
        Assert.Equal(before.Records[0].MagicInventory, session.State.CharacterVitals.Records[0].MagicInventory);
        Assert.Equal(items, JsonSerializer.Serialize(session.State.Economy!.Items));
        Assert.Equal(42, session.State.Variables[800]);
        Assert.Equal(0, Assert.Single(world.Events.Scene.NativeBattleCompletions!).EmulatedCombatCallbacks);
        world.Events.Step();
        Assert.Single(session.State.AppliedBattleResultIds);
        Assert.Equal(expectedAp, session.State.CharacterVitals.GuardianForces[0].AbilityProgress[learningIndex]);
    }

    [Fact]
    public void RandomOutcomeCannotReplaceAnActuallyPendingMandatoryBattleOwner()
    {
        (GameSession session, _) = Fixture();
        session.SetFullBattlePlaythrough(true);
        using GameWorld world = new(session, LaunchOptions.DefaultGameDirectory);
        // This component exercises native ownership, independently of interactive presentation assets.
        world.Events.ValidateInteractiveBattle = (_, _) => null;
        world.Events.LoadField(999, Program(), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        world.Events.Step();
        world.Events.Step();
        Assert.NotNull(world.Events.PendingInteractiveBattle);
        BattleEncounterOccurrence random = session.State.PendingBattleEncounter! with { ScriptedOwner = null };
        session.SetPendingBattleEncounter(random);
        NativeCharacterVitals vitals = session.State.CharacterVitals!;
        BattlePartyResult[] party = vitals.CombatParty.Where(row => row != 255).Select(row => new BattlePartyResult(row, vitals.Records[row].CharacterId, vitals.Records[row].HitPoints.Minimum, vitals.Records[row].HitPoints.Minimum, vitals.Records[row].Status, vitals.Records[row].Status, [])).ToArray();
        using BattleAssets assets = new(LaunchOptions.DefaultGameDirectory);
        BattleResult result = new(random, BattleOutcome.Victory, party, [], assets.LoadEncounter(random.Formation).Enemies.Select(enemy => enemy.Slot).ToArray(), [], true, BattleRewardPolicy.DebugNoRewards, []);
        string before = JsonSerializer.Serialize(session.State);
        Assert.Throws<InvalidDataException>(() => world.ApplyBattleResult(random, result));
        Assert.Equal(before, JsonSerializer.Serialize(session.State));
        Assert.Empty(session.State.AppliedBattleResultIds);
    }

    private static (GameSession Session, int LearningIndex) Fixture()
    {
        using GameAssets assets = new(LaunchOptions.DefaultGameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0).ToggleGuardianForceJunction(0, 0).RebuildParty().Rest();
        NativeGuardianForceAbility learning = vitals.GuardianForceAbilities(0).First(ability => !ability.Learned && ability.RequiredPoints >= 20);
        vitals = vitals.SelectGuardianForceLearning(0, learning.Ability);
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.InitializeNewGameCharacterVitals(vitals);
        session.InitializeNewGameCharacterAppearance(assets.ReadNewGameCharacterAppearance());
        session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
        session.InitializeNewGameGlobalRandom(assets.ReadNewGameGlobalRandom());
        session.InitializeNewGameKnownMagic(assets.ReadNewGameKnownMagic());
        session.Restore(session.State with { Scene = SceneKind.Field, FieldId = 999, FieldName = "synthetic-automatic-battle-component" });
        return (session, learning.LearningIndex);
    }

    private static byte[] Program(int encounter = 512)
    {
        (Jsm.Opcode Opcode, int Parameter)[] operations = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, encounter), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.BATTLE, 0), (Jsm.Opcode.PSHN_L, 42), (Jsm.Opcode.POPM_W, 800), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        operations = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0), .. operations];
        operations[2] = (Jsm.Opcode.LBL, 1);
        const int operationsOffset = 16;
        byte[] bytes = new byte[operationsOffset + operations.Length * 4];
        bytes[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), operationsOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), checked((ushort)operations.Length));
        for (int index = 0; index < operations.Length; index++)
        {
            (Jsm.Opcode opcode, int parameter) = operations[index];
            int encoded = parameter != 0 || opcode is Jsm.Opcode.PSHN_L or Jsm.Opcode.PSHI_L ? (int)opcode << 24 | parameter & 0xFFFFFF : (int)opcode;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(operationsOffset + index * 4), encoded);
        }
        return bytes;
    }
}
