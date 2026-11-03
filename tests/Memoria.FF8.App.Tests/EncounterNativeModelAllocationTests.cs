using System.Reflection;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class EncounterNativeModelAllocationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InstalledModelCompletionLoadsReservedCopiesOrRollsBackWholeBatch(bool failSecondCompletion)
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        string? fixture = Environment.GetEnvironmentVariable("MEMORIA_FF8_ENCOUNTER_ALLOCATION_FIXTURE");
        Assert.SkipUnless(Directory.Exists(directory), "Configured installed native resources are required.");
        Assert.SkipUnless(fixture is not null && File.Exists(fixture), "Set MEMORIA_FF8_ENCOUNTER_ALLOCATION_FIXTURE to a standalone active encounter before its first reinforcement allocation.");
        GameSession session = new(GameSession.Load(fixture!));
        using GameWorld world = new(session, directory, validateWorldPresentation: false);
        BattleSession battle = world.Battle ?? throw new InvalidDataException("The selected fixture must contain an active native battle.");
        BattleSnapshot before = battle.Capture();
        Assert.True(battle.NativeEntryReady);
        Assert.Empty(before.NativeAllocations);
        BattleCombatant original = battle.Combatants.Single(actor => actor.Id == before.Definition.EncounterMod!.OriginalActorId);
        int currentHp = original.CurrentHp;
        int maximumHp = original.MaximumHp;
        BattleEncounterAllocationState batch = battle.BeginEncounterAllocation("fixture.native-load", original.Id, "support", "assault");
        if (failSecondCompletion)
        {
            BattleCombatant second = battle.Combatants.Single(actor => actor.Id == batch.ActorIds[1]);
            BattleCombatantDefinition allocated = battle.GetCombatantDefinition(second.Id);
            typeof(BattleCombatant).GetField("definition", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(second, allocated with { Information246 = null, NativeTemplate = allocated.NativeTemplate! with { Information246 = null } });
        }
        Exception? failure = Record.Exception(() =>
        {
            for (int callback = 0; callback < 2000 && battle.EncounterAllocation!.Phase == BattleEncounterAllocationPhase.Loading; callback++)
            {
                foreach (string id in batch.ActorIds)
                {
                    int physical = battle.NativeGraphics!.ActorIds.IndexOf(id);
                    if (physical >= 0 && (battle.NativeGraphics.Models[physical].Flags0 & 2) != 0)
                    {
                        Assert.NotEqual(0, battle.NativeGraphics.Models[physical].Flags0 & 4);
                        Assert.False(battle.Combatants.Single(actor => actor.Id == id).IsActive);
                    }
                }
                battle.Tick();
            }
        });
        if (failSecondCompletion)
        {
            InvalidDataException error = Assert.IsType<InvalidDataException>(failure);
            Assert.Contains("information BYTE246", error.Message);
            Assert.Null(battle.EncounterAllocation);
            Assert.Empty(battle.Capture().NativeAllocations);
            Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(battle.Capture()));
            return;
        }
        Assert.Null(failure);
        Assert.Equal(BattleEncounterAllocationPhase.Ready, battle.EncounterAllocation!.Phase);
        Assert.Equal(currentHp, original.CurrentHp);
        foreach (string id in batch.ActorIds)
        {
            BattleCombatant copy = battle.Combatants.Single(actor => actor.Id == id);
            Assert.False(copy.IsActive);
            Assert.True(copy.Hidden);
            Assert.True(copy.Untargetable);
            Assert.Equal(maximumHp, copy.MaximumHp);
            Assert.Equal(maximumHp, copy.CurrentHp);
            int physical = battle.NativeGraphics!.ActorIds.IndexOf(id);
            Assert.InRange(physical, 3, 6);
            NativeBattleGraphicsModel model = battle.NativeGraphics.Models[physical];
            Assert.Equal((ushort)7, (ushort)(model.Flags0 & 7));
            Assert.Equal((byte)2, model.EffectCounter97);
            Assert.NotNull(model.ActiveResourceRow100);
            Assert.NotNull(model.BodyTextureSlots);
        }
        BattleSession restored = BattleSession.Restore(battle.Capture());
        Assert.Equal(BattleEncounterAllocationPhase.Ready, restored.EncounterAllocation!.Phase);
        Assert.All(batch.ActorIds, id => Assert.False(restored.Combatants.Single(actor => actor.Id == id).IsActive));
    }
    [Fact]
    public void ConstructedFourthActorUsesInstalledGraphicsForCounterCompletionAndCopyRetirement()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        string? fixture = Environment.GetEnvironmentVariable("MEMORIA_FF8_ENCOUNTER_ALLOCATION_FIXTURE");
        Assert.SkipUnless(Directory.Exists(directory), "Configured installed native resources are required.");
        Assert.SkipUnless(fixture is not null && File.Exists(fixture), "An externally supplied standalone pre-reinforcement native encounter is required.");
        GameState state = GameSession.Load(fixture!);
        BattleSnapshot source = state.Battle ?? throw new InvalidDataException("The selected fixture must contain an active native battle.");
        string originalId = source.Definition.EncounterMod!.OriginalActorId;
        BattleCombatantDefinition originalDefinition = source.Definition.Combatants.Single(actor => actor.Id == originalId);
        string originalContract = JsonSerializer.Serialize(originalDefinition);
        NativeBattleAiDefinition originalAi = originalDefinition.NativeAi!;
        // This fourth actor is a constructed integration participant using the selected installed archetype, not an original story formation.
        NativeBattleAiDefinition fourthAi = originalAi with { Scripts = [[0], [4, 201, 12, 0, 0], [0], [0], [18, 220, 1, 9, 18, 4, 201, 12, 0, 0]], Texts = [] };
        BattleCombatantDefinition fourthPrototype = originalDefinition with
        {
            Id = source.Definition.Combatants.Single(actor => !actor.IsPlayer && actor.SceneSlot == 1).Id,
            Name = "Graphical fourth integration actor",
            SceneSlot = 1,
            InitialActive = false,
            InitialAtb = 0,
            MaximumHpMultiplier = 1,
            MaximumHp = originalDefinition.NativeTemplate!.Levels[originalDefinition.Level].MaximumHp,
            CurrentHp = originalDefinition.NativeTemplate.Levels[originalDefinition.Level].MaximumHp,
            NativeAi = fourthAi,
            NativeCombatantFlags = originalDefinition.NativeCombatantFlags & ~0xE3u,
            NativeTemplate = originalDefinition.NativeTemplate with { NativeCombatantFlags = originalDefinition.NativeTemplate.NativeCombatantFlags & ~0xE3u },
            Rewards = null,
            RewardSlot = null,
            NativeMug = null
        };
        BattleSession seed = new(new() { NativeClockRules = source.Definition.NativeClockRules, NativeBattleSpeed = source.Definition.NativeBattleSpeed, NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), Combatants = [new() { Id = "seed-party", Name = "Seed party", IsPlayer = true }, fourthPrototype] }, BattleAtbMode.TurnBased);
        source = source with
        {
            Mode = BattleAtbMode.TurnBased,
            Definition = source.Definition with { Combatants = source.Definition.Combatants.Select(actor => actor.Id == fourthPrototype.Id ? fourthPrototype : actor).ToArray() },
            Combatants = source.Combatants.Select(actor => actor.Id == fourthPrototype.Id ? seed.Capture().Combatants.Single(seedActor => seedActor.Id == fourthPrototype.Id) : actor).ToArray()
        };
        using GameWorld world = new(new GameSession(state with { Battle = source }), directory, validateWorldPresentation: false);
        BattleSession battle = world.Battle!;
        BattleCombatant original = battle.Combatants.Single(actor => actor.Id == originalId);
        int originalHp = original.CurrentHp;
        BattleCombatant fourth = Invoke<BattleCombatant>(battle, "AllocateNativeEnemy", original, 1, 6, true);
        TickUntil(battle, () => fourth.IsActive && battle.NativeGraphics!.Models[6].EffectCounter97 == 2 && Drained(battle), "fourth native model allocation");
        Invoke<object?>(battle, "QueueNativeCallback", original, (ushort)1, (byte)0);
        TickUntil(battle, () => battle.EncounterAllocation is { Phase: BattleEncounterAllocationPhase.Complete } && Drained(battle), "source-authored Summon and installed arrival");
        BattleEncounterAllocationState batch = battle.EncounterAllocation!;
        Assert.Equal(2, batch.ActorIds.Length);
        Assert.Equal(originalHp, original.CurrentHp);
        Assert.Equal(originalContract, JsonSerializer.Serialize(battle.ReadDefinition().Combatants.Single(actor => actor.Id == originalId)));
        Assert.All(batch.ActorIds, id => Assert.True(battle.Combatants.Single(actor => actor.Id == id).IsActive));
        Assert.Equal(6, battle.NativeGraphics!.ActorIds.IndexOf(fourth.Id));
        string player = battle.Combatants.First(actor => actor.IsPlayer && actor.IsAlive && actor.Atb == BattleSession.AtbMaximum).Id;
        int partyHp = battle.Combatants.Where(actor => actor.IsPlayer).Sum(actor => actor.CurrentHp);
        battle.QueueNativePlayerCommand(player, fourth.Id, 1, 0);
        TickUntil(battle, () => battle.NativeActionPackets.ActionCount >= 2, "player header and fourth immediate-counter child");
        NativeBattleActionHeaderSlot child = Assert.Single(battle.NativeActionPackets.Headers.Take(battle.NativeActionPackets.ActionCount), header => header.ReadByte(0) == 6 && header.ReadByte(1) == originalAi.Abilities[0].Command && System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(header.Payload.AsSpan().Slice(4)) == originalAi.Abilities[0].Id);
        Assert.NotNull(child.FirstContactIndex);
        Assert.Equal(1u, battle.Capture().Combatants.Single(actor => actor.Id == fourth.Id).NativeParticipant!.Variables[0]);
        BattleSnapshot accepted = JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!;
        BattleSession restored = (BattleSession)typeof(GameWorld).GetMethod("AttachNativeEffectResources", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(world, [BattleSession.Restore(accepted)])!;
        TickUntil(battle, () => Drained(battle), "native player/counter event, child header and effect completion");
        TickUntil(restored, () => Drained(restored), "restored native player/counter event completion");
        Assert.False(fourth.IsBusy);
        Assert.True(battle.Combatants.Where(actor => actor.IsPlayer).Sum(actor => actor.CurrentHp) < partyHp);
        Assert.Equal(1u, battle.Capture().Combatants.Single(actor => actor.Id == fourth.Id).NativeParticipant!.Variables[0]);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        int counterEndPartyHp = battle.Combatants.Where(actor => actor.IsPlayer).Sum(actor => actor.CurrentHp);
        Invoke<object?>(battle, "QueueNativeCallback", fourth, (ushort)1, (byte)0);
        TickUntil(battle, () => Drained(battle), "fourth next ordinary installed ability");
        Assert.False(fourth.IsBusy);
        Assert.True(battle.Combatants.Where(actor => actor.IsPlayer).Sum(actor => actor.CurrentHp) < counterEndPartyHp);
        int[] copyRows = batch.ActorIds.Select(id => battle.NativeGraphics.Models[battle.NativeGraphics.ActorIds.IndexOf(id)].TextureResourceIndex!.Value).ToArray();
        int[] privateLengths = copyRows.Select(row => battle.NativeModelCpuResources!.Rows[row].PrivateLength4!.Value).ToArray();
        int heapBefore = battle.NativeModelCpuResources!.EnemyCursor!.Value;
        int originalRow = battle.NativeGraphics.Models[3].TextureResourceIndex!.Value;
        int originalPrivateLength = battle.NativeModelCpuResources.Rows[originalRow].PrivateLength4!.Value;
        Invoke<object?>(battle, "QueueNativeGraphicsRemoval", original);
        TickUntil(battle, () => battle.EncounterPolicies?.CleanedUp == true && !battle.HasPendingEncounterRetirements && Drained(battle), "native105 retirement with unrelated fourth alive");
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        Assert.True(fourth.IsActive);
        Assert.False(battle.EncounterPolicies!.TerminalReached);
        Assert.Empty(battle.EnemyDefeats);
        Assert.Equal(heapBefore - originalPrivateLength - privateLengths.Sum(), battle.NativeModelCpuResources.EnemyCursor);
        foreach (string id in batch.ActorIds)
        {
            NativeBattleEnemyAllocation allocation = battle.Capture().NativeAllocations.Single(allocation => allocation.Id == id);
            Assert.True(allocation.Retirement!.ResourcesReleased);
            Assert.Equal(0, battle.NativeGraphics.Models[allocation.Retirement.PhysicalIndex].Flags0 & 3);
        }
        Assert.All(copyRows.Append(originalRow), row => Assert.Equal((byte)0, battle.NativeModelTextureCache!.Rows[row].Kind));
        Assert.Equal((byte)3, battle.NativeModelTextureCache!.Rows[battle.NativeGraphics.Models[6].TextureResourceIndex!.Value].Kind);
        int removed = battle.Events.Count(entry => entry.Kind == "NativeModelRemoved");
        Invoke<object?>(battle, "QueueNativeCallback", fourth, (ushort)1, (byte)0);
        TickUntil(battle, () => Drained(battle), "fourth ordinary turn after resource compaction");
        Assert.Equal(removed, battle.Events.Count(entry => entry.Kind == "NativeModelRemoved"));
        BattleCombatant reused = Invoke<BattleCombatant>(battle, "AllocateNativeEnemy", fourth, 1, 4, true);
        TickUntil(battle, () => reused.IsActive && battle.NativeGraphics.Models[4].EffectCounter97 == 2 && Drained(battle), "released physical slot reuse through native loader");
        Assert.Equal(reused.Id, battle.NativeGraphics.ActorIds[4]);
        Assert.True(fourth.IsActive);
    }

    private static bool Drained(BattleSession battle)
    {
        return battle.NativeActionPackets.ActionCount == 0 && battle.ActiveAction is null && battle.QueuedActions.Count == 0 && !battle.NativeCommandBanks.BoundCommands.Any() && battle.NativeGraphics is { OriginalEffect: null, ActionActorId: null } && battle.NativeGraphics.Descriptors.Queue.IsEmpty && battle.NativeGraphics.Descriptors.Tasks.All(task => (task.Flags & 1) == 0 || task.Kind == NativeBattleDescriptorTaskKind.Pump500CC0);
    }

    private static void TickUntil(BattleSession battle, Func<bool> complete, string boundary)
    {
        for (int frame = 0; frame < 2000 && !complete(); frame++)
        {
            battle.Tick();
        }
        if (!complete())
        {
            string details = JsonSerializer.Serialize(new { Actors = battle.Combatants.Select(actor => new { actor.Id, actor.IsActive, actor.IsBusy, actor.Atb }), Models = battle.NativeGraphics!.Models.Select(model => new { model.Flags0, model.Kind5, model.Phase6, model.EffectCounter97 }), battle.NativeGraphics.ActionActorId, Effect = battle.NativeGraphics.OriginalEffect is not null, Headers = battle.NativeActionPackets.ActionCount, battle.ActiveAction, Queued = battle.QueuedActions, Bound = battle.NativeCommandBanks.BoundCommands, DescriptorQueue = battle.NativeGraphics.Descriptors.Queue, Tasks = battle.NativeGraphics.Descriptors.Tasks.Where(task => (task.Flags & 1) != 0).Select(task => new { task.Kind, task.Flags, task.State }) });
            Assert.Fail("Installed graphical integration did not complete: " + boundary + "; " + details);
        }
    }

    private static T Invoke<T>(BattleSession battle, string method, params object[] arguments)
    {
        object? result = typeof(BattleSession).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(battle, arguments);
        return (T)result!;
    }

}
