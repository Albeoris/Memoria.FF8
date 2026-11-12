using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record CrossoverGuestAbility(ushort Key, byte Command, string Category, string Name, BattleNativeAction? NativeAction, int Stock, string? RuleUnsupportedReason)
{
    public int? AbilityLevel { get; init; }
    public int DamagePercent { get; init; }
}

internal static class CrossoverGuestAbilityCatalog
{
    public static IReadOnlyList<CrossoverGuestAbility> Read(BattleAssets assets, NativeCharacterVitals vitals, CrossoverFf8BattleKit kit, GameplaySaveLimitProgress? limits, byte crisisLevel)
    {
        ArgumentNullException.ThrowIfNull(assets);
        return Read(assets.NativeActions, assets.ActionDefinitions, assets.ReadNativeCommand, vitals, kit, limits, crisisLevel);
    }

    internal static IReadOnlyList<CrossoverGuestAbility> Read(IReadOnlyList<BattleNativeAction> nativeActions, IReadOnlyList<BattleActionDefinition> actionDefinitions, Func<byte, BattleNativeCommandBinding> readCommand, NativeCharacterVitals vitals, CrossoverFf8BattleKit kit, GameplaySaveLimitProgress? limits, byte crisisLevel)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(crisisLevel, 4);
        List<CrossoverGuestAbility> choices = [];
        if (kit.Commands.Contains((byte)2))
        {
            foreach (BattleNativeAction action in nativeActions.Where(action => action.Command == 2 && action.ActionId != 0))
            {
                MagicCastQuote quote = MagicCastingService.Quote(vitals, kit.RecordRow, action.ActionId, MagicCastContext.Battle);
                if (quote.Allowed && quote.Available > 0)
                {
                    choices.Add(Choice(action, "Magic", quote.Available));
                }
            }
        }
        if (kit.Commands.Contains((byte)3))
        {
            foreach (int id in vitals.Records[kit.RecordRow].Junction.GuardianForceAssignments)
            {
                NativeGuardianForceVital gf = vitals.GuardianForces[id];
                if ((gf.Flags & 1) == 0 || gf.HitPoints == 0)
                {
                    continue;
                }
                BattleActionDefinition definition = BattlePartyProjection.ProjectGuardianForceAction(vitals, actionDefinitions.Single(action => action.Ability == BattleAbility.GuardianForce && action.Id == id));
                BattleNativeAction action = definition.NativeAction ?? throw new InvalidDataException("The assigned GF has no original action record.");
                choices.Add(Choice(action, "GF", 1) with { Name = definition.DisplayName ?? definition.Name, AbilityLevel = definition.Level, DamagePercent = definition.DamagePercent });
            }
        }
        foreach (byte command in kit.Commands.Distinct().Where(command => command is not (0 or 1 or 2 or 3 or 4 or 12)))
        {
            BattleNativeCommandBinding binding = readCommand(command);
            BattleNativeAction? action = nativeActions.SingleOrDefault(action => action.Command == command && action.ActionId == binding.ActionId && action.CrisisLevel is null);
            choices.Add(action is null ? Invocation(binding, "Command", "This command requires its native selection process.") : Choice(action, "Command", 1) with { Name = binding.Name, RuleUnsupportedReason = "This equipped command requires native command operations beyond the shared ability transaction." });
        }
        if (crisisLevel != 0)
        {
            AddLimits(choices, nativeActions, readCommand, kit, limits, crisisLevel);
        }
        return choices;
    }

    public static byte SelectLimitLevel(CrossoverFf8BattleKit kit, Ff7BattleAiActor source, int deadParty, bool disabled, NativeBattleRandomState random, out NativeBattleRandomState nextRandom)
    {
        ushort saved = Ff7GuestBattleStatuses.ToOrigin(source.Statuses, kit.Statuses.Saved);
        uint battle = kit.Statuses.Battle | Ff7GuestBattleStatuses.ToOriginBattle(source.Statuses);
        byte roll = 0;
        nextRandom = disabled || (battle & 0x200) != 0 ? random : random.Draw(out roll);
        return NativeBattleLimitRules.CrisisLevel(kit.Limit, checked((int)source.HitPoints), checked((int)source.MaxHitPoints), saved, battle, deadParty, roll, disabled);
    }

    internal static ushort Key(byte command, ushort actionId)
    {
        if (actionId > byte.MaxValue)
        {
            throw new NotSupportedException("The native action requires an extended selection identifier.");
        }
        return command == 2 ? actionId : (ushort)((command << 8) | actionId);
    }

    private static void AddLimits(List<CrossoverGuestAbility> choices, IReadOnlyList<BattleNativeAction> nativeActions, Func<byte, BattleNativeCommandBinding> readCommand, CrossoverFf8BattleKit kit, GameplaySaveLimitProgress? limits, byte crisis)
    {
        byte command = kit.Limit.Command;
        if (command == 15 && limits is not null)
        {
            limits.Validate();
            foreach (BattleNativeAction action in nativeActions.Where(action => action.Command == 15 && action.CrisisLevel == crisis && action.ActionId < 16 && (limits.BlueMagicFlags & (1 << action.ActionId)) != 0))
            {
                choices.Add(Choice(action, "Blue Magic", 1));
            }
            return;
        }
        if (kit.CharacterId is >= 6 and <= 10)
        {
            ushort id = NativeBattleLimitRules.GuestActionId(kit.CharacterId);
            BattleNativeAction action = nativeActions.Single(action => action.Command == command && action.ActionId == id && (action.CrisisLevel is null || action.CrisisLevel == crisis));
            choices.Add(Choice(action, "Limit", 1));
            return;
        }
        string reason = command switch
        {
            5 => "Renzokuken requires its native trigger and finisher selection process.",
            11 => "Duel requires its native timed move selection process.",
            14 => "Shot requires its native ammunition selection and firing process.",
            15 => "Blue Magic requires the saved learned spell flags.",
            16 => "Slots requires its native spell and repetition selection process.",
            19 => "Combine and Angel Wing require their saved battle flags and native selection process.",
            _ => "This Limit requires its native selection process."
        };
        choices.Add(Invocation(readCommand(command), "Limit", reason));
    }

    private static CrossoverGuestAbility Choice(BattleNativeAction action, string category, int stock)
    {
        return new(Key(action.Command, action.ActionId), action.Command, category, action.Name ?? throw new InvalidDataException("The native ability has no original name."), action, stock, null);
    }

    private static CrossoverGuestAbility Invocation(BattleNativeCommandBinding binding, string category, string reason)
    {
        return new(Key(binding.Command, binding.ActionId), binding.Command, category, binding.Name, null, 1, reason);
    }
}
