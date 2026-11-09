using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public enum SessionGame
{
    Ff8,
    Ff7
}

public sealed record CharacterAppearanceMapping(SessionGame StoryGame, int StoryCharacter, SessionGame AppearanceGame, int AppearanceCharacter, string RigProfile);

public sealed record Ff7SoundVoicePosition(int Channel, int SoundId, string Fingerprint, int SampleRate, int Channels, long PositionFrames, bool Loop, int LoopStartFrames, int LoopEndFrames, bool LoopStarted, int Pan, int Gain, int NativeVolume = 127, int NativeSampleRate = 0, int PrimaryFrequency = 0, int SecondaryFrequency = 0, double? LoopHandoffRemainingSeconds = null, long NativePanVersion = 0, int? SecondaryPan = null, long NativeFrequencyVersion = 0, long NativeGainVersion = 0);
public sealed record Ff7MusicVoicePosition(int Track, string Fingerprint, int SampleRate, int Channels, long PositionFrames, int LoopStartFrames, int LoopEndFrames, bool LoopStarted);

public sealed record Ff7AudioPresentationState
{
    public int Version { get; init; } = 1;
    public Ff7SoundVoicePosition[] Sounds { get; init; } = [];
    public Ff7MusicVoicePosition? Music { get; init; }

    public void Validate()
    {
        if (Version != 1)
        {
            return;
        }
        if (Sounds is null || Sounds.Length > 6 || Sounds.Any(sound => sound is null) || Sounds.Select(sound => sound.Channel).Distinct().Count() != Sounds.Length)
        {
            throw new InvalidDataException("FFVII saved audio presentation has invalid voice ownership.");
        }
        foreach (Ff7SoundVoicePosition sound in Sounds)
        {
            ValidatePosition(sound.Fingerprint, sound.SampleRate, sound.Channels, sound.PositionFrames, sound.LoopStartFrames, sound.LoopEndFrames, sound.LoopStarted, allowEnd: sound.Loop && !sound.LoopStarted && sound.LoopHandoffRemainingSeconds is not null);
            if (sound.Channel is < 0 or > 5 || sound.SoundId is < 1 or > 750 || sound.Pan is < -10000 or > 10000 || sound.Gain is < -10000 or > 0 || sound.NativeVolume is < 0 or > 127 || sound.NativeSampleRate < 0 || sound.NativePanVersion < 0 || sound.NativeFrequencyVersion < 0 || sound.NativeGainVersion < 0 || sound.SecondaryPan is < -10000 or > 10000 || sound.PrimaryFrequency != 0 && !Ff7NativeSoundFrequency.Accepted(sound.PrimaryFrequency) || sound.SecondaryFrequency != 0 && !Ff7NativeSoundFrequency.Accepted(sound.SecondaryFrequency) || sound.LoopHandoffRemainingSeconds is double remaining && (!double.IsFinite(remaining) || remaining < 0 || !sound.Loop || sound.LoopStarted) || sound.Channel == 0 && sound.Loop || !sound.Loop && (sound.LoopStarted || sound.LoopStartFrames != 0))
            {
                throw new InvalidDataException("FFVII saved sound voice contract is corrupt.");
            }
        }
        if (Music is Ff7MusicVoicePosition music)
        {
            ValidatePosition(music.Fingerprint, music.SampleRate, music.Channels, music.PositionFrames, music.LoopStartFrames, music.LoopEndFrames, music.LoopStarted);
            if (music.Track is < 1 or > 98)
            {
                throw new InvalidDataException("FFVII saved music presentation has an invalid track.");
            }
        }
    }

    private static void ValidatePosition(string fingerprint, int rate, int channels, long position, int loopStart, int loopEnd, bool repeating, bool allowEnd = false)
    {
        if (fingerprint is null || fingerprint.Length != 64 || !fingerprint.All(Uri.IsHexDigit) || rate <= 0 || channels is not (1 or 2) || position < 0 || (allowEnd ? position > loopEnd : position >= loopEnd) || loopStart < 0 || loopEnd <= loopStart || repeating && position < loopStart)
        {
            throw new InvalidDataException("FFVII saved PCM presentation cursor is corrupt.");
        }
    }
}

public sealed record Ff7TickContinuation(FieldInputState Input);

public sealed record Ff7SessionSave
{
    public const int CurrentVersion = 1;
    public int Version { get; init; } = CurrentVersion;
    public required string ResourceProfile { get; init; }
    public required string Language { get; init; }
    public required string FieldName { get; init; }
    public long FieldGeneration { get; init; }
    public required Ff7Progress Progress { get; init; }
    public required FieldVmSnapshot Vm { get; init; }
    public required Ff7FieldProcess Process { get; init; }
    public Ff7AudioPresentationState? AudioPresentation { get; init; }
    public Ff7TickContinuation? TickContinuation { get; init; }

