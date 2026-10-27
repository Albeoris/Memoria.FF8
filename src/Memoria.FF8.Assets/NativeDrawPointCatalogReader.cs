using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Runtime;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets;

public static class NativeDrawPointCatalogReader
{
    public static NativeDrawPointCatalog Read(byte[] executable, byte[] kernel)
    {
        return Read(new NativeExecutableMemory(executable), kernel, language: "en");
    }

    public static NativeDrawPointCatalog Read(IGameNativeMemorySource memory, byte[] kernel, byte[]? commonMessages = null, StaticTextResource? selectedMessages = null, string? language = null)
    {
        string executableHash = memory.SourceHash;
        if (!NativeFieldRuntimeProfile.IsSupportedImage(executableHash) || kernel.Length < 228 || BinaryPrimitives.ReadUInt32LittleEndian(kernel) != 56)
        {
            throw new NotSupportedException("Draw-point catalog requires authenticated native memory and a complete kernel.");
        }
        uint commonAddress = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xB92324, 4));
        ReadOnlySpan<byte> common = commonMessages ?? memory.Read(commonAddress, 320);
        if (commonAddress != 0xB921E4 || common.Length != 320 || BinaryPrimitives.ReadUInt32LittleEndian(common) != 36)
        {
            throw new InvalidDataException("Original draw-point common message bank header is invalid.");
        }
        FF8Encoding encoding = NativeTextEncoding.Create(language ?? throw new InvalidOperationException("Draw-point text decoding requires its native language."));
        ImmutableArray<string>.Builder messages = ImmutableArray.CreateBuilder<string>(8);
        ImmutableArray<string>.Builder messageHashes = ImmutableArray.CreateBuilder<string>(8);
        ImmutableArray<ImmutableArray<byte>>.Builder messageBytes = ImmutableArray.CreateBuilder<ImmutableArray<byte>>(8);
        for (int id = 0; id < 8; id++)
        {
            int start = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(common.Slice(id * 4, 4)));
            int end = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(common.Slice((id + 1) * 4, 4)));
            if (start < 36 || end <= start || end > common.Length || common[end - 1] != 0)
            {
                throw new InvalidDataException($"Original draw-point common message{id} bounds are invalid.");
            }
            ReadOnlySpan<byte> bytes = common.Slice(start, end - start);
            messages.Add(encoding.GetString(bytes[..^1]));
            messageHashes.Add(Convert.ToHexString(SHA256.HashData(bytes)));
            messageBytes.Add(ImmutableArray.CreateRange(bytes.ToArray()));
        }
        int magicStart = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(kernel.AsSpan(4 + 4 * 1)));
        int magicEnd = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(kernel.AsSpan(4 + 4 * 2)));
        int magicNameStart = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(kernel.AsSpan(4 + 4 * 32)));
        int magicNameEnd = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(kernel.AsSpan(4 + 4 * 33)));
        if (magicStart < 228 || magicEnd > kernel.Length || magicEnd - magicStart != 57 * 60 || magicNameStart < 228 || magicNameEnd > kernel.Length || magicNameEnd <= magicNameStart)
        {
            throw new InvalidDataException("Original magic rows and names require their kernel section1/32 bounds.");
        }
        ImmutableArray<string>.Builder magicNames = ImmutableArray.CreateBuilder<string>(57);
        for (int id = 0; id < 57; id++)
        {
            ushort nameOffset = BinaryPrimitives.ReadUInt16LittleEndian(kernel.AsSpan(magicStart + id * 60));
            if (nameOffset == ushort.MaxValue)
            {
                magicNames.Add(string.Empty);
                continue;
            }
            int name = checked(magicNameStart + nameOffset);
            int zero = name < magicNameEnd ? Array.IndexOf(kernel, (byte)0, name, magicNameEnd - name) : -1;
            if (zero < 0)
            {
                throw new InvalidDataException($"Original magic{id} name is outside its kernel text section.");
            }
            magicNames.Add(encoding.GetString(kernel.AsSpan(name, zero - name)));
        }
        int commandStart = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(kernel.AsSpan(4 + 4 * 12)));
        int commandEnd = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(kernel.AsSpan(8 + 4 * 12)));
        if (commandStart < 228 || commandEnd > kernel.Length || commandEnd - commandStart != 19 * 8)
        {
            throw new InvalidDataException("Original command ability section12 requires nineteen8-byte records.");
        }
        ImmutableArray<byte> commands = Enumerable.Range(0, 19).Select(index => kernel[commandStart + index * 8 + 5]).ToImmutableArray();
        NativeDrawPointCatalog catalog = new(executableHash, Convert.ToHexString(SHA256.HashData(kernel)), memory.Read(0xB92328, 256).ToImmutableArray(), commands, messages[7], messageHashes[7])
        {
            CommonMessages = messages.MoveToImmutable(),
            CommonMessageHashes = messageHashes.MoveToImmutable(),
            CommonMessageBytes = messageBytes.MoveToImmutable(),
            MagicNames = magicNames.MoveToImmutable()
        };
        catalog.Validate();
        return ApplyMessages(catalog, selectedMessages);
    }

    public static NativeDrawPointCatalog ApplyMessages(NativeDrawPointCatalog original, StaticTextResource? selected)
    {
        ArgumentNullException.ThrowIfNull(original);
        if (selected is null)
        {
            return original;
        }
        if (selected.Key != StaticTextResource.DrawPointMessages || selected.Texts.IsDefault || selected.Texts.Length != 9)
        {
            throw new InvalidDataException("Draw-point text requires its complete nine-entry source bank.");
        }
        FF8Encoding encoding = FF8Encoding.CreateEuropean(FF8TextLanguage.EN);
        ImmutableArray<string> messages = selected.Texts.Take(8).ToImmutableArray();
        ImmutableArray<ImmutableArray<byte>> bytes = messages.Select(text =>
        {
            byte[] encoded = encoding.GetBytes(text);
            return encoded.Length > 0 && encoded[^1] == 0 ? encoded.ToImmutableArray() : encoded.Append((byte)0).ToImmutableArray();
        }).ToImmutableArray();
        ImmutableArray<string> hashes = bytes.Select(value => Convert.ToHexString(SHA256.HashData(value.AsSpan()))).ToImmutableArray();
        NativeDrawPointCatalog catalog = original with { CommonMessages = messages, CommonMessageBytes = bytes, CommonMessageHashes = hashes, NoEligibleMessage = messages[7], CommonMessageHash = hashes[7] };
        catalog.Validate();
        return catalog;
    }
}
