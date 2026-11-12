using System.Buffers.Binary;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7BattleSession
{
    private void RestoreNativeAiState(byte[]? initialSaveBank1, uint initialGil, bool fresh = false)
    {
        if (state.AiGlobalMemory is { Length: not 64 } || state.AiSaveBank1 is { Length: not 256 } || initialSaveBank1 is { Length: not 256 } || state.FormationAiMemory is { Length: not Ff7BattleAi.LocalMemorySize } || state.AiSaveBankDirty?.Distinct().Count() != state.AiSaveBankDirty?.Length)
        {
            throw new InvalidDataException("Native battle AI memory has an invalid shape.");
        }
        if (state.ImprisonedType > 3 || state.CounterProcessedMask is ushort processed && (processed & ~0x3FF) != 0 || state.Actors.Any(actor => actor.SuccessorSlot is int successor && (successor is < 0 or 3 or > 9 || !state.Actors.Any(candidate => candidate.Actor.Slot == successor))))
        {
            throw new InvalidDataException("Native battle continuation has an invalid actor or status owner.");
        }
        byte[] globals = state.AiGlobalMemory?.ToArray() ?? new byte[64];
        if (state.AiGlobalMemory is null)
        {
            globals[4] = (byte)Ff7NativeAtb.BattleTypeForLayout(state.Layout);
            BinaryPrimitives.WriteUInt16LittleEndian(globals.AsSpan(0x28), checked((ushort)Formation.FormationId));
            BinaryPrimitives.WriteUInt16LittleEndian(globals.AsSpan(0x2C), Formation.BattleFlags);
            BinaryPrimitives.WriteUInt32LittleEndian(globals.AsSpan(0x38), initialGil);
        }
        state = state with { AiGlobalMemory = globals, AiSaveBank1 = state.AiSaveBank1?.ToArray() ?? initialSaveBank1?.ToArray() ?? new byte[256], AiSaveBankDirty = state.AiSaveBankDirty?.ToArray() ?? [], FormationAiMemory = state.FormationAiMemory?.ToArray() ?? new byte[Ff7BattleAi.LocalMemorySize] };
        foreach (Ff7BattleActorState runtime in state.Actors.ToArray())
        {
            Ff7BattleAiActor actor = runtime.Actor;
            Ff7BattleDamageActor stats = actor.Slot < 3 ? party[actor.Slot]!.Value.Actor : Ff7NativeBattleDamage.FromEnemy(Enemy(actor.Slot));
            SceneBattleEnemy? enemy = actor.Slot >= 4 ? Enemy(actor.Slot) : null;
            uint ElementMask(byte rate) => enemy is null ? 0 : enemy.Affinities.Where(affinity => affinity.Type < 16 && affinity.Rate == rate).Aggregate(0U, (mask, affinity) => mask | 1U << affinity.Type);
            Replace(runtime with
            {
                Actor = actor with
                {
                    Level = actor.Level ?? stats.Level,
                    EnemyId = actor.EnemyId ?? (enemy is null ? null : unchecked((ushort)enemy.Placement.EnemyId)),
                    Strength = actor.Strength ?? stats.Attack,
                    Magic = actor.Magic ?? stats.Magic,
                    Speed = actor.Speed ?? stats.Dexterity,
                    FormationRow = actor.FormationRow ?? (enemy is null ? (byte)0 : unchecked((byte)enemy.Placement.Row)),
                    Variant = enemy is null || actor.Level is not null ? actor.Variant : checked((byte)Data.Enemies.Count(previous => previous.FormationSlot < enemy.FormationSlot && previous.Placement.EnemyId == enemy.Placement.EnemyId)),
                    ElementHalve = actor.ElementHalve ?? ElementMask(4),
                    ElementNullify = actor.ElementNullify ?? ElementMask(5),
                    ElementAbsorb = actor.ElementAbsorb ?? ElementMask(6),
                    ElementWeakness = actor.ElementWeakness ?? ElementMask(2),
                    ExtraElementAbsorb = actor.ExtraElementAbsorb ?? 0,
                    PreviousStatuses = actor.PreviousStatuses ?? (fresh ? 0U : actor.Statuses),
                    CaptureMask = actor.CaptureMask ?? 0,
                    PresentationByte = actor.PresentationByte ?? byte.MaxValue,
                    LastPhysicalAttackerMask = actor.LastPhysicalAttackerMask ?? 0,
                    LastMagicAttackerMask = actor.LastMagicAttackerMask ?? 0,
                    PhysicalEvasion = actor.PhysicalEvasion ?? stats.Evade,
                    HitEffect = actor.HitEffect ?? (enemy is null ? (byte)8 : (byte)2)
                }
            });
        }
    }

    private void FlushFormationAi()
    {
        byte pending = state.FormationAiPending;
        state = state with { FormationAiPending = 0 };
        if (Data.FormationScripts is null)
        {
            return;
        }
        for (byte callback = 0; callback < 8; callback++)
        {
            if ((pending & 1 << callback) == 0)
            {
                continue;
            }
            Ff7BattleAiActor[] actors = state.Actors.Select(actor => actor.Actor).ToArray();
            Ff7BattleAiMasks masks = Ff7BattleAiCallbacks.ProjectMasks(actors, NativeImprisonedType);
            Ff7BattleAiResult result = Ff7BattleAi.Run(Data.FormationScripts[callback].Span, new(3, actors, masks.Targetable, masks.KnockedOut, masks.ExcludedOpponents, callback, QueuePriority: 0, GlobalMemory: state.AiGlobalMemory, SaveBank1: state.AiSaveBank1), state.FormationAiMemory ?? [], Data, random, kernel[1].Data);
            state = state with { FormationAiMemory = result.LocalMemory, AiGlobalMemory = result.GlobalMemory.ToArray() };
            ApplyNativeSaveWrites(result);
            ApplyNativeAiTransfers(result.TransferUpdates);
            foreach (Ff7BattleAiActor updated in result.ActorUpdates)
            {
                Ff7BattleActorState previous = Actor(updated.Slot);
                Replace(previous with { Actor = updated, Timers = Ff7NativeBattleStatusClock.Apply(previous.Timers, previous.Actor.Statuses, updated.Statuses) });
            }
            foreach (Ff7BattleAiCommand command in result.Commands)
            {
                Ff7BattleQueuedCommand queued = Ff7BattleQueuedCommand.FromAi(command);
                state = state with { Queue = Ff7BattleCommandQueue.Enqueue(state.Queue, queued) };
                ApplyQueueConditions(queued);
            }
        }
    }

    private void ApplyNativeAiTransfers(IReadOnlyList<Ff7BattleAiTransfer> transfers)
    {
        foreach (Ff7BattleAiTransfer transfer in transfers)
        {
            Ff7BattleActorState source = Actor(transfer.SourceSlot);
            Ff7BattleActorState destination = Actor(transfer.DestinationSlot);
            Replace(destination with { Timers = source.Timers });
            Replace(Actor(source.Actor.Slot) with { SuccessorSlot = destination.Actor.Slot, StatusProtection = source.StatusProtection | Ff7NativeBattleActionResolver.Death });
        }
    }

    private void PublishNativeActionContext(Ff7BattleQueuedCommand command, ushort elements = 0)
    {
        byte[] globals = state.AiGlobalMemory?.ToArray() ?? new byte[64];
        Ff7GuestScriptInteraction? interaction = command.GuestAttack ? (GetGuestAttack(command.ActorSlot) as IFf7BattleGuestScriptActions)?.DescribeScriptInteraction(command.CommandId, command.GuestAbility, System.Numerics.BitOperations.TrailingZeroCount(command.TargetMask)) : null;
        globals[0] = interaction?.CommandId ?? command.CommandId;
        globals[1] = unchecked((byte)(interaction?.ActionId ?? (command.GuestAttack ? ushort.MaxValue : command.AttackId ?? command.Argument)));
        BinaryPrimitives.WriteUInt16LittleEndian(globals.AsSpan(0x24), elements == ushort.MaxValue ? (ushort)0 : elements);
        BinaryPrimitives.WriteUInt16LittleEndian(globals.AsSpan(0x2A), interaction?.ActionId ?? (command.GuestAttack ? ushort.MaxValue : command.AttackId ?? command.Argument));
        state = state with { AiGlobalMemory = globals };
    }

    private void PublishGuestSourceInteraction(Ff7BattleQueuedCommand command, int recipient)
    {
        Ff7GuestScriptInteraction? interaction = (GetGuestAttack(command.ActorSlot) as IFf7BattleGuestScriptActions)?.DescribeScriptInteraction(command.CommandId, command.GuestAbility, recipient);
        if (interaction?.DrawSourceSlot is not int source || source == recipient && interaction.HasRecipientEffect || !IsAlive(Actor(source)))
        {
            return;
        }
        byte[] globals = state.AiGlobalMemory?.ToArray() ?? new byte[64];
        globals[0] = byte.MaxValue;
        globals[1] = byte.MaxValue;
        BinaryPrimitives.WriteUInt16LittleEndian(globals.AsSpan(0x2A), ushort.MaxValue);
        state = state with { AiGlobalMemory = globals };
        PublishEnemyCounters(command, Actor(source).Actor, false, false, typed: false);
    }

    private Ff7BattleDamageAffinity ResolveNativeEnemyAffinity(int slot, ushort elements)
    {
        if (elements == ushort.MaxValue)
        {
            return Ff7BattleDamageAffinity.Normal;
        }
        foreach (SceneEnemyAffinity affinity in Enemy(slot).Affinities)
        {
            if (affinity.Type < 16 && (elements & 1 << affinity.Type) != 0 && affinity.Rate is not (2 or 4 or 5 or 6))
            {
                throw new NotSupportedException($"FFVII enemy {Enemy(slot).Placement.EnemyId} has unsupported elemental affinity rate {affinity.Rate}.");
            }
        }
        Ff7BattleAiActor actor = Actor(slot).Actor;
        Ff7BattleDamageAffinity result = Ff7BattleDamageAffinity.Normal;
        if (((actor.ElementHalve ?? 0) & elements) != 0)
        {
            result |= Ff7BattleDamageAffinity.Half;
        }
        if (((actor.ElementNullify ?? 0) & elements) != 0)
        {
            result |= Ff7BattleDamageAffinity.Nullify;
        }
        if ((((actor.ElementAbsorb ?? 0) | (actor.ExtraElementAbsorb ?? 0)) & elements) != 0)
        {
            result |= Ff7BattleDamageAffinity.Absorb;
        }
        if (((actor.ElementWeakness ?? 0) & elements) != 0)
        {
            result |= Ff7BattleDamageAffinity.Double;
        }
        return result;
    }

    private ushort RedirectNativeSuccessorTargets(ushort queued, ushort eligible)
    {
        ushort targets = queued;
        foreach (Ff7BattleActorState actor in state.Actors)
        {
            ushort bit = (ushort)(1 << actor.Actor.Slot);
            if ((queued & bit) == 0 || (eligible & bit) != 0 || actor.SuccessorSlot is not int successor || (eligible & 1 << successor) == 0)
            {
                continue;
            }
            targets = (ushort)((targets & ~bit) | 1 << successor);
        }
        return targets;
    }
}