    public void Validate()
    {
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(ResourceProfile) || string.IsNullOrWhiteSpace(Language) || string.IsNullOrWhiteSpace(FieldName) || FieldGeneration < 0 || Progress is null || Vm is null || Process is null)
        {
            throw new InvalidDataException("Unsupported or corrupt FFVII session save.");
        }
        Progress.Validate();
        Process.Validate();
        AudioPresentation?.Validate();
        if ((TickContinuation is null) != (Vm.CycleCursor is null) || TickContinuation is not null && (TickContinuation.Input is null || TickContinuation.Input != Vm.Input))
        {
            throw new InvalidDataException("A partial FFVII field tick requires matching scheduler and latched-input ownership.");
        }
    }
}

/// <summary>The inactive FFVIII payload retains its own codec and required execution contract.</summary>
public sealed record CombinedSessionSave
{
    public const string FormatIdentifier = "memoria-crossover-session";
    public string Format { get; init; } = FormatIdentifier;
    public int Version { get; init; } = 1;
    public SessionGame ActiveGame { get; init; }
    public byte[]? Ff8Payload { get; init; }
    public string? Ff8PayloadFormat { get; init; }
    public Ff7SessionSave? Ff7 { get; init; }
    public CharacterAppearanceMapping[] Mappings { get; init; } = [];

    public void Validate()
    {
        if (Format != FormatIdentifier || Version != 1 || !Enum.IsDefined(ActiveGame) || Mappings is null)
        {
            throw new InvalidDataException("Unsupported crossover save envelope.");
        }
        if (ActiveGame == SessionGame.Ff7 && Ff7 is null || ActiveGame == SessionGame.Ff8 && Ff8Payload is not { Length: > 0 } || Ff8Payload is not null && string.IsNullOrWhiteSpace(Ff8PayloadFormat))
        {
            throw new InvalidDataException("The active game or stored FFVIII codec is missing from the crossover save.");
        }
        if (Mappings.Select(mapping => (mapping.StoryGame, mapping.StoryCharacter)).Distinct().Count() != Mappings.Length || Mappings.Any(mapping => mapping.StoryGame != SessionGame.Ff7 || mapping.StoryCharacter != 0 || mapping.AppearanceGame != SessionGame.Ff8 || mapping.AppearanceCharacter != 0 || string.IsNullOrWhiteSpace(mapping.RigProfile)))
        {
            throw new NotSupportedException("Only the explicit Cloud to Squall appearance mapping is currently supported.");
        }
        Ff7?.Validate();
    }
}

public static class CombinedSessionCodec
{
    private static readonly byte[] Signature = "MEMORIA-CROSSOVER\0"u8.ToArray();
    private const int MaximumPayloadBytes = 64 * 1024 * 1024;
    private static readonly JsonSerializerOptions Options = new() { Converters = { new JsonStringEnumConverter() } };

    public static bool HasSignature(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanSeek)
        {
            throw new ArgumentException("Save signature detection requires a seekable stream.", nameof(source));
        }
        long original = source.Position;
        try
        {
            Span<byte> signature = stackalloc byte[Signature.Length];
            return source.ReadAtLeast(signature, signature.Length, false) == signature.Length && signature.SequenceEqual(Signature);
        }
        finally
        {
            source.Position = original;
        }
    }

    public static void Write(Stream destination, CombinedSessionSave save)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(save);
        save.Validate();
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(save, Options);
        if (payload.Length > MaximumPayloadBytes)
        {
            throw new InvalidDataException("The crossover save exceeds the supported payload limit.");
        }
        using BinaryWriter writer = new(destination, Encoding.UTF8, true);
        writer.Write(Signature);
        writer.Write(payload.Length);
        writer.Write(SHA256.HashData(payload));
        using BrotliStream compressed = new(destination, CompressionLevel.Optimal, true);
        compressed.Write(payload);
    }

    public static CombinedSessionSave Read(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        try
        {
            using BinaryReader reader = new(source, Encoding.UTF8, true);
            if (!reader.ReadBytes(Signature.Length).AsSpan().SequenceEqual(Signature))
            {
                throw new InvalidDataException("The file is not a crossover session save.");
            }
            int length = reader.ReadInt32();
            if (length is < 1 or > MaximumPayloadBytes)
            {
                throw new InvalidDataException("The crossover save payload length is invalid.");
            }
            byte[] hash = reader.ReadBytes(32);
            byte[] payload = new byte[length];
            using BrotliStream compressed = new(source, CompressionMode.Decompress, true);
            compressed.ReadExactly(payload);
            if (compressed.ReadByte() != -1 || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(payload), hash))
            {
                throw new InvalidDataException("The crossover save payload is corrupt.");
            }
            CombinedSessionSave save = JsonSerializer.Deserialize<CombinedSessionSave>(payload, Options) ?? throw new InvalidDataException("The crossover save is empty.");
            save.Validate();
            return save;
        }
        catch (Exception exception) when (exception is EndOfStreamException or JsonException or InvalidOperationException)
        {
            throw new InvalidDataException("The crossover save is truncated or malformed.", exception);
        }
    }

    public static void Save(string path, CombinedSessionSave save)
    {
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        string temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (FileStream destination = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                Write(destination, save);
                destination.Flush(true);
            }
            using (FileStream verification = File.OpenRead(temporary))
            {
                Read(verification);
            }
            File.Move(temporary, fullPath, true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
