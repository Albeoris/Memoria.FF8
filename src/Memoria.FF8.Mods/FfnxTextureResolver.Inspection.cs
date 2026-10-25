using System.Text.RegularExpressions;
using Memoria.FF8.Resources;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Mods;

public sealed partial class FfnxTextureResolver
{
    private readonly record struct NativeTimIdentity(ushort X, ushort Y, ushort WidthWords, ushort Height, ushort PaletteX, ushort PaletteY, ushort PaletteWidth, ushort PaletteHeight);

    private static bool IsDormantNativeWorldMember(IResourceSource source, string target)
    {
        if (source is not IGameNativeMemorySource memory || !NativeFieldRuntimeProfile.IsSupportedImage(memory.SourceHash))
        {
            return false;
        }
        Match match = Regex.Match(target, @"^(?:eng/)?world/dat/wmset/section38/texture([0-9]+)_[0-9]+(?:_[0-9]+_[0-9]+)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success || !Number(match.Groups[1].Value, out int member))
        {
            return false;
        }
        NativeTimIdentity? identity = ReadNativeWorldMemberIdentity(source, 38, member);
        return identity is NativeTimIdentity(832, 64, 24, 96, 848, 224, 16, 1)
            or NativeTimIdentity(880, 64, 16, 16, 960, 225, 16, 1)
            or NativeTimIdentity(872, 64, 8, 32, 1008, 225, 16, 1);
    }

    private static NativeTimIdentity? ReadNativeWorldMemberIdentity(IResourceSource source, int section, int member)
    {
        ResourceContent? content = source.Find(new("world/wmsetus.obj"));
        if (content is null)
        {
            return null;
        }
        using Stream input = content.OpenRead();
        using MemoryStream? materialized = input.CanSeek ? null : new MemoryStream();
        if (materialized is not null)
        {
            input.CopyTo(materialized);
            materialized.Position = 0;
        }
        Stream stream = materialized ?? input;
        using BinaryReader reader = new(stream, System.Text.Encoding.UTF8, true);
        stream.Position = (section - 1) * 4;
        int sectionStart = reader.ReadInt32();
        stream.Position = sectionStart + member * 4;
        int memberOffset = reader.ReadInt32();
        long start = (long)sectionStart + memberOffset;
        if (start < 196 || start > stream.Length - 20)
        {
            throw new InvalidDataException("Original world texture header leaves its resource.");
        }
        stream.Position = start;
        if (reader.ReadUInt32() != 0x10 || reader.ReadUInt32() != 8)
        {
            return null;
        }
        int paletteLength = reader.ReadInt32();
        ushort paletteX = reader.ReadUInt16();
        ushort paletteY = reader.ReadUInt16();
        ushort paletteWidth = reader.ReadUInt16();
        ushort paletteHeight = reader.ReadUInt16();
        if (paletteLength != 12L + (long)paletteWidth * paletteHeight * 2 || paletteLength > stream.Length - start - 20)
        {
            throw new InvalidDataException("Original world palette header leaves its resource.");
        }
        stream.Position = start + 8 + paletteLength;
        int imageLength = reader.ReadInt32();
        ushort x = reader.ReadUInt16();
        ushort y = reader.ReadUInt16();
        ushort width = reader.ReadUInt16();
        ushort height = reader.ReadUInt16();
        if (imageLength != 12L + (long)width * height * 2 || imageLength > stream.Length - start - 8 - paletteLength)
        {
            throw new InvalidDataException("Original world pixel rectangle leaves its resource.");
        }
        return new(x, y, width, height, paletteX, paletteY, paletteWidth, paletteHeight);
    }

    private static bool IsAbsentNativeWorldMember(IResourceSource source, string target, Dictionary<int, int?> counts)
    {
        Match match = Regex.Match(target, @"^(?:eng/)?world/dat/wmset/section([0-9]+)/texture([0-9]+)_[0-9]+(?:_[0-9]+_[0-9]+)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success || !Number(match.Groups[1].Value, out int section) || !Number(match.Groups[2].Value, out int member) || section is not (38 or 39 or 40 or 42))
        {
            return false;
        }
        if (!counts.TryGetValue(section, out int? count))
        {
            count = ReadNativeWorldMemberCount(source, section);
            counts.Add(section, count);
        }
        return count is int present && member >= present;
    }

    private static int? ReadNativeWorldMemberCount(IResourceSource source, int section)
    {
        ResourceContent? content = source.Find(new("world/wmsetus.obj"));
        if (content is null)
        {
            return null;
        }
        using Stream input = content.OpenRead();
        using MemoryStream? materialized = input.CanSeek ? null : new MemoryStream();
        if (materialized is not null)
        {
            input.CopyTo(materialized);
            materialized.Position = 0;
        }
        Stream stream = materialized ?? input;
        using BinaryReader reader = new(stream, System.Text.Encoding.UTF8, true);
        if (stream.Length < 196)
        {
            throw new InvalidDataException("Original world resource has no complete section directory.");
        }
        stream.Position = (section - 1) * 4;
        int offset = reader.ReadInt32();
        if (offset < 196 || offset > stream.Length - 4)
        {
            throw new InvalidDataException("Original world section directory leaves its resource.");
        }
        stream.Position = offset;
        for (int count = 0; count < 256; count++)
        {
            if (stream.Position > stream.Length - 4)
            {
                throw new InvalidDataException("Original world member directory is truncated.");
            }
            int pointer = reader.ReadInt32();
            if (pointer == 0)
            {
                return count;
            }
            if (pointer < 0 || pointer >= stream.Length - offset)
            {
                throw new InvalidDataException("Original world member directory leaves its resource.");
            }
        }
        throw new InvalidDataException("Original world member directory is unbounded.");
    }
}
