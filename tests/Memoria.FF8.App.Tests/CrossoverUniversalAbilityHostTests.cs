using System.Numerics;
using System.Reflection;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverUniversalAbilityHostTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("Life")]
    [InlineData("Full-life")]
    [InlineData("Full-cure")]
    [InlineData("Quake")]
    public void OriginalAbilityCompletesOneQueueEntryAndOneStockTransactionForEverySelectedTarget(string name)
    {
        string? ff8 = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? ff7 = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(ff8) && !string.IsNullOrWhiteSpace(ff7), "Select both original game installations.");
        using GameAssets game = new(ff8!);
        using BattleAssets assets = new(ff8!);
        BattleNativeAction action = Assert.Single(assets.NativeActions, action => action.Command == 2 && action.Name == name);
        NativeCharacterVitals origin = game.ReadNewGameCharacterVitals().DeepCopy();
        int row = origin.FindPhysicalRow(0);
        int magicCommand = origin.MenuDefinitions.CommandAbilityCommands.IndexOf((byte)2) + 20;
        origin.Records[row] = origin.Records[row] with { HitPointBonus = 9999, HitPoints = new(4000, 4000), Status = 0, Junction = origin.Records[row].Junction with { Commands = (uint)magicCommand } };
        origin = origin with { MagicSystem = origin.MagicSystem with { SharedMagicStock = true } };
        origin = origin.ChangeMagicStock(row, action.ActionId, 4, MagicOperationReason.ScriptReward, out _);
        CrossoverFf8BattleKit kit = CrossoverFf8BattleKit.Read(origin, assets, 0);
        Ff7Installation installation = Ff7Installation.Discover(ff7!);
        using Ff7Session original = Ff7Session.NewGame(installation);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(item => item.FormationId == 300);
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        Ff7BattleAnimationResources animations = new(archive, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
        Ff7Progress progress = original.Progress with { Party = [0, 1, 255] };
        Ff7BattleSession initial = Create();
        CrossoverBattleRoster roster = CrossoverBattleRoster.Create(Guid.NewGuid(), initial.Capture(), [new(0, 0, 0)], []);
        Ff7BattleGuestProjection projection = CrossoverGuestHostActions.Projection(kit);
        initial.BindGuestVitals(new(roster.EncounterId, new('A', 64), 0, 0, 0, row, kit.HitPoints, (ushort)kit.MaximumHitPoints, projection.Level, projection.Luck, projection.Defense, projection.MagicDefense));
        bool revive = action.AttackType is 5 or 6;
        Ff7BattleSnapshot fixture = initial.Capture();
        fixture = fixture with
        {
            ActorModels = fixture.ActorModels.Select(model => revive && model.Slot == 1 ? model with { PresentedKnockedOut = true } : model).ToArray(),
            Actors = fixture.Actors.Select(actor => actor.Actor.Slot == 0 ? actor with { Gauge = ushort.MaxValue, AwaitingTurn = true } : actor with
            {
                Gauge = 0,
                AwaitingTurn = false,
                Actor = actor.Actor with { HitPoints = actor.Actor.Slot == 1 ? revive ? 0u : 100u : actor.Actor.HitPoints, Statuses = actor.Actor.Slot == 1 && revive ? 1u : 0x400u },
                Timers = actor.Actor.Slot == 1 && revive ? default : Ff7NativeBattleStatusClock.Apply(actor.Timers, actor.Actor.Statuses, 0x400)
            }).ToArray()
        };
        Ff7BattleSession host = Create(fixture);
        CrossoverGunbladeHostAttack attack = new(roster, kit, assets, game, game.ReadNewGameGlobalRandom(), () => host.RenderCameraOutput, _ => 128, readHostImpactAnchor: host.GuestEffectTargetAnchor, readHostTargetBone: host.GuestEffectTargetBone);
        attack.PrepareStanding(host.VisualPlacement(0));
        CrossoverGuestHostActions provider = new(roster, kit, origin, NativeCrtRandomState.FromColdProcess(), attack, host, game, assets);
        Ff7BattleRenderer renderer = new(archive, formation.LocationId);
        MethodInfo actorVisual = typeof(Ff7Scene).GetMethod("BattleActorVisual", BindingFlags.Static | BindingFlags.NonPublic)!;
        provider.BindForeignGeometry(slot => renderer.CaptureActorGeometry((Ff7BattleActorVisual)actorVisual.Invoke(null, [host, host.Actors.Single(actor => actor.Actor.Slot == slot)])!));
        host.BindGuestAttack(provider);
        for (int tick = 0; tick < 1000 && host.ReadyPlayer != 0; tick++)
        {
            host.Advance(waitForCommand: true);
        }
        Assert.Equal(0, host.ReadyPlayer);
        Ff7BattleCommandOption option = Assert.Single(host.Commands(0), option => option.Command == Ff7BattleCommand.Magic && option.Argument == action.ActionId);
        Assert.Null(option.UnsupportedReason);
        ushort targets = revive ? (ushort)2 : (ushort)((option.CandidateTargets ?? option.ValidTargets) & (name == "Quake" ? 0x3F0 : 7));
        int expected = BitOperations.PopCount(targets);
        Assert.True(revive ? expected == 1 : expected >= 2, $"The original action must exercise its full target group: {targets:X}.");
        Assert.True(host.Submit(0, Ff7BattleCommand.Magic, action.ActionId, targets));
        int contacts = 0;
        bool began = false;
        for (int tick = 0; tick < 10000; tick++)
        {
            host.Advance(waitForCommand: true);
            Assert.Null(host.GuestAbilityFailure);
            began |= provider.Invocation is not null;
            if (provider.ActiveAbility is { } active)
            {
                contacts = Math.Max(contacts, active.Presentation.PublishedContacts);
            }
            if (began && provider.Invocation is null && host.Action is null)
            {
                break;
            }
        }
        Assert.True(began);
        Assert.Null(provider.Invocation);
        Assert.Null(host.Action);
        Assert.Equal(expected, contacts);
        Assert.Equal(3, provider.WorkingStock.GetMagicCount(row, action.ActionId));
        foreach (Ff7BattleActorState target in host.Actors.Where(actor => (targets & (1 << actor.Actor.Slot)) != 0))
        {
            if (name == "Quake")
            {
                Assert.Equal(0u, target.Actor.HitPoints);
            }
            else
            {
                Assert.Equal(name == "Life" ? Math.Max(1, target.Actor.MaxHitPoints / 8) : target.Actor.MaxHitPoints, target.Actor.HitPoints);
                Assert.Equal(0u, target.Actor.Statuses & 1);
                if (revive)
                {
                    Assert.Equal(false, host.Capture().ActorModels.Single(model => model.Slot == target.Actor.Slot).PresentedKnockedOut);
                }
            }
        }
        output.WriteLine($"{name}: mask {targets:X}, {contacts} original contacts, one stock debit, host action complete.");

        Ff7BattleSession Create(Ff7BattleSnapshot? saved = null) => new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, requestFlags: 4, snapshot: saved, animations: animations);
    }
}
