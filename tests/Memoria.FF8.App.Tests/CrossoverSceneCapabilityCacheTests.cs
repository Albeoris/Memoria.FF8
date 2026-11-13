using System.Collections;
using System.Reflection;
using System.Text.Json;
using Memoria.FF7.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverSceneCapabilityCacheTests
{
    [Fact]
    public void A_nontarget_scene_participant_change_invalidates_cached_refusal_and_menu_identity_without_advancing_combat()
    {
        using CrossoverGuestStatusHostTests.Fixture fixture = new(guardianName: "Carbuncle");
        CrossoverGuestHostActions provider = fixture.Providers[0];
        ushort guardian = fixture.Guardian("Carbuncle");
        const string previousRefusal = "Controlled prior scene capacity refusal.";
        const string currentRefusal = "Controlled changed scene capability result.";
        MethodInfo actionMethod = typeof(CrossoverGuestHostActions).GetMethod("NativeAbility", BindingFlags.Instance | BindingFlags.NonPublic)!;
        MethodInfo targetMaskMethod = typeof(CrossoverGuestHostActions).GetMethod("AbilityTargetMask", BindingFlags.Instance | BindingFlags.NonPublic)!;
        MethodInfo participantsMethod = typeof(CrossoverGuestHostActions).GetMethod("ReadSceneParticipants", BindingFlags.Instance | BindingFlags.NonPublic)!;
        object originalAction = actionMethod.Invoke(provider, [guardian])!;
        ushort allyTargets = (ushort)targetMaskMethod.Invoke(provider, [originalAction, 0])!;
        Assert.Equal((ushort)3, allyTargets);
        int[] previousParticipants = ((IEnumerable<int>)participantsMethod.Invoke(provider, null)!).ToArray();
        IDictionary cache = (IDictionary)typeof(CrossoverGuestHostActions).GetField("abilityCapabilities", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(provider)!;
        MethodInfo keyMethod = typeof(CrossoverGuestHostActions).GetMethod("AbilityCapabilityKey", BindingFlags.Instance | BindingFlags.NonPublic)!;
        object oldKey = keyMethod.Invoke(provider, [guardian, 0, allyTargets])!;
        cache.Add(oldKey, previousRefusal);
        Assert.Equal(previousRefusal, provider.AbilityTargetUnsupportedReason(guardian, 0));
        object previousMenu = provider.CommandMenuState;
        Assert.Same(previousMenu, provider.CommandMenuState);
        Ff7BattleSnapshot before = fixture.Host.Capture();
        string sourceBefore = JsonSerializer.Serialize(provider.Capture());
        Ff7BattleActorState opponent = before.Actors.First(actor => actor.Actor.Slot >= 4 && (actor.Actor.Conditions & 8) != 0);
        Assert.Equal(0, allyTargets & 1 << opponent.Actor.Slot);
        Ff7BattleSnapshot changed = before with { Actors = before.Actors.Select(actor => actor.Actor.Slot == opponent.Actor.Slot ? actor with { Actor = actor.Actor with { Conditions = actor.Actor.Conditions & ~8u } } : actor).ToArray() };
        typeof(Ff7BattleSession).GetField("state", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(fixture.Host, changed);

        object newKey = keyMethod.Invoke(provider, [guardian, 0, allyTargets])!;

        Assert.Equal(originalAction, actionMethod.Invoke(provider, [guardian]));
        Assert.Equal(allyTargets, (ushort)targetMaskMethod.Invoke(provider, [originalAction, 0])!);
        Assert.Equal(previousParticipants.Where(slot => slot != opponent.Actor.Slot), (IEnumerable<int>)participantsMethod.Invoke(provider, null)!);
        Assert.Equal(before.Actors.Single(actor => actor.Actor.Slot == 0).Actor, fixture.Host.Actors.Single(actor => actor.Actor.Slot == 0).Actor);
        Assert.NotEqual(oldKey, newKey);
        Assert.False(cache.Contains(newKey));
        Assert.Equal(previousRefusal, cache[oldKey]);
        object currentMenu = provider.CommandMenuState;
        Assert.NotSame(previousMenu, currentMenu);
        Assert.Same(currentMenu, provider.CommandMenuState);
        //Controlled cache entries exercise normal lookup without replaying an entire original effect.
        cache.Add(newKey, currentRefusal);
        Assert.Equal(currentRefusal, provider.AbilityTargetUnsupportedReason(guardian, 0));
        Assert.Equal(JsonSerializer.Serialize(changed), JsonSerializer.Serialize(fixture.Host.Capture()));
        Assert.Equal(sourceBefore, JsonSerializer.Serialize(provider.Capture()));
        Assert.Null(fixture.Host.Action);
        Assert.Null(provider.Invocation);
    }
}
