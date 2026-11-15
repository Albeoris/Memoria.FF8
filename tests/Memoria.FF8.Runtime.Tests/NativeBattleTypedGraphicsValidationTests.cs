using System.Collections.Immutable;
using System.Reflection;

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
        NativeBattleOriginalEffectState originalEffect = graphics.OriginalEffect!;
        graphics = graphics with
        {
            ActorModels = models,
            ActionGraphics = graphics.ActionGraphics! with { Guardian = guardian },
            OriginalEffect = originalEffect with { FireModelCompletion = originalEffect.FireModelCompletion!.Add(extraAlly, 1) }
        };

        graphics.Validate();
        Assert.Throws<InvalidDataException>(() => (graphics with { ActorModels = models.Remove(extraAlly), OriginalEffect = originalEffect }).Validate());
        foreach (BattleModelTargets? invalid in new[] { BattleModelTargets.From([Target]), BattleModelTargets.From([RetiredSource]), null })
        {
            NativeBattleActionGraphicsState action = graphics.ActionGraphics! with { Guardian = guardian with { HiddenPartyActors = invalid! } };
            Assert.Throws<InvalidDataException>(() => (graphics with { ActionGraphics = action }).Validate());
        }
    }

    [Fact]
    public void RejectsWrongSideAndRetiredOwnersWhereGraphicsNeedsTheirModels()
    {
        NativeBattleGraphicsState graphics = Graphics(1);
        NativeBattleActionGraphicsState action = graphics.ActionGraphics!;
        NativeBattleActionGeometry geometry = graphics.ActionContext!;
        foreach (NativeBattleActionGraphicsState invalid in new[]
        {
            action with { Actor = null! },
            action with { TargetActors = null! },
            action with { CoverActors = null! },
            action with { HiddenPartyActors = BattleModelTargets.From([Target]) }
        })
        {
            Assert.Throws<InvalidDataException>(() => (graphics with { ActionGraphics = invalid }).Validate());
        }
        BattleRetainedModelReference missing = new BattleRetainedModelReference.CanonicalOwner(RetiredSource);
        BattleRetainedModelReference wrongSide = new BattleRetainedModelReference.CanonicalOwner(WrongSideTarget);
        BattleRetainedModelSelection wrongSelection = BattleRetainedModelSelection.From([wrongSide]);
        NativeBattleActionGraphicsState retained = action with { Actor = missing, TargetActors = wrongSelection, CoverActors = wrongSelection, CameraActor = missing };
        (graphics with { ActionGraphics = retained }).Validate();
        Assert.Throws<InvalidDataException>(() => retained.ResolveActor(graphics));
        Assert.Throws<InvalidDataException>(() => retained.ResolveTargets(graphics));
        Assert.Throws<InvalidDataException>(() => retained.ResolveCover(graphics));
        Assert.Throws<InvalidDataException>(() => retained.ResolveCameraActor(graphics));
        Assert.Throws<InvalidDataException>(() => (geometry with { Source = missing }).ResolveSource(graphics.ActorModels, graphics.NativeBindings));
        Assert.Throws<InvalidDataException>(() => wrongSelection.Resolve(graphics.ActorModels, graphics.NativeBindings));
        Assert.Throws<InvalidDataException>(() => (geometry with { Target = wrongSide, TargetActors = wrongSelection }).ResolveTarget(graphics.ActorModels, graphics.NativeBindings));
        foreach (NativeBattleActionGeometry invalid in new[]
        {
            geometry with { Source = null! },
            geometry with { SourceTargets = null! },
            geometry with { Target = wrongSide }
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
    public void DetachedEffectValidatesStructurallyAndLiveEffectRequiresItsEnteredRecipientKeys()
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
        BattleModelOwner recipient = Assert.Single(parameters.TargetActors.Enumerate());
        Assert.Throws<InvalidDataException>(() => (effect with { FireModelCompletion = completion.Remove(recipient) }).Validate(roster));
        Assert.Throws<InvalidDataException>(() => (effect with { FireModelCompletion = completion.Add(WrongSideTarget, 0) }).Validate(roster));
        (effect with { FireModelCompletion = completion.Remove(Target).Add(WrongSideTarget, 1) }).Validate(roster);
        (effect with { Parameters = null, FireModelCompletion = null }).Validate();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Completed_effect_keeps_canonical_parameters_but_active_task_rejects_retired_participant(bool detached)
    {
        const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
        NativeBattleGraphicsState graphics = Graphics(0);
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleOriginalEffectState completed = effect with { Parameters = effect.Parameters! with { Source = RetiredSource }, ReturnedDescriptor = 0 };
        graphics = graphics with { OriginalEffect = completed };
        graphics.Validate();
        Assert.Equal(RetiredSource, graphics.OriginalEffect!.Parameters!.Source);
        Assert.Throws<InvalidDataException>(() => completed.Validate(graphics.ActorModels.Roster));

        BattleSession session = new(new BattleDefinition
        {
            Combatants = [new() { Id = "ally", Name = "Ally", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.TurnBased);
        NativeBattleOriginalEffectState pending = completed with { ReturnedDescriptor = null };
        NativeBattleEffectMemory idleMemory = NativeBattleEffectMemory.FromColdProcess().InitializeReturnedTask500870();
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeEffectMemory))!.SetValue(session, idleMemory);
        MethodInfo invoke = typeof(BattleSession).GetMethod("InvokeNativeOriginalEffect", instance, [typeof(NativeBattleGraphicsState), typeof(bool)])!;
        TargetInvocationException pendingError = Assert.Throws<TargetInvocationException>(() => invoke.Invoke(session, [graphics with { OriginalEffect = pending }, true]));
        Assert.Contains("bound source", Assert.IsType<InvalidDataException>(pendingError.InnerException).Message);
        Assert.Same(idleMemory, session.NativeEffectMemory);

        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x123000, 0x123100, 16, 1);
        NativeBattleOriginalEffectState active = completed with { Pools = [pool], ReturnedDescriptor = pool.DescriptorAddress };
        NativeBattleEffectMemory memory = idleMemory.AssignReturnedTask(active) with { DetachedReturnedTask = detached ? active : null };
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeEffectMemory))!.SetValue(session, memory);
        graphics = graphics with { OriginalEffect = detached ? effect : active };
        MethodInfo advance = typeof(BattleSession).GetMethod("AdvanceNativeOriginalEffect", instance)!;
        TargetInvocationException activeError = Assert.Throws<TargetInvocationException>(() => advance.Invoke(session, [graphics]));
        Assert.Contains("bound source", Assert.IsType<InvalidDataException>(activeError.InnerException).Message);
        Assert.Same(memory, session.NativeEffectMemory);
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
        NativeBattleModelAliases aliases = boundary.CaptureAliases(models);
        NativeBattleUiServices ui = NativeBattleUiServices.Initialize();
        BattleNativeRenzokukenBlock block = new(0, [16, 9], NativeBattleRenzokukenRules.Timeline([16, 9]));

        BattleContactTaskStorageContract storageContract = additionalCount == 0 ? BattleContactTaskStorageContract.OriginalArena : BattleContactTaskStorageContract.ExpandableArena;

        return new(models, boundary, 0, 0, NativeBattleEffectsState.Clear(storageContract))
        {
            ActionGraphics = new(new(0), BattleRetainedModelReference.Capture(Source, aliases), new(0), BattleRetainedModelSelection.Capture(targets, aliases), BattleRetainedModelSelection.Capture(party, aliases), new BattleRetainedModelReference.CanonicalOwner(Source), new BattleRetainedModelReference.CanonicalOwner(recipient), 0, null, 0) { HiddenPartyActors = party },
            ActionContext = new(BattleRetainedModelReference.Capture(Source, aliases), BattleRetainedModelSelection.Capture(party, aliases), false)
            {
                TargetActors = BattleRetainedModelSelection.Capture(targets, aliases),
                Target = BattleRetainedModelReference.Capture(recipient, aliases)
            },
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
