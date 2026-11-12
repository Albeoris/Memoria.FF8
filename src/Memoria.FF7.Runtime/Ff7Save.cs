using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF7.Events;
using Memoria.AutoPlay;

namespace Memoria.FF7.Runtime;

public enum SessionGame
{
    Ff8,
    Ff7
}

public sealed record CharacterAppearanceMapping(SessionGame StoryGame, int StoryCharacter, SessionGame AppearanceGame, int AppearanceCharacter, string RigProfile);
public sealed record Ff7MixedCombatBinding(int StoryCharacter, int OriginCharacter = 0, int RulesVersion = 1);

public sealed record Ff7SoundVoicePosition(int Channel, int SoundId, string Fingerprint, int SampleRate, int Channels, long PositionFrames, bool Loop, int LoopStartFrames, int LoopEndFrames, bool LoopStarted, int Pan, int Gain, int NativeVolume = 127, int NativeSampleRate = 0, int PrimaryFrequency = 0, int SecondaryFrequency = 0, double? LoopHandoffRemainingSeconds = null, long NativePanVersion = 0, int? SecondaryPan = null, long NativeFrequencyVersion = 0, long NativeGainVersion = 0)
{
    public double FractionalFrame { get; init; }
}
public sealed record Ff7MusicVoicePosition(int Track, string Fingerprint, int SampleRate, int Channels, long PositionFrames, int LoopStartFrames, int LoopEndFrames, bool LoopStarted);

public sealed record Ff7AudioPresentationState
{
    public int Version { get; init; } = 1;
    public Ff7SoundVoicePosition[] Sounds { get; init; } = [];
    public Ff7MusicVoicePosition? Music { get; init; }
    public byte[]? GuestSoundPayload { get; init; }

