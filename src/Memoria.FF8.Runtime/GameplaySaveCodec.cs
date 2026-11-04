using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Bounded, checksummed component storage. Existing snapshot and original-save readers remain independent.</summary>
public static class GameplaySaveCodec
{
    private static readonly byte[] Signature = "MFF8GPC1"u8.ToArray();
    private const int ContainerVersion = 1;
    public const int MaximumPayloadBytes = 4 * 1024 * 1024;
    private static readonly JsonSerializerOptions Options = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(), new ImmutableBytesJsonConverter() }
    };

    public static void Write(Stream destination, GameplaySaveComponentDocument document)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(document);
        document.Validate();
        byte[] payload = Serialize(document);
        using BinaryWriter writer = new(destination, Encoding.UTF8, leaveOpen: true);
        writer.Write(Signature);
        writer.Write(ContainerVersion);
        writer.Write(payload.Length);
        writer.Write(SHA256.HashData(payload));
        writer.Write(payload);
    }

    public static GameplaySaveComponentDocument Read(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        try
        {
            using BinaryReader reader = new(source, Encoding.UTF8, leaveOpen: true);
            if (!ReadBytes(reader, Signature.Length).AsSpan().SequenceEqual(Signature) || reader.ReadInt32() != ContainerVersion)
            {
                throw new InvalidDataException("Unsupported gameplay component container.");
            }
            int length = reader.ReadInt32();
            if (length is < 1 or > MaximumPayloadBytes)
            {
                throw new InvalidDataException("Gameplay component payload length is invalid.");
            }
            byte[] checksum = ReadBytes(reader, 32);
            byte[] payload = ReadBytes(reader, length);
            if (source.ReadByte() != -1)
            {
                throw new InvalidDataException("Gameplay component container has trailing data.");
            }
            if (!CryptographicOperations.FixedTimeEquals(checksum, SHA256.HashData(payload)))
            {
                throw new InvalidDataException("Gameplay component checksum does not match.");
            }
            GameplaySaveComponentDocument document = JsonSerializer.Deserialize<GameplaySaveComponentDocument>(payload, Options) ?? throw new InvalidDataException("Empty gameplay component document.");
            document.Validate();
            return document;
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException("Truncated gameplay component container.", exception);
        }
    }

    internal static T Detach<T>(T source)
    {
        return JsonSerializer.Deserialize<T>(Serialize(source), Options) ?? throw new InvalidDataException("Empty gameplay component.");
    }

    private static byte[] Serialize<T>(T value)
    {
        using BoundedPayloadStream stream = new();
        JsonSerializer.Serialize(stream, value, Options);
        return stream.ToArray();
    }

    private static byte[] ReadBytes(BinaryReader reader, int count)
    {
        byte[] bytes = reader.ReadBytes(count);
        if (bytes.Length != count)
        {
            throw new EndOfStreamException();
        }
        return bytes;
    }

    private sealed class BoundedPayloadStream : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count)
        {
            RequireLength(count);
            base.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            RequireLength(buffer.Length);
            base.Write(buffer);
        }

        public override void WriteByte(byte value)
        {
            RequireLength(1);
            base.WriteByte(value);
        }

        private void RequireLength(int count)
        {
            if (Position > MaximumPayloadBytes - count)
            {
                throw new InvalidDataException("Gameplay component payload exceeds its supported size.");
            }
        }
    }
}
