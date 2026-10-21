using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeSoftwarePrimitiveTable([property: JsonRequired] ImmutableArray<uint> Handlers)
{
    public uint Handler(byte opcode)
    {
        if (Handlers.IsDefault || Handlers.Length != 256)
        {
            throw new InvalidDataException("Native software primitive dispatch requires its actual256-entry table.");
        }
        return Handlers[opcode];
    }
}
public sealed record NativeSoftwarePolygonPacketResult(NativeSoftwareTextureState Texture, NativeSoftwareClippedPolygon? Polygon, bool Depth3Diagnostic);
public sealed record NativeSoftwarePolygonPacketDraw(NativeSoftwarePolygonRasterState Raster, NativeSoftwareTextureState Texture, NativeSoftwareClippedPolygon? Polygon, bool Depth3Diagnostic);

/// <summary>Original software polygon and rectangle dispatch; source offsets include the physical packet tag.</summary>
public static class NativeSoftwarePolygonPackets
{
    public static int CoordinateShift45D080(ushort proxyFlags20) => (proxyFlags20 & 1) != 0 ? 16 : 13;

    public static NativeSoftwarePolygonPacketResult Adapt(byte opcode, ReadOnlySpan<byte> packet, int coordinateShift, int drawOffsetX, int drawOffsetY, NativeSoftwareTextureState texture, NativeSoftwarePrimitiveTable table, int imageLength)
    {
        ArgumentNullException.ThrowIfNull(table);
        uint expected = (opcode & 0xFC) switch
        {
            0x20 => 0x4667C0,
            0x24 => 0x4669F0,
            0x28 => 0x4668F0,
            0x2C => 0x466B10,
            0x30 => 0x4669D0,
            0x34 => 0x466C70,
            0x38 => 0x4669E0,
            0x3C => 0x466D90,
            0x60 => 0x467010,
            0x64 => 0x467050,
            _ => throw new NotSupportedException("Native software polygon opcode has no closed producer.")
        };
        if (table.Handler(opcode) != expected)
        {
            throw new NotSupportedException("Native software opcode selected a different actual producer.");
        }
        if (expected is 0x4669D0 or 0x4669E0 or 0x467010 or 0x467050)
        {
            return new(texture, null, false);
        }
        ArgumentNullException.ThrowIfNull(texture);
        (int[] xy, int[] uv, int page, int length) layout = (opcode & 0xFC) switch
        {
            0x20 => ([8, 0xC, 0x10], [], -1, 20),
            0x28 => ([8, 0xC, 0x14, 0x10], [], -1, 24),
            0x24 => ([8, 0x10, 0x18], [0xC, 0x14, 0x1C], 0x16, 32),
            0x2C => ([8, 0x10, 0x20, 0x18], [0xC, 0x14, 0x24, 0x1C], 0x16, 40),
            0x34 => ([8, 0x14, 0x20], [0xC, 0x18, 0x24], 0x1A, 40),
            0x3C => ([8, 0x14, 0x2C, 0x20], [0xC, 0x18, 0x30, 0x24], 0x1A, 52),
            _ => throw new InvalidDataException("Native software producer dimensions are unavailable.")
        };
        (int[] xy, int[] uv, int page, int length) = layout;
        if (packet.Length < length || packet[7] != opcode)
        {
            throw new InvalidDataException("Native software polygon requires its actual packet identity and consumed field extent.");
        }
        bool diagnostic = false;
        if (page >= 0)
        {
            NativeSoftwareTexturePagePreparation prepared = NativeSoftwareTexturePreparation.PrepareTPage45B960(texture, BinaryPrimitives.ReadUInt16LittleEndian(packet.Slice(page, 2)));
            texture = NativeSoftwareTexturePreparation.PrepareClut45B920(prepared.State, BinaryPrimitives.ReadUInt16LittleEndian(packet.Slice(0xE, 2)), imageLength);
            diagnostic = prepared.Depth3Diagnostic;
        }
        byte abr = texture.Abr ?? throw new NotSupportedException("Native software polygon requires its retained current ABR owner.");
        byte flags = (byte)(uv.Length == 0 ? (opcode & 2) << 1 : ((opcode & 2) | 1) << 1);
        ImmutableArray<byte> header = [flags, 0, 0, 0, packet[4], packet[5], packet[6], abr];
        ImmutableArray<NativeSoftwareClipVertex>.Builder vertices = ImmutableArray.CreateBuilder<NativeSoftwareClipVertex>(xy.Length);
        for (int index = 0; index < xy.Length; index++)
        {
            int x = unchecked((BinaryPrimitives.ReadInt16LittleEndian(packet.Slice(xy[index], 2)) << (coordinateShift & 31)) + drawOffsetX);
            int y = unchecked((BinaryPrimitives.ReadInt16LittleEndian(packet.Slice(xy[index] + 2, 2)) << (coordinateShift & 31)) + drawOffsetY) >> 16;
            vertices.Add(new(x, y, uv.Length == 0 ? null : packet[uv[index]] << 10, uv.Length == 0 ? null : packet[uv[index] + 1] << 10, null, null, null));
        }
        return new(texture, new(header, vertices.MoveToImmutable()), diagnostic);
    }

    public static NativeSoftwarePolygonPacketDraw Draw(byte[] image, NativeSoftwarePolygonRasterState raster, NativeSoftwareTextureState texture, byte opcode, ReadOnlySpan<byte> packet, int coordinateShift, int drawOffsetX, int drawOffsetY, NativeSoftwareClipBounds bounds, NativeSoftwarePrimitiveTable table, NativeSoftwareReciprocalTable reciprocals)
    {
        ArgumentNullException.ThrowIfNull(image);
        NativeSoftwarePolygonPacketResult adapted = Adapt(opcode, packet, coordinateShift, drawOffsetX, drawOffsetY, texture, table, image.Length);
        if (adapted.Polygon is { } polygon)
        {
            NativeSoftwareTextureState prepared = adapted.Texture;
            NativeSoftwarePolygonClip.Submit466880(polygon, bounds, Submit, Submit);
            void Submit(NativeSoftwareClippedPolygon selected)
            {
                if ((selected.Flags & 2) != 0 && (prepared.TexturePointerIsNull != false || prepared.PalettePointerIsNull != false))
                {
                    throw new NotSupportedException("Native textured polygon requires known non-NULL texture and palette pointer owners.");
                }
                int depth = (selected.Flags & 2) != 0 ? prepared.Depth ?? throw new NotSupportedException("Native textured polygon lacks its retained depth.") : 0;
                int source = (selected.Flags & 2) != 0 ? prepared.TextureByteOffset ?? throw new NotSupportedException("Native textured polygon lacks its prepared texture pointer.") : 0;
                int palette = (selected.Flags & 2) != 0 ? prepared.PaletteByteOffset ?? throw new NotSupportedException("Native textured polygon lacks its prepared palette pointer.") : 0;
                raster = NativeSoftwarePolygonEdges.Rasterize463DF0(image, raster, selected, reciprocals, depth, source, palette);
            }
        }
        return new(raster, adapted.Texture, adapted.Polygon, adapted.Depth3Diagnostic);
    }
}
