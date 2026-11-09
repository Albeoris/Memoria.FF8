namespace Memoria.FF7.Runtime;

public sealed record Ff7BattleRngState(byte[] Indices, int Stream = 0, uint WordCounter = 0, uint CrtSeed = 1)
{
    public void Validate()
    {
        if (Indices is not { Length: 8 } || Stream is < 0 or > 7)
        {
            throw new InvalidDataException("FFVII battle random continuation is invalid.");
        }
    }
}

public sealed class Ff7BattleRng
{
    private readonly byte[] table;
    private readonly byte[] indices;
    private int stream;
    private uint wordCounter;
    private readonly uint crtSeed;

    public Ff7BattleRng(ReadOnlySpan<byte> kernelSection, Ff7BattleRngState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.Validate();
        if (kernelSection.Length != 3988)
        {
            throw new InvalidDataException("FFVII battle randomness requires the original battle kernel section.");
        }
        table = kernelSection.Slice(0xE1C, 256).ToArray();
        indices = (byte[])state.Indices.Clone();
        stream = state.Stream;
        wordCounter = state.WordCounter;
        crtSeed = state.CrtSeed;
    }

    public static Ff7BattleRngState BeginBattle(uint crtSeed = 1)
    {
        uint next = unchecked(crtSeed * 214013 + 2531011);
        uint seed = next >> 16 & 32767;
        byte[] indices = new byte[8];
        for (int index = 0; index < indices.Length; index++)
        {
            indices[index] = unchecked((byte)(seed >> index));
        }
        return new(indices, CrtSeed: next);
    }

    public byte NextByte()
    {
        byte result = table[indices[stream]];
        indices[stream] = unchecked((byte)(indices[stream] + 1));
        return result;
    }

    public ushort NextWord()
    {
        byte low = NextByte();
        uint previous = wordCounter++;
        if ((previous & 7) != 0)
        {
            stream = (stream + 1) & 7;
        }
        return (ushort)(low | NextByte() << 8);
    }

    public Ff7BattleRngState Capture()
    {
        return new((byte[])indices.Clone(), stream, wordCounter, crtSeed);
    }
}
