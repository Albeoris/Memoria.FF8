using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverGunbladeHostTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void Formation_300_queues_original_equipped_Gunblade_contact_once_and_resumes_both_owners(byte equipped)
    {
        string? ff7Root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8Root = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(ff7Root) && !string.IsNullOrWhiteSpace(ff8Root), "Select both original game installations.");
        Ff7Installation installation = Ff7Installation.Discover(ff7Root!);
        using Ff7Session origin = Ff7Session.NewGame(installation);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        Ff7BattleAnimationResources animations = new(archive, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(item => item.FormationId == 300);
        Ff7BattleSession host = Host(null);
        using GameAssets game = new(ff8Root!);
        using BattleAssets assets = new(ff8Root!);
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals().DeepCopy();
        int row = vitals.FindPhysicalRow(0);
        vitals.Records[row] = vitals.Records[row] with { Weapon = equipped };
        CrossoverFf8BattleKit kit = CrossoverFf8BattleKit.Read(vitals, assets, 0);
        BattleWeaponDefinition selectedWeapon = assets.WeaponDefinitions[equipped];
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
        CrossoverFf8BattleActorVisual visual = Assert.IsType<CrossoverFf8BattleActorVisual>(guest.Visual);
        Assert.Equal(assets.ResolveWeaponModel(0, equipped, visual.Body)!.Name, visual.Weapon!.Name);
        Assert.Equal((byte)selectedWeapon.Tier, visual.Model.FileSelector128);
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
        Assert.Equal(selectedWeapon.AttackPower * 16 / 20, CrossoverGunbladeHostAttack.ProjectContact(kit, 0).Attack.Power);
        Assert.Equal(selectedWeapon.AttackPower * 24 / 20, CrossoverGunbladeHostAttack.ProjectContact(kit, 20).Attack.Power);
        if (equipped == 0)
        {
            Assert.Equal(new(false, false, 38), normal.Damage);
            Assert.Equal(new(false, false, 58), triggered.Damage);
        }
        else
        {
            Assert.True(normal.Damage.Damage > 38);
            Assert.True(triggered.Damage.Damage > 58);
        }
        Assert.Equal((0, 4), (triggered.Source, triggered.Target));
        Assert.Equal((short)20, restored.Guest.Presentation!.ContactTiming!.Timing);
        Assert.Equal((short)0, restored.Guest.Presentation.Model.SequenceState!.Header40);
        Assert.Equal(Math.Max(0, (long)target.HitPoints - triggered.Damage.Damage), restored.Host.Actors.Single(actor => actor.Actor.Slot == 4).Actor.HitPoints);
        Assert.Null(restored.Guest.Auxiliary.Head);
        Assert.Throws<InvalidDataException>(() => Guest(restored.Guest with { EncounterId = Guid.NewGuid() }));
        CrossoverGunbladePresentationState standing = restored.Guest.Presentation!;
        Assert.Throws<InvalidDataException>(() => Guest(restored.Guest with { Presentation = standing with { Model = standing.Model with { FileSelector128 = 255 } } }));
        Assert.Equal((byte)selectedWeapon.Tier, Guest(restored.Guest with { Presentation = standing with { Model = standing.Model with { FileSelector128 = null, FileSelector129 = null } } }).Visual!.Model.FileSelector128);
        output.WriteLine($"Normal damage {normal.Damage.Damage}; triggered damage {triggered.Damage.Damage}; hit tick {triggered.Tick}; returned tick {restored.Host.Tick}; original host HP {target.HitPoints}");

        (Ff7BattleSession secondHost, CrossoverGunbladeHostAttack secondGuest) = Restore(restored);
        for (int tick = 0; tick < 2000 && secondHost.ReadyPlayer != 0; tick++)
        {
            secondHost.Advance();
        }
        Assert.True(secondHost.SubmitGuestAttack(0, 5));
        for (int tick = 0; tick < 2000 && secondGuest.Invocation is null; tick++)
        {
            secondHost.Advance();
        }
        Assert.Equal(5, secondGuest.Invocation?.TargetSlot);
        Assert.Equal(restored.Guest.GlobalRandom, secondGuest.Capture().GlobalRandom);

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
        CrossoverGunbladeHostAttack Guest(CrossoverGunbladeHostSnapshot? snapshot) => new(roster, kit, assets, game, guestRandom, () => host.Data.Cameras[0], _ => 128, snapshot, host.GuestEffectTargetAnchor, host.GuestEffectTargetBone);
        (Ff7BattleSession Host, CrossoverGunbladeHostAttack Guest) Restore(Saved source)
        {
            Saved saved = JsonSerializer.Deserialize<Saved>(JsonSerializer.Serialize(source))!;
            Ff7BattleSession next = Host(saved.Host);
            CrossoverGunbladeHostAttack animation = Guest(saved.Guest);
            next.BindGuestAttack(animation);
            return (next, animation);
        }
    }

    [Fact]
    public void Original_party_attack_sequences_publish_one_contact_and_restore_the_selected_actor()
    {
        string? ff7Root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8Root = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(ff7Root) && !string.IsNullOrWhiteSpace(ff8Root), "Select both original game installations.");
        Ff7Installation installation = Ff7Installation.Discover(ff7Root!);
        using Ff7Session origin = Ff7Session.NewGame(installation);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        Ff7BattleAnimationResources animations = new(archive, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(item => item.FormationId == 300);
        using GameAssets game = new(ff8Root!);
        using BattleAssets assets = new(ff8Root!);
        NativeCharacterVitals original = game.ReadNewGameCharacterVitals();
        byte[] actors = [1, 2, 3, 5, 6, 7, 8, 9, 10];
        foreach (byte character in actors)
        {
            NativeCharacterVitals vitals = original.DeepCopy();
            if (character >= 8)
            {
                vitals = vitals with { DreamEnabled = true };
                vitals.Records[0] = vitals.Records[0] with { CharacterId = character };
            }
            CrossoverFf8BattleKit kit = CrossoverFf8BattleKit.Read(vitals, assets, character);
            Ff7BattleSession host = Host(null);
            CrossoverBattleRoster roster = CrossoverBattleRoster.Create(Guid.NewGuid(), host.Capture(), [new(0, 0, character)], []);
            CrossoverGunbladeHostAttack guest = Guest(null);
            host.BindGuestAttack(guest);
            for (int tick = 0; tick < 2000 && host.ReadyPlayer != 0; tick++)
            {
                host.Advance();
            }
            Assert.True(host.SubmitGuestAttack(0, 4), $"Actor {character} did not admit the original target.");
            int contacts = 0;
            bool queued = false;
            bool trigger = false;
            bool restoredCue = false;
            long? hitTick = null;
            for (int tick = 0; tick < 300; tick++)
            {
                host.Advance();
                if (host.LastHit is { Source: 0, Target: 4 } hit && hit.Tick != hitTick)
                {
                    contacts++;
                    hitTick = hit.Tick;
                }
                if (guest.Invocation is not null)
                {
                    trigger |= guest.TriggerWindowOpen;
                    Assert.Equal(character, guest.Visual!.Model.ModelId4);
                    bool pendingCue = host.Action?.GuestAttackTargetCue is not null;
                    if (!queued || pendingCue && !restoredCue)
                    {
                        Saved snapshot = JsonSerializer.Deserialize<Saved>(JsonSerializer.Serialize(Capture(host, guest)))!;
                        host = Host(snapshot.Host);
                        guest = Guest(snapshot.Guest);
                        host.BindGuestAttack(guest);
                        queued = true;
                        restoredCue |= pendingCue;
                    }
                }
                else if (queued)
                {
                    break;
                }
            }
            Assert.True(queued, $"Actor {character} never started Attack.");
            Assert.True(guest.Invocation is null, $"Actor {character} did not return from Attack.");
            Assert.Equal(1, contacts);
            Assert.Equal(character == 6, trigger);
            if (character == 1)
            {
                Assert.True(restoredCue);
            }
            output.WriteLine($"Original actor {character}, weapon {kit.Weapon}, contact tick {hitTick}, returned tick {host.Tick}");

            Ff7BattleSession Host(Ff7BattleSnapshot? snapshot) => new(formation, origin.Progress, kernel, executable.NativeMateriaStatModifiers, 1, requestFlags: 4, snapshot: snapshot, animations: animations);
            CrossoverGunbladeHostAttack Guest(CrossoverGunbladeHostSnapshot? snapshot) => new(roster, kit, assets, game, game.ReadNewGameGlobalRandom(), () => host.Data.Cameras[0], _ => 128, snapshot, host.GuestEffectTargetAnchor, host.GuestEffectTargetBone);
        }
    }

    [Fact]
    public void Original_MP_reaction_KO_and_Barret_revive_use_Squall_native_sequences_across_cold_restore()
    {
        string? ff7Root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8Root = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(ff7Root) && !string.IsNullOrWhiteSpace(ff8Root), "Select both original game installations.");
        Ff7Installation installation = Ff7Installation.Discover(ff7Root!);
        using Ff7Session original = Ff7Session.NewGame(installation);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        Ff7BattleAnimationResources animations = new(archive, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(item => item.FormationId == 300);
        Ff7Progress progress = original.Progress with { Party = [0, 1, 255] };
        Ff7BattleSession host = Host(null);
        using GameAssets game = new(ff8Root!);
        using BattleAssets assets = new(ff8Root!);
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals();
        vitals = vitals.WithSavedHitPoints(vitals.FindPhysicalRow(0), 8);
        CrossoverFf8BattleKit kit = CrossoverFf8BattleKit.Read(vitals, assets, 0);
        CrossoverBattleRoster roster = CrossoverBattleRoster.Create(Guid.NewGuid(), host.Capture(), [new(0, 0, 0)], []);
        host.BindGuestVitals(new(roster.EncounterId, new('A', 64), 0, 0, 0, kit.RecordRow, kit.HitPoints, checked((ushort)kit.MaximumHitPoints), 6, 14, 24, 17));
        CrossoverGunbladeHostAttack guest = Guest(null);
        guest.PrepareStanding(host.VisualPlacement(0));
        host.BindGuestAttack(guest);
        bool hurt = false;
        for (int tick = 0; tick < 3500 && guest.Visual!.Model.Sequence != 3; tick++)
        {
            host.Advance();
            if (!hurt && guest.Visual!.Model.Sequence == 4)
            {
                Assert.True(host.LastHit is { Source: >= 4, Target: 0, Damage.Damage: > 0 });
                Assert.InRange(Squall().HitPoints, 1u, 7u);
                Cold();
                hurt = true;
            }
        }
        Assert.True(hurt);
        Assert.Equal((byte)3, guest.Visual!.Model.Sequence);
        Assert.Equal(0u, Squall().HitPoints);
        Assert.Equal(1u, Squall().Statuses & 1);
        Cold();
        for (int tick = 0; tick < 1500 && host.ReadyPlayer != 1; tick++)
        {
            host.Advance();
            Assert.Equal((byte)3, guest.Visual!.Model.Sequence);
        }
        Assert.Equal(1, host.ReadyPlayer);
        Assert.True(host.Submit(1, Ff7BattleCommand.Item, 7, 1));
        for (int tick = 0; tick < 1200 && (Squall().Statuses & 1) != 0; tick++)
        {
            host.Advance();
        }
        Assert.True(host.LastHit is { Source: 1, Target: 0, AttackId: 7 });
        Assert.Equal((uint)kit.MaximumHitPoints / 4, Squall().HitPoints);
        Assert.Equal(0u, Squall().Statuses & 1);
        Assert.True(PresentedKo());
        Assert.Equal((byte)3, guest.Visual!.Model.Sequence);
        Cold();
        long healedAt = host.Tick;
        for (int tick = 0; tick < 1000 && PresentedKo() == true; tick++)
        {
            host.Advance();
            if (PresentedKo() == true)
            {
                Assert.Equal((byte)3, guest.Visual!.Model.Sequence);
            }
        }
        Assert.True(PresentedKo() == false, $"The native Phoenix Down did not present revival after {host.Tick - healedAt} ticks; action {host.Action?.Command.CommandId}, last hit {host.LastHit}.");
        Cold();
        for (int tick = 0; tick < 120 && guest.Visual!.Model.Sequence != 1; tick++)
        {
            host.Advance();
        }
        Assert.Equal((byte)1, guest.Visual!.Model.Sequence);
        output.WriteLine($"Original MP native hurt4 and KO3; Barret's Phoenix Down revived Squall at {Squall().HitPoints} HP in native stand1, with exact cold state at all three boundaries.");

        bool? PresentedKo() => host.Capture().ActorModels.Single(model => model.Slot == 0).PresentedKnockedOut;
        Ff7BattleAiActor Squall() => host.Actors.Single(actor => actor.Actor.Slot == 0).Actor;
        Ff7BattleSession Host(Ff7BattleSnapshot? snapshot) => new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, requestFlags: 4, snapshot: snapshot, animations: animations);
        CrossoverGunbladeHostAttack Guest(CrossoverGunbladeHostSnapshot? snapshot) => new(roster, kit, assets, game, game.ReadNewGameGlobalRandom(), () => host.Data.Cameras[0], _ => 128, snapshot);
        void Cold()
        {
            string before = JsonSerializer.Serialize(Capture(host, guest));
            Saved saved = JsonSerializer.Deserialize<Saved>(before)!;
            host = Host(saved.Host);
            guest = Guest(saved.Guest);
            host.BindGuestAttack(guest);
            Assert.Equal(before, JsonSerializer.Serialize(Capture(host, guest)));
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
