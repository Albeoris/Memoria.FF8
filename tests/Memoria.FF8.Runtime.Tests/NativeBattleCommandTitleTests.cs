using System.Collections.Immutable;
using System.Reflection;
using static Memoria.FF8.Tests.NativeBattleTypedFixture;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCommandTitleTests
{
    [Theory]
    [InlineData(false, 0, 8)]
    [InlineData(false, 3, 32)]
    [InlineData(true, 1, 16)]
    public void Sequence_BB_uses_current_typed_actor_facts_and_emits_one_ordered_title(bool player, byte speed, int duration)
    {
        BattleSession child = Create(player, speed, true);
        NativeBattleGraphicsState graphics = Assert.IsType<NativeBattleGraphicsState>(child.Presentation?.Graphics);
        NativeBattleModelLifecycleServices services = child.CaptureModelLifecycleServices(child.Presentation!.NativeActionPackets.Roster);
        const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        NativeBattleHostedTestFixture.SetField(child.Presentation!, "hostedFrameServices", services);

        NativeBattleGraphicsState advanced = Assert.IsType<NativeBattleGraphicsState>(typeof(BattlePresentationProcess).GetMethod("AdvanceNativeLoadedModels", privateInstance)!.Invoke(child.Presentation!, [graphics, 0u, services]));

        BattleEvent title = Assert.IsType<NativeBattlePresentationDescriptorText>(Assert.Single(services.ReadOrderedReceipts())).Message;
        Assert.Equal(player ? "party" : "enemy", title.ActorId);
        Assert.Equal("Fire", title.Message);
        Assert.Equal(duration, title.Amount);
        Assert.Equal(1, title.NativeMessageMode);
        Assert.Equal(!player, title.IsEnemyActionTitle);
        Assert.Equal(new BattleTextIdentity("kernel_magicspells_name", 1), title.NativeTextIdentity);
        Assert.Null(advanced.ActionGraphics!.PendingData);
        Assert.DoesNotContain(child.Events, entry => entry.Kind == "NativeText");
    }

    [Fact]
    public void Sequence_BB_with_no_pending_name_does_not_publish_a_title()
    {
        BattleSession battle = Create(false, 2, false);

        BattlePresentationProcess process = battle.Presentation!;
        NativeBattleModelLifecycleServices services = battle.CaptureModelLifecycleServices(process.ActionPackets.Roster);
        NativeBattleHostedTestFixture.SetField(process, "hostedFrameServices", services);
        NativeBattleGraphicsState advanced = Assert.IsType<NativeBattleGraphicsState>(typeof(BattlePresentationProcess).GetMethod("AdvanceNativeLoadedModels", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(process, [process.Graphics, 0u, services]));

        Assert.DoesNotContain(services.ReadOrderedReceipts(), receipt => receipt is NativeBattlePresentationDescriptorText);
        Assert.Null(advanced.ActionGraphics!.PendingData);
    }

    [Theory]
    [InlineData(2, 0, 0x47E970u)]
    [InlineData(8, 0, 0u)]
    [InlineData(8, 0x80, 0x4950A0u)]
    [InlineData(236, 0, 0u)]
    [InlineData(236, 0x80, 0x4950A0u)]
    public void Enemy_header_name_follows_the_command_namespace_and_native_display_flag(byte command, byte flags, uint producer)
    {
        BattleSession battle = Create(false, 2, false);
        byte[] record = new byte[20];
        record[9] = flags;
        BattleNativeAction action = new(command, 1, command == 2 ? 1 : 3, 1)
        {
            Name = "Fire",
            MagicId = 1,
            AttackType = 2,
            Power = 1,
            HitCount = 1,
            TargetFlags = 0x20,
            AttackFlags = 0,
            DamagePresentation = 0,
            Element = 0,
            ElementPercent = 0,
            StatusAttack = 255,
            Statuses0 = 0,
            Statuses1 = 0,
            Hit = 255,
            Critical = 0,
            PowerModifier = 0,
            LevelModifier = 0,
            RawRecord = record.ToImmutableArray(),
            SupplementalRecord = []
        };
        BattleTargetRequest target = new(new BattleTargetSelection.Explicit([new BattlePartyActorReference("party")]), BattleTargetEligibility.SavedStatus, false, BattleTargetReplacement.None);
        BattleActionPresentation presentation = new("enemy", "party", BattleActionPhase.Windup, target) { NativeSequence = 1 };

        NativeBattleActionHeaderPreparation header = battle.BuildExpandedActionHeaderPreparation(presentation, action);

        if (producer == 0)
        {
            Assert.Null(header.Data);
        }
        else
        {
            Assert.Equal(new NativeBattleActionDataReference(producer, 1, "Fire"), header.Data);
        }
    }

    private static BattleSession Create(bool player, byte speed, bool pending)
    {
        BattleSession session = new(new BattleDefinition
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeMusicProcess = new NativeBattleMusicProcessState(new(1, null, null, null, 0, null), 0, null).InitializeCommands501C60(),
            NativeMessageSpeed = speed,
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.Normal);
        string actor = player ? "party" : "enemy";
        int index = player ? 0 : 3;
        // Two actual BB opcodes in one callback verify that the pending pointer is consumed once.
        NativeBattleGraphicsResource resource = new([1, 0, 4, 0, 0xBB, 0xBB, 0xA1]) { AnimationFrames = [1] };
        NativeBattleGraphicsRules rules = new(NativeFieldRuntimeProfile.FF8EnExecutableHash, Enumerable.Repeat((sbyte)-1, 16).ToImmutableArray(), Enumerable.Repeat((sbyte)-1, 32).ToImmutableArray());
        BattleModelOwner owner = player ? new BattlePartyActorModel(new(actor)) : new BattleEnemyActorModel(new(actor));
        NativeBattleGraphicsModel model = new(3) { Flags0 = 3, ModelId4 = 3, Flags2 = 4, Flags8 = 0, Kind5 = 0, Sequence = 1, ResourceFlags44 = 1, SequenceState = new() { Pose = 0, PoseFrame114 = 0, PoseLimit115 = 1 } };
        NativeBattleActionDataReference? title = pending ? new(0x47E970, 1, "Fire") : null;
        NativeBattleGraphicsState graphics = OriginalGraphics([(index, owner, model)]) with
        {
            ActionGraphics = new(new BattleActionHeaderReference(0), new BattleRetainedModelReference.CanonicalOwner(owner), new BattleContactCursor(0), BattleRetainedModelSelection.Empty, BattleRetainedModelSelection.Empty, null, null, 0, title, 0x50A9A0)
        };
        NativeBattleHostedTestFixture.BindProcess(session, graphics, new(rules, [new(actor, 3, resource)], []));
        return session;
    }

}
