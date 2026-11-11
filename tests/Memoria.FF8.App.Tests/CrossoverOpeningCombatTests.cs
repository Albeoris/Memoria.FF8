using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverOpeningCombatTests(ITestOutputHelper output)
{
    [Fact]
    public void Original_opening_formations_accept_a_guest_turn_and_continue_the_native_queue()
    {
        string? ff7Root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8Root = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(ff7Root) && !string.IsNullOrWhiteSpace(ff8Root), "Select both original game installations.");
        Ff7Installation installation = Ff7Installation.Discover(ff7Root!);
        using Ff7Session original = Ff7Session.NewGame(installation);
        using GameAssets game = new(ff8Root!);
        using BattleAssets assets = new(ff8Root!);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        IReadOnlyList<SceneFormation> formations = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path));
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        Ff7BattleAnimationResources animations = new(archive, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
        CrossoverFf8BattleKit kit = CrossoverFf8BattleKit.ReadSquall(game.ReadNewGameCharacterVitals(), assets);
        int[] selection = [.. Enumerable.Range(300, 25), 328, 329, 330, .. Enumerable.Range(332, 28), 364, 367, .. Enumerable.Range(392, 4)];
        foreach (int formationId in selection)
        {
            SceneFormation formation = formations.Single(item => item.FormationId == formationId);
            Ff7BattleSession host = new(formation, original.Progress, kernel, executable.NativeMateriaStatModifiers, 1, requestFlags: 4, animations: animations);
            CrossoverBattleRoster roster = CrossoverBattleRoster.Create(Guid.NewGuid(), host.Capture(), [new(0, 0, 0)], []);
            Ff7BattleGuestVitalsBinding binding = new(roster.EncounterId, new('A', 64), 0, 0, 0, kit.RecordRow, kit.HitPoints, (ushort)kit.MaximumHitPoints, 6, 14, 24, 17);
            host.BindGuestVitals(binding);
            CrossoverGunbladeHostAttack guest = new(roster, kit, assets, game, game.ReadNewGameGlobalRandom(), () => host.Data.Cameras[0], _ => 128);
            guest.PrepareStanding(host.VisualPlacement(0));
            host.BindGuestAttack(guest);
            bool restored = false;
            Ff7BattleHit? contact = null;
            for (int tick = 0; tick < 5000 && host.Outcome == Ff7BattleOutcome.Fighting; tick++)
            {
                if (host.ReadyPlayer == 0)
                {
                    Ff7BattleActorState? target = host.Actors.FirstOrDefault(actor => actor.Actor.Slot >= 4 && actor.Actor.HitPoints != 0 && (actor.Actor.Conditions & 8) != 0);
                    if (target is not null)
                    {
                        Assert.True(host.SubmitGuestAttack(0, target.Actor.Slot), $"Formation {formationId} rejected its guest target.");
                    }
                }
                if (guest.TriggerWindowOpen)
                {
                    if (!restored)
                    {
                        Ff7BattleSnapshot native = JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(host.Capture()))!;
                        CrossoverGunbladeHostSnapshot presentation = JsonSerializer.Deserialize<CrossoverGunbladeHostSnapshot>(JsonSerializer.Serialize(guest.Capture()))!;
                        host = new(formation, original.Progress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: native, animations: animations);
                        guest = new(roster, kit, assets, game, game.ReadNewGameGlobalRandom(), () => host.Data.Cameras[0], _ => 128, presentation);
                        host.BindGuestAttack(guest);
                        restored = true;
                    }
                    guest.PressTrigger();
                }
                host.Advance();
                if (host.LastHit is { Source: 0 } hit)
                {
                    contact = hit;
                }
                if (contact is not null && guest.Invocation is null)
                {
                    break;
                }
            }
            Assert.NotNull(contact);
            Assert.True(restored, $"Formation {formationId} never reached its original trigger window.");
            Assert.Null(guest.Invocation);
        }
        output.WriteLine($"Original native initialization, guest turn and trigger-window JSON restore passed for {selection.Length} opening formations.");
    }
}
