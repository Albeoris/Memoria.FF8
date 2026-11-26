using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using static Memoria.FF8.Runtime.BattlePresentationProcess;
using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGuardianFormVisibilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LandingRestoresEveryParticipantAndPreservesPreviouslyHiddenModels(bool roundTrip)
    {
        NativeBattleGuardianFormPresentation presentation = Presentation(GuardianFormInvocationMode.EntryOnly, 2);
        if (roundTrip)
        {
            presentation = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(presentation));
        }
        NativeBattleGraphicsState current = HiddenGraphics();
        NativeBattleGraphicsState restored = RestoreVisibility(current, presentation);
        AssertVisibility(restored, presentation);
        foreach (BattleModelEntry<BattleModelOwner> entry in restored.ActorModels.Entries)
        {
            NativeBattleGraphicsModel before = current.ActorModels.Get(entry.Owner);
            Assert.Equal(before.Flags0 & ~0xC, entry.State.Flags0 & ~0xC);
            Assert.Equal(before.Flags2!.Value & ~4, entry.State.Flags2!.Value & ~4);
            Assert.Equal(before.Flags8, entry.State.Flags8);
        }
    }

    [Theory]
    [InlineData(GuardianFormInvocationMode.EntryOnly, 1, false)]
    [InlineData(GuardianFormInvocationMode.FinisherWithoutEntry, 5, false)]
    [InlineData(GuardianFormInvocationMode.FinisherWithoutEntry, 5, true)]
    public void CinematicReturnRestoresAllParticipantsAndTheCapturedHumanVisibility(GuardianFormInvocationMode mode, byte phase, bool skip)
    {
        NativeBattleGuardianFormPresentation presentation = Presentation(mode, phase) with { SkipRequested = skip };
        NativeBattleGraphicsState graphics = HiddenGraphics() with { Flags = 0x10, GuardianFormPresentation = presentation };
        NativeBattleGraphicsState restored = Return(graphics, presentation);
        Assert.Null(restored.GuardianFormPresentation);
        AssertVisibility(restored, presentation);
        Assert.Equal(presentation.HumanModel.Flags0, ReadOriginalModelCell(restored, 0).Flags0);
        Assert.Equal(presentation.HumanModel.Flags2, ReadOriginalModelCell(restored, 0).Flags2);
    }

    [Fact]
    public void ActiveReturnPreservesLaterVisibilityChangesToOtherParticipants()
    {
        NativeBattleGuardianFormPresentation presentation = Presentation(GuardianFormInvocationMode.EntryOnly, 3);
        NativeBattleGraphicsState current = HiddenGraphics();
        NativeBattleGraphicsState restored = Return(current with { GuardianFormPresentation = presentation }, presentation);
        Assert.Null(restored.GuardianFormPresentation);
        Assert.Equal(current.ActorModels.Entries.Where(entry => entry.Owner != Actor), restored.ActorModels.Entries.Where(entry => entry.Owner != Actor));
    }

    [Fact]
    public void InactiveRowsKeepUnknownSecondaryFlags()
    {
        NativeBattleGuardianFormPresentation presentation = Presentation(GuardianFormInvocationMode.EntryOnly, 2);
        NativeBattleGraphicsState current = HiddenGraphics();
        current = current with { NativeBindings = current.NativeBindings.WriteUnboundCell(NativeBattleModelSlot.Decode(6), new(0xC)) };
        NativeBattleGraphicsState restored = RestoreVisibility(current, presentation);
        Assert.Null(ReadOriginalModelCell(restored, 6).Flags2);
        Assert.Equal((ushort)0, ReadOriginalModelCell(restored, 6).Flags0);
    }

    private static readonly BattlePartyActorModel Actor = new(new("actor"));
    private static readonly BattlePartyActorModel Other = new(new("other"));
    private static readonly BattleEnemyActorModel Enemy = new(new("enemy"));
    private static readonly BattlePartyActorModel Extra = new(new("extra"));

    private static NativeBattleGuardianFormPresentation Presentation(GuardianFormInvocationMode mode, byte phase)
    {
        ImmutableDictionary<BattleModelOwner, NativeBattleGuardianModelVisibility> saved = ImmutableDictionary<BattleModelOwner, NativeBattleGuardianModelVisibility>.Empty
            .Add(Actor, new(6, 4)).Add(Other, new(2, 0)).Add(Enemy, new(2, 0)).Add(Extra, new(10, 0));
        return new("actor", 1, mode, Actor, phase, LoadedModel(6, 4, 0) with { ModelId4 = 0 }, NativeBattleCameraState.FromColdProcess(), NativeBattleStageState.FromColdProcess(), saved, 0)
        {
            UnboundVisibility = ImmutableDictionary<NativeBattleModelSlot, NativeBattleGuardianModelVisibility>.Empty.Add(NativeBattleModelSlot.Decode(6), new(0, null)),
            HumanFocalLength = 512
        };
    }

    private static NativeBattleGraphicsState HiddenGraphics()
    {
        NativeBattleGraphicsState graphics = OriginalGraphics([(0, Actor, Model(0)), (1, Other, Model(1)), (3, Enemy, Model(3))]);
        return graphics with { ActorModels = graphics.ActorModels.Add(Extra, Model(7)) };

        static NativeBattleGraphicsModel Model(uint flags) => LoadedModel(0x40E, 0x8C, flags);
    }

    private static NativeBattleGraphicsModel LoadedModel(ushort flags0, ushort flags2, uint flags8)
    {
        return new(flags0) { Flags2 = flags2, Flags8 = flags8, ResourceFlags44 = 0, SequenceState = new() };
    }

    private static NativeBattleGraphicsState RestoreVisibility(NativeBattleGraphicsState graphics, NativeBattleGuardianFormPresentation presentation)
    {
        return (NativeBattleGraphicsState)typeof(BattlePresentationProcess).GetMethod("RestoreGuardianFormVisibility", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [graphics, presentation])!;
    }

    private static void AssertVisibility(NativeBattleGraphicsState graphics, NativeBattleGuardianFormPresentation presentation)
    {
        foreach (BattleModelEntry<BattleModelOwner> entry in graphics.ActorModels.Entries)
        {
            NativeBattleGuardianModelVisibility saved = presentation.ModelVisibility[entry.Owner];
            Assert.Equal(saved.Flags0 & 0xC, entry.State.Flags0 & 0xC);
            Assert.Equal((saved.Flags2 ?? 0) & 4, (entry.State.Flags2 ?? 0) & 4);
        }
        Assert.Equal(2, graphics.ActorModels.Get(Other).Flags0 & 6);
        Assert.Equal(2, graphics.ActorModels.Get(Enemy).Flags0 & 6);
        Assert.Equal(2, graphics.ActorModels.Get(Extra).Flags0 & 6);
        Assert.Equal(0, ReadOriginalModelCell(graphics, 6).Flags0 & 2);
    }

    private static NativeBattleGraphicsState Return(NativeBattleGraphicsState graphics, NativeBattleGuardianFormPresentation presentation)
    {
        BattleSession logical = new(new BattleDefinition
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeClockRules = new(10),
            NativeBattleSpeed = 2,
            Combatants =
            [
                new() { Id = "actor", Name = "Actor", IsPlayer = true },
                new() { Id = "other", Name = "Other", IsPlayer = true },
                new() { Id = "extra", Name = "Extra", IsPlayer = true },
                new() { Id = "enemy", Name = "Enemy" }
            ]
        }, BattleAtbMode.Normal);
        BattlePresentationProcess process = NativeBattleHostedTestFixture.SeedProcess(logical, graphics with { EntryReady = true });
        HostedGuardianFormVisualOwner owner = new(presentation.Actor, presentation.ActorId, presentation.InvocationId, presentation.GuardianForceId, presentation.VariantId);
        HostedGuardianFormVisualChange change = new(owner, Entering: false, new(owner, BattleGuardianFormPhase.Returning, HasAction: false, Finishing: false));
        PreparedHostedGuardianFormChange prepared = process.PrepareGuardianFormChange(change);
        process.ValidatePreparedGuardianFormChange(prepared);
        process.CommitGuardianFormChange(prepared);
        return process.Graphics;
    }
}
