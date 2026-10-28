namespace Memoria.FF8.Runtime;

/// <summary>Monotonic explicit data/unknown classification; implicit cleared holes are not classified stores.</summary>
internal sealed class NativeBattleArenaCoverage : INativeBattleCheckpointParticipant
{
    internal readonly List<(int Offset, int End)> Ranges = [];
    private readonly record struct Undo(int Index, int Kind, (int Offset, int End) Value);
    private readonly List<Undo> undo = [];
    private readonly List<int> marks = [];
    internal int Extent => Ranges.Count == 0 ? 0 : Ranges[^1].End;
    internal int Prefix => Ranges.Count != 0 && Ranges[0].Offset == 0 ? Ranges[0].End : 0;
    internal void Reserve(int additional, bool reserveJournal = true)
    {
        Ranges.EnsureCapacity(checked(Ranges.Count + additional));
        if (reserveJournal || marks.Count != 0)
        {
            undo.EnsureCapacity(checked(undo.Count + Ranges.Count + additional * 4));
            marks.EnsureCapacity(8);
        }
    }

    internal void ReserveCheckpointDepth(int depth) => marks.EnsureCapacity(checked(marks.Count + depth));

    public void BeginCheckpoint() => marks.Add(undo.Count);

    public void CommitCheckpoint()
    {
        marks.RemoveAt(marks.Count - 1);
        if (marks.Count == 0)
        {
            undo.Clear();
        }
    }

    public void RollbackCheckpoint()
    {
        int first = marks[^1];
        for (int index = undo.Count - 1; index >= first; index--)
        {
            Undo entry = undo[index];
            if (entry.Kind == 0)
            {
                Ranges[entry.Index] = entry.Value;
            }
            else if (entry.Kind == 1)
            {
                Ranges.RemoveAt(entry.Index);
            }
            else
            {
                Ranges.Insert(entry.Index, entry.Value);
            }
        }
        undo.RemoveRange(first, undo.Count - first);
        marks.RemoveAt(marks.Count - 1);
    }

    internal void Clear()
    {
        for (int index = Ranges.Count - 1; index >= 0; index--)
        {
            Delete(index);
        }
    }

    private void Set(int index, (int Offset, int End) value)
    {
        if (Ranges[index] == value)
        {
            return;
        }
        if (marks.Count != 0)
        {
            undo.Add(new(index, 0, Ranges[index]));
        }
        Ranges[index] = value;
    }

    private void Insert(int index, (int Offset, int End) value)
    {
        if (marks.Count != 0)
        {
            undo.Add(new(index, 1, default));
        }
        Ranges.Insert(index, value);
    }

    private void Delete(int index)
    {
        if (marks.Count != 0)
        {
            undo.Add(new(index, 2, Ranges[index]));
        }
        Ranges.RemoveAt(index);
    }

    internal void Remove(int offset, int length)
    {
        if (length == 0)
        {
            return;
        }
        int end = checked(offset + length);
        for (int index = 0; index < Ranges.Count; index++)
        {
            var range = Ranges[index];
            if (range.End <= offset || range.Offset >= end)
            {
                continue;
            }
            if (range.Offset < offset && range.End > end)
            {
                Set(index, (range.Offset, offset));
                Insert(index + 1, (end, range.End));
                break;
            }
            if (range.Offset < offset)
            {
                Set(index, (range.Offset, offset));
            }
            else if (range.End > end)
            {
                Set(index, (end, range.End));
                break;
            }
            else
            {
                Delete(index--);
            }
        }
    }

    internal void Add(int offset, int length)
    {
        if (length == 0)
        {
            return;
        }
        int end = offset + length;
        int first = 0;
        int last = Ranges.Count;
        while (first < last)
        {
            int middle = first + (last - first) / 2;
            if (Ranges[middle].End < offset)
            {
                first = middle + 1;
            }
            else
            {
                last = middle;
            }
        }
        int after = first;
        while (after < Ranges.Count && Ranges[after].Offset <= end)
        {
            offset = Math.Min(offset, Ranges[after].Offset);
            end = Math.Max(end, Ranges[after].End);
            after++;
        }
        if (after > first)
        {
            Set(first, (offset, end));
            for (int index = after - 1; index > first; index--)
            {
                Delete(index);
            }
        }
        else
        {
            Insert(first, (offset, end));
        }
    }
}
