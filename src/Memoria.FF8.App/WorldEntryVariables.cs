using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Original world loader WORD256 and BYTE264/266 from their executed selected event-byte owner.</summary>
internal readonly record struct WorldEntryVariables(ushort StoryWord, byte ReplacementFlags, byte StoryByte, byte EventFlags265)
{
    public static WorldEntryVariables Read(GameState state)
    {
        if (state.SavedWorld is null)
        {
            throw new NotSupportedException("World entry requires native saved-world provenance.");
        }
        state.SavedWorld.Validate();
        NativeWorldEntryMemory memory = state.WorldEntryMemory ?? throw new NotSupportedException("World entry requires its executed native NewGame/CLEAR and typed store byte history.");
        memory.ValidateVariables(state.Variables, state.NativeVariableValues);
        return new WorldEntryVariables(memory.StoryWord, memory.ReplacementFlags264, memory.StoryByte266, unchecked((byte)memory.ReadBytes(265, 1)));
    }
}
