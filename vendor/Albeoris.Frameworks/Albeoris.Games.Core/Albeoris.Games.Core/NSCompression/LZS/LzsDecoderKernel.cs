namespace Albeoris.Games.Core.NSCompression.LZS;

/// <summary>Shared flag, token and zero-initialized 4096-byte dictionary state for LZS decoders.</summary>
public sealed class LzsDecoderKernel
{
    private readonly byte[] dictionary = new byte[4096];
    private readonly byte[] pending = new byte[18];
    private int cursor;
    private byte flags;
    private int flagCount;
    private int pendingOffset;
    private int pendingCount;

    public int RequiredInputBytes => flagCount == 0 || (flags & 1) != 0 ? 1 : 2;
    public bool FlagHasNoToken { get; private set; }

    public void AcceptInput(ReadOnlySpan<byte> input)
    {
        if (pendingOffset != pendingCount)
        {
            throw new InvalidOperationException("Consume pending LZS output before supplying another token.");
        }
        if (input.Length != RequiredInputBytes)
        {
            throw new ArgumentException("LZS input must contain one complete flag or token.", nameof(input));
        }
        pendingOffset = pendingCount = 0;
        if (flagCount == 0)
        {
            flags = input[0];
            flagCount = 8;
            FlagHasNoToken = true;
            return;
        }
        if ((flags & 1) != 0)
        {
            Append(input[0]);
        }
        else
        {
            int reference = (input[0] | ((input[1] & 0xF0) << 4)) + 18;
            int length = (input[1] & 15) + 3;
            for (int index = 0; index < length; index++)
            {
                Append(dictionary[(reference + index) & 4095]);
            }
        }
        flags >>= 1;
        flagCount--;
        FlagHasNoToken = false;
    }

    public bool TryReadOutput(out byte value)
    {
        if (pendingOffset == pendingCount)
        {
            value = 0;
            return false;
        }
        value = pending[pendingOffset++];
        return true;
    }

    private void Append(byte value)
    {
        dictionary[cursor] = value;
        cursor = (cursor + 1) & 4095;
        pending[pendingCount++] = value;
    }
}
