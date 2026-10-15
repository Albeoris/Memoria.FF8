namespace Memoria.FF8.Runtime;

public sealed record NativeWorldMusicResource(int Version, int Section, byte MusicId, int Length, string Sha256)
{
    public void Validate()
    {
        if (Version != 1 || Section is not (42 or 44) || MusicId != (Section == 42 ? 41 : 24) || Length < 16 || Sha256.Length != 64 || Sha256.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidDataException("World music resource does not identify an original preloaded WMSET AKAO section.");
        }
    }
}
