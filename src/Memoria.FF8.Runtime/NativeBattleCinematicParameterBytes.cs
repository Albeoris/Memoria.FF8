namespace Memoria.FF8.Runtime;

/// <summary>The addressed bytes of the current original action parameter block.</summary>
public static class NativeBattleCinematicParameterBytes
{
    public static byte[] Read(NativeBattleEffectParameters parameters, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > 12 - length)
        {
            throw new NotSupportedException("Native cinematic parameter read leaves its actual twelve-byte owner.");
        }
        return Enumerable.Range(offset, length).Select(index => index switch
        {
            < NativeBattleEffectParameterWindow.Length => (parameters.OriginalWindow ?? throw new NotSupportedException("Native cinematic parameters have no publishing writer's original projection.")).ReadByte(index),
            8 => parameters.Parameter80 ?? throw new NotSupportedException("Native cinematic parameter80 has no observed byte owner."),
            9 => parameters.Parameter81,
            10 => parameters.Parameter82 ?? throw new NotSupportedException("Native cinematic parameter82 has no observed byte owner."),
            11 => parameters.Parameter83,
            _ => throw new InvalidOperationException()
        }).ToArray();
    }
}
