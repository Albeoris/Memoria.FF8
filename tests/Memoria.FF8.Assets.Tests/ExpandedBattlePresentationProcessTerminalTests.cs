using System.Reflection;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class ExpandedBattlePresentationProcessTerminalTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Theory]
    [InlineData(BattleOutcome.Victory, 4, 0, false)]
    [InlineData(BattleOutcome.Victory, 4, 1, true)]
    [InlineData(BattleOutcome.Defeat, 1, 3, false)]
    [InlineData(BattleOutcome.Escaped, 2, 2, false)]
    [InlineData(BattleOutcome.ScriptedExit, 1, 3, false)]
    public void Attached_native_terminal_waits_for_fade_and_drained_callbacks(BattleOutcome expected, byte result, byte style, bool explicitTurn)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        var (battle, process) = Enter(game, assets, explicitTurn ? BattleAtbMode.TurnBased : BattleAtbMode.Normal, skipVictory: explicitTurn);
        int eventStart = battle.Events.Count;
        BattleCombatant[] allies = battle.Combatants.Where(actor => actor.IsPlayer).ToArray();
        foreach (BattleCombatant actor in allies)
        {
            actor.NativeCombatantFlags |= 0x80000000u;
        }

        if (expected == BattleOutcome.Defeat)
        {
            SetPauseRequest(process, true);
            battle.Tick();
            Assert.True(process.Graphics.UiServices!.PauseDE9);
        }
        if (expected is BattleOutcome.Victory or BattleOutcome.Defeat)
        {
            if (expected == BattleOutcome.Victory && !explicitTurn)
            {
                battle.QueueAttack("party-3", "enemy-8");
                for (int callback = 0; callback < 1200 && (battle.QueuedActions.Count != 0 || battle.ActiveAction is not null); callback++)
                {
                    battle.Tick();
                }
                Assert.Null(battle.ActiveAction);
                Assert.Empty(battle.QueuedActions);
                Assert.Contains(battle.Events.Skip(eventStart), item => item.Kind == "EffectContact" && item.ActorId == "party-3");
            }
            if (explicitTurn)
            {
                foreach (BattleCombatant actor in battle.Combatants.Where(actor => !actor.IsPlayer))
                {
                    actor.NativeStatus1 |= 0x1000u;
                    actor.NativeStatusTimers[12] = 0;
                }
                allies[0].Atb = 0;
                long childBefore = process.Graphics.Callback;
                Assert.Equal(1, battle.AdvanceTurn());
                Assert.Equal(childBefore, process.Graphics.Callback);
                Assert.All(battle.Combatants.Where(actor => !actor.IsPlayer), actor => Assert.NotEqual(0, actor.NativeStatus0 & 4));
                Assert.All(allies, actor => Assert.NotEqual(0u, actor.NativeCombatantFlags & 0x80000000u));
                Assert.Throws<InvalidDataException>(() => BattleSession.Restore(battle.Capture()));
            }
            else
            {
                foreach (BattleCombatant actor in battle.Combatants.Where(actor => actor.IsPlayer == (expected == BattleOutcome.Defeat)))
                {
                    actor.CurrentHp = 0;
                    actor.NativeStatus0 |= 1;
                }
                if (expected == BattleOutcome.Defeat)
                {
                    long paused = process.Graphics.Callback;
                    battle.Tick();
                    Assert.Equal(paused, process.Graphics.Callback);
                    Assert.Null(process.Graphics.NativeOutcome);
                    Assert.Equal(BattleOutcome.Active, battle.Outcome);
                    SetPauseRequest(process, false);
                }
                battle.Tick();
            }
        }
        else if (expected == BattleOutcome.Escaped)
        {
            typeof(BattleSession).GetMethod("BeginNativeEscape4862A0", PrivateInstance)!.Invoke(battle, null);
        }
        else
        {
            FieldInfo script = typeof(BattleSession).GetField("nativeScriptState", PrivateInstance)!;
            NativeBattleScriptState current = Assert.IsType<NativeBattleScriptState>(script.GetValue(battle));
            script.SetValue(battle, current with { ScriptedExitRequested = true });
            battle.Tick();
        }

        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        Assert.False(battle.CanReturnToField);
        Assert.Equal(result, process.Graphics.NativeOutcome?.ResultByte6E7);
        Assert.Equal(style, process.Graphics.NativeOutcome?.FadeStyleE01);
        Assert.False(battle.CanChoosePlayerCommand("party-3"));
        Assert.Throws<InvalidOperationException>(() => battle.QueueAttack("party-3", "enemy-8"));
        Assert.Equal((byte)0, battle.Escape.ExpandedOutcomeFrames);
        Assert.Null(battle.ExpandedEscapeScreenFade);
        if (explicitTurn)
        {
            Assert.Equal(0, battle.AdvanceTurn());
        }

        long logicalFrame = battle.Frame;
        long lastSeenGraphics = -1;
        List<NativeBattleTerminalEntered> entries = [];
        List<NativeBattleTerminalFinished> finishes = [];
        bool countdownStarted = false;
        for (int callback = 0; callback < 500; callback++)
        {
            if (process.Graphics.Callback != lastSeenGraphics && process.LastFrameServices is { } services)
            {
                var receipts = services.ReadOrderedReceipts();
                NativeBattleTerminalEntered? entered = receipts.OfType<NativeBattleTerminalEntered>().SingleOrDefault();
                if (entered is not null)
                {
                    entries.Add(entered);
                    Assert.All(allies, actor => Assert.Equal(0u, actor.NativeCombatantFlags & 0x80000000u));
                    Assert.Equal((byte)0, battle.Capture().NativeClock!.Active);
                }
                finishes.AddRange(receipts.OfType<NativeBattleTerminalFinished>());
                lastSeenGraphics = process.Graphics.Callback;
            }
            countdownStarted |= process.Graphics.NativeOutcome?.FadeDurationB0C is > 0 and < 255;
            if (battle.Outcome != BattleOutcome.Active)
            {
                break;
            }
            Assert.DoesNotContain(battle.Events.Skip(eventStart), item => item.Kind == "Outcome");
            battle.Tick();
            Assert.Equal(logicalFrame, battle.Frame);
        }
        Assert.True(countdownStarted);
        Assert.Equal(expected, battle.Outcome);
        Assert.Equal((byte)0, process.Graphics.NativeOutcome?.FadeDurationB0C);
        Assert.Equal((byte)0, process.Memory.PrimitiveProcess?.ScreenFadeEB8);
        Assert.Empty(process.Graphics.Descriptors.Queue);
        Assert.Empty(process.Graphics.Descriptors.Tasks.Where(task => (task.Flags & 1) != 0 && task.Kind != NativeBattleDescriptorTaskKind.Pump500CC0));
        Assert.Same(Assert.Single(entries).Terminal, Assert.Single(finishes).Terminal);
        Assert.Equal(expected, Assert.Single(finishes).Terminal.Outcome);
        Assert.Equal(expected != BattleOutcome.Defeat, battle.CanReturnToField);
        Assert.Equal(expected == BattleOutcome.Escaped, battle.Escape.Completed);
        Assert.Single(battle.Events.Skip(eventStart), item => item.Kind == "Outcome");
        int expectedTexts = expected is BattleOutcome.Defeat or BattleOutcome.Escaped ? 1 : 0;
        Assert.Equal(expectedTexts, battle.Events.Skip(eventStart).Count(item => item.Kind == "NativeText"));
        if (expected == BattleOutcome.Victory && !explicitTurn)
        {
            BattleModelOwner extra = battle.ReadPresentationModelOwner("party-3");
            Assert.False(battle.ReadBattlePresentationPacketBindings().Aliases.TryEncodeExact(extra, out _));
            Assert.Equal("victory-3", process.Graphics.ActorModels.Get(extra).ActiveAnimationBank);
            Assert.Null(process.Graphics.ActorModels.Get(battle.ReadPresentationModelOwner("party-1")).ActiveAnimationBank);
        }

        long finishedCallback = process.Graphics.Callback;
        battle.Tick();
        Assert.Equal(finishedCallback, process.Graphics.Callback);
        TargetInvocationException duplicate = Assert.Throws<TargetInvocationException>(() => typeof(BattleSession).GetMethod("AcknowledgeHostedTerminalFinish", PrivateInstance)!.Invoke(battle, [Assert.Single(finishes)]));
        Assert.IsType<InvalidDataException>(duplicate.InnerException);
        Assert.Single(battle.Events.Skip(eventStart), item => item.Kind == "Outcome");
    }

    [Theory]
    [InlineData(BattleOutcome.Victory, 0, 115, false)]
    [InlineData(BattleOutcome.Victory, 2, 109, false)]
    [InlineData(BattleOutcome.Defeat, 0, 0, true)]
    [InlineData(BattleOutcome.Escaped, 0, 116, true)]
    public void Original_terminal_admission_keeps_the_native_descriptor_order(BattleOutcome outcome, ushort flags, ushort animation, bool text)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        var (_, selected) = ExpandedBattlePresentationProcessEntryTests.CreateDefinitions(game, assets, 3, 4, 4, expanded: false, nativeClock: true);
        selected = selected with
        {
            NativeFlags = (ushort)(0x80 | flags),
            NativeGlobalMessages = assets.ReadNativeGlobalBattleMessages(),
            NativeMessageSpeed = game.ReadNewGameInputConfiguration().BattleMessageSpeed
        };
        BattleSession battle = new(selected, BattleAtbMode.Normal);
        NativeBattleGraphicsState graphics = battle.NativeGraphics! with { Descriptors = NativeBattleDescriptorState.Clear() };
        MethodInfo prepare = typeof(BattleSession).GetMethod("BuildNativeTerminalGraphics", PrivateInstance)!;
        NativeBattleGraphicsState queued = Assert.IsType<NativeBattleGraphicsState>(prepare.Invoke(battle, [graphics, outcome]));
        NativeBattleDescriptorPacket[] packets = queued.Descriptors.Queue.Select(slot => queued.Descriptors.Packets[slot]).ToArray();
        List<ushort> commands = [112, 10];
        if (text)
        {
            commands.Add(8);
            Assert.Equal(BattleTextIdentity.Global(outcome == BattleOutcome.Escaped ? 1 : 0), packets[2].Message!.NativeTextIdentity);
        }
        if (animation != 0)
        {
            commands.Add(animation);
        }
        commands.Add(10);
        Assert.Equal(commands, packets.Select(packet => packet.Command));
        Assert.Equal(0x47DF60u, packets[1].Argument);
        Assert.Equal(0x47DFC0u, packets[^1].Argument);
        Assert.All(packets, packet => Assert.Equal((byte)128, packet.PriorityState));
    }

    private static void SetPauseRequest(ExpandedBattlePresentationProcess process, bool paused)
    {
        BattleSession child = Assert.IsType<BattleSession>(typeof(ExpandedBattlePresentationProcess).GetField("presentation", PrivateInstance)!.GetValue(process));
        NativeBattleGraphicsState graphics = process.Graphics;
        NativeBattleUiServices ui = graphics.UiServices!;
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeGraphics))!.SetValue(child, graphics with { UiServices = paused ? ui.SetFlags(2) : ui.SetFlags(0, 2) });
    }

    [Fact]
    public void Preparing_native_terminal_rejects_missing_text_and_stale_or_foreign_graphics_boundaries()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        var (battle, process) = Enter(game, assets, BattleAtbMode.TurnBased, missingText: true);
        NativeBattleGraphicsState before = process.Graphics;
        Assert.Throws<NotSupportedException>(() => process.PrepareTerminal(battle, BattleOutcome.Defeat));
        Assert.Same(before, process.Graphics);
        Assert.Null(process.Graphics.NativeOutcome?.ResultByte6E7);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        BattleSession foreign = new(battle.ReadDefinition(), BattleAtbMode.Normal);
        Assert.Throws<InvalidOperationException>(() => process.PrepareTerminal(foreign, BattleOutcome.Victory));
        ExpandedBattlePresentationProcess.PreparedTerminal stale = process.PrepareTerminal(battle, BattleOutcome.Victory);
        battle.Tick();
        Assert.Throws<InvalidOperationException>(() => process.ValidateTerminal(stale));
        Assert.Null(process.Graphics.NativeOutcome?.ResultByte6E7);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);

        foreach (BattleCombatant enemy in battle.Combatants.Where(actor => !actor.IsPlayer))
        {
            enemy.CurrentHp = 0;
            enemy.NativeStatus0 |= 1;
        }
        long childBefore = process.Graphics.Callback;
        long parentBefore = battle.Frame;
        Assert.Equal(0, battle.AdvanceTurn());
        Assert.Equal(parentBefore, battle.Frame);
        Assert.Equal(childBefore, process.Graphics.Callback);
        Assert.Equal((byte)4, process.Graphics.NativeOutcome?.ResultByte6E7);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        Assert.False(battle.CanReturnToField);
    }

    private static (BattleSession Battle, ExpandedBattlePresentationProcess Process) Enter(GameAssets game, BattleAssets assets, BattleAtbMode mode, bool skipVictory = false, bool missingText = false)
    {
        var (definition, selected) = ExpandedBattlePresentationProcessEntryTests.CreateDefinitions(game, assets, 4, 5, 4, expanded: true, nativeClock: true);
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals().WithGuestBattleRecords();
        NativeLimitProgress options = selected.LimitProgress! with { Options978 = (ushort)(selected.LimitProgress!.Options978!.Value & ~1) };
        definition = definition with
        {
            NativeFlags = (ushort)(definition.NativeFlags | (skipVictory ? 2 : 0)),
            NativeFieldBattleMode = 0x200,
            NativeFieldBattleRules = 0,
            NativeCommandSequences = selected.NativeCommandSequences,
            NativeRetainedMemory = selected.NativeRetainedMemory,
            NativeMusicProcess = selected.NativeMusicProcess,
            NativeGlobalSoundProcess = selected.NativeGlobalSoundProcess,
            NativeGlobalMessages = missingText ? [] : assets.ReadNativeGlobalBattleMessages(),
            NativeMessageSpeed = game.ReadNewGameInputConfiguration().BattleMessageSpeed,
            LimitProgress = options,
            Combatants = definition.Combatants.OrderBy(actor => actor.IsPlayer ? actor.Id == "party-3" ? 1 : 0 : 2).Select(actor => actor.IsPlayer ? actor with
            {
                NativeAttack = assets.ReadPlayerNativeAttackData(vitals, Array.FindIndex(vitals.Records, row => row.CharacterId == actor.ModelId)),
                Attack = 35,
                NativeStatusResistances = new byte[40]
            } : actor.NativeTemplate is { } template ? actor with
            {
                MaximumHp = template.Levels[actor.Level].MaximumHp,
                CurrentHp = template.Levels[actor.Level].MaximumHp
            } : actor with { MaximumHp = 6000, CurrentHp = 6000, NativeStatusResistances = new byte[40] }).ToArray()
        };
        selected = selected with
        {
            NativeFlags = definition.NativeFlags,
            NativeGlobalMessages = definition.NativeGlobalMessages,
            NativeMessageSpeed = definition.NativeMessageSpeed,
            NativeVictoryFile = assets.ReadNativeBattleFile(766),
            NativeActionCameraRules = game.ReadNativeActionCameraRules(),
            LimitProgress = options
        };
        BattleSession battle = new(definition, mode);
        ExpandedBattlePresentationProcess process = new(battle, selected);
        battle.AttachExpandedPresentationProcess(process);
        for (int callback = 0; callback < 1200 && !battle.NativeEntryReady; callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        return (battle, process);
    }
}
