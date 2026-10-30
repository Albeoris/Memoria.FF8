using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class DiagnosticInvocationAftercareTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Real_no_op_callback_releases_its_bank_and_completes_its_native_descriptor_without_changing_main_receipt(bool terminalTexture)
    {
        BattleSession plain = Fixture(false, terminalTexture);
        BattleSession observed = Fixture(true, terminalTexture);
        CompleteMain(plain);
        CompleteMain(observed);
        BattleDiagnosticInvocationRelease main = Assert.IsType<BattleDiagnosticInvocationRelease>(observed.LastDiagnosticInvocationCompletion);
        Assert.True(main.Released);
        Assert.True(main.Owners.ActionOrCommand);
        Assert.Equal(terminalTexture, main.Owners.AuxiliaryHead);
        Assert.Equal(BattleDiagnosticInvocationRelease.AftercareBoundary, main.Boundary);
        BattleDiagnosticInvocationAftercarePlan plan = Assert.IsType<BattleDiagnosticInvocationAftercarePlan>(main.Aftercare);
        Assert.Equal((byte)0, plan.Callback.Bank);
        Assert.Equal((byte)0, plan.Callback.Subcommand);
        Assert.Equal((ushort)2, plan.Callback.Stage);
        Assert.Equal("party", plan.Callback.ActorId);
        Assert.Equal((byte)255, plan.Callback.Packet.ReadByte(1));
        Assert.Equal(3, plan.CommandBanks.Length);
        foreach (BattleDiagnosticCommandBankClosure bank in plan.CommandBanks)
        {
            Assert.Equal(observed.NativeCommandBanks.Banks[bank.Bank].Tail, bank.Tail);
            Assert.Equal(observed.NativeCommandBanks.Banks[bank.Bank].Links, bank.Links);
            Assert.Equal(11, bank.Links.Length);
        }
        Assert.Single(observed.NativeCommandBanks.BoundCommands);
        Assert.Null(observed.LastDiagnosticInvocationAftercare);
        string sealedMain = NativeBattleTestSnapshots.Json(main);
        List<BattleDiagnosticInvocationAftercareReceipt> receipts = [];
        observed.DiagnosticInvocationAftercareCompleted += receipts.Add;

        DispatchChild(plain);
        DispatchChild(observed);
        Assert.Empty(observed.NativeCommandBanks.BoundCommands);
        Assert.Null(observed.LastDiagnosticInvocationAftercare);
        NativeBattleDescriptorPacket child = observed.NativeGraphics!.Descriptors.Packets[Assert.Single(observed.NativeGraphics.Descriptors.Queue)];
        Assert.Equal((ushort)10, child.Command);
        Assert.Equal(0x47DDA0u, child.Argument);
        CompleteChild(plain);
        CompleteChild(observed);

        BattleDiagnosticInvocationAftercareReceipt receipt = Assert.Single(receipts);
        Assert.Same(receipt, observed.LastDiagnosticInvocationAftercare);
        Assert.True(receipt.NativeEventCompleted);
        Assert.True(receipt.BankReleased);
        Assert.True(receipt.PacketRemoved);
        Assert.Equal((byte)255, receipt.ReleasedBankPacket.ReadByte(0));
        Assert.Equal((byte)0, receipt.ReleasedBankLink.State);
        Assert.Equal(receipt.HandlerRandomBefore, receipt.HandlerRandomAfter);
        Assert.Equal(0, receipt.ProducedActions);
        Assert.Equal(0, receipt.ProducedHeaders);
        Assert.Equal(0, receipt.ProducedContacts);
        Assert.Equal(receipt.DispatchSerialBefore, receipt.DispatchSerialAfter);
        Assert.Equal(plan.FocalSerial, receipt.FocalSerial);
        Assert.Equal(plan.MainCallback, receipt.MainCallback);
        Assert.Equal(plan.LifecycleId, receipt.LifecycleId);
        Assert.Equal((byte)0, receipt.RemovedCompletionPacket.PriorityState);
        Assert.True(receipt.CompletionPacket.Bytes.SetItem(1, 0).AsSpan().SequenceEqual(receipt.RemovedCompletionPacket.Bytes.AsSpan()));
        Assert.False(receipt.CleanupOwners.ActionOrCommand);
        Assert.Equal(0, receipt.CleanupOwners.DescriptorPackets);
        Assert.Equal(terminalTexture, receipt.CleanupOwners.AuxiliaryHead);
        Assert.Equal(3, receipt.CommandBanks.Length);
        foreach (BattleDiagnosticCommandBankClosure bank in receipt.CommandBanks)
        {
            Assert.Equal((byte)255, bank.Tail);
            Assert.Equal(observed.NativeCommandBanks.Banks[bank.Bank].Links, bank.Links);
            Assert.All(bank.Links, link =>
            {
                Assert.Equal((byte)0, link.Previous);
                Assert.Equal((byte)0, link.Next);
                Assert.Equal((byte)0, link.State);
            });
        }
        Assert.Equal(sealedMain, NativeBattleTestSnapshots.Json(main));
        Assert.Same(main, observed.LastDiagnosticInvocationCompletion);
        Assert.Equal(NativeBattleTestSnapshots.Json(plain.Capture()), NativeBattleTestSnapshots.Json(observed.Capture()));
        Assert.Equal(plain.Events, observed.Events);
        Call(observed, "ObserveDiagnosticInvocationAftercareFrameEnd");
        Assert.Single(receipts);
        string sealedCleanup = NativeBattleTestSnapshots.Json(receipt);
        Call(observed, "NextRandomByte");
        Assert.Equal(sealedMain, NativeBattleTestSnapshots.Json(main));
        Assert.Equal(sealedCleanup, NativeBattleTestSnapshots.Json(receipt));
        Assert.Single(receipts);
    }

    [Theory]
    [InlineData("unknown-passives")]
    [InlineData("counter")]
    [InlineData("auto-potion")]
    [InlineData("angelo-model")]
    [InlineData("wrong-target")]
    [InlineData("missing-target")]
    [InlineData("wrong-stage")]
    [InlineData("preexisting-callback")]
    [InlineData("extra-bank")]
    [InlineData("unbound-bank")]
    [InlineData("active-texture")]
    public void Main_boundary_does_not_arm_aftercare_for_unproved_callback_or_live_owner(string fault)
    {
        BattleSession session = Fixture(true, true, fault switch { "counter" => 4u, "auto-potion" => 0x40000u, _ => 0u }, fault == "angelo-model" ? 4 : 0, fault == "wrong-target" ? (ushort)8 : fault == "missing-target" ? null : (ushort)1);
        if (fault == "unknown-passives")
        {
            BattleCombatant actor = session.Combatants[0];
            typeof(BattleCombatant).GetField("definition", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(actor, actor.SourceDefinition with { NativePlayerStatRules = Rules(null) });
            Call(session, "QueueNativeReaction", actor, 2);
            TargetInvocationException error = Assert.Throws<TargetInvocationException>(() => Pump(session));
            Assert.IsType<NotSupportedException>(error.InnerException);
            Assert.Null(session.LastDiagnosticInvocationAftercare);
            Assert.Null(session.LastDiagnosticInvocationCompletion);
            return;
        }
        if (fault == "preexisting-callback")
        {
            Call(session, "QueueNativeCallback", session.Combatants[0], (ushort)2, (byte)0);
        }
        if (fault == "extra-bank")
        {
            Call(session, "QueueNativeCallback", session.Combatants[0], (ushort)2, (byte)1);
        }
        if (fault == "unbound-bank")
        {
            NativeBattleCommandBanks banks = session.NativeCommandBanks.EnqueueCallback(1, 0, 2).State;
            banks.Validate();
            Assert.Empty(banks.BoundCommands);
            Assert.NotNull(banks.FirstEligible(_ => false));
            Field("nativeCommandBanks").SetValue(session, banks);
        }
        if (fault == "active-texture")
        {
            NativeBattleGraphicsState graphics = session.NativeGraphics!;
            Set(session, nameof(BattleSession.NativeGraphics), graphics with { Models = graphics.Models.SetItem(0, graphics.Models[0] with { Sequence = 12 }) });
        }
        Call(session, "QueueNativeReaction", session.Combatants[0], fault == "wrong-stage" ? 3 : 2);
        Pump(session);
        EndFrame(session);
        BattleDiagnosticInvocationRelease main = Assert.IsType<BattleDiagnosticInvocationRelease>(session.LastDiagnosticInvocationCompletion);
        Assert.False(main.Released);
        Assert.Null(main.Aftercare);
        Assert.Null(session.LastDiagnosticInvocationAftercare);
        Assert.NotEmpty(session.NativeCommandBanks.BoundCommands);
    }

    [Theory]
    [InlineData("no-handler")]
    [InlineData("no-event")]
    [InlineData("no-bank-release")]
    [InlineData("no-removal-witness")]
    [InlineData("packet-reuse")]
    [InlineData("callback")]
    [InlineData("simulation")]
    [InlineData("graphics")]
    [InlineData("lifecycle")]
    [InlineData("live-owner")]
    [InlineData("texture-changed")]
    [InlineData("texture-replaced")]
    [InlineData("unbound-bank")]
    public void Cleanup_requires_real_handler_event_retirement_and_same_boundary_with_known_owners(string fault)
    {
        BattleSession session = Fixture(true, true);
        CompleteMain(session);
        BattleDiagnosticInvocationRelease main = Assert.IsType<BattleDiagnosticInvocationRelease>(session.LastDiagnosticInvocationCompletion);
        Assert.True(main.Released);
        string before = NativeBattleTestSnapshots.Json(main);
        if (fault == "no-bank-release")
        {
            Set(session, nameof(BattleSession.NativeHostCallbacks), 21L);
            object?[] arguments = [null];
            Assert.True((bool)typeof(BattleSession).GetMethod("TryTakeNativeBankCommand", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, arguments)!);
            BattleAttackCommand command = Assert.IsType<BattleAttackCommand>(arguments[0]);
            Call(session, "DispatchNativeCallback", command.NativeCallback!);
            Assert.Single(session.NativeCommandBanks.BoundCommands);
        }
        else if (fault != "no-handler")
        {
            DispatchChild(session);
        }
        if (fault is not ("no-handler" or "no-event"))
        {
            if (fault == "no-removal-witness")
            {
                NativeBattleGraphicsState graphics = session.NativeGraphics!;
                NativeBattleGraphicsState current = graphics;
                NativeBattleDescriptorState final = graphics.Descriptors.Advance((state, _) =>
                {
                    current = (NativeBattleGraphicsState)Call(session, "RunNativeDescriptorActionCallback", current with { Descriptors = state }, 0x47DDA0u)!;
                    return new(current.Descriptors, 15);
                }, (_, _) => throw new InvalidOperationException("Unexpected child descriptor task."));
                Set(session, nameof(BattleSession.NativeGraphics), current with { Descriptors = final });
            }
            else
            {
                Pump(session);
            }
        }
        NativeBattleGraphicsState post = session.NativeGraphics!;
        if (fault == "packet-reuse")
        {
            NativeBattleDescriptorState descriptors = post.Descriptors;
            for (int index = 0; index < 256; index++)
            {
                descriptors = descriptors.Enqueue(10, 128, 0x47DDA0).State;
                descriptors = descriptors.Advance((state, _) => new(state, 15), (_, _) => throw new InvalidOperationException("Unexpected recycled descriptor task."));
            }
            post = post with { Descriptors = descriptors };
        }
        if (fault == "callback")
        {
            Set(session, nameof(BattleSession.NativeHostCallbacks), 22L);
        }
        if (fault == "simulation")
        {
            Set(session, nameof(BattleSession.Frame), session.Frame + 1);
        }
        if (fault == "lifecycle")
        {
            Field("<DiagnosticLifecycleId>k__BackingField").SetValue(session, Guid.NewGuid());
        }
        if (fault == "live-owner")
        {
            Call(session, "QueueNativeCallback", session.Combatants[0], (ushort)2, (byte)1);
        }
        if (fault == "texture-changed")
        {
            post = post with { Models = post.Models.SetItem(0, post.Models[0] with { Sequence = 12 }) };
        }
        if (fault == "texture-replaced")
        {
            NativeBattleAuxiliaryTask original = post.Auxiliary.Slots[0];
            NativeBattleAuxiliaryTask replaced = original with { };
            Assert.NotSame(original, replaced);
            Assert.Equal(NativeBattleTestSnapshots.Json(original), NativeBattleTestSnapshots.Json(replaced));
            post = post with { Auxiliary = post.Auxiliary with { Slots = post.Auxiliary.Slots.SetItem(0, replaced) } };
        }
        if (fault == "unbound-bank")
        {
            NativeBattleCommandBanks banks = session.NativeCommandBanks.EnqueueCallback(1, 0, 2).State;
            banks.Validate();
            Assert.Empty(banks.BoundCommands);
            Assert.NotNull(banks.FirstEligible(_ => false));
            Field("nativeCommandBanks").SetValue(session, banks);
        }
        Set(session, nameof(BattleSession.NativeGraphics), post with { Callback = post.Callback + (fault == "graphics" ? 2 : 1) });
        Call(session, "ObserveDiagnosticInvocationAftercareFrameEnd");
        Assert.Null(session.LastDiagnosticInvocationAftercare);
        Assert.Equal(before, NativeBattleTestSnapshots.Json(main));
        Assert.Same(main, session.LastDiagnosticInvocationCompletion);
    }

    [Theory]
    [InlineData("random")]
    [InlineData("header")]
    [InlineData("contact")]
    [InlineData("action")]
    [InlineData("new-callback")]
    [InlineData("foreign-dispatch")]
    [InlineData("wrong-stage")]
    [InlineData("wrong-actor")]
    [InlineData("wrong-bank")]
    public void Handler_observation_rejects_actual_new_outputs_random_or_wrong_bound_callback(string fault)
    {
        BattleSession session = Fixture(true, false);
        CompleteMain(session);
        BattleDiagnosticInvocationRelease main = Assert.IsType<BattleDiagnosticInvocationRelease>(session.LastDiagnosticInvocationCompletion);
        Assert.True(main.Released);
        string before = NativeBattleTestSnapshots.Json(main);
        object?[] arguments = [null];
        Assert.True((bool)typeof(BattleSession).GetMethod("TryTakeNativeBankCommand", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, arguments)!);
        BattleAttackCommand command = Assert.IsType<BattleAttackCommand>(arguments[0]);
        Assert.Equal((ushort)2, command.NativeCallback!.Stage);
        if (fault == "wrong-bank")
        {
            Field("nativeDispatchCommandLocation").SetValue(session, new NativeBattleCommandLocation(1, 0));
        }
        BattleNativeCallback callback = fault == "wrong-stage" ? command.NativeCallback with { Stage = 3 } : fault == "wrong-actor" ? command.NativeCallback with { ActorIndex = 3 } : command.NativeCallback;
        Call(session, "BeginDiagnosticInvocationAftercareHandler", callback, session.Combatants[0]);
        if (fault == "random")
        {
            Call(session, "NextRandomByte");
        }
        if (fault == "header")
        {
            NativeBattleActionPacketsState packets = session.NativeActionPackets;
            (packets, _) = packets.Prepare(new(0, 252, 0, 0, 1, 1, null));
            (packets, _) = packets.Publish(0);
            Field("nativeActionPackets").SetValue(session, packets);
        }
        if (fault == "contact")
        {
            NativeBattleActionPacketsState packets = session.NativeActionPackets;
            (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
            Field("nativeActionPackets").SetValue(session, packets);
        }
        if (fault == "new-callback")
        {
            Call(session, "QueueNativeCallback", session.Combatants[0], (ushort)2, (byte)1);
        }
        if (fault == "action")
        {
            Queue<BattleAttackCommand> commands = (Queue<BattleAttackCommand>)Field("actions").GetValue(session)!;
            commands.Enqueue(new("party", "enemy", BattleAbility.Attack, 0));
        }
        if (fault == "foreign-dispatch")
        {
            Field("diagnosticDispatchSerial").SetValue(session, 2L);
        }
        Call(session, "QueueNativeActionClockCompletion", 0x47DDA0u, null, null);
        Call(session, "EndDiagnosticInvocationAftercareHandler");
        object state = Field("diagnosticAftercare").GetValue(session)!;
        Assert.True((bool)state.GetType().GetField("Failed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(state)!);
        Assert.Null(session.LastDiagnosticInvocationAftercare);
        Assert.Equal(before, NativeBattleTestSnapshots.Json(main));
    }

    private static BattleSession Fixture(bool observe, bool terminalTexture, uint? passiveFlags = 0, int model = 0, ushort? targetMask = 1)
    {
        BattleDefinition definition = new()
        {
            NativeClockRules = new(10),
            NativeBattleSpeed = 2,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, ModelId = model, MaximumHp = 100, CurrentHp = 100, NativePlayerStatRules = Rules(passiveFlags), NativeGrowth = new(100, new byte[6].ToImmutableArray()) },
                new() { Id = "enemy", Name = "Enemy", ModelId = 1, MaximumHp = 100, CurrentHp = 100 }
            ]
        };
        BattleSession session = new(definition, BattleAtbMode.Normal);
        session.Combatants[0].ReprojectNativePlayerStatistics();
        session.Combatants[0].LastNativeAction = new(0, 0, 2, 1, 0, 0);
        Call(session, "BeginNativeReactionBatch");
        NativeBattleActionPacketsState packets = session.NativeActionPackets;
        (packets, _) = packets.Prepare(new(0, 252, 0, 0, 1, 1, null));
        (packets, _) = packets.AppendContact(new(new(0, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        Field("nativeActionPackets").SetValue(session, packets);
        Field("nativeActionScratch").SetValue(session, session.Capture().NativeActionScratch! with { EventActorB0F = 0, EventLane = 2, Command = 2 });
        Set(session, nameof(BattleSession.ActiveAction), new BattleActionPresentation("party", "party", BattleActionPhase.Windup, 1, BattleAbility.EnemyAbility, 1) { NativeHeaderIndex = 0, NativeQueuedCommand = 252, NativeGraphic = new(1) });
        Set(session, nameof(BattleSession.NativeCamera), NativeBattleCameraState.Initialize());
        Set(session, nameof(BattleSession.NativeFileIo), NativeBattleFileIoState.FromColdProcess());
        Set(session, nameof(BattleSession.NativeEffectMemory), NativeBattleEffectMemory.FromColdProcess() with { ReturnedTaskPointer1D96AAC = 0, SoundPublishedFlags = 0 });
        Set(session, nameof(BattleSession.NativeHostCallbacks), 20L);
        NativeBattleAuxiliaryState auxiliary = NativeBattleAuxiliaryState.Clear();
        if (terminalTexture)
        {
            auxiliary = auxiliary.AppendTextureAnimation(0, 11, new([255], 0));
        }
        NativeBattleGraphicsState graphics = new(Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 3) { ResourceFlags44 = 10, Sequence = 11 }).ToImmutableArray(), Enumerable.Repeat<string?>(null, 7).ToImmutableArray(), 0, 0)
        {
            Auxiliary = auxiliary,
            ActionCallbackReady = false,
            ActionActorId = "party",
            OriginalEffect = new(1, 0, new(1, 1, 1), null, 0)
        };
        Set(session, nameof(BattleSession.NativeGraphics), graphics with { Descriptors = graphics.Descriptors.Enqueue(10, 128, 0x47DDA0).State });
        if (observe)
        {
            session.DiagnosticEffectDispatched += _ =>
            {
            };
            session.ConfigureDiagnosticInvocationCompletion(1, "party", 252, 1);
            Field("diagnosticDispatchSerial").SetValue(session, 1L);
            Call(session, "ObserveDiagnosticPreparedHeader", 0);
            Call(session, "PublishDiagnosticEffectDispatch", new Action<BattleDiagnosticEffectDispatch>(_ =>
            {
            }), new BattleDiagnosticEffectDispatch(1, 20, "party", BattleAbility.EnemyAbility, 1, 1, 1, 252) { NativeActorIndex = 0, NativeHeaderIndex = 0, NativeHeaderGeneration = 1, NativeTargetMask = targetMask });
        }
        return session;
    }

    private static void CompleteMain(BattleSession session)
    {
        Call(session, "QueueNativeReaction", session.Combatants[0], 2);
        Pump(session);
        EndFrame(session);
    }

    private static void DispatchChild(BattleSession session)
    {
        Set(session, nameof(BattleSession.NativeHostCallbacks), 21L);
        Call(session, "DispatchAction");
    }

    private static void CompleteChild(BattleSession session)
    {
        Pump(session);
        EndFrame(session);
        Call(session, "ObserveDiagnosticInvocationAftercareFrameEnd");
    }

    private static void Pump(BattleSession session)
    {
        Set(session, nameof(BattleSession.NativeGraphics), Call(session, "AdvanceNativeDescriptors", session.NativeGraphics!));
    }

    private static void EndFrame(BattleSession session)
    {
        Set(session, nameof(BattleSession.NativeGraphics), session.NativeGraphics! with { Callback = session.NativeGraphics.Callback + 1 });
        Call(session, "ObserveDiagnosticInvocationGraphicsFrameEnd");
    }

    private static BattleNativePlayerStatRules Rules(uint? passives)
    {
        byte[] character = new byte[36];
        for (int offset = 12; offset < 36; offset += 4)
        {
            character[offset + 1] = 1;
            character[offset + 2] = 100;
            character[offset + 3] = 1;
        }
        return new(0, 1, character.ToImmutableArray(), new byte[12].ToImmutableArray(), Enumerable.Range(0, 57).Select(_ => new byte[60].ToImmutableArray()).ToImmutableArray(), Enumerable.Range(0, 19).Select(_ => new byte[8].ToImmutableArray()).ToImmutableArray(), new byte[20].ToImmutableArray(), 0, MagicSystemFixture.Create().Definitions) { NativeStatusPassiveFlags = passives };
    }

    private static object? Call(BattleSession session, string method, params object?[] arguments) => typeof(BattleSession).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, arguments);
    private static FieldInfo Field(string name) => typeof(BattleSession).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static void Set(BattleSession session, string property, object? value) => typeof(BattleSession).GetProperty(property, BindingFlags.Instance | BindingFlags.Public)!.SetValue(session, value);
}
