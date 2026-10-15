namespace Memoria.FF8.Runtime;

/// <summary>The original CAM record flags, independently bound to their installed source.</summary>
public sealed record NativeMovieCameraFlags(int Version, string SourceSha256, byte[] Flags)
{
    public void Validate()
    {
        if (Version != 1 || SourceSha256 is null || SourceSha256.Length != 64 || !SourceSha256.All(Uri.IsHexDigit) || Flags is null || Flags.Length is < 2 or > ushort.MaxValue)
        {
            throw new InvalidDataException("Native movie camera flags require their original source hash and bounded 44-byte record bank.");
        }
    }

    public byte Apply(int record, byte current)
    {
        Validate();
        if ((uint)record >= Flags.Length)
        {
            throw new NotSupportedException("Native movie frame leaves its original CAM flag records.");
        }
        byte flags = Flags[record];
        if ((flags & 8) != 0)
        {
            current = 0;
        }
        if ((flags & 16) != 0)
        {
            current = 1;
        }
        return current;
    }
}
