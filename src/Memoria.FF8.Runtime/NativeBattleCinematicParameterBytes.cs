namespace Memoria.FF8.Runtime;

/// <summary>The addressed bytes of the current original action parameter block.</summary>
public static class NativeBattleCinematicParameterBytes
{
    public static byte[] Read(NativeBattleEffectParameters parameters, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > 12 - length || parameters.Header is < 0 or >= NativeBattleActionPacketsState.HeaderCapacity)
        {
            throw new NotSupportedException("Native cinematic parameter read leaves its actual twelve-byte owner.");
        }
        uint header = checked(0x1D280C4u + (uint)parameters.Header * 20);
        return Enumerable.Range(offset, length).Select(index => index switch
        {
            0 => parameters.Source,
            1 => parameters.Flags79,
            2 => unchecked((byte)parameters.TargetActors),
            3 => unchecked((byte)(parameters.TargetActors >> 8)),
            >= 4 and <= 7 => unchecked((byte)(header >> ((index - 4) * 8))),
            8 => parameters.Parameter80 ?? throw new NotSupportedException("Native cinematic parameter80 has no observed byte owner."),
            9 => parameters.Parameter81,
            10 => parameters.Parameter82 ?? throw new NotSupportedException("Native cinematic parameter82 has no observed byte owner."),
            11 => parameters.Parameter83,
            _ => throw new InvalidOperationException()
        }).ToArray();
    }
}
