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

public sealed class CrossoverParticipantResourceHostTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("Electrocute", (byte)3, (byte)4)]
    [InlineData("Gatling Gun", (byte)3, (byte)4)]
    [InlineData("Micro Missiles", (byte)3, (byte)4)]
    [InlineData("Mighty Guard", (byte)3, (byte)1)]
    [InlineData("Ray-Bomb", (byte)3, (byte)4)]
    [InlineData("Ultra Waves", (byte)3, (byte)4)]
    [InlineData("No Mercy", (byte)6, (byte)4)]
    public void Original_participant_resources_survive_full_host_queue_and_next_ordinary_action(string name, byte character, byte crisis)
    {
        string? ff8 = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? ff7 = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(ff8) && !string.IsNullOrWhiteSpace(ff7), "Select both original game installations.");
        using GameAssets game = new(ff8!);
        using BattleAssets assets = new(ff8!);
        NativeCharacterVitals origin = game.ReadNewGameCharacterVitals().DeepCopy();
        if (character >= 6)
        {
            origin = origin.WithExtendedRoster(true);
        }
        int row = CrossoverGuestRecord.FindOriginalRow(origin, character);
        int magicCommand = origin.MenuDefinitions.CommandAbilityCommands.IndexOf((byte)2) + 20;
        origin.Records[row] = origin.Records[row] with { HitPointBonus = 9999, HitPoints = new(9000, 9000), Status = 0, Junction = origin.Records[row].Junction with { Commands = (uint)magicCommand } };
        origin = origin with { MagicSystem = origin.MagicSystem with { SharedMagicStock = true } };
        BattleNativeAction fire = Assert.Single(assets.NativeActions, action => action.Command == 2 && action.Name == "Fire");
        origin = origin.ChangeMagicStock(row, fire.ActionId, 4, MagicOperationReason.ScriptReward, out _);
        CrossoverFf8BattleKit kit = CrossoverFf8BattleKit.Read(origin, assets, character);
        GameplaySaveLimitProgress learned = GameplaySaveLimitProgress.Capture(game.ReadNewGameLimitProgress()) with { BlueMagicFlags = ushort.MaxValue };
        CrossoverGuestAbility ability = Assert.Single(CrossoverGuestAbilityCatalog.Read(assets, origin, kit, learned, crisis), choice => choice.Name == name);
        BattleNativeAction action = ability.NativeAction!;
        Assert.Equal(character == 3 ? (byte)15 : kit.Limit.Command, action.Command);
        if (character == 3)
        {
            Assert.Equal(crisis, action.CrisisLevel);
        }

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
        CrossoverBattleRoster roster = CrossoverBattleRoster.Create(Guid.NewGuid(), initial.Capture(), [new(0, 0, character)], []);
        Ff7BattleGuestProjection projection = CrossoverGuestHostActions.Projection(kit);
        initial.BindGuestVitals(new(roster.EncounterId, new('A', 64), 0, 0, character, row, kit.HitPoints, (ushort)kit.MaximumHitPoints, projection.Level, projection.Luck, projection.Defense, projection.MagicDefense));
        Ff7BattleSnapshot fixture = initial.Capture();
        fixture = fixture with
        {
            Actors = fixture.Actors.Select(actor => actor.Actor.Slot == 0 ? actor with { Gauge = ushort.MaxValue, AwaitingTurn = true } : actor with
            {
                Gauge = 0,
                AwaitingTurn = false,
                Actor = actor.Actor with { HitPoints = actor.Actor.Slot >= 4 ? 60000u : 100u, MaxHitPoints = actor.Actor.Slot >= 4 ? 60000u : actor.Actor.MaxHitPoints, Statuses = 0x400 },
                Timers = Ff7NativeBattleStatusClock.Apply(actor.Timers, actor.Actor.Statuses, 0x400)
            }).ToArray()
        };
        Ff7BattleSession host = Create(fixture);
        CrossoverGunbladeHostAttack attack = new(roster, kit, assets, game, game.ReadNewGameGlobalRandom(), () => host.RenderCameraOutput, _ => 128, readHostImpactAnchor: host.GuestEffectTargetAnchor, readHostTargetBone: host.GuestEffectTargetBone);
        attack.PrepareStanding(host.VisualPlacement(0));
        CrossoverGuestHostActions provider = new(roster, kit, origin, NativeCrtRandomState.FromColdProcess(), attack, host, game, assets);
        provider.BindLimitProgress(learned);
        Ff7BattleRenderer renderer = new(archive, formation.LocationId);
        MethodInfo actorVisual = typeof(Ff7Scene).GetMethod("BattleActorVisual", BindingFlags.Static | BindingFlags.NonPublic)!;
        provider.BindForeignGeometry(slot => renderer.CaptureActorGeometry((Ff7BattleActorVisual)actorVisual.Invoke(null, [host, host.Actors.Single(actor => actor.Actor.Slot == slot)])!));
        provider.BindArena(renderer.CaptureArenaGeometry);
        host.BindGuestAttack(provider);
        for (int tick = 0; tick < 1000 && host.ReadyPlayer != 0; tick++)
        {
            host.Advance(waitForCommand: true);
        }
        Assert.Equal(0, host.ReadyPlayer);
        typeof(CrossoverGuestHostActions).GetField("limitSelection", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(provider, new CrossoverNativeRenzokukenSelection(NativeBattleRandomState.Initialize(0), host.Tick, crisis));
        Ff7BattleCommandOption option = Assert.Single(host.Commands(0), option => option.Command == Ff7BattleCommand.Magic && option.Argument == ability.Key);
        Assert.Null(option.UnsupportedReason);
        int selected = name == "Mighty Guard" ? 0 : 4;
        ushort side = selected < 3 ? (ushort)7 : (ushort)0x3F0;
        ushort targets = option.SelectSingle && option.GroupUses == 0 ? (ushort)(1 << selected) : (ushort)((option.CandidateTargets ?? option.ValidTargets) & (option.AllTargets ? 0x3F7 : side));
        Assert.True(targets != 0);
        Assert.Equal(targets, (ushort)(targets & option.ValidTargets));
        string stock = JsonSerializer.Serialize(provider.WorkingStock.MagicSystem);
        NativeMagicStock[][] inventories = provider.WorkingStock.Records.Select(record => record.MagicInventory.ToArray()).ToArray();
        Assert.True(host.Submit(0, Ff7BattleCommand.Magic, ability.Key, targets));
        Assert.Equal(targets, Assert.Single(host.Capture().Queue.Ready.OfType<Ff7BattleQueuedCommand>()).TargetMask);
        bool began = false;
        int contacts = 0;
        int visuals = 0;
        NativeBattleGraphicsModel? restored = null;
        for (int tick = 0; tick < 10000; tick++)
        {
            host.Advance(waitForCommand: true);
            Assert.Null(host.GuestAbilityFailure);
            began |= provider.Invocation is not null;
            if (provider.ActiveAbility is { } active)
            {
                contacts = Math.Max(contacts, active.Presentation.PublishedContacts);
                visuals += active.Presentation.Ordering is { Packets.Count: > 0 } || !active.Presentation.Draws.IsEmpty ? 1 : 0;
                restored = active.Presentation.Cast.Model;
                Assert.Null(active.StockAction);
                Assert.Null(active.ItemCost);
            }
            if (began && provider.Invocation is null && host.Action is null)
            {
                restored = provider.Visual!.Model;
                break;
            }
        }
        Assert.True(began);
        Assert.Null(provider.Invocation);
        Assert.Null(host.Action);
        Assert.Equal(BitOperations.PopCount(targets), contacts);
        Assert.True(visuals > 0);
        Assert.NotNull(restored);
        Assert.Equal((byte)1, restored.Sequence);
        Assert.Null(restored.ResourceMagicId);
        Assert.Null(restored.ActiveStagedResourceRow100);
        Assert.Equal(restored.OriginalResourceRow104, restored.ActiveResourceRow100);
        Assert.NotNull(restored.ActiveResourceRow100);
        Assert.NotNull(restored.BodyTextureSlots);
        Assert.Equal(stock, JsonSerializer.Serialize(provider.WorkingStock.MagicSystem));
        for (int index = 0; index < inventories.Length; index++)
        {
            Assert.Equal(inventories[index], provider.WorkingStock.Records[index].MagicInventory);
        }
        Assert.Equal(4, provider.WorkingStock.GetMagicCount(row, fire.ActionId));

        for (int tick = 0; tick < 10000 && host.ReadyPlayer != 0; tick++)
        {
            host.Advance(waitForCommand: true);
        }
        Assert.Equal(0, host.ReadyPlayer);
        Assert.True(host.Submit(0, Ff7BattleCommand.Magic, fire.ActionId, (ushort)(1 << 4)));
        bool nextBegan = false;
        for (int tick = 0; tick < 4000; tick++)
        {
            host.Advance(waitForCommand: true);
            Assert.Null(host.GuestAbilityFailure);
            nextBegan |= provider.Invocation is not null;
            if (nextBegan && provider.Invocation is null && host.Action is null)
            {
                break;
            }
        }
        Assert.True(nextBegan);
        Assert.Null(provider.Invocation);
        Assert.Null(host.Action);
        Assert.Equal(3, provider.WorkingStock.GetMagicCount(row, fire.ActionId));
        Assert.Equal((byte)1, provider.Visual!.Model.Sequence);
        output.WriteLine($"{name} Crisis{crisis}: mask {targets:X}, {contacts} contacts, {visuals} visual callbacks, no source debit, original source restored and following Fire completed.");

        Ff7BattleSession Create(Ff7BattleSnapshot? saved = null) => new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, requestFlags: 4, snapshot: saved, animations: animations);
    }
}
