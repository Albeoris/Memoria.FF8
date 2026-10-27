namespace Memoria.FF8.Runtime;

public static class MagicCastingService
{
    public static int Capacity(int stock, bool bound, int actorId)
    {
        if (stock is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(stock));
        }
        if (stock == 0)
        {
            return 0;
        }
        if (actorId is < 0 or > 10)
        {
            System.Diagnostics.Trace.TraceWarning("Magic capacity actor {0} has no declared profile; using conservative male limits.", actorId);
        }
        return !bound ? 1 : actorId is 3 or 4 or 5 or 7 ? 1 + (stock - 1) / 33 : 1 + stock / 50;
    }

    public static MagicCastQuote Quote(NativeCharacterVitals vitals, int row, int spellId, MagicCastContext context, string? actionId = null)
    {
        if (!MagicJunctionPolicy.CanAccess(vitals, row, spellId))
        {
            return new(0, 0, false, "This spell is reserved for another Junction owner or has no stock.");
        }
        MagicSystemState state = vitals.MagicSystem;
        int bank = state.SharedMagicStock ? -1 : row;
        int stock = vitals.GetMagicCount(row, spellId);
        bool bound = Enumerable.Range(0, 19).Any(slot => vitals.Records[row].Junction.MagicJunctions[slot] == spellId && vitals.JunctionMagicSlotAvailable(row, slot));
        int capacity = Capacity(stock, bound, vitals.Records[row].CharacterId);
        MagicCastReservation[] others = state.Reservations.Where(r => r.BankId == bank && r.SpellId == spellId && r.Context == context && r.ActionId != actionId).ToArray();
        int available;
        if (state.UnlimitedMagic)
        {
            available = int.MaxValue;
        }
        else if (state.InfiniteMagic)
        {
            int mask = GetMask(state, bank, spellId, context);
            MagicCastReservation? own = state.Reservations.FirstOrDefault(r => r.ActionId == actionId && r.BankId == bank && r.SpellId == spellId && r.Context == context);
            if (own is not null && ((own.TicketMask & mask) != 0 || (own.TicketMask & ~((1 << capacity) - 1)) != 0))
            {
                return new(0, capacity, false, "This reservation is no longer valid at the current cast capacity.");
            }
            foreach (MagicCastReservation reservation in others)
            {
                mask |= reservation.TicketMask;
            }
            available = Enumerable.Range(0, capacity).Count(ticket => (mask & (1 << ticket)) == 0);
        }
        else
        {
            available = Math.Max(0, stock - others.Sum(r => r.Count - r.CommittedCount));
        }
        return new(available, capacity, available > 0, available > 0 ? null : "No unreserved casts remain.");
    }

    public static NativeCharacterVitals Reserve(NativeCharacterVitals vitals, int row, int spellId, MagicCastContext context, string actionId, int count = 1)
    {
        if (string.IsNullOrWhiteSpace(actionId) || count < 1)
        {
            throw new ArgumentException("A cast reservation requires a stable action identity and positive count.");
        }
        MagicSystemState state = vitals.MagicSystem;
        if (state.CommittedActionIds.Contains(actionId, StringComparer.Ordinal))
        {
            return vitals;
        }
        MagicCastReservation? existing = state.Reservations.FirstOrDefault(r => r.ActionId == actionId);
        if (existing is not null)
        {
            if (existing.ActorRow != row || existing.SpellId != spellId || existing.Context != context || existing.Count != count)
            {
                throw new InvalidOperationException("A cast action identity cannot be reused for a different reservation.");
            }
            return vitals;
        }
        MagicCastQuote quote = Quote(vitals, row, spellId, context);
        if (quote.Available < count)
        {
            throw new InvalidOperationException("The requested magic casts are no longer affordable.");
        }
        int bank = state.SharedMagicStock ? -1 : row;
        int tickets = 0;
        if (state.InfiniteMagic && !state.UnlimitedMagic)
        {
            int unavailable = GetMask(state, bank, spellId, context);
            foreach (MagicCastReservation other in state.Reservations.Where(r => r.BankId == bank && r.SpellId == spellId && r.Context == context))
            {
                unavailable |= other.TicketMask;
            }
            for (int ticket = 0; ticket < quote.Capacity && System.Numerics.BitOperations.PopCount((uint)tickets) < count; ticket++)
            {
                if ((unavailable & (1 << ticket)) == 0)
                {
                    tickets |= 1 << ticket;
                }
            }
        }
        return vitals with { MagicSystem = state with { Reservations = [.. state.Reservations, new(actionId, bank, row, spellId, context, count, tickets)] } };
    }

    /// <summary>Commits the cumulative number of casts actually performed by an action. Repeated receipts are harmless.</summary>
    public static NativeCharacterVitals Commit(NativeCharacterVitals vitals, string actionId, int performedCount = 1)
    {
        MagicCastReservation? reservation = vitals.MagicSystem.Reservations.FirstOrDefault(r => r.ActionId == actionId);
        if (reservation is null)
        {
            if (vitals.MagicSystem.CommittedActionIds.Contains(actionId, StringComparer.Ordinal))
            {
                return vitals;
            }
            throw new InvalidOperationException("Magic execution has no matching reservation.");
        }
        if (performedCount < reservation.CommittedCount || performedCount > reservation.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(performedCount));
        }
        NativeCharacterVitals next = vitals.DeepCopy();
        MagicSystemState state = next.MagicSystem;
        int tickets = reservation.TicketMask;
        for (int cast = reservation.CommittedCount; cast < performedCount; cast++)
        {
            if (!state.InfiniteMagic && !state.UnlimitedMagic)
            {
                next = next.ChangeMagicStock(reservation.ActorRow, reservation.SpellId, -1, MagicOperationReason.OrdinaryCast, out MagicStockMutation debit);
                if (debit.Accepted != 1)
                {
                    throw new InvalidOperationException("Reserved magic stock is no longer present.");
                }
                state = next.MagicSystem;
            }
            else if (!state.UnlimitedMagic)
            {
                int ticket = LowestBit(tickets);
                if (ticket == 0)
                {
                    throw new InvalidDataException("Reserved magic tickets are missing.");
                }
                tickets &= ~ticket;
                SetMask(state, reservation.BankId, reservation.SpellId, reservation.Context, GetMask(state, reservation.BankId, reservation.SpellId, reservation.Context) | ticket);
            }
            if (reservation.Context == MagicCastContext.Field && state.HistoriesInitialized && !state.UnlimitedMagic)
            {
                if (!state.InfiniteMagic)
                {
                    SpendShadow(state, reservation.BankId, reservation.SpellId);
                }
                if (reservation.BankId == -1)
                {
                    for (int bank = 0; bank < 8; bank++)
                    {
                        SpendShadow(state, bank, reservation.SpellId);
                    }
                }
                else
                {
                    SpendShadow(state, -1, reservation.SpellId);
                }
            }
        }
        bool complete = performedCount == reservation.Count;
        MagicCastReservation updated = reservation with { CommittedCount = performedCount, TicketMask = tickets };
        state = state with
        {
            Reservations = complete ? state.Reservations.Where(r => r.ActionId != actionId).ToArray() : state.Reservations.Select(r => r.ActionId == actionId ? updated : r).ToArray(),
            CommittedActionIds = complete ? [.. state.CommittedActionIds, actionId] : state.CommittedActionIds
        };
        return next with { MagicSystem = state };
    }

    public static NativeCharacterVitals Release(NativeCharacterVitals vitals, string actionId)
    {
        MagicSystemState state = vitals.MagicSystem;
        bool performed = state.Reservations.Any(r => r.ActionId == actionId && r.CommittedCount > 0);
        return vitals with { MagicSystem = state with { Reservations = state.Reservations.Where(r => r.ActionId != actionId).ToArray(), CommittedActionIds = performed && !state.CommittedActionIds.Contains(actionId, StringComparer.Ordinal) ? [.. state.CommittedActionIds, actionId] : state.CommittedActionIds } };
    }

    public static NativeCharacterVitals BeginEncounter(NativeCharacterVitals vitals, string encounterId)
    {
        if (string.IsNullOrWhiteSpace(encounterId))
        {
            throw new ArgumentException("A genuine encounter requires a stable identity.", nameof(encounterId));
        }
        if (vitals.MagicSystem.EncounterId == encounterId)
        {
            return vitals;
        }
        return vitals with { MagicSystem = vitals.MagicSystem with { EncounterId = encounterId, PersonalBattleMasks = Enumerable.Range(0, 8).Select(_ => new byte[57]).ToArray(), SharedBattleMasks = new byte[57], Reservations = [] } };
    }

    public static NativeCharacterVitals RecoverVictory(NativeCharacterVitals vitals, string resultId)
    {
        if (string.IsNullOrWhiteSpace(resultId))
        {
            throw new ArgumentException("Victory recovery requires a stable result identity.", nameof(resultId));
        }
        if (vitals.MagicSystem.RecoveredResultIds.Contains(resultId, StringComparer.Ordinal))
        {
            return vitals;
        }
        MagicSystemState state = vitals.MagicSystem.DeepCopy();
        if (state.HistoriesInitialized)
        {
            foreach (byte[] masks in state.PersonalFieldMasks.Append(state.SharedFieldMasks))
            {
                for (int spell = 1; spell <= 56; spell++)
                {
                    masks[spell] = (byte)(masks[spell] & ~LowestBit(masks[spell]));
                }
            }
        }
        return vitals with { MagicSystem = state with { RecoveredResultIds = [.. state.RecoveredResultIds, resultId] } };
    }

    public static NativeCharacterVitals PropagatePersonalHistory(NativeCharacterVitals vitals, int sourceRow, int targetRow, int spellId)
    {
        if (!vitals.MagicSystem.HistoriesInitialized)
        {
            return vitals;
        }
        MagicSystemState state = vitals.MagicSystem.DeepCopy();
        state.PersonalFieldMasks[targetRow][spellId] |= state.PersonalFieldMasks[sourceRow][spellId];
        return vitals with { MagicSystem = state };
    }

    private static int LowestBit(int value) => value & -value;
    private static int GetMask(MagicSystemState state, int bank, int spell, MagicCastContext context) => context == MagicCastContext.Field ? bank == -1 ? state.SharedFieldMasks[spell] : state.PersonalFieldMasks[bank][spell] : bank == -1 ? state.SharedBattleMasks[spell] : state.PersonalBattleMasks[bank][spell];
    private static void SetMask(MagicSystemState state, int bank, int spell, MagicCastContext context, int value)
    {
        byte[] masks = context == MagicCastContext.Field ? bank == -1 ? state.SharedFieldMasks : state.PersonalFieldMasks[bank] : bank == -1 ? state.SharedBattleMasks : state.PersonalBattleMasks[bank];
        masks[spell] = (byte)value;
    }
    private static void SpendShadow(MagicSystemState state, int bank, int spell)
    {
        int mask = GetMask(state, bank, spell, MagicCastContext.Field);
        SetMask(state, bank, spell, MagicCastContext.Field, mask | LowestBit(~mask & 15));
    }
}
