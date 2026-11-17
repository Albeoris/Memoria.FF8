using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePresentationPartyOwnershipTests
{
    private static readonly BattleModelOwner Source = new BattlePartyActorModel(new("source"));

    [Fact]
    public void NativeAndForeignBindingsTakeTheirSideFromTheOwner()
    {
        NativeBattleEffectModelBinding transformedAlly = Target(new BattlePartyActorModel(new("transformed")), 22);
        NativeBattleEffectModelBinding enemyUsingPartyModel = Target(new BattleEnemyActorModel(new("enemy")), 3);
        NativeBattleForeignActor ally = Foreign(new BattleForeignPartyModel(new("guest-ally")));
        NativeBattleForeignActor enemy = Foreign(new BattleForeignEnemyModel(new("guest-enemy")));
        Assert.True(transformedAlly.IsParty);
        Assert.False(enemyUsingPartyModel.IsParty);
        Assert.Equal("transformed", transformedAlly.ActorId);
        Assert.True(ally.IsParty);
        Assert.False(enemy.IsParty);
    }

    [Fact]
    public void ActivePresentationRequestsRejectTheSameOwnerAcrossSuppliedModelBindings()
    {
        BattleModelOwner other = new BattleEnemyActorModel(new("target"));
        foreach (NativeBattleEffectPresentationInputs inputs in new[]
        {
            Inputs([Source], [Target(Source, 1)], []),
            Inputs([Source], [], [Foreign(Source)]),
            Inputs([Source, other], [Target(other, 16)], [Foreign(other)]),
            Inputs([Source, other], [Target(other, 16), Target(other, 17)], []),
            Inputs([Source, other], [], [Foreign(other), Foreign(other)])
        })
        {
            Assert.Throws<InvalidDataException>(() => new NativeBattleEffectPresentation(inputs));
        }
    }

    [Fact]
    public void PresentationRequestsRejectUnboundOrUnsuppliedOwners()
    {
        BattleModelOwner other = new BattleForeignPartyModel(new("guest"));
        Assert.Throws<InvalidDataException>(() => new NativeBattleEffectPresentation(Inputs([Source], [], [Foreign(other)])));
        Assert.Throws<InvalidDataException>(() => new NativeBattleEffectPresentation(Inputs([Source, other], [], [])));
        Assert.Throws<InvalidDataException>(() => new NativeBattleEffectPresentation(Inputs([Source, other], [Target(other, 20)], [])));
    }

    private static NativeBattleEffectPresentationInputs Inputs(BattleModelOwner[] owners, ImmutableArray<NativeBattleEffectModelBinding> targets, ImmutableArray<NativeBattleForeignActor> foreign)
    {
        BattleModelRoster roster = new(owners);
        NativeBattleModelAliases aliases = new(roster, []);
        var invocation = new NativeBattleEffectInvocation.Positioning(Source, roster, aliases);
        return new(new(new(), NativeBattleHostedTestFixture.PrimitiveExecutionGraphics()), new(0) { ModelId4 = 22 }, new([]), invocation, 201, NativeBattleEffectTextureState.FromColdProcess(), new(1), new(null, null, null, null), foreign)
        {
            NativeTargets = targets,
            GuardianFormMode = GuardianFormInvocationMode.EntryOnly,
            CameraMode = NativeBattleEffectCameraMode.NativeCinematic
        };
    }

    private static NativeBattleEffectModelBinding Target(BattleModelOwner owner, byte modelId) => new(owner, new(0) { ModelId4 = modelId }, new([]));

    private static NativeBattleForeignActor Foreign(BattleModelOwner owner) => new(owner, new(default, default, 0, 0, 4096), new(0, 0, 0, 0), (_, _) => default);
}
