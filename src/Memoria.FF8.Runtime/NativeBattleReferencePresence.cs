namespace Memoria.FF8.Runtime;

/// <summary>One conservative bit per 64-byte descriptor start block; removals leave false positives.</summary>
internal sealed class NativeBattleReferencePresence
{
    private ulong[] words = [];

    internal void Add(int offset, int bound)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        int word = offset >> 12;
        if (word >= words.Length)
        {
            int maximumWords = (int)(((long)bound + 4095) >> 12);
            Array.Resize(ref words, Math.Min(maximumWords, Math.Max(word + 1, Math.Max(1, words.Length * 2))));
        }
        words[word] |= 1UL << ((offset >> 6) & 63);
    }

    internal bool MayContain(int first, long end)
    {
        if (end <= first || words.Length == 0)
        {
            return false;
        }
        int firstWord = first >> 12;
        if (firstWord >= words.Length)
        {
            return false;
        }
        end = Math.Min(end, (long)words.Length << 12);
        int lastWord = (int)Math.Min((end - 1) >> 12, words.Length - 1);
        ulong firstMask = ulong.MaxValue << ((first >> 6) & 63);
        ulong lastMask = ulong.MaxValue >> (63 - (int)(((end - 1) >> 6) & 63));
        if (firstWord == lastWord)
        {
            return (words[firstWord] & firstMask & lastMask) != 0;
        }
        if ((words[firstWord] & firstMask) != 0 || (words[lastWord] & lastMask) != 0)
        {
            return true;
        }
        for (int word = firstWord + 1; word < lastWord; word++)
        {
            if (words[word] != 0)
            {
                return true;
            }
        }
        return false;
    }

    internal void Clear()
    {
        Array.Clear(words);
    }
}
