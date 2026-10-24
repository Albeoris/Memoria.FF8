using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleAnimationAttackSequenceResolutionTests
{
    [Fact]
    public void Native_model_sampling_does_not_resolve_an_unused_scaffold_attack_sequence()
    {
        BattleCombatantDefinition definition = new() { Id = "enemy", Name = "Enemy" };
        int calls = 0;
        BattleAnimationPresentation animation = new(BattleDebugModels.CreateDummy(), null, definition)
        {
            AttackSequenceResolver = () =>
            {
                calls++;
                throw new NotSupportedException("No installed attack entry.");
            }
        };

        BattleAnimationSample sample = animation.SampleNativeModel(new(3, 0));

        Assert.Equal(0, sample.Opacity);
        Assert.Equal(0, calls);
        Assert.Throws<NotSupportedException>(() => animation.AttackSequence);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void The_scaffold_resolves_its_installed_attack_sequence_once_when_consumed()
    {
        BattleCombatantDefinition definition = new() { Id = "enemy", Name = "Enemy" };
        int calls = 0;
        BattleAnimationPresentation animation = new(BattleDebugModels.CreateDummy(), null, definition)
        {
            AttackSequenceResolver = () =>
            {
                calls++;
                return 12;
            }
        };

        Assert.Equal(0, calls);
        Assert.Equal(12, animation.AttackSequence);
        Assert.Equal(12, animation.AttackSequence);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void An_explicit_attack_sequence_preserves_the_existing_caller_binding()
    {
        BattleCombatantDefinition definition = new() { Id = "enemy", Name = "Enemy" };
        BattleAnimationPresentation animation = new(BattleDebugModels.CreateDummy(), null, definition)
        {
            AttackSequence = 14,
            AttackSequenceResolver = () => throw new InvalidOperationException("An explicit binding is already present.")
        };

        Assert.Equal(14, animation.AttackSequence);
    }
}
