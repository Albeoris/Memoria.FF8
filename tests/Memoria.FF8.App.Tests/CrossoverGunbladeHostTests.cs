using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverGunbladeHostTests(ITestOutputHelper output)
{
    [Fact]
    public void Formation_300_queues_original_Gunblade_contact_once_and_resumes_both_owners()
    {
        string? ff7Root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8Root = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(ff7Root) && !string.IsNullOrWhiteSpace(ff8Root), "Select both original game installations.");
        Ff7Installation installation = Ff7Installation.Discover(ff7Root!);
        using Ff7Session origin = Ff7Session.NewGame(installation);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        Ff7BattleAnimationResources animations = new(archive, Ff7PcExecutableData.LoadBattlePlacement(installation), magic);
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(item => item.FormationId == 300);
        Ff7BattleSession host = Host(null);
        using GameAssets game = new(ff8Root!);
        using BattleAssets assets = new(ff8Root!);
        CrossoverFf8BattleKit kit = CrossoverFf8BattleKit.ReadSquall(game.ReadNewGameCharacterVitals(), assets);
        CrossoverBattleRoster roster = CrossoverBattleRoster.Create(Guid.NewGuid(), host.Capture(), [new(0, 0, 0)], [new(SessionGame.Ff7, 0, SessionGame.Ff8, 0, "explicit")]);
        NativeGlobalRandomState guestRandom = game.ReadNewGameGlobalRandom();
        CrossoverGunbladeHostAttack guest = Guest(null);
        host.BindGuestAttack(guest);
        for (int tick = 0; tick < 2000 && host.ReadyPlayer != 0; tick++)
        {
            host.Advance();
        }
        Assert.Equal(0, host.ReadyPlayer);
        Assert.False(host.SubmitGuestAttack(0, 0));
        Assert.True(host.SubmitGuestAttack(0, 4));
        Assert.Contains(host.Capture().Queue.Ready, command => command is { GuestAttack: true, ActorSlot: 0, TargetMask: 16 });
        Assert.False(host.SubmitGuestAttack(0, 5));
        (host, guest) = Restore(Capture(host, guest));
        for (int tick = 0; tick < 2000 && guest.Invocation is null; tick++)
        {
            host.Advance();
        }
        Assert.NotNull(guest.Invocation);
        Assert.True(host.Action?.Command.GuestAttack);
        Assert.NotNull(guest.Visual);
        Ff7BattleAiActor target = host.Actors.Single(actor => actor.Actor.Slot == 4).Actor;
        Vector3 mp = host.VisualPlacement(4).Position;
        float initialDistance = Vector3.Distance(RenderRoot(guest), mp);
        for (int tick = 0; tick < 200 && !guest.TriggerWindowOpen; tick++)
        {
            host.Advance();
        }
        Assert.True(guest.TriggerWindowOpen);
        Assert.True(Vector3.Distance(RenderRoot(guest), mp) < initialDistance);
        Assert.Equal(target.HitPoints, host.Actors.Single(actor => actor.Actor.Slot == 4).Actor.HitPoints);
        Saved atWindow = Capture(host, guest);
        (Ff7BattleHit normal, Saved _) = Play(atWindow, false, false);
        (Ff7BattleHit triggered, Saved complete) = Play(atWindow, true, false);
        (Ff7BattleHit coldTriggered, Saved restored) = Play(atWindow, true, true);
        Assert.Equal(triggered, coldTriggered);
        Assert.Equal(JsonSerializer.Serialize(complete), JsonSerializer.Serialize(restored));
        Assert.True(triggered.Damage.Damage > normal.Damage.Damage && !triggered.Damage.Critical);
        Assert.Equal((0, 4), (triggered.Source, triggered.Target));
        Assert.Equal((short)20, restored.Guest.Presentation!.ContactTiming!.Timing);
        Assert.Equal((short)0, restored.Guest.Presentation.Model.SequenceState!.Header40);
        Assert.Equal(Math.Max(0, (long)target.HitPoints - triggered.Damage.Damage), restored.Host.Actors.Single(actor => actor.Actor.Slot == 4).Actor.HitPoints);
        output.WriteLine($"Normal damage {normal.Damage.Damage}; triggered damage {triggered.Damage.Damage}; hit tick {triggered.Tick}; returned tick {restored.Host.Tick}; original host HP {target.HitPoints}");

        (Ff7BattleHit Hit, Saved Result) Play(Saved start, bool trigger, bool coldAfterContact)
        {
            (Ff7BattleSession live, CrossoverGunbladeHostAttack animation) = Restore(start);
            if (trigger)
            {
                animation.PressTrigger();
            }
            Ff7BattleHit? contact = null;
            for (int tick = 0; tick < 200 && animation.Invocation is not null; tick++)
            {
                live.Advance();
                if (live.Action?.Applied == true && contact is null)
                {
                    contact = Assert.IsType<Ff7BattleHit>(live.LastHit);
                    Assert.Equal(4, live.Reaction?.Slot);
                    Assert.NotNull(animation.LastContactEffect);
                    if (coldAfterContact)
                    {
                        (live, animation) = Restore(Capture(live, animation));
                    }
                }
            }
            Assert.Null(animation.Invocation);
            Assert.False(live.Action?.Command.GuestAttack ?? false);
            Assert.Equal(contact, live.LastHit);
            return (Assert.IsType<Ff7BattleHit>(contact), Capture(live, animation));
        }

        Ff7BattleSession Host(Ff7BattleSnapshot? snapshot) => new(formation, origin.Progress, kernel, executable.NativeMateriaStatModifiers, 1, requestFlags: 4, snapshot: snapshot, animations: animations);
        CrossoverGunbladeHostAttack Guest(CrossoverGunbladeHostSnapshot? snapshot) => new(roster, kit, assets, game, guestRandom, _ => 128, snapshot);
        (Ff7BattleSession Host, CrossoverGunbladeHostAttack Guest) Restore(Saved source)
        {
            Saved saved = JsonSerializer.Deserialize<Saved>(JsonSerializer.Serialize(source))!;
            Ff7BattleSession next = Host(saved.Host);
            CrossoverGunbladeHostAttack animation = Guest(saved.Guest);
            next.BindGuestAttack(animation);
            return (next, animation);
        }
    }

    private static Vector3 RenderRoot(CrossoverGunbladeHostAttack guest)
    {
        var visual = guest.Visual!;
        NativeBattleActionMatrix world = visual.Model.GeometryWorldMatrix!;
        return Vector3.Transform(new(world.X, world.Y, world.Z), visual.OriginToHost);
    }

    private static Saved Capture(Ff7BattleSession host, CrossoverGunbladeHostAttack guest) => new(host.Capture(), guest.Capture());
    private sealed record Saved(Ff7BattleSnapshot Host, CrossoverGunbladeHostSnapshot Guest);
}
