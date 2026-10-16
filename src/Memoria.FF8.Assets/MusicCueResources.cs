namespace Memoria.FF8.Assets;

public enum MusicCue
{
    EyesOnMe
}

public sealed record MusicCueResource(MusicCue Cue, string RelativePath)
{
    public void Validate()
    {
        if (!Enum.IsDefined(Cue) || string.IsNullOrWhiteSpace(RelativePath) || Path.IsPathRooted(RelativePath) || RelativePath.Split('/', '\\').Any(part => part is ".." or "." or ""))
        {
            throw new InvalidDataException("Music cues require a logical identity and a relative resource path within the installed game.");
        }
    }
}

public interface IMusicCueResolver
{
    MusicCueResource Resolve(MusicCue cue);
}

public sealed class ClassicMusicCueResolver : IMusicCueResolver
{
    public MusicCueResource Resolve(MusicCue cue)
    {
        return cue switch
        {
            MusicCue.EyesOnMe => new(cue, "Data/Music/eyes_on_me.wav"),
            _ => throw new NotSupportedException($"Classic music cue {cue} has no resource mapping.")
        };
    }
}