    public void Validate()
    {
        if (Version != 1)
        {
            return;
        }
        if (GuestSoundPayload is { Length: > 262144 })
        {
            throw new InvalidDataException("Guest sound presentation exceeds its saved metadata limit.");
        }
        if (Sounds is null || Sounds.Length > 6 || Sounds.Any(sound => sound is null) || Sounds.Select(sound => sound.Channel).Distinct().Count() != Sounds.Length)
        {
            throw new InvalidDataException("FFVII saved audio presentation has invalid voice ownership.");
        }
        foreach (Ff7SoundVoicePosition sound in Sounds)
        {
            ValidatePosition(sound.Fingerprint, sound.SampleRate, sound.Channels, sound.PositionFrames, sound.LoopStartFrames, sound.LoopEndFrames, sound.LoopStarted, allowEnd: sound.Loop && !sound.LoopStarted && sound.LoopHandoffRemainingSeconds is not null);
            if (!double.IsFinite(sound.FractionalFrame) || sound.FractionalFrame is < 0 or >= 1 || sound.Channel is < 0 or > 5 || sound.SoundId is < 1 or > 750 || sound.Pan is < -10000 or > 10000 || sound.Gain is < -10000 or > 0 || sound.NativeVolume is < 0 or > 127 || sound.NativeSampleRate < 0 || sound.NativePanVersion < 0 || sound.NativeFrequencyVersion < 0 || sound.NativeGainVersion < 0 || sound.SecondaryPan is < -10000 or > 10000 || sound.PrimaryFrequency != 0 && !Ff7NativeSoundFrequency.Accepted(sound.PrimaryFrequency) || sound.SecondaryFrequency != 0 && !Ff7NativeSoundFrequency.Accepted(sound.SecondaryFrequency) || sound.LoopHandoffRemainingSeconds is double remaining && (!double.IsFinite(remaining) || remaining < 0 || !sound.Loop || sound.LoopStarted) || sound.Channel == 0 && sound.Loop || !sound.Loop && (sound.LoopStarted || sound.LoopStartFrames != 0))
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

public sealed record Ff7TickContinuation(FieldInputState Input, FieldScriptReturn[]? ScriptReturns = null);

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
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte[]? NativeSaveData { get; init; }
    public Ff7AudioPresentationState? AudioPresentation { get; init; }
    public short BattleBigShotChargeRotation { get; init; }
    public short BattleBigShotCounterRotation { get; init; }
    public short BattleSearchScopeScaleCounter { get; init; }
    public short BattleSilkDirection { get; init; } = 1;
    public byte BattleCameraPresentationCounter { get; init; }
    public Ff7TickContinuation? TickContinuation { get; init; }
    public Ff7WorldProcess? World { get; init; }
    public AutoPlayEventCoverageState? EventCoverage { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Ff7BattleSnapshot? Battle { get; init; }

    public void Validate()
    {
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(ResourceProfile) || string.IsNullOrWhiteSpace(Language) || string.IsNullOrWhiteSpace(FieldName) || FieldGeneration < 0 || Progress is null || Vm is null || Process is null)
        {
            throw new InvalidDataException("Unsupported or corrupt FFVII session save.");
        }
        Progress.Validate();
        if (NativeSaveData is not null && NativeSaveData.Length != Ff7NativeSaveSlot.Size)
        {
            throw new InvalidDataException("The preserved native FFVII save slot has an invalid size.");
        }
        Process.Validate();
        if (Battle is not null && (World is not null || Process.GlobalCommand != Ff7GlobalCommand.Battle || Process.GlobalRequestId != Battle.RequestId || Progress.CompletedBattles.Any(receipt => receipt.FieldGeneration == FieldGeneration && receipt.RequestId == Battle.RequestId) || !Process.Receipts.Any(receipt => receipt.Request.Id == Battle.RequestId && receipt.Request.Command is FieldBattleCommand command && command.Formation == Battle.FormationId && !receipt.Reply.Completed)))
        {
            throw new InvalidDataException("The FFVII battle snapshot requires its matching unfinished field request.");
        }
        if (Battle is { } battle)
        {
            Ff7BattleSession.ValidateGuestVitalsSnapshot(battle);
        }
        AudioPresentation?.Validate();
        if (EventCoverage is not null)
        {
            AutoPlayEventCoverageLedger.Validate(EventCoverage);
        }
        if (World is not null && (TickContinuation is not null || World.WorldId is not (0 or 2 or 3) || World.EntrySelector is < 0 or > 65535 || World.Vm is null))
        {
            throw new InvalidDataException("FFVII world continuation conflicts with its field scheduler or resource identity.");
        }
        if ((TickContinuation is null) != (Vm.CycleCursor is null) || TickContinuation is not null && (TickContinuation.Input is null || TickContinuation.Input != Vm.Input))
        {
            throw new InvalidDataException("A partial FFVII field tick requires matching scheduler and latched-input ownership.");
        }
        if (TickContinuation?.ScriptReturns is { } returns && (returns.Length > 4096 || returns.Any(value => value is null || value.Entity < 0 || value.Entity >= Vm.Entities.Length || value.Priority is < 0 or >= 7 || value.ProgramCounter is < 0 or > 65535 || value.Tick != Vm.Tick)))
        {
            throw new InvalidDataException("A partial FFVII field tick has corrupt native script-return observations.");
        }
    }
}

public sealed record Ff7GuestBattleSettlementCharacter(int StoryCharacter, int OriginRow, ushort SettledHp, string DormantCharacterHash)
{
    internal bool IsValid => StoryCharacter is >= 0 and <= 8 && OriginRow is >= 0 and <= 7 && SettledHp <= 9999 && IsHash(DormantCharacterHash);

    internal static bool IsHash(string hash) => hash is { Length: 64 } && hash.All(Uri.IsHexDigit);
}

public sealed record Ff7GuestBattleSettlementReceipt(Guid EncounterId, long FieldGeneration, long RequestId, int FormationId, int StoryCharacter, int OriginRow, string SourceFf8Hash, string SettledFf8Hash, ushort SettledHp, string DormantCharacterHash)
{
    public Ff7BattleOutcome Outcome { get; init; } = Ff7BattleOutcome.Victory;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Ff7GuestBattleSettlementCharacter[]? AdditionalGuests { get; init; }

    public IReadOnlyList<Ff7GuestBattleSettlementCharacter> GetGuests() => [new(StoryCharacter, OriginRow, SettledHp, DormantCharacterHash), .. AdditionalGuests ?? []];

    internal bool IsValid
    {
        get
        {
            IReadOnlyList<Ff7GuestBattleSettlementCharacter> guests = GetGuests();
            return EncounterId != Guid.Empty && FieldGeneration >= 0 && RequestId > 0 && FormationId is >= 0 and <= 1023
                && Ff7GuestBattleSettlementCharacter.IsHash(SourceFf8Hash) && Ff7GuestBattleSettlementCharacter.IsHash(SettledFf8Hash)
                && (AdditionalGuests is null or { Length: 1 or 2 }) && guests.All(guest => guest is { IsValid: true })
                && guests.Select(guest => guest.StoryCharacter).Distinct().Count() == guests.Count && guests.Select(guest => guest.OriginRow).Distinct().Count() == guests.Count;
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
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? TurnBasedBattle { get; init; }
    public CharacterAppearanceMapping[] Mappings { get; init; } = [];
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<int, string>? CharacterNameOverrides { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Ff7GuestBattleSettlementReceipt[]? GuestBattleSettlements { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Ff7MixedCombatBinding? MixedCombat { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Ff7MixedCombatBinding[]? MixedCombatBindings { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte[]? MixedBattlePayload { get; init; }

    public IReadOnlyList<Ff7MixedCombatBinding> GetMixedCombatBindings() => MixedCombatBindings ?? (MixedCombat is { } binding ? [binding] : []);

    public void ValidateCharacterNameOverrides()
    {
        if (CharacterNameOverrides?.Any(pair => pair.Key is < 0 or > 8 || string.IsNullOrWhiteSpace(pair.Value) || pair.Value != pair.Value.Trim() || pair.Value.Any(char.IsControl) || System.Globalization.StringInfo.ParseCombiningCharacters(pair.Value).Length > 20) == true)
        {
            throw new InvalidDataException("Crossover character presentation names require supported story identities and one through twenty text elements.");
        }
    }

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
        if (Mappings.Select(mapping => (mapping.StoryGame, mapping.StoryCharacter)).Distinct().Count() != Mappings.Length || Mappings.Any(mapping => mapping.StoryGame != SessionGame.Ff7 || mapping.StoryCharacter is < 0 or > 8 || mapping.AppearanceGame != SessionGame.Ff8 || mapping.AppearanceCharacter is < 0 or > 10 || string.IsNullOrWhiteSpace(mapping.RigProfile)))
        {
            throw new NotSupportedException("The appearance mapping requires a supported character identity and explicit rig profile.");
        }
        ValidateCharacterNameOverrides();
        IReadOnlyList<Ff7MixedCombatBinding> mixed = GetMixedCombatBindings();
        if (MixedCombatBindings is not null && (MixedCombat is not null || MixedCombatBindings.Length is < 2 or > 3)
            || mixed.Count > 0 && (Ff8Payload is not { Length: > 0 } || mixed.Any(binding => binding is not { StoryCharacter: >= 0 and <= 8, OriginCharacter: >= 0 and <= 10, RulesVersion: 1 })
            || mixed.Select(binding => binding.StoryCharacter).Distinct().Count() != mixed.Count || mixed.Select(binding => binding.OriginCharacter).Distinct().Count() != mixed.Count))
        {
            throw new InvalidDataException("Mixed combat requires an explicit supported origin character and suspended save.");
        }
        Ff7?.Validate();
        IReadOnlyList<Ff7BattleGuestSlotState> slots = Ff7?.Battle?.GetGuestSlotStates() ?? [];
        if ((MixedBattlePayload is { Length: > 0 } || slots.Count > 1) && (slots.Count == 0 || mixed.Count != slots.Count || slots.Any(slot => !mixed.Contains(new(slot.Binding.StoryCharacter, slot.Binding.OriginCharacter)))))
        {
            throw new InvalidDataException("A mixed action payload requires its active native host battle and matching explicit bindings.");
        }
        if (GuestBattleSettlements is { } settlements && (Ff7 is null || Ff8Payload is not { Length: > 0 } || settlements.Any(receipt => receipt is null || !receipt.IsValid || !ValidGuestSettlementHost(receipt)) || settlements.Select(receipt => receipt.EncounterId).Distinct().Count() != settlements.Length || settlements.Select(receipt => (receipt.FieldGeneration, receipt.RequestId)).Distinct().Count() != settlements.Length))
        {
            throw new InvalidDataException("Combined guest settlements require unique encounter identities and matching committed host battle results.");
        }
    }

    private bool ValidGuestSettlementHost(Ff7GuestBattleSettlementReceipt receipt)
    {
        if (receipt.Outcome == Ff7BattleOutcome.Victory)
        {
            return Ff7!.Progress.CompletedBattles.Any(host => host.FieldGeneration == receipt.FieldGeneration && host.RequestId == receipt.RequestId && host.Formation == receipt.FormationId);
        }
        if (receipt.Outcome is not (Ff7BattleOutcome.Escaped or Ff7BattleOutcome.Defeat))
        {
            return false;
        }
        if (Ff7!.FieldGeneration > receipt.FieldGeneration)
        {
            return true;
        }
        if (Ff7!.FieldGeneration == receipt.FieldGeneration && Ff7.Process.Receipts.Any(host => host.Request.Id == receipt.RequestId && host.Request.Command is FieldBattleCommand command && command.Formation == receipt.FormationId && host.Reply.Completed))
        {
            return true;
        }
        if (receipt.Outcome != Ff7BattleOutcome.Defeat || Ff7.FieldGeneration != receipt.FieldGeneration || Ff7.Battle is not { Outcome: Ff7BattleOutcome.Defeat } battle || battle.RequestId != receipt.RequestId || battle.FormationId != receipt.FormationId)
        {
            return false;
        }
        IReadOnlyList<Ff7BattleGuestSlotState> slots = battle.GetGuestSlotStates();
        IReadOnlyList<Ff7GuestBattleSettlementCharacter> guests = receipt.GetGuests();
        return slots.Count == guests.Count && slots.All(slot => slot.Binding.EncounterId == receipt.EncounterId && slot.Binding.SourcePayloadSha256 == receipt.SourceFf8Hash && guests.Any(guest => guest.StoryCharacter == slot.Binding.StoryCharacter && guest.OriginRow == slot.Binding.OriginRow));
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
        ArgumentNullException.ThrowIfNull(save);
        if (save.Ff7 is { } ff7 && (ff7.Battle is not null || ff7.Process?.GlobalCommand == Ff7GlobalCommand.Battle))
        {
            throw new InvalidOperationException("Finish the current FFVII battle before saving.");
        }
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
