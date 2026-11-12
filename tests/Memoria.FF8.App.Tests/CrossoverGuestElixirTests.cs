using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverGuestElixirTests
{
    [Fact]
    public void Queued_original_elixir_opens_the_native_item_gate_only_at_contact_after_cold_restore()
    {
        string? ff7 = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8 = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(ff7) && !string.IsNullOrWhiteSpace(ff8), "Select both original game installations.");
        Ff7Installation installation = Ff7Installation.Discover(ff7!);
        using Ff7Session original = Ff7Session.NewGame(installation);
        using GameAssets game = new(ff8!);
        using BattleAssets assets = new(ff8!);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(scene => scene.FormationId == 885);
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        Ff7BattleAnimationResources animations = new(archive, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
        Ff7BattleRenderer renderer = new(archive, formation.LocationId);
        NativeCharacterVitals origin = game.ReadNewGameCharacterVitals().DeepCopy();
        int row = origin.FindPhysicalRow(0);
        int itemCommand = origin.MenuDefinitions.CommandAbilityCommands.IndexOf((byte)4) + 20;
        origin.Records[row] = origin.Records[row] with { Junction = origin.Records[row].Junction with { Commands = (uint)itemCommand } };
        CrossoverFf8BattleKit kit = CrossoverFf8BattleKit.Read(origin, assets, 0);
        NativeItemStock[] slots = Enumerable.Repeat(new NativeItemStock(0, 0), 198).ToArray();
        slots[0] = new(9, 2);
        GameplaySaveEconomy economy = new(slots, null, 0, 0, 0, 0);
        Ff7BattleSession host = new(formation, original.Progress, kernel, executable.NativeMateriaStatModifiers, 1, animations: animations);
        CrossoverBattleRoster roster = CrossoverBattleRoster.Create(Guid.NewGuid(), host.Capture(), [new(0, 0, 0)], []);
        Ff7BattleGuestProjection projection = CrossoverGuestHostActions.Projection(kit);
        host.BindGuestVitals(new(roster.EncounterId, new('A', 64), 0, 0, 0, row, kit.HitPoints, projection.MaximumHp, projection.Level, projection.Luck, projection.Defense, projection.MagicDefense));
        CrossoverGunbladeHostAttack attack = new(roster, kit, assets, game, game.ReadNewGameGlobalRandom(), () => host.Data.Cameras[0], _ => 128);
        attack.PrepareStanding(host.VisualPlacement(0));
        CrossoverGuestHostActions actions = new(roster, kit, origin, NativeCrtRandomState.FromColdProcess(), attack, host, game, assets, originEconomy: economy);
        Bind();
        int pot = host.Actors.Single(actor => actor.Identity == 310).Actor.Slot;
        for (int frame = 0; frame < 1000 && host.ReadyPlayer != 0; frame++)
        {
            host.Advance();
        }
        Assert.Equal(0, host.ReadyPlayer);
        Assert.Equal(0x300u, Gate());
        string retainedBefore = RecoveryDigest();
        Ff7BattleCommandOption option = Assert.Single(host.Commands(0), option => option.Command == Ff7BattleCommand.Item && option.Argument == 9);
        Assert.Null(option.UnsupportedReason);
        Assert.NotEqual(0, option.ValidTargets & (1 << pot));
        string nativeInventory = JsonSerializer.Serialize(host.Capture().Inventory);
        Assert.True(host.SubmitGuestItem(0, pot, 9));
        Assert.Equal(1, actions.ItemStock(9));
        Assert.Equal(0x300u, Gate());
        Assert.Single(actions.Capture().Items!.AllPending);
        Assert.Empty(actions.Capture().Items!.Ledger);

        Ff7BattleSnapshot queued = RoundTrip(host.Capture());
        CrossoverGunbladeHostSnapshot caster = RoundTrip(attack.Capture());
        CrossoverGuestHostActionsSnapshot source = RoundTrip(actions.Capture());
        host = new(formation, original.Progress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: queued, animations: animations);
        attack = new(roster, kit, assets, game, game.ReadNewGameGlobalRandom(), () => host.Data.Cameras[0], _ => 128, caster);
        actions = new(roster, kit, origin, NativeCrtRandomState.FromColdProcess(), attack, host, game, assets, source, originEconomy: economy);
        Bind();
        Assert.Equal(1, actions.ItemStock(9));
        Assert.Equal(0x300u, Gate());
        bool observedEffect = false;
        bool committedContact = false;
        int publications = 0;
        for (int frame = 0; frame < 1600; frame++)
        {
            host.Advance();
            Assert.Null(host.GuestAbilityFailure);
            observedEffect |= actions.ActiveAbility?.Presentation.Effect is not null;
            if (host.LastHit is { Source: 0 } hit && hit.Target == pot && hit.AttackId == 9 && hit.Tick == host.Tick)
            {
                committedContact = true;
                publications++;
            }
            if (!committedContact)
            {
                Assert.Equal(0x300u, Gate());
            }
            if (committedContact && actions.Invocation is null && host.Capture().Action is null)
            {
                break;
            }
        }
        Assert.True(observedEffect);
        Assert.True(committedContact);
        Assert.Equal(1, publications);
        Assert.Equal(0u, Gate());
        Assert.Equal(1, actions.ItemStock(9));
        Assert.Empty(actions.Capture().Items!.AllPending);
        CrossoverGuestItemDebit debit = Assert.Single(actions.Capture().Items!.Ledger);
        Assert.False(debit.Refunded);
        Assert.Equal((ushort)9, debit.Selection.ItemId);
        Assert.Equal(pot, debit.Selection.TargetSlot);
        Assert.Equal(nativeInventory, JsonSerializer.Serialize(host.Capture().Inventory));
        Assert.Equal(2, economy.Items.Single(item => item.ItemId == 9).Count);
        Assert.Equal(retainedBefore, RecoveryDigest());

        uint Gate() => host.Actors.Single(actor => actor.Actor.Slot == pot).Actor.Conditions & 0x300;

        string RecoveryDigest()
        {
            NativeBattleRecoveryExecutableState state = game.ReadColdProcessNativeBattleMemory().EffectMemory!.GetRecoveryExecutable(58) ?? throw new InvalidDataException("No original recovery initialization owner.");
            byte[] bytes = new byte[state.Data.Length];
            state.Data.ReadInto(0, bytes);
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)) + ":" + (state.Channels is { } channels ? Convert.ToHexString(channels.AsSpan()) : "missing");
        }

        void Bind()
        {
            actions.BindForeignGeometry(slot =>
            {
                Ff7BattleActorState actor = host.Actors.Single(actor => actor.Actor.Slot == slot);
                Ff7BattlePlacement placement = host.RenderActorPlacement(slot);
                (int clip, int frame) = host.RenderActorPose(slot);
                return renderer.CaptureActorGeometry(new(Ff7BattleAnimationResources.ModelPrefix(actor.Identity, slot >= 4), placement.Position, placement.Yaw * MathF.Tau / 4096, clip, frame, slot < 3 ? host.WeaponModel(slot) : 0, host.RenderActorLimit(slot)) { ModelModifiers = host.ActorModelOutput(slot) });
            });
            host.BindGuestAttack(actions);
        }
    }

    private static T RoundTrip<T>(T value) where T : class
    {
        return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value)) ?? throw new InvalidDataException("No restored interaction state.");
    }

    [Fact]
    public void Elixir_projects_source_maximum_hp_and_common_status_recovery()
    {
        BattleNativeAction original = new(4, 9, 7, 9)
        {
            MagicId = 58,
            AttackType = 32,
            Power = 16,
            HitCount = 1,
            TargetFlags = 20,
            AttackFlags = 0xE2,
            DamagePresentation = 0,
            Element = 0,
            ElementPercent = null,
            StatusAttack = 200,
            Statuses0 = 0x7E,
            Statuses1 = 0x0100560D,
            Hit = 255,
            Critical = null,
            PowerModifier = null,
            LevelModifier = null,
            RawRecord = [],
            SupplementalRecord = []
        };
        Ff7BattleGuestItemEffect effect = CrossoverGuestHostActions.DescribeOriginItem(original, 0);
        Assert.Equal(16, effect.MaximumHitPointFraction);
        Assert.Equal(0, effect.HitPoints);
        Assert.Equal(0, effect.ReviveDivisor);
        Assert.NotEqual(0u, effect.RemoveStatuses & Ff7NativeBattleActionResolver.Sleep);
        Assert.NotEqual(0u, effect.RemoveStatuses & Ff7NativeBattleActionResolver.Poison);
        Assert.Equal(0u, effect.RemoveStatuses & Ff7NativeBattleActionResolver.Death);
        Ff7BattleGuestItemEffect medData = CrossoverGuestHostActions.DescribeOriginItem(original, 2);
        Assert.Equal(effect.MaximumHitPointFraction, medData.MaximumHitPointFraction);
        Assert.Equal(2, medData.RecoveryMultiplier);
        Assert.Equal(9999, medData.FractionRecoveryLimit);
        Assert.Equal(new Ff7GuestScriptInteraction(4, 5), CrossoverGuestHostActions.ClassifyScriptInteraction(4, 9, 4));
    }

    [Fact]
    public void Installed_elixir_keeps_its_actual_origin_record_and_selectable_enemy_side()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory), "Select the original FFVIII installation.");
        using BattleAssets assets = new(directory!);
        BattleNativeAction original = Assert.Single(assets.NativeActions, action => action.Command == 4 && action.ActionId == 9);
        Assert.Equal(32, original.AttackType);
        Assert.Equal(16, original.Power);
        Assert.Equal((byte)20, original.TargetFlags);
        Assert.True(NativeBattleLimitRules.CanSelectEitherSide(original.TargetFlags!.Value));
        Assert.Equal((ushort)0x7E, original.Statuses0);
        Assert.Equal(0x0100560Du, original.Statuses1);
        Assert.Equal(16, CrossoverGuestHostActions.DescribeOriginItem(original, 0).MaximumHitPointFraction);
    }
}
