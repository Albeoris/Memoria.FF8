namespace Memoria.FF8.Runtime;

public sealed record NativeWorldMusicFile(int Index, string RelativePath, int Length, string Sha256, int PayloadOffset, int PayloadEnd);

public sealed record NativeWorldMusicResource(int Version, int Section, byte MusicId, int Length, string Sha256, NativeWorldMusicFile? File = null)
{
    public static byte FileMusic(int index) => index switch { 0 => 41, 1 => 24, 2 => 81, 3 => 35, 4 => 59, 5 => 89, _ => throw new NotSupportedException("Native world music file index is outside543340's six selections.") };

    public void Validate()
    {
        bool resource = File is null ? Section is 42 or 44 && MusicId == (Section == 42 ? 41 : 24) : Section == -1 && File.Index is >= 0 and <= 5 && MusicId == FileMusic(File.Index) && File.RelativePath == $"dat/music{File.Index}.obj" && File.Length >= 8 && File.PayloadOffset >= 8 && File.PayloadEnd <= File.Length && File.PayloadEnd - File.PayloadOffset == Length && File.Sha256.Length == 64 && File.Sha256.All(Uri.IsHexDigit);
        if (Version != 1 || !resource || Length < 16 || Sha256.Length != 64 || Sha256.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidDataException("World music resource does not identify its original WMSET or completed native file AKAO payload.");
        }
    }
}
