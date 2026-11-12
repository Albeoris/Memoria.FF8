using System.Security.Cryptography;
using System.Text;
using Memoria.FF7.Runtime;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class CrossoverGuestHostActions
{
    private readonly Dictionary<(int Target, ushort Ability), (Ff7BattleGuestPhysicalHit Hit, Ff7BattleGuestContactContext Context, BattleDamageForecast Forecast)> smartForecasts = [];

    public BattleSmartAttackPreview PreviewSmartAttack(int targetSlot, bool forcePhysical = false)
    {
        Ff7BattleGuestPhysicalHit physicalHit = CrossoverGunbladeHostAttack.ProjectContact(kit, 0);
        Ff7BattleGuestContactContext physicalContext = host.GuestContactContext(ActorSlot, targetSlot, physicalHit, physical: true);
        BattleDamageForecast physical = ReadSmartForecast(targetSlot, 0, physicalHit, physicalContext);
        List<BattleSmartAttackCandidate> candidates = [];
        StringBuilder revision = new();
        revision.Append(physicalContext with { RandomKernel = default }).Append('|').Append(encounter.Working.MagicSystem.AttackModes[kit.RecordRow]);
        bool canCast = !forcePhysical && encounter.Working.MagicSystem.AttackModes[kit.RecordRow] == MagicAttackMode.SmartMagic &&
            !kit.Commands.IsDefaultOrEmpty && kit.Commands[0] is 1 or 12 && kit.Commands.Contains((byte)2) && targetSlot >= 4 &&
            physicalContext.SourceVitals.Hp > 0 && physicalContext.TargetVitals.Hp > 0 &&
            Ff7GuestBattleStatuses.CanCastMagic(physicalContext.SourceVitals.Statuses);
        if (canCast && physical.KillProbability <= 0.9)
        {
            foreach (BattleActionDefinition definition in assets.ActionDefinitions.Where(BattleSession.IsSmartSpell))
            {
                BattleNativeAction native = definition.NativeAction!;
                MagicCastQuote quote = MagicCastingService.Quote(encounter.Working, kit.RecordRow, definition.Id, MagicCastContext.Battle);
                if (!quote.Allowed || quote.Available <= 0 || AbilityTargetUnsupportedReason((ushort)definition.Id, targetSlot) is not null)
                {
                    continue;
                }
                Ff7BattleGuestPhysicalHit hit = CrossoverBattleAbilityRules.Describe(kit, native);
                Ff7BattleGuestContactContext context = host.GuestContactContext(ActorSlot, targetSlot, hit, physical: false);
                int stock = encounter.Working.GetMagicCount(kit.RecordRow, definition.Id);
                // The host transaction owns one selected cast and one contact per command.
                candidates.Add(new(definition, quote.Available, stock, 1, ReadSmartForecast(targetSlot, (ushort)definition.Id, hit, context)));
                revision.Append('|').Append(native).Append('|').Append(quote.Available).Append('|').Append(stock).Append('|').Append(context with { RandomKernel = default });
            }
        }
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(revision.ToString())));
        BattleSmartAttackPreview preview = new(ActorSlot.ToString(), targetSlot.ToString(), BattleAbility.Attack, 0, kit.Attack.Name ?? "Attack", 1, forcePhysical, hash, targetSlot, physical.ExpectedDamage, physical.KillProbability);
        return BattleSmartAttackSelector.Select(preview, candidates);
    }

    public bool SubmitSmartAttack(BattleSmartAttackPreview preview)
    {
        if (preview.ActorId != ActorSlot.ToString() || preview != PreviewSmartAttack(preview.TargetIndex, preview.ForcePhysical))
        {
            return false;
        }
        return preview.Ability == BattleAbility.Magic
            ? host.SubmitGuestMagic(ActorSlot, preview.TargetIndex, checked((ushort)preview.ActionId))
            : host.SubmitGuestAttack(ActorSlot, preview.TargetIndex);
    }

    private BattleDamageForecast ReadSmartForecast(int target, ushort ability, Ff7BattleGuestPhysicalHit hit, Ff7BattleGuestContactContext context)
    {
        var key = (target, ability);
        if (smartForecasts.TryGetValue(key, out var cached) && cached.Hit == hit && cached.Context == context)
        {
            return cached.Forecast;
        }
        BattleDamageForecast forecast = ForecastSmartContact(hit, context);
        smartForecasts[key] = (hit, context, forecast);
        return forecast;
    }

    internal static BattleDamageForecast ForecastSmartContact(Ff7BattleGuestPhysicalHit hit, Ff7BattleGuestContactContext context)
    {
        return BattleActionForecaster.FromSamples(Enumerable.Range(0, 256).Select(phase =>
        {
            Ff7BattleRng random = new(context.RandomKernel.Span, new(Enumerable.Repeat((byte)phase, 8).ToArray()));
            Ff7BattleActionReceipt receipt = Ff7NativeBattleActionResolver.ResolveGuestContact(context.Source, context.SourceVitals, context.Target, context.TargetVitals, context.Attack, hit.StatusRecord, random);
            return (receipt.Damage.Damage, 1d);
        }), checked((int)Math.Max(1, context.TargetVitals.Hp)));
    }
}
