using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Selected early-Garden inputs from native New Game/CLEAR and the original story WORD stores; this is not general byte-addressed event memory.</summary>
internal readonly record struct WorldEntryVariables(ushort StoryWord, byte ReplacementFlags, byte StoryByte)
{
    public static WorldEntryVariables Read(GameState state)
    {
        if (state.SavedWorld is null)
        {
            throw new NotSupportedException("Early Garden event-byte defaults require native New Game provenance.");
        }
        state.SavedWorld.Validate();
        IReadOnlyDictionary<int, int> variables = state.Variables;
        if (variables.Keys.Any(address => address is >= 253 and <= 255 or 257))
        {
            throw new NotSupportedException("The original story WORD has an unproved overlapping variable alias.");
        }
        int story = variables.GetValueOrDefault(256);
        if (story is < ushort.MinValue or > ushort.MaxValue)
        {
            throw new NotSupportedException("The original story WORD is outside its supported native width.");
        }
        if (story >= 34)
        {
            throw new NotSupportedException("World entry is outside the certified early Garden story range.");
        }
        // Native New Game and CLEAR establish zero bytes264/266. The selected
        // history stores B261=2. A small neighboring value cannot write a nonzero
        // upper byte at264/266 under any supported byte/WORD/DWORD interpretation.
        if (variables.GetValueOrDefault(264) != 0 || variables.GetValueOrDefault(266) != 0 || variables.Where(pair => pair.Key is >= 261 and <= 263 or 265).Any(pair => pair.Value is < 0 or > byte.MaxValue))
        {
            throw new NotSupportedException("Early Garden replacement/story bytes need additional original width evidence.");
        }
        return new WorldEntryVariables((ushort)story, 0, 0);
    }
}
