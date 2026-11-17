using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Buffers.Binary;
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
        BattleSession battle = Session();
        NativeBattleGuardianFormPresentation presentation = Presentation(mode, phase) with { SkipRequested = skip };
        NativeBattleGraphicsState graphics = HiddenGraphics() with { Flags = 0x10, GuardianFormPresentation = presentation };
        typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeGraphics))!.SetValue(battle.Presentation!, graphics);
        Return(battle);
        Assert.Null(battle.Presentation!.Graphics.GuardianFormPresentation);
        AssertVisibility(battle.Presentation?.Graphics, presentation);
        Assert.Equal(presentation.HumanModel.Flags0, ReadOriginalModelCell(battle.Presentation?.Graphics, 0).Flags0);
        Assert.Equal(presentation.HumanModel.Flags2, ReadOriginalModelCell(battle.Presentation?.Graphics, 0).Flags2);
    }

    [Fact]
    public void ActiveReturnPreservesLaterVisibilityChangesToOtherParticipants()
    {
        BattleSession battle = Session();
        NativeBattleGuardianFormPresentation presentation = Presentation(GuardianFormInvocationMode.EntryOnly, 3);
        NativeBattleGraphicsState current = HiddenGraphics();
        NativeBattleGraphicsState graphics = current with { GuardianFormPresentation = presentation };
        typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeGraphics))!.SetValue(battle.Presentation!, graphics);
        Return(battle);
        Assert.Equal(current.ActorModels.Entries.Where(entry => entry.Owner != Actor), battle.Presentation!.Graphics.ActorModels.Entries.Where(entry => entry.Owner != Actor));
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

    private static BattleSession Session()
    {
        return new(new BattleDefinition
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeCommonEffectTexture = CommonTexture(),
            NativeClockRules = new(10),
            NativeBattleSpeed = 2,
            Combatants = [new() { Id = "actor", Name = "Actor", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.Normal);
    }

    private static NativeBattleEffectInitializerAsset CommonTexture()
    {
        byte[] data = new byte[66];
        BinaryPrimitives.WriteUInt32LittleEndian(data, 16);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), 8);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(8), 44);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(16), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(18), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(52), 14);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(56), 64);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(58), 256);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(60), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(62), 1);
        return new("common.tim", data.ToImmutableArray());
    }

    private static readonly BattlePartyActorModel Actor = new(new("actor"));
    private static readonly BattlePartyActorModel Other = new(new("other"));
    private static readonly BattleEnemyActorModel Enemy = new(new("enemy"));
    private static readonly BattlePartyActorModel Extra = new(new("extra"));

    private static NativeBattleGuardianFormPresentation Presentation(GuardianFormInvocationMode mode, byte phase)
    {
        ImmutableDictionary<BattleModelOwner, NativeBattleGuardianModelVisibility> saved = ImmutableDictionary<BattleModelOwner, NativeBattleGuardianModelVisibility>.Empty
            .Add(Actor, new(6, 4)).Add(Other, new(2, 0)).Add(Enemy, new(2, 0)).Add(Extra, new(10, 0));
        return new("actor", 1, mode, Actor, phase, new(6) { ModelId4 = 0, Flags2 = 4 }, NativeBattleCameraState.FromColdProcess(), NativeBattleStageState.FromColdProcess(), saved, 0)
        {
            UnboundVisibility = ImmutableDictionary<NativeBattleModelSlot, NativeBattleGuardianModelVisibility>.Empty.Add(NativeBattleModelSlot.Decode(6), new(0, null)),
            HumanFocalLength = 512
        };
    }

    private static NativeBattleGraphicsState HiddenGraphics()
    {
        NativeBattleGraphicsState graphics = OriginalGraphics([(0, Actor, Model(0)), (1, Other, Model(1)), (3, Enemy, Model(3))]);
        return graphics with { ActorModels = graphics.ActorModels.Add(Extra, Model(7)) };

        static NativeBattleGraphicsModel Model(uint flags) => new(0x40E) { Flags2 = 0x8C, Flags8 = flags };
    }

    private static NativeBattleGraphicsState RestoreVisibility(NativeBattleGraphicsState graphics, NativeBattleGuardianFormPresentation presentation)
    {
        return (NativeBattleGraphicsState)typeof(BattleSession).GetMethod("RestoreGuardianFormVisibility", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [graphics, presentation])!;
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

    private static void Return(BattleSession battle)
    {
        BattleGuardianFormState form = new("actor", 2, "ifrit-v1", BattleGuardianFormPhase.Active, 1, 1) { EntryCompleted = true };
        typeof(BattleSession).GetMethod("ReturnGuardianFormPresentation", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(battle, [battle.Combatants[0], form]);
    }
}
