namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleLogicalOrdering
{
    internal void RequirePublishedProducerPrefix() => RequireOwner().RequirePublishedProducerPrefix();

    internal sealed class AutomaticScope
    {
        private readonly Owner storage;
        private readonly Checkpoint mark;
        private bool completed;

        private AutomaticScope(Owner storage, Checkpoint mark)
        {
            this.storage = storage;
            this.mark = mark;
        }

        internal static AutomaticScope Begin(NativeBattleLogicalOrdering ordering)
        {
            Owner storage = ordering.RequireOwner();
            return new(storage, storage.BeginAutomaticMark());
        }

        internal bool Matches(NativeBattleLogicalOrdering ordering) => ReferenceEquals(storage, ordering.owner);
        internal void Preflight() => storage.RequireAutomaticTop(mark);
        internal void ValidatePublication() => storage.ValidateAutomaticPublication(mark);
        internal void RememberPublication() => storage.RememberAutomaticPublication(mark);

        internal void Complete(bool rollback)
        {
            ObjectDisposedException.ThrowIf(completed, this);
            if (rollback)
            {
                storage.RollbackMark(mark);
            }
            else
            {
                storage.CommitMark(mark);
            }
            completed = true;
        }
    }

    private sealed partial class Owner
    {
        internal Checkpoint BeginAutomaticMark()
        {
            bool automaticAncestor = false;
            foreach (Checkpoint current in marks)
            {
                automaticAncestor |= current.Automatic;
            }
            if (HasMarks && !automaticAncestor)
            {
                throw new InvalidOperationException("Task ordering admission cannot enter an unknown native producer scope.");
            }
            Checkpoint result = BeginMark();
            result.Automatic = true;
            return result;
        }

        internal void RequireAutomaticTop(Checkpoint mark)
        {
            if (!mark.Automatic)
            {
                throw new InvalidOperationException("Task ordering scope lost its automatic bookmark.");
            }
            RequireTop(mark);
        }

        internal void ValidateAutomaticPublication(Checkpoint mark)
        {
            int index = marks.IndexOf(mark);
            if (index < 0 || !mark.Automatic)
            {
                throw new InvalidOperationException("Published ordering lost its automatic producer boundary.");
            }
            for (int current = index + 1; current < marks.Count; current++)
            {
                Checkpoint producer = marks[current];
                if (!producer.Automatic && !RepresentsCurrentPrefix(producer))
                {
                    throw new InvalidOperationException("Ordering publication requires every native producer to freeze its current prefix.");
                }
            }
        }

        internal void RememberAutomaticPublication(Checkpoint mark)
        {
            if (!marks.Contains(mark) || !mark.Automatic)
            {
                throw new InvalidOperationException("Published ordering lost its automatic producer boundary.");
            }
            Remember(mark);
        }

        internal void RequirePublishedProducerPrefix()
        {
            foreach (Checkpoint mark in marks)
            {
                if (!mark.Automatic && !RepresentsCurrentPrefix(mark))
                {
                    throw new InvalidOperationException("Ordering publication requires every native producer to freeze its current prefix.");
                }
            }
        }

        private bool RepresentsCurrentPrefix(Checkpoint mark)
        {
            return mark.Tables == tableUndo.Count && mark.ProxiesUndo == proxyUndo.Count && mark.PacketsUndo == packetUndo.Count && mark.Ranges == rangeUndo.Count && mark.Indices == indexUndo.Count && mark.Edits == edits.Count
                && ReferenceEquals(mark.Table, table) && ReferenceEquals(mark.Other, otherTable) && ReferenceEquals(mark.Proxies, proxies) && ReferenceEquals(mark.Packets, packets) && ReferenceEquals(mark.PacketIndices, packetIndices) && ReferenceEquals(mark.PacketRanges, packetRanges)
                && mark.ProxyCount == proxies.Count && mark.PacketCount == packets.Count && mark.Cursor == proxyCursor && mark.Buffer == tableBuffer && mark.Maximum == maximumPacketBytes && mark.Header == lastHeaderLink
                && mark.TableDefault == tableDefault && mark.OtherDefault == otherTableDefault && mark.OtherPresent == otherTablePresent && mark.ProxyDefault == proxyDefault && mark.PacketDefault == packetDefault && mark.Duplicate == duplicateIdentity;
        }
    }
}
