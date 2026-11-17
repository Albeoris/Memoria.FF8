using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record BattleDiagnosticEffectInvocation(ushort RequestedEffect, ushort ExecutedEffect, byte ActorIndex, int HeaderIndex, byte? Parameter80, byte? Parameter81, byte? Parameter82);

public sealed record BattleDiagnosticHostedShotPackets(long Token, bool ContextOnly, int FirstContact, ImmutableArray<string> Recipients);
public sealed record BattleDiagnosticHostedDuelTargets(long Token, int FirstContact, ImmutableArray<string> Recipients);

public sealed partial class BattleSession
{

    public BattleDiagnosticHostedShotPackets ObserveDiagnosticHostedShotPackets(BattleDiagnosticEffectDispatch dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        if (Presentation is not { } process)
        {
            throw new InvalidOperationException("Shot observations require the attached presentation process.");
        }
        var (token, header, recipients) = process.ReadDiagnosticTimedInputPackets(dispatch);
        if (CurrentExpandedHostedInvocation is not { } invocation || dispatch.NativeTargetMask is not null ||
            invocation.Command.ActorId != dispatch.ActorId || invocation.Header.Command != header.Command || invocation.Header.Action != header.Action || invocation.Header.Effect != header.Effect ||
            header.Command is not (14 or 237 or 238) || invocation.Command.NativeLimitCallback is not (NativeLimitCallback.ShotOpening or NativeLimitCallback.ShotEffect or NativeLimitCallback.ShotEnding))
        {
            throw new InvalidOperationException("Shot observations require the current admitted Shot command and action header.");
        }
        return new(token, invocation.ContactDisposition == ExpandedHostedContactDisposition.ContextOnly, header.FirstContact.Ordinal, recipients.Select(DiagnosticActorId).ToImmutableArray());
    }

    public BattleDiagnosticHostedDuelTargets ObserveDiagnosticHostedDuelTargets(BattleDiagnosticEffectDispatch dispatch)
    {
        BattlePresentationProcess process = Presentation ?? throw new InvalidOperationException("Duel observations require the attached presentation process.");
        var (token, header, recipients) = process.ReadDiagnosticTimedInputPackets(dispatch);
        if (CurrentExpandedHostedInvocation is not { } invocation || invocation.Command.ActorId != dispatch.ActorId ||
            invocation.Header.Command != 239 || invocation.Header.Action != header.Action || invocation.Header.Effect != header.Effect ||
            header.Command != 239 || header.Action is < 6 or > 9 || invocation.Command.NativeLimitCallback != NativeLimitCallback.DuelFinisher ||
            recipients.Any(owner => owner is not BattleEnemyActorModel))
        {
            throw new InvalidOperationException("Duel observations require the current admitted finishing command and enemy recipients.");
        }
        return new(token, header.FirstContact.Ordinal, recipients.Select(DiagnosticActorId).ToImmutableArray());
    }

    public NativeBattleRetainedActionPackets ObserveDiagnosticDuelClosingPackets(BattleDiagnosticEffectDispatch dispatch)
    {
        if (dispatch.ActorId is not { } id || !byId.TryGetValue(id, out BattleCombatant? actor) ||
            actor.NativeLimit is not { CharacterId: 1, Command: 11 } rules ||
            nativeDuelProcesses.Count != 1 || !nativeDuelProcesses.TryGetValue(id, out BattleNativeDuelState? duel) ||
            duel is not { Phase: BattleNativeDuelPhase.Finisher, Selection1D28DEF: 254 })
        {
            throw new InvalidDataException("Duel closing packets require the actor's retained finisher state.");
        }
        bool finisher = dispatch.ActionId == 65530;
        ushort selector = finisher ? NativeBattleDuelRules.ActionId(rules, duel.MoveSetId) : (ushort)0;
        if (finisher ? selector is < 6 or > 9 || duel.RemainingTimer <= 0 : duel.RemainingTimer > 0)
        {
            throw new InvalidDataException("Duel closing packets disagree with the remaining time and selected move.");
        }
        BattlePresentationProcess process = Presentation ?? throw new InvalidOperationException("Duel observations require the attached presentation process.");
        return process.ObserveDiagnosticDuelClosingPackets(dispatch, selector);
    }

    private static string DiagnosticActorId(BattleModelOwner owner)
    {
        return owner switch
        {
            BattlePartyActorModel party => party.Actor.ActorId,
            BattleEnemyActorModel enemy => enemy.Actor.ActorId,
            _ => throw new InvalidDataException("Diagnostic observation requires a logical actor identity.")
        };
    }
}
