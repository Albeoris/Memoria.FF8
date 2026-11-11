using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverRinoaHostAttackTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Formation_300_Rinoa_weapon_flies_draws_and_publishes_one_contact_across_JSON(bool lethal)
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
        Ff7BattleSession host = Host(null);
        if (lethal)
        {
            Ff7BattleSnapshot opening = host.Capture();
            int index = Array.FindIndex(opening.Actors, actor => actor.Actor.Slot == 4);
            Ff7BattleActorState guard = opening.Actors[index];
            opening.Actors[index] = guard with { Actor = guard.Actor with { HitPoints = 1 } };
            host = Host(opening);
        }
        CrossoverBattleRoster roster = CrossoverBattleRoster.Create(Guid.NewGuid(), host.Capture(), [new(0, 0, 4)], [new(SessionGame.Ff7, 0, SessionGame.Ff8, 4, "explicit")]);
        CrossoverFf8BattleKit kit = CrossoverFf8BattleKit.Read(game.ReadNewGameCharacterVitals(), assets, 4);
        CrossoverNativeSpriteRenderer compositor = new(assets.ReadNativePrimitiveTables(), game.ReadNativeEffectMeshes(), game.ReadNativeBattleGraphicsRules().SummonColors!);
        CrossoverGunbladeHostAttack attack = Guest(host, null);
        for (int i = 0; i < 2000 && host.ReadyPlayer != 0; i++)
        {
            host.Advance();
        }
        Assert.True(host.SubmitGuestAttack(0, 4));
        for (int i = 0; i < 1000 && (attack.Visual?.DetachedWeapon is null || attack.AttackEffectVisual(compositor)?.Ordering?.Proxies.Length is not > 0); i++)
        {
            host.Advance();
        }
        Assert.NotNull(attack.Visual?.DetachedWeapon);
        Assert.False(attack.ContactPublished);
        Assert.False(attack.TriggerWindowOpen);
        CrossoverNativeEffectVisual effects = Assert.IsType<CrossoverNativeEffectVisual>(attack.AttackEffectVisual(compositor));
        Assert.NotEmpty(effects.Draws);
        Assert.True(effects.Ordering?.Proxies.Length > 0);
        CrossoverGunbladeHostSnapshot guestSave = RoundTrip(attack.Capture());
        Ff7BattleSession restored = Host(RoundTrip(host.Capture()));
        CrossoverGunbladeHostAttack restoredAttack = Guest(restored, guestSave);
        Assert.Equal(JsonSerializer.Serialize(attack.Visual!.DetachedWeapon), JsonSerializer.Serialize(restoredAttack.Visual!.DetachedWeapon));
        Capture before = Render(host, attack);
        Capture after = Render(restored, restoredAttack);
        Assert.NotEmpty(before.Triangles);
        Assert.Equal(before.Triangles, after.Triangles);

        int contacts = 0;
        for (int i = 0; i < 300 && attack.Invocation is not null; i++)
        {
            bool hadContact = attack.ContactPublished;
            host.Advance();
            restored.Advance();
            Assert.Equal(attack.ContactPublished, restoredAttack.ContactPublished);
            Assert.Equal(host.LastHit, restored.LastHit);
            if (!hadContact && attack.ContactPublished)
            {
                contacts++;
                Assert.Equal(4, host.LastHit?.Target);
                Assert.Equal(host.LastHit, host.DisplayedHit);
            }
        }
        Assert.Equal(1, contacts);
        Assert.Null(attack.Invocation);
        Assert.Null(restoredAttack.Invocation);
        Assert.Null(attack.Visual!.DetachedWeapon);
        Assert.Equal((byte)1, attack.Visual.Model.Sequence);
        Assert.Equal(JsonSerializer.Serialize(host.Capture()), JsonSerializer.Serialize(restored.Capture()));
        Assert.Equal(JsonSerializer.Serialize(attack.Capture()), JsonSerializer.Serialize(restoredAttack.Capture()));
        if (lethal)
        {
            Assert.Equal(0u, host.Actors.Single(actor => actor.Actor.Slot == 4).Actor.HitPoints);
            bool faded = false;
            for (int tick = 0; tick < 150 && host.ActorModelOutput(4).Visible; tick++)
            {
                faded |= host.ActorModelOutput(4) is { Red: > 0, Alpha: < 255 };
                host.Advance(waitForCommand: true);
                restored.Advance(waitForCommand: true);
                Assert.Equal(host.ActorModelOutput(4), restored.ActorModelOutput(4));
            }
            Assert.True(faded);
            Assert.False(host.ActorModelOutput(4).Visible);
        }

        Ff7BattleSession Host(Ff7BattleSnapshot? snapshot) => new(formation, origin.Progress, kernel, executable.NativeMateriaStatModifiers, 1, requestFlags: 4, snapshot: snapshot, animations: animations);

        CrossoverGunbladeHostAttack Guest(Ff7BattleSession owner, CrossoverGunbladeHostSnapshot? snapshot)
        {
            CrossoverGunbladeHostAttack guest = new(roster, kit, assets, game, game.ReadNewGameGlobalRandom(), () => owner.GuestRenderCameraOutput, _ => 128, snapshot) { AttackCrtRandom = new(0) };
            owner.BindGuestAttack(guest);
            return guest;
        }

        Capture Render(Ff7BattleSession owner, CrossoverGunbladeHostAttack guest)
        {
            Capture capture = new();
            Ff7BattleRenderer renderer = new(archive, formation.LocationId);
            renderer.Render(capture, 320, 240, new(owner.GuestRenderCameraOutput, []) { Guest = guest.Visual, GuestEffects = guest.AttackEffectVisual(compositor) });
            return capture;
        }
    }

    private static T RoundTrip<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;

    private sealed class Capture : ITriangleRenderer
    {
        public List<(Vector2 A, Vector2 B, Vector2 C)> Triangles { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
            Triangles.Clear();
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Triangles.Add((a.Position, b.Position, c.Position));
        }

        public void End()
        {
        }
    }
}
