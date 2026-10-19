using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Memoria.FF8.Runtime;

/// <summary>Small display data; loading a selected save still validates its complete state.</summary>
public sealed record SaveSummary(SceneKind Scene, int FieldId, string FieldName, NativePlaytimeState? Playtime, int[] PartyPortraits, int? PlaceId, int? WorldX, int? WorldZ)
{
    public DateTimeOffset? SavedLocalAt { get; init; }

    public GameState ToLocationState()
    {
        return new GameState
        {
            Scene = Scene,
            FieldId = FieldId,
            FieldName = FieldName,
            EventState = PlaceId is int place ? JsonSerializer.SerializeToElement(new { Scene = new { PlaceId = place } }) : null,
            World = WorldX is int x && WorldZ is int z ? new WorldState { X = x, Z = z } : null
        };
    }

    internal static SaveSummary Capture(GameState state)
    {
        int? placeId = null;
        if (state.EventState is { } vm && vm.TryGetProperty("Scene", out JsonElement scene) && scene.TryGetProperty("PlaceId", out JsonElement place) && place.TryGetInt32(out int id))
        {
            placeId = id;
        }
        int[] portraits = state.CharacterVitals is { } vitals ? vitals.CombatParty.Where(row => row != 255).Select(row => (int)vitals.Records[row].CharacterId).ToArray() : [];
        return new(state.Scene, state.FieldId, state.FieldName, state.Playtime, portraits, placeId, state.World?.X, state.World?.Z);
    }
}

internal static class SnapshotStorage
{
    private static readonly byte[] Magic = "MFF8SAVE"u8.ToArray();
    private const int Version = 1;
    private const int HeaderSize = 88;
    private const int MaximumSummarySize = 65536;

    internal static void Write(FileStream stream, GameState state, JsonSerializerOptions options)
    {
        byte[] summary = JsonSerializer.SerializeToUtf8Bytes(SaveSummary.Capture(state) with { SavedLocalAt = DateTimeOffset.Now }, options);
        if (summary.Length > MaximumSummarySize)
        {
            throw new InvalidDataException("Save display data exceeds its supported size.");
        }
        using BinaryWriter header = new(stream, Encoding.UTF8, leaveOpen: true);
        header.Write(Magic);
        header.Write(Version);
        header.Write(summary.Length);
        header.Write(0L);
        header.Write(SHA256.HashData(summary));
        header.Write(new byte[32]);
        header.Write(summary);
        using SHA256 hash = SHA256.Create();
        using (CryptoStream hashed = new(stream, hash, CryptoStreamMode.Write, leaveOpen: true))
        {
            using (GZipStream compressed = new(hashed, CompressionLevel.Fastest, leaveOpen: true))
            {
                JsonSerializer.Serialize(compressed, state, options);
            }
            hashed.FlushFinalBlock();
        }
        long end = stream.Position;
        stream.Position = 16;
        header.Write(end - HeaderSize - summary.Length);
        stream.Position = 56;
        header.Write(hash.Hash!);
        stream.Position = end;
    }

    internal static GameState Read(FileStream stream, JsonSerializerOptions options)
    {
        Header? header = ReadHeader(stream, options);
        if (header is null)
        {
            return SnapshotDefaults.Deserialize(stream, options);
        }
        using SHA256 hash = SHA256.Create();
        using CryptoStream hashed = new(stream, hash, CryptoStreamMode.Read, leaveOpen: true);
        GameState state;
        using (GZipStream compressed = new(hashed, CompressionMode.Decompress, leaveOpen: true))
        {
            state = SnapshotDefaults.Deserialize(compressed, options);
            compressed.CopyTo(Stream.Null);
        }
        hashed.CopyTo(Stream.Null);
        if (!CryptographicOperations.FixedTimeEquals(hash.Hash!, header.PayloadHash))
        {
            throw new InvalidDataException("Save payload checksum does not match.");
        }
        return state;
    }

    internal static SaveSummary? ReadSummary(FileStream stream, JsonSerializerOptions options)
    {
        return ReadHeader(stream, options)?.Summary;
    }

    private static Header? ReadHeader(FileStream stream, JsonSerializerOptions options)
    {
        Span<byte> magic = stackalloc byte[8];
        int count = stream.Read(magic);
        if (count != magic.Length || !magic.SequenceEqual(Magic))
        {
            stream.Position = 0;
            return null;
        }
        try
        {
            using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: true);
            if (reader.ReadInt32() != Version)
            {
                throw new InvalidDataException("Unsupported save container version.");
            }
            int summaryLength = reader.ReadInt32();
            long payloadLength = reader.ReadInt64();
            if (summaryLength is < 1 or > MaximumSummarySize || payloadLength < 1 || stream.Length - HeaderSize - summaryLength != payloadLength)
            {
                throw new InvalidDataException("Invalid or truncated save container.");
            }
            byte[] summaryHash = reader.ReadBytes(32);
            byte[] payloadHash = reader.ReadBytes(32);
            byte[] summaryBytes = reader.ReadBytes(summaryLength);
            if (!CryptographicOperations.FixedTimeEquals(summaryHash, SHA256.HashData(summaryBytes)))
            {
                throw new InvalidDataException("Save display checksum does not match.");
            }
            SaveSummary summary = JsonSerializer.Deserialize<SaveSummary>(summaryBytes, options) ?? throw new InvalidDataException("Empty save display data.");
            if (!Enum.IsDefined(summary.Scene) || summary.FieldName is null || summary.PartyPortraits is null || summary.PartyPortraits.Length > 3 || summary.PartyPortraits.Any(id => id is < 0 or > 255))
            {
                throw new InvalidDataException("Invalid save display data.");
            }
            summary.Playtime?.Validate();
            return new(summary, payloadHash);
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException("Truncated save container header.", exception);
        }
    }

    private sealed record Header(SaveSummary Summary, byte[] PayloadHash);
}
