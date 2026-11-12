using System.Reflection;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverRevivalHostTests
{
    [Theory]
    [InlineData("Life", false)]
    [InlineData("Life", true)]
    [InlineData("Full-life", false)]
    [InlineData("Full-life", true)]
    public void OriginalRevivalQueuesOneContactForAKnockedOutNativeAllyOrMappedGuest(string name, bool mappedTarget)
    {
        string? ff8 = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? ff7 = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(ff8) && !string.IsNullOrWhiteSpace(ff7), "Select both original game installations.");
        using GameAssets game = new(ff8!);
        using BattleAssets assets = new(ff8!);
        BattleNativeAction action = Assert.Single(assets.NativeActions, action => action.Command == 2 && action.Name == name);
        NativeCharacterVitals origin = game.ReadNewGameCharacterVitals().DeepCopy();
        int sourceRow = origin.FindPhysicalRow(0);
        int targetRow = origin.FindPhysicalRow(1);
        int magicCommand = origin.MenuDefinitions.CommandAbilityCommands.IndexOf((byte)2) + 20;
        origin.Records[sourceRow] = origin.Records[sourceRow] with { HitPointBonus = 9999, HitPoints = new(4000, 4000), Status = 0, Junction = origin.Records[sourceRow].Junction with { Commands = (uint)magicCommand } };
        origin.Records[targetRow] = origin.Records[targetRow] with { HitPoints = new(0, 0), Status = 1 };
        origin = origin with { MagicSystem = origin.MagicSystem with { SharedMagicStock = true } };
        origin = origin.ChangeMagicStock(sourceRow, action.ActionId, 4, MagicOperationReason.ScriptReward, out _);
        CrossoverFf8BattleKit sourceKit = CrossoverFf8BattleKit.Read(origin, assets, 0);
        CrossoverFf8BattleKit targetKit = CrossoverFf8BattleKit.Read(origin, assets, 1);
        Ff7Installation installation = Ff7Installation.Discover(ff7!);
        using Ff7Session original = Ff7Session.NewGame(installation);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(item => item.FormationId == 300);
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        Ff7BattleAnimationResources animations = new(archive, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
        Ff7Progress progress = original.Progress with { Party = [0, 1, 2] };
        Ff7BattleSession initial = Create();
        CrossoverBattleBinding[] bindings = mappedTarget ? [new(0, 0, 0), new(1, 1, 1)] : [new(0, 0, 0)];
        CrossoverBattleRoster roster = CrossoverBattleRoster.Create(Guid.NewGuid(), initial.Capture(), bindings, []);
        BindVitals(0, sourceRow, sourceKit);
        if (mappedTarget)
        {
            BindVitals(1, targetRow, targetKit);
        }
        Ff7BattleSnapshot snapshot = initial.Capture();
        snapshot = snapshot with
        {
            ActorModels = snapshot.ActorModels.Select(model => model.Slot is 1 or 4 ? model with { PresentedKnockedOut = true } : model).ToArray(),
            Actors = snapshot.Actors.Select(actor => actor.Actor.Slot == 0 ? actor with { Gauge = ushort.MaxValue, AwaitingTurn = true } : actor with
            {
                Gauge = 0,
                AwaitingTurn = false,
                Actor = actor.Actor with { HitPoints = actor.Actor.Slot is 1 or 4 ? 0u : actor.Actor.HitPoints, Statuses = actor.Actor.Slot is 1 or 4 ? 1u : 0x400u },
                Timers = actor.Actor.Slot is 1 or 4 ? default : Ff7NativeBattleStatusClock.Apply(actor.Timers, actor.Actor.Statuses, 0x400)
            }).ToArray()
        };
        Ff7BattleSession host = Create(snapshot);
        CrossoverGuestEncounter encounter = new(roster, origin, host, game.ReadNewGameGlobalRandom(), NativeCrtRandomState.FromColdProcess(), null, null);
        Dictionary<int, CrossoverGuestHostActions> guests = [];
        foreach (CrossoverBattleBinding binding in bindings)
        {
            CrossoverFf8BattleKit kit = binding.HostSlot == 0 ? sourceKit : targetKit;
            CrossoverGunbladeHostAttack attack = new(roster, kit, assets, game, encounter.GlobalRandom, () => host.RenderCameraOutput, _ => 128, readHostImpactAnchor: host.GuestEffectTargetAnchor, readHostTargetBone: host.GuestEffectTargetBone);
            attack.PrepareStanding(host.VisualPlacement(binding.HostSlot));
            attack.AdvanceIdle(host.Tick, host.Actors.Single(actor => actor.Actor.Slot == binding.HostSlot).Actor.Statuses, null);
            CrossoverGuestHostActions guest = new(roster, kit, attack, host, game, assets, encounter, null, null, null);
            guests.Add(binding.HostSlot, guest);
            host.BindGuestAttack(guest);
        }
        Ff7BattleRenderer renderer = new(archive, formation.LocationId);
        MethodInfo actorVisual = typeof(Ff7Scene).GetMethod("BattleActorVisual", BindingFlags.Static | BindingFlags.NonPublic)!;
        foreach (CrossoverGuestHostActions guest in guests.Values)
        {
            guest.BindForeignGeometry(slot => guests.TryGetValue(slot, out CrossoverGuestHostActions? mapped)
                ? CrossoverFf8BattleActorRenderer.CaptureActorGeometry(mapped.Visual!)
                : renderer.CaptureActorGeometry((Ff7BattleActorVisual)actorVisual.Invoke(null, [host, host.Actors.Single(actor => actor.Actor.Slot == slot)])!));
        }
        for (int tick = 0; tick < 1000 && host.ReadyPlayer != 0; tick++)
        {
            host.Advance(waitForCommand: true);
        }
        Assert.Equal(0, host.ReadyPlayer);
        Assert.Equal(true, host.Capture().ActorModels.Single(model => model.Slot == 1).PresentedKnockedOut);
        if (mappedTarget)
        {
            Assert.NotEqual(0u, guests[1].Visual!.Model.Flags8!.Value & 2);
        }
        Ff7BattleCommandOption option = Assert.Single(host.Commands(0), option => option.Command == Ff7BattleCommand.Magic && option.Argument == action.ActionId);
        Assert.Null(option.UnsupportedReason);
        Assert.Equal((ushort)2, option.CandidateTargets);
        Assert.Equal((ushort)2, option.ValidTargets);
        Assert.Null(Reason(1));
        Assert.NotNull(Reason(0));
        Assert.NotNull(Reason(2));
        Assert.NotNull(Reason(4));
        Assert.NotNull(Reason(5));
        Assert.Equal("The original ability requires a knocked-out target.", guests[0].TargetUnsupportedReason(Ff7BattleCommand.Magic, action.ActionId, host.GuestAbilityTarget(0)));
        Assert.Equal("The host cannot restore a knocked-out enemy after its removal lifecycle.", guests[0].TargetUnsupportedReason(Ff7BattleCommand.Magic, action.ActionId, host.GuestAbilityTarget(4)));
        Assert.Equal("The selected actor is not present.", guests[0].AbilityTargetUnsupportedReason(action.ActionId, 3));
        BattleUiTests.TestCanvas canvas = new();
        UiTheme theme = new();
        Ff7Scene.BattleTargetRow[] rows = [new(1, "KO ally", Reason(1)), new(0, "Living guest", Reason(0)), new(4, "KO enemy", Reason(4))];
        Ff7Scene.DrawBattleTargetChoices(canvas, theme, new(new(0, 0, 960, 672)), 1, rows, 0, false);
        Assert.Equal(theme.Foreground, Assert.Single(canvas.TextDraws, draw => draw.Text == "KO ally").Color);
        Assert.Equal(new UiColor(255, 80, 80), Assert.Single(canvas.TextDraws, draw => draw.Text == "Living guest").Color);
        Assert.Equal(new UiColor(255, 80, 80), Assert.Single(canvas.TextDraws, draw => draw.Text == "KO enemy").Color);
        string beforeRejected = JsonSerializer.Serialize(host.Capture());
        foreach (ushort illegal in new ushort[] { 1, 4, 16, 32 })
        {
            Assert.False(host.Submit(0, Ff7BattleCommand.Magic, action.ActionId, illegal));
            Assert.Equal(beforeRejected, JsonSerializer.Serialize(host.Capture()));
        }
        CrossoverGuestHostActions source = guests[0];
        Assert.Equal(4, source.WorkingStock.GetMagicCount(sourceRow, action.ActionId));
        Dictionary<int, Ff7BattleAiActor> before = host.Actors.ToDictionary(actor => actor.Actor.Slot, actor => actor.Actor);
        Assert.True(host.Submit(0, Ff7BattleCommand.Magic, action.ActionId, 2));
        Ff7BattleQueuedCommand queued = Assert.Single(host.Capture().Queue.Ready.OfType<Ff7BattleQueuedCommand>());
        Assert.Equal((ushort)2, queued.TargetMask);
        int contacts = 0;
        bool began = false;
        for (int tick = 0; tick < 10000; tick++)
        {
            host.Advance(waitForCommand: true);
            Assert.Null(host.GuestAbilityFailure);
            began |= source.Invocation is not null;
            if (host.LastHit is { Source: 0, Target: 1 } hit && hit.Tick == host.Tick)
            {
                contacts++;
                Assert.False(hit.Damage.Missed);
                Assert.True(hit.Damage.Damage < 0);
            }
            if (began && source.Invocation is null && host.Action is null)
            {
                break;
            }
        }
        Assert.True(began);
        Assert.Null(source.Invocation);
        Assert.Null(host.Action);
        Assert.Equal(1, contacts);
        Assert.Equal(3, source.WorkingStock.GetMagicCount(sourceRow, action.ActionId));
        Assert.Single(encounter.CommittedSpells);
        Ff7BattleAiActor revived = host.Actors.Single(actor => actor.Actor.Slot == 1).Actor;
        Assert.Equal(name == "Life" ? Math.Max(1u, revived.MaxHitPoints / 8) : revived.MaxHitPoints, revived.HitPoints);
        Assert.Equal(0u, revived.Statuses & 1);
        Assert.Equal(false, host.Capture().ActorModels.Single(model => model.Slot == 1).PresentedKnockedOut);
        Assert.All(host.Actors.Where(actor => actor.Actor.Slot >= 4), actor => Assert.Equal(before[actor.Actor.Slot], actor.Actor));
        if (mappedTarget)
        {
            Assert.Equal(0u, guests[1].Visual!.Model.Flags8!.Value & 2);
            Assert.NotEqual((byte)3, guests[1].Visual!.Model.Sequence);
        }
        Ff7BattleGuestAbility after = Assert.Single(source.Abilities, option => option.Ability == action.ActionId);
        Assert.NotNull(after.UnsupportedReason);
        Assert.NotNull(Reason(1));

        string? Reason(int slot) => host.GuestAbilityTargetUnsupportedReason(0, Ff7BattleCommand.Magic, action.ActionId, slot);

        Ff7BattleSession Create(Ff7BattleSnapshot? saved = null) => new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, requestFlags: 4, snapshot: saved, animations: animations);

        void BindVitals(int slot, int row, CrossoverFf8BattleKit kit)
        {
            Ff7BattleGuestProjection projection = CrossoverGuestHostActions.Projection(kit);
            initial.BindGuestVitals(new(roster.EncounterId, new('A', 64), slot, slot, kit.CharacterId, row, kit.HitPoints, (ushort)kit.MaximumHitPoints, projection.Level, projection.Luck, projection.Defense, projection.MagicDefense) { OriginStatuses = kit.Statuses.Saved });
        }
    }
}
