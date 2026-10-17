namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public short? SequenceSine { get; init; }
    public short? SequenceCosine { get; init; }

    public short ReadSequenceGlobal5044B0(byte variable)
    {
        return variable switch
        {
            18 => SequenceSine ?? throw new NotSupportedException("Native sequence sine requires its retained process WORD; migrate the standalone snapshot explicitly."),
            19 => SequenceCosine ?? throw new NotSupportedException("Native sequence cosine requires its retained process WORD; migrate the standalone snapshot explicitly."),
            _ => throw new NotSupportedException($"Native sequence variable{variable:X2} requires its original graphics service owner.")
        };
    }

    public NativeBattleEffectMemory WriteSequenceGlobal5048E0(byte variable, int value)
    {
        return variable switch
        {
            18 => this with { SequenceSine = unchecked((short)NativeBattleCameraMath.Sine(value)) },
            19 => this with { SequenceCosine = unchecked((short)NativeBattleCameraMath.Cosine(value)) },
            _ => throw new NotSupportedException($"Native sequence write{variable:X2} requires its original graphics service owner.")
        };
    }
}
