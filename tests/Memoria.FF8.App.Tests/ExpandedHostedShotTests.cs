using System.Reflection;
using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ExpandedHostedShotTests
{
    private const string IrvineId = "party-2";
    private const int CallbackBudget = 1200;

    [Fact]
    public void Installed_fire_ammo_finishes_the_shot_event_before_the_enemy_death_callback()
    {
        string directory = TestResources.GameDirectory;
        GameSession session = GameWorld.CreateColdProcess(directory);
        session.StartNewGame();
        using GameWorld world = new(session, directory);
        (int Formation, int Slot)[] placements = [(385, 0), (318, 0), (370, 0), (717, 0), (388, 2), (659, 0), (92, 1), (673, 2)];
        ImmutableArray<BattleEnemyRosterSource> donors = placements.Select(row => new BattleEnemyRosterSource(row.Formation, row.Slot, world.BattleResources.FormationSourceHash(row.Formation))).ToImmutableArray();
        DiagnosticCharacterPreparation irvine = new(2, 2, [], []) { HitPoints = 1, Level = 100 };
        BattleInput command = new(BattleInputKind.Action, ActorId: IrvineId, TargetId: "enemy-0", Ability: BattleAbility.LimitBreak, ActionId: 14 << 16 | 1);
        DiagnosticBattleRequest request = new(390, 0, 128, 0, 0)
        {
            BattleMusic = 5,
            PrimaryMusicChannel = -1,
            Fixture = new([], [irvine], true) { CombatParty = [2, 255, 255], Items = [new(102, 3)] },
            Target = new("enemy-0", 99, 32, 0, 3, 0xC3u, 1, null, null),
            Roster = new(donors, 0) { DonorCount = 4 },
            Driver = new(command, 192) { ExpectedNativeCommand = 237, ExpectedNativeAction = 1, LimitSelection = new(1, 4, 256) }
        };
        BattleSession battle = world.StartDiagnosticBattle(request);
        ExpandedBattlePresentationProcess process = Assert.IsType<ExpandedBattlePresentationProcess>(battle.ExpandedPresentationProcess);
        List<BattleDiagnosticEffectDispatch> shotDispatches = [];
        battle.DiagnosticEffectDispatched += dispatch =>
        {
            if (dispatch.ActorId == IrvineId && dispatch.ExecutedEffect == 192)
            {
                shotDispatches.Add(dispatch);
            }
        };
        TickUntil(() => RecreateGame.DiagnosticCommandReady(battle, request.Driver!), "ready Irvine");
        Assert.True(battle.SelectDiagnosticLimit(IrvineId, request.Driver!.LimitSelection!).Accepted);
        world.ApplyBattleInput(command);
        TickUntil(() => battle.GetNativeShot(IrvineId)?.Phase == BattleNativeShotPhase.Ready, "Shot opening");
        Assert.True(battle.TriggerNativeShot(IrvineId));
        TickUntil(() => battle.GetNativeShot(IrvineId)?.Phase == BattleNativeShotPhase.Ready, "Fire Ammo readiness");
        Assert.Contains(battle.QueuedActions, pending => pending.ExpandedAiCallback is { Stage: 2 or 3 });
        battle.EndNativeShot(IrvineId);
        Assert.Contains(battle.QueuedActions, pending => pending.ActorId == IrvineId && pending.NativeLimitCallback == NativeLimitCallback.ShotEnding);
        TickUntil(() => battle.GetNativeShot(IrvineId) is null && !process.HasActiveAction && process.PendingCompletion is null, "Shot close");
        Assert.Equal(new byte?[] { 14, 237, 238 }, shotDispatches.Select(dispatch => dispatch.NativeCommand).ToArray());
        Assert.True(battle.Combatants.Single(actor => actor.Id == IrvineId).IsAlive);
        Assert.Null(battle.GetNativeShot(IrvineId));
        Assert.DoesNotContain(battle.QueuedActions, pending => pending.ActorId == IrvineId && pending.NativeLimitCallback is not null);
        Assert.Equal(2, battle.Items.Single(stock => stock.Id == 102).Count);
        Assert.Contains(battle.QueuedActions, pending => pending.ExpandedAiCallback is { Stage: 2 or 3 });
        TickUntil(() => battle.QueuedActions.Count == 0 && !process.HasActiveAction && process.PendingCompletion is null, "queued enemy reactions");

        void TickUntil(Func<bool> condition, string boundary)
        {
            for (int frame = 0; frame < CallbackBudget && !condition(); frame++)
            {
                if (battle.NativeEntryReady && battle.ObserveDiagnosticDrainBlockers().Count == 0)
                {
                    battle.AdvanceTurn();
                }
                battle.Tick();
            }
            if (!condition())
            {
                string queue = string.Join(", ", battle.QueuedActions.Select(pending => $"{pending.ActorId}/{pending.ExpandedAiCallback?.Stage}/{pending.NativeLimitCallback}"));
                string details = $"Shot {battle.GetNativeShot(IrvineId)?.Phase}, active {battle.ActiveAction?.ActorId}/{battle.ActiveAction?.NativeLimitCallback}, token {process.ActiveActionToken}, effect {process.Graphics.OriginalEffect?.RequestedEffect}/{process.Graphics.OriginalEffect?.ReturnedDescriptor:X}, queue [{queue}].";
                world.AbandonFaultedDiagnosticBattle();
                Assert.Fail($"Did not reach {boundary}. {details}");
            }
        }
    }

    [Theory]
    [InlineData(0, 2, "Manual")]
    [InlineData(1, 1, "Ammo")]
    [InlineData(0, 2, "Timer")]
    [InlineData(1, 1, "Terminal")]
    [InlineData(0, 2, "BeforeShot")]
    public void Installed_Shot_uses_actual_opening_readiness_contact_and_ending(int ammo, byte stock, string close)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameState field = ExpandedBattleEffectMatrixTests.PrepareField(directory, assets.ReadColdProcessNativeBattleMemory()) with { GlobalRandom = new(917) };
        GameSession session = new(field);
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        GameWorld world = new(session, directory, sharedAssets: assets);
        try
        {
            ExpandedBattleInstalledEntry.Prepare(world, session, current => PrepareParty(current, ammo + 101, stock));
            RunShot(world, ammo, stock, close);
        }
        catch (Exception failure)
        {
            try
            {
                world.Dispose();
            }
            catch (Exception cleanup)
            {
                throw new AggregateException(failure, cleanup);
            }
            throw;
        }
        world.Dispose();
    }

    private static void PrepareParty(GameSession session, int itemId, byte stock)
    {
        NativeCharacterVitals vitals = (session.State.CharacterVitals! with { CombatParty = [0, 1, 3] }).WithGuestBattleRecords().Rest();
        NativeCharacterVital[] records = vitals.Records.ToArray();
        foreach (int ally in new[] { 0, 1, 3 })
        {
            records[ally] = records[ally] with { MemberFlags = (ushort)(records[ally].MemberFlags | 1), HitPoints = new(0, 0), Status = 1 };
        }
        records[2] = records[2] with { MemberFlags = (ushort)(records[2].MemberFlags | 1), HitPoints = new(1, 1), Status = 0 };
        session.SetCharacterVitals((vitals with { Records = records }).WithBattleParty([0, 1, 3, 2], 4));
        NativeEconomy economy = session.State.Economy!;
        NativeItemStock[] items = economy.Items.ToArray();
        int existing = Array.FindIndex(items, item => item.ItemId == itemId);
        items[existing >= 0 ? existing : Array.FindIndex(items, item => item.ItemId == 0)] = new((byte)itemId, stock);
        session.SetEconomy(economy with { Items = items });
    }

    private static void RunShot(GameWorld world, int ammo, byte stock, string close)
    {
        BattleSession battle = ExpandedBattleInstalledEntry.Start(world);
        ExpandedBattlePresentationProcess process = Assert.IsType<ExpandedBattlePresentationProcess>(battle.ExpandedPresentationProcess);
        BattleSession child = Assert.IsType<BattleSession>(typeof(ExpandedBattlePresentationProcess).GetField("presentation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(process));
        BattleCombatant irvine = Assert.Single(battle.Combatants, actor => actor.Id == IrvineId);
        BattleModelOwner source = battle.ReadPresentationModelOwner(IrvineId);
        Assert.False(child.NativeActionPackets.ModelAliases.TryEncodeExact(source, out _));
        Assert.Equal(4, battle.Combatants.Count(actor => actor.IsPlayer));
        Assert.Equal(5, battle.Combatants.Count(actor => !actor.IsPlayer));
        BattleCombatant[] enemies = battle.Combatants.Where(IsAvailableEnemy).ToArray();
        BattleCombatant target = enemies.First(enemy => !child.NativeActionPackets.ModelAliases.TryEncodeExact(battle.ReadPresentationModelOwner(enemy.Id), out _));
        BattleNativeAction effect = Assert.Single(battle.ReadDefinition().NativeActions, action => action.Command == 237 && action.ActionId == ammo);
        bool group = (effect.TargetFlags!.Value & 0x10) == 0;
        Assert.Equal(ammo == 1, group);
        int item = ammo + 101;
        irvine.Atb = BattleSession.AtbMaximum;
        int begin = battle.Events.Count;
        Dictionary<byte, NativeBattleContactPacket[]> contacts = [];
        List<NativeBattlePresentationReceipt> receipts = [];
        if (close == "BeforeShot")
        {
            world.ApplyBattleInput(new(BattleInputKind.Attack, ActorId: IrvineId, TargetId: enemies[0].Id));
            AdvanceUntil(() => CompletedCount() == 1);
            return;
        }
        Assert.NotEqual((byte)0, battle.RefreshNativeLimit(IrvineId));
        world.ApplyBattleInput(new(BattleInputKind.Action, ActorId: IrvineId, TargetId: target.Id, Ability: BattleAbility.LimitBreak, ActionId: (14 << 16) | ammo));

        Assert.Equal(BattleNativeShotPhase.Opening, battle.GetNativeShot(IrvineId)!.Phase);
        Assert.False(battle.TriggerNativeShot(IrvineId));
        AdvanceUntil(() => battle.GetNativeShot(IrvineId)?.Phase == BattleNativeShotPhase.Ready && CompletedCount() == 1);
        Assert.Equal(stock, Stock());
        Assert.Equal(group ? enemies.Length : 1, contacts[14].Length);
        Assert.Empty(receipts.OfType<NativeBattlePresentationContactReceived>());
        Assert.Single(receipts.OfType<NativeBattleShotReadinessReached>(), ready => ready.Shot.Callback == 0x48D1A0);

        if (close == "Timer")
        {
            short remaining = battle.GetNativeShot(IrvineId)!.RemainingTimer;
            battle.AdvanceNativeShotPresentation(TimeSpan.FromSeconds((remaining + 1d) / BattleSession.FramesPerSecond));
            Assert.Equal(BattleNativeShotPhase.Ending, battle.GetNativeShot(IrvineId)!.Phase);
        }
        else
        {
            if (close == "Terminal")
            {
                foreach (BattleCombatant enemy in enemies)
                {
                    enemy.CurrentHp = 1;
                }
            }
            world.ApplyBattleInput(new(BattleInputKind.ShotTrigger, ActorId: IrvineId));
            Assert.Equal(BattleNativeShotPhase.AwaitingEffect, battle.GetNativeShot(IrvineId)!.Phase);
            Assert.False(battle.TriggerNativeShot(IrvineId));
            Assert.Equal(stock - 1, Stock());
            AdvanceUntil(() => CompletedCount() >= 2);
            BattleModelOwner[] selected = (group ? enemies : [target]).Select(enemy => battle.ReadPresentationModelOwner(enemy.Id)).ToArray();
            Assert.Equal(selected, contacts[237].Select(contact => contact.Primary.Recipient));
            Assert.Single(receipts.OfType<NativeBattleShotReadinessReached>(), ready => ready.Shot.Callback == 0x4AD7C0);
            Assert.All(contacts[237], contact => Assert.Contains(receipts.OfType<NativeBattlePresentationContactReceived>(), receipt => receipt.Packet.Occurrence == contact.Occurrence));
            if (close == "Manual")
            {
                Assert.Equal(BattleNativeShotPhase.Ready, battle.GetNativeShot(IrvineId)!.Phase);
                battle.EndNativeShot(IrvineId);
            }
        }

        BattleModelOwner[] closingGroup = battle.Combatants.Where(IsAvailableEnemy).Select(enemy => battle.ReadPresentationModelOwner(enemy.Id)).ToArray();
        AdvanceUntil(() => battle.GetNativeShot(IrvineId) is null && battle.ActiveAction is null && process.ActiveActionToken is null);
        Assert.Equal(close == "Timer" ? stock : stock - 1, Stock());
        Assert.False(irvine.IsBusy);
        if (close == "Terminal")
        {
            string survivors = string.Join(", ", battle.Combatants.Where(IsAvailableEnemy).Select(enemy => $"{enemy.Id}/model{enemy.ModelId}:hp={enemy.CurrentHp}"));
            string impacts = string.Join(", ", contacts[237].Select(contact => $"{contact.Primary.Recipient}:amount={contact.Primary.PresentedAmount}/flags={contact.Primary.HitFlags:X2}"));
            Assert.True(!battle.Combatants.Any(IsAvailableEnemy), $"The final group shot left eligible enemies: {survivors}. Header contacts: {impacts}.");
            NativeBattleContactPacket[] closing = contacts[238];
            Assert.Equal(enemies.Select(enemy => battle.ReadPresentationModelOwner(enemy.Id)), closing.Select(contact => contact.Primary.Recipient));
            Assert.All(closing, contact =>
            {
                Assert.Equal(0, contact.Primary.PresentedAmount);
                Assert.Contains(receipts.OfType<NativeBattlePresentationContactReceived>(), receipt => receipt.Packet.Occurrence == contact.Occurrence && receipt.Packet.Primary.PresentedAmount == 0);
            });
            AdvanceUntil(() => battle.Outcome == BattleOutcome.Victory);
            Assert.True(process.TerminalFinished);
            world.ApplyBattleInput(new(BattleInputKind.Return));
            Assert.Null(world.Battle);
            return;
        }
        Assert.Equal(closingGroup, contacts[238].Select(contact => contact.Primary.Recipient));
        Assert.All(contacts[238], contact =>
        {
            Assert.Equal(0, contact.Primary.PresentedAmount);
            Assert.Contains(receipts.OfType<NativeBattlePresentationContactReceived>(), receipt => receipt.Packet.Occurrence == contact.Occurrence && receipt.Packet.Primary.PresentedAmount == 0);
        });
        int firstAttack = battle.Events.Count;
        irvine.Atb = BattleSession.AtbMaximum;
        BattleCombatant survivor = battle.Combatants.First(IsAvailableEnemy);
        world.ApplyBattleInput(new(BattleInputKind.Attack, ActorId: IrvineId, TargetId: survivor.Id));
        AdvanceUntil(() => battle.Events.Skip(firstAttack).Any(entry => entry is { Kind: "ActionComplete", ActorId: IrvineId }));

        int CompletedCount() => battle.Events.Skip(begin).Count(entry => entry is { Kind: "ActionComplete", ActorId: IrvineId });
        int Stock() => battle.Items.SingleOrDefault(itemStock => itemStock.Id == item)?.Count ?? 0;

        void AdvanceUntil(Func<bool> completed)
        {
            for (int frame = 0; frame < CallbackBudget && !completed(); frame++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
                Assert.Same(process, battle.ExpandedPresentationProcess);
                foreach (var header in child.NativeActionPackets.PublishedHeaders().Where(header => header.Slot.Source == source && header.Slot.Command is 14 or 237 or 238))
                {
                    if (!contacts.ContainsKey(header.Slot.Command))
                    {
                        contacts.Add(header.Slot.Command, child.NativeActionPackets.ReadHeaderContacts(header.Reference).ToArray());
                    }
                }
                if (process.LastFrameServices is { } services)
                {
                    receipts.AddRange(services.ReadOrderedReceipts());
                }
            }
            if (!completed())
            {
                NativeBattleGraphicsState graphics = child.NativeGraphics!;
                string queue = string.Join(", ", graphics.Descriptors.Queue.Select(slot => $"{graphics.Descriptors.Packets[slot].Command}/{graphics.Descriptors.Packets[slot].PriorityState:X2}"));
                string logical = battle.ActiveAction is { } action ? $"{action.Ability}/{action.Phase}/{action.NativeLimitCallback}" : "none";
                NativeBattleGraphicsModel model = graphics.ActorModels.Get(source);
                byte? sequence = graphics.ActionGraphics is { } active ? child.NativeActionPackets.ReadHeader(active.Header).Sequence : null;
                string track = model.SequenceState is { } state ? $"offset={state.Offset46:X}, pending={state.Pending3:X}, callback={state.Callback42:X}" : "absent";
                Assert.Fail($"Shot did not reach its expected boundary. Phase={battle.GetNativeShot(IrvineId)?.Phase}; action={logical}; token={process.ActiveActionToken}; outcome={battle.Outcome}; descriptor queue={queue}; callback={graphics.ActionGraphics?.Callback:X}; source flags={model.Flags0:X}/{model.ResourceFlags44:X}; header sequence={sequence}; model sequence={model.Sequence}, frame={model.SequenceFrame}, {track}; headers={string.Join(",", contacts.Keys)}.");
            }
        }
    }

    private static bool IsAvailableEnemy(BattleCombatant enemy) => !enemy.IsPlayer && enemy.IsActive && enemy.IsAlive && !enemy.Untargetable && !enemy.IsEjected;
}
