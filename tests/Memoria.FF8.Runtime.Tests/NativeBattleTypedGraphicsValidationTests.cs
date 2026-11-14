using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTypedGraphicsValidationTests
{
    private static readonly BattlePartyActorModel Source = new(new("party-allocation-1"));
    private static readonly BattleEnemyActorModel Target = new(new("enemy"));
    private static readonly BattlePartyActorModel WrongSideTarget = new(new("enemy"));
    private static readonly BattlePartyActorModel RetiredSource = new(new("party-previous-allocation"));

    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    public void ValidatesBoundSelectionsAndLeavesRawHolesOutsideThePools(int additionalCount)
    {
        NativeBattleGraphicsState graphics = Graphics(additionalCount);
        graphics.Validate();
        Assert.Equal(2 + additionalCount, graphics.ActorModels.Roster.Count);
        NativeBattleModelSlot hole = NativeBattleModelSlot.Decode(1);
        Assert.Null(graphics.NativeBindings.ReadOriginalCell(hole, graphics.ActorModels).Flags8);
        Assert.Throws<InvalidDataException>(() => graphics.NativeBindings.DecodeLiteral(1, graphics.ActorModels));
        Assert.Throws<InvalidDataException>(() => (graphics with { ActorModels = graphics.ActorModels.Remove(Source) }).Validate());
        Assert.Throws<InvalidDataException>(() => (graphics with { ActorModels = graphics.ActorModels.Set(Target, new(2)) }).Validate());
        Assert.Throws<InvalidDataException>(() => (graphics with { ActorModels = graphics.ActorModels.Set(Target, new NativeBattleGraphicsModel(0).WriteLinkedModel(RetiredSource)) }).Validate());
        Assert.Throws<InvalidDataException>(() => (graphics with { ActorModels = graphics.ActorModels.Set(Target, new(0) { TextureResourceIndex = -1 }) }).Validate());
    }

    [Fact]
    public void GuardianHiddenSelectionRequiresExactBoundPartyOwners()
    {
        NativeBattleGraphicsState graphics = Graphics(0);
        var extraAlly = new BattlePartyActorModel(new("additional-ally"));
        BattleActorModels models = graphics.ActorModels.Add(extraAlly, new NativeBattleGraphicsModel(0));
        NativeBattleGuardianServiceState guardian = new(BattleModelTargets.From([extraAlly]), 7, 0);
        graphics = graphics with { ActorModels = models, ActionGraphics = graphics.ActionGraphics! with { Guardian = guardian } };

        graphics.Validate();
        Assert.Throws<InvalidDataException>(() => (graphics with { ActorModels = models.Remove(extraAlly) }).Validate());
        foreach (BattleModelTargets? invalid in new[] { BattleModelTargets.From([Target]), BattleModelTargets.From([RetiredSource]), null })
        {
            NativeBattleActionGraphicsState action = graphics.ActionGraphics! with { Guardian = guardian with { HiddenPartyActors = invalid! } };
            Assert.Throws<InvalidDataException>(() => (graphics with { ActionGraphics = action }).Validate());
        }
    }

    [Fact]
    public void RejectsWrongSideAndRetiredOwnersInActionGraphicsAndGeometry()
    {
        NativeBattleGraphicsState graphics = Graphics(1);
        NativeBattleActionGraphicsState action = graphics.ActionGraphics!;
        NativeBattleActionGeometry geometry = graphics.ActionContext!;
        BattleModelTargets wrongTargets = BattleModelTargets.From([WrongSideTarget]);
        foreach (NativeBattleActionGraphicsState invalid in new[]
        {
            action with { Actor = RetiredSource },
            action with { TargetActors = wrongTargets },
            action with { CoverActors = wrongTargets },
            action with { CoverSource = RetiredSource },
            action with { FirstTarget = WrongSideTarget },
            action with { CameraActor = RetiredSource },
            action with { HiddenPartyActors = BattleModelTargets.From([Target]) }
        })
        {
            Assert.Throws<InvalidDataException>(() => (graphics with { ActionGraphics = invalid }).Validate());
        }
        foreach (NativeBattleActionGeometry invalid in new[]
        {
            geometry with { Source = RetiredSource },
            geometry with { SourceTargets = wrongTargets },
            geometry with { Target = WrongSideTarget, TargetActors = wrongTargets }
        })
        {
            Assert.Throws<InvalidDataException>(() => (graphics with { ActionContext = invalid }).Validate());
        }
    }

    [Fact]
    public void RejectsUnboundWidgetReceiptSoundAndContactOwners()
    {
        NativeBattleGraphicsState graphics = Graphics(1);
        NativeBattleContactPacket contact = Assert.Single(graphics.PendingContacts);
        Assert.Throws<InvalidDataException>(() => (graphics with { TriggerWidget = graphics.TriggerWidget! with { Actor = RetiredSource } }).Validate());
        Assert.Throws<InvalidDataException>(() => (graphics with { PendingTrigger = graphics.PendingTrigger! with { Actor = RetiredSource } }).Validate());
        Assert.Throws<InvalidDataException>(() => (graphics with { PendingTriggerSoundActor = RetiredSource }).Validate());
        foreach (NativeBattleContactPacket invalid in new[]
        {
            contact with { Primary = contact.Primary with { Recipient = WrongSideTarget } },
            contact with { Primary = contact.Primary with { CoverSource = RetiredSource } },
            contact with { Secondary = contact.Secondary! with { Recipient = RetiredSource } }
        })
        {
            Assert.Throws<InvalidDataException>(() => (graphics with { PendingContacts = [invalid] }).Validate());
        }
        (graphics with { PendingContacts = [contact with { Secondary = null }] }).Validate();
    }

    [Fact]
    public void DetachedEffectValidatesStructurallyAndLiveEffectRequiresExactCompletionKeys()
    {
        NativeBattleGraphicsState graphics = Graphics(1);
        BattleModelRoster roster = graphics.ActorModels.Roster;
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleEffectParameters parameters = effect.Parameters!;
        ImmutableDictionary<BattleModelOwner, int> completion = effect.FireModelCompletion!;
        NativeBattleOriginalEffectState detached = effect with
        {
            Parameters = parameters with { Source = RetiredSource, TargetActors = BattleModelTargets.From([WrongSideTarget]) },
            FireModelCompletion = completion.Remove(Target).Add(WrongSideTarget, 1)
        };

        detached.Validate();
        Assert.Throws<InvalidDataException>(() => detached.Validate(roster));
        Assert.Throws<InvalidDataException>(() => (effect with { Parameters = parameters with { Source = null! } }).Validate());
        Assert.Throws<InvalidDataException>(() => (effect with { Parameters = parameters with { TargetActors = null! } }).Validate());
        Assert.Throws<InvalidDataException>(() => (effect with { FireModelCompletion = completion.Remove(Target) }).Validate(roster));
        Assert.Throws<InvalidDataException>(() => (effect with { FireModelCompletion = completion.Remove(Target).Add(WrongSideTarget, 1) }).Validate(roster));
        (effect with { Parameters = null, FireModelCompletion = null }).Validate();
    }

    private static NativeBattleGraphicsState Graphics(int additionalCount)
    {
        NativeBattleModelBoundary boundary = new(Enumerable.Repeat(new NativeBattleGraphicsModel(0), 7));
        BattleActorModels models = new(Enumerable.Range(0, additionalCount).Select(index =>
            new BattleModelEntry<BattleModelOwner>(new BattleEnemyActorModel(new($"additional-{index}")), new(0))));
        (boundary, models) = boundary.BindOriginalSlot(NativeBattleModelSlot.Decode(0), Source, models);
        (boundary, models) = boundary.BindOriginalSlot(NativeBattleModelSlot.Decode(3), Target, models);
        models = models.Set(Target, new NativeBattleGraphicsModel(2) { ResourceFlags44 = 0x40, Flags8 = 0, Flags2 = 0, SequenceState = new(), TextureResourceIndex = 100 }.WriteLinkedModel(Source));
        BattleModelOwner recipient = models.Enemies.Entries.Last().Owner;
        BattleModelTargets targets = BattleModelTargets.From([recipient]);
        BattleModelTargets party = BattleModelTargets.From([Source]);
        NativeBattleUiServices ui = NativeBattleUiServices.Initialize();
        BattleNativeRenzokukenBlock block = new(0, [16, 9], NativeBattleRenzokukenRules.Timeline([16, 9]));

        BattleContactTaskStorageContract storageContract = additionalCount == 0 ? BattleContactTaskStorageContract.OriginalArena : BattleContactTaskStorageContract.ExpandableArena;

        return new(models, boundary, 0, 0, NativeBattleEffectsState.Clear(storageContract))
        {
            ActionGraphics = new(new(0), Source, new(0), targets, party, Source, recipient, 0, null, 0) { HiddenPartyActors = party },
            ActionContext = new(Source, party, false) { TargetActors = targets, Target = recipient },
            OriginalEffect = new(2, 1, new(2, 0x6298A0, 0x629880), new(Source, 0, targets, new(0), 0, 0), null)
            {
                FireModelCompletion = models.Owners.ToImmutableDictionary(owner => owner, _ => 1)
            },
            UiServices = ui,
            TriggerWidget = NativeBattleTriggerWidget.Register(Source, block, 0, ui, models.Roster),
            PendingTrigger = new(Source, 1, 1, 25, 32),
            PendingTriggerSoundActor = Source,
            PendingContacts = [new(new(recipient, 0, 0, 8, 0, 0, 0) { CoverSource = Source }, new(Source, 0, 0, 0, 0, 0, 0))]
        };
    }
}
