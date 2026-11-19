using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleComponentSourceTests
{
    internal static BattleSession PrepareOriginalEffect141Component(GameWorld world, EffectPreviewRequest request)
    {
        ushort effect = request.EffectId ?? throw new InvalidDataException("The component requires its selected original timeline.");
        (int formation, int targetModel) = effect switch
        {
            141 or 160 => (71, 36),
            159 => (9, 92),
            161 => (738, 131),
            259 or 332 or 333 => (94, 61),
            334 => (326, 67),
            335 => (63, 77),
            336 => (354, 90),
            337 => (813, 117),
            339 => (796, 112),
            340 => (982, 126),
            341 => (712, 84),
            342 => (510, 124),
            343 => (509, 122),
            _ => throw new InvalidDataException("The selected original Renzokuken component is unsupported.")
        };
        DiagnosticCharacterPreparation squall = new(0, 0, [], [0]) { HitPoints = 1, Weapon = 0, MagicCommand = new(0, 20) };
        BattleSession source = world.StartDiagnosticBattle(new(formation, request.Seed, 0x80, 0, 0)
        {
            BattleMusic = 5,
            PrimaryMusicChannel = -1,
            AtbMode = BattleAtbMode.Normal,
            Fixture = new([new(0, true)], [squall], true) { CombatParty = [0, 255, 255] }
        });
        BattleCombatant actor = source.Combatants.Single(candidate => candidate.IsPlayer && candidate.NativeActorIndex == 0);
        bool requiresNaturalPrelude = effect is 335 or 337;
        BattleCombatant? target = requiresNaturalPrelude ? null : source.Combatants.First(candidate => !candidate.IsPlayer && candidate.IsActive && candidate.ModelId == targetModel);
        if (actor.ModelId != 0 || target is not null && (target.NativeRenzokukenMagicIds.Length != 7 || !target.NativeRenzokukenMagicIds.Contains(effect)))
        {
            throw new InvalidDataException("The component requires its original Renzokuken caster and target selectors.");
        }
        for (int callback = 0; callback < 3600 && (!source.NativeEntryReady || source.ObserveDiagnosticDrainBlockers().Count != 0 || !source.CanChoosePlayerCommand(actor.Id)); callback++)
        {
            source.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
        }
        if (!source.NativeEntryReady || source.ObserveDiagnosticDrainBlockers().Count != 0 || !source.CanChoosePlayerCommand(actor.Id))
        {
            throw new InvalidDataException("The original component caster did not reach ordinary command readiness.");
        }
        if (requiresNaturalPrelude)
        {
            AdvanceOriginalRenz335337Prelude(world, source, effect);
            target = source.Combatants.First(candidate => !candidate.IsPlayer && candidate.IsActive && candidate.ModelId == targetModel);
            if (target.NativeRenzokukenMagicIds.Length != 7 || !target.NativeRenzokukenMagicIds.Contains(effect))
            {
                throw new InvalidDataException("The component requires its original Renzokuken caster and target selectors.");
            }
        }
        if (target is null)
        {
            throw new InvalidDataException("The original Renzokuken target is absent.");
        }
        if (!target.IsActive || !target.IsAlive || target.Untargetable || target.Suspended)
        {
            throw new InvalidDataException("The original Renzokuken target did not reach its living targetable native lifecycle boundary.");
        }
        byte desiredCrisis = Enumerable.Range(1, 4).Where(crisis => target.NativeRenzokukenMagicIds[crisis + 1] == effect || target.NativeRenzokukenMagicIds[crisis + 2] == effect).Select(crisis => checked((byte)crisis)).FirstOrDefault();
        if (desiredCrisis == 0)
        {
            throw new InvalidDataException("The requested timeline lacks a selectable original crisis entry.");
        }
        bool selected = false;
        for (int attempt = 0; attempt < 256 && !selected; attempt++)
        {
            BattleDiagnosticLimitSelectionReceipt receipt = source.SelectDiagnosticLimit(actor.Id, new(desiredCrisis, desiredCrisis, 256));
            if (!receipt.Accepted)
            {
                break;
            }
            BattleSnapshot forecast = source.CaptureForContinuation();
            NativeBattleRandomState next = forecast.Random!.Draw(out byte variantRoll);
            next.Draw(out byte finisherRoll);
            byte crisis = source.GetNativeLimitLevel(actor.Id);
            selected = target.NativeRenzokukenMagicIds[crisis + variantRoll % 2 + 1] == effect && !NativeBattleRenzokukenRules.HasFinisher(crisis, finisherRoll);
        }
        if (!selected)
        {
            throw new InvalidDataException("The bounded ordinary Limit selections did not select the requested original timeline without a finisher.");
        }
        world.ApplyBattleInput(new(BattleInputKind.Action, ActorId: actor.Id, TargetId: target.Id, Ability: BattleAbility.LimitBreak, ActionId: 5 << 16));
        for (int callback = 0; callback < 3600; callback++)
        {
            BattlePresentationProcess process = source.Presentation!;
            if (process.ActiveActionToken is not null && process.ActionPackets.PublishedHeaders().Any(entry => entry.Slot.Effect == effect))
            {
                _ = GameWorld.ValidateEffectComponentSource(source, effect);
                if (source.GetNativeRenzokuken(actor.Id)?.MagicId != effect || source.CaptureForContinuation().LogicalContinuation?.Event?.Renzokuken is null)
                {
                    throw new InvalidDataException("The component lost its actual logical Renzokuken invocation owner.");
                }
                return source;
            }
            try
            {
                source.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
            }
            catch (InvalidDataException error) when (error.Message == "Saved expanded Renzokuken lacks its logical command5 transaction.")
            {
                BattleAttackCommand? queued = source.QueuedActions.FirstOrDefault(command => command.ActorId == actor.Id);
                throw new InvalidDataException($"Ordinary Renzokuken publication failed: effect={effect}, atb={actor.Atb}, nativeCurrent={actor.NativeAtb?.Current}, nativeMaximum={actor.NativeAtb?.Maximum}, busy={actor.IsBusy}, queuedCommand={queued?.NativeQueuedCommand}, ability={queued?.Ability}, action={queued?.ActionId}, hasNativeAction={queued?.NativeAction is not null}, crisis={queued?.NativeRenzokuken?.CrisisLevel}, selection={queued?.TargetRequest.Selection.GetType().Name}, callback={source.NativeHostCallbacks}.", error);
            }
        }
        throw new InvalidDataException("The ordinary Limit input did not publish its unstarted requested component.");
    }
}
