using System.Numerics;
using System.Reflection;
using System.Text.Json;
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
        Run(name);
    }

    internal void Run(string name, ushort? selectedTargets = null, bool arenaBridge = false, CrossoverArenaSurfaceCapture? arenaCapture = null, Action<Ff7BattleRenderer, Ff7BattleSession, CrossoverGuestHostActions>? observe = null)
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
        Assert.NotNull(attack.Visual!.Model.Color44);
        Assert.Equal(game.ReadNativeBattleGraphicsRules().SummonColors!.Normal | 0x32000000u, attack.Visual.Model.Color44);
        CrossoverGuestHostActions provider = new(roster, kit, origin, NativeCrtRandomState.FromColdProcess(), attack, host, game, assets);
        Ff7BattleRenderer renderer = new(archive, formation.LocationId);
        MethodInfo actorVisual = typeof(Ff7Scene).GetMethod("BattleActorVisual", BindingFlags.Static | BindingFlags.NonPublic)!;
        provider.BindForeignGeometry(slot => renderer.CaptureActorGeometry((Ff7BattleActorVisual)actorVisual.Invoke(null, [host, host.Actors.Single(actor => actor.Actor.Slot == slot)])!));
        if (arenaBridge)
        {
            provider.BindArena(renderer.CaptureArenaGeometry, arenaCapture is null ? null : arenaCapture.Resolve);
        }
        host.BindGuestAttack(provider);
        for (int tick = 0; tick < 1000 && host.ReadyPlayer != 0; tick++)
        {
            host.Advance(waitForCommand: true);
        }
        Assert.Equal(0, host.ReadyPlayer);
        Ff7BattleCommandOption option = Assert.Single(host.Commands(0), option => option.Command == Ff7BattleCommand.Magic && option.Argument == action.ActionId);
        Assert.Null(option.UnsupportedReason);
        ushort targets = selectedTargets ?? (revive ? (ushort)2 : (ushort)((option.CandidateTargets ?? option.ValidTargets) & (name == "Quake" ? 0x3F0 : 7)));
        int expected = BitOperations.PopCount(targets);
        if (selectedTargets is null)
        {
            Assert.True(revive ? expected == 1 : expected >= 2, $"The original action must exercise its full target group: {targets:X}.");
        }
        else if (expected > 1 && option.SelectSingle && option.GroupUses == 0)
        {
            string before = JsonSerializer.Serialize(host.Capture());
            Assert.False(host.Submit(0, Ff7BattleCommand.Magic, action.ActionId, targets));
            Assert.Equal(before, JsonSerializer.Serialize(host.Capture()));
            Assert.Equal(4, provider.WorkingStock.GetMagicCount(row, action.ActionId));
            Assert.Null(provider.Invocation);
            output.WriteLine($"{name}: native single-target action rejects mask {targets:X} without spending stock or a turn.");
            return;
        }
        Assert.True(expected > 0);
        foreach (Ff7BattleActorState target in host.Actors.Where(actor => (targets & 1 << actor.Actor.Slot) != 0))
        {
            Assert.Null(host.GuestAbilityTargetUnsupportedReason(0, Ff7BattleCommand.Magic, action.ActionId, target.Actor.Slot));
        }
        Assert.Equal(targets, (ushort)(targets & option.ValidTargets));
        Dictionary<int, Ff7BattleGuestPreparedContact> prepared = [];
        HashSet<int> publications = [];
        Assert.True(host.Submit(0, Ff7BattleCommand.Magic, action.ActionId, targets));
        Ff7BattleQueuedCommand queued = Assert.Single(host.Capture().Queue.Ready.OfType<Ff7BattleQueuedCommand>());
        Assert.Equal(targets, queued.TargetMask);
        Assert.Equal(4, provider.WorkingStock.GetMagicCount(row, action.ActionId));
        int contacts = 0;
        bool began = false;
        bool movedForeignTarget = false;
        NativeBattleEffectPresentation? lastEffect = null;
        int foreignTarget = -1;
        for (int tick = 0; tick < 10000; tick++)
        {
            host.Advance(waitForCommand: true);
            Assert.Null(host.GuestAbilityFailure);
            observe?.Invoke(renderer, host, provider);
            began |= provider.Invocation is not null;
            if (provider.ActiveAbility is { } active)
            {
                contacts = Math.Max(contacts, active.Presentation.PublishedContacts);
                IReadOnlyList<Ff7BattleGuestPhysicalHit> hits = active.GroupPrepared.IsEmpty ? [active.Prepared] : active.GroupPrepared;
                foreach (Ff7BattleGuestPhysicalHit hit in hits)
                {
                    Ff7BattleGuestPreparedContact receipt = hit.PreparedContact!;
                    int slot = receipt.Invocation.TargetSlot;
                    prepared[slot] = receipt;
                    if ((active.Presentation.PublishedTargetMask & 1 << slot) != 0 && publications.Add(slot))
                    {
                        Ff7BattleAiActor current = host.Actors.Single(actor => actor.Actor.Slot == slot).Actor;
                        Assert.Equal(receipt.Receipt.Target.Hp, current.HitPoints);
                        Assert.Equal(receipt.Receipt.Target.Statuses, current.Statuses);
                    }
                }
                if (name == "Water" && (targets & ~1) != 0 && active.Presentation.Effect is { } effect)
                {
                    lastEffect = effect;
                    foreignTarget = active.Presentation.BoundTargets.First(target => target.HostSlot != 0).Contact.ActorIndex;
                    movedForeignTarget |= !IsIdentity(effect.ForeignPlacement(foreignTarget));
                }
            }
            if (host.LastHit is { Source: 0 } published && published.Tick == host.Tick)
            {
                Ff7BattleGuestPreparedContact receipt = prepared[published.Target];
                Assert.Equal(receipt.Receipt.Damage, published.Damage);
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
        Assert.Equal(expected, publications.Count);
        Assert.Equal(targets, publications.Aggregate(0, (mask, target) => mask | 1 << target));
        Assert.Equal(3, provider.WorkingStock.GetMagicCount(row, action.ActionId));
        if (name == "Water" && (targets & ~1) != 0)
        {
            Assert.True(movedForeignTarget, "The original Water action must publish its temporary foreign actor placement.");
            Assert.NotNull(lastEffect);
            Assert.True(IsIdentity(lastEffect.ForeignPlacement(foreignTarget)), "Water must restore the foreign actor's base placement before completing.");
        }
        foreach (Ff7BattleActorState target in host.Actors.Where(actor => (targets & (1 << actor.Actor.Slot)) != 0))
        {
            if (action.AttackType is 3 or 4 or 5 or 6 or 21 or 32)
            {
                Assert.Equal(action.AttackType == 5 ? Math.Max(1, target.Actor.MaxHitPoints / 8) : target.Actor.MaxHitPoints, target.Actor.HitPoints);
                Assert.Equal(0u, target.Actor.Statuses & 1);
                if (revive)
                {
                    Assert.Equal(false, host.Capture().ActorModels.Single(model => model.Slot == target.Actor.Slot).PresentedKnockedOut);
                }
            }
            else
            {
                Ff7BattleGuestPreparedContact receipt = prepared[target.Actor.Slot];
                Assert.False(receipt.Receipt.Damage.Missed);
                Assert.True(receipt.Receipt.Damage.Damage > 0);
                Assert.True(target.Actor.HitPoints < receipt.TargetBefore.Hp);
                uint added = receipt.Receipt.AddedStatuses & ~Ff7NativeBattleActionResolver.Death;
                if (target.Actor.HitPoints != 0)
                {
                    Assert.Equal(added, target.Actor.Statuses & added);
                }
            }
        }
        output.WriteLine($"{name}: mask {targets:X}, {contacts} original contacts, one stock debit, host action complete.");

        static bool IsIdentity(NativeBattleActionMatrix matrix) => matrix.X == 0 && matrix.Y == 0 && matrix.Z == 0 && matrix.Rotation.SequenceEqual(NativeBattleForeignModelGeometry.Identity.Rotation);

        Ff7BattleSession Create(Ff7BattleSnapshot? saved = null) => new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, requestFlags: 4, snapshot: saved, animations: animations);
    }
}
