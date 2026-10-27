using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicTextureSelection(uint RectangleAddress, NativeBattleEffectTextureRectangle Rectangle, uint SourceAddress, byte SourceMapByte, uint? TexturePage)
{
    public NativeBattleTextureOrigin? Origin { get; init; }
}

/// <summary>Shared texture helpers resolve actual current file pointers, including streamed aliases.</summary>
public static class NativeBattleCinematicTextureMetadata
{
    public static NativeBattleCinematicTextureSelection ResolvePixelB664A0(NativeBattleWaterExecutionFrame frame, INativeBattleScriptedTextureProfile profile, int index)
    {
        NativeBattleCinematicTextureSelection selection = Resolve(frame, profile, index, false);
        int x = selection.Rectangle.X;
        int y = unchecked((ushort)selection.Rectangle.Y);
        uint page = unchecked((uint)((((x >> 2) & 0x3F0) | (y & 0x100)) >> 4));
        page |= (uint)(selection.SourceMapByte & 0x80) | (uint)(ReadSlot(frame, 0x92, 1)[0] & 0x60);
        return selection with { TexturePage = page };
    }

    public static NativeBattleCinematicTextureSelection ResolveClutB66560(NativeBattleWaterExecutionFrame frame, INativeBattleScriptedTextureProfile profile, int index) => Resolve(frame, profile, index, true);

    public static NativeBattleEffectTextureState Enqueue505DF0(NativeBattleEffectTextureState textures, NativeBattleWaterExecutionFrame frame, NativeBattleCinematicTextureSelection selection)
    {
        return textures.EnqueueArenaRectangle505DF0(selection.Rectangle, frame.Arena, ArenaOffset(selection.SourceAddress, 0), selection.Origin);
    }

    private static NativeBattleCinematicTextureSelection Resolve(NativeBattleWaterExecutionFrame frame, INativeBattleScriptedTextureProfile profile, int index, bool clut)
    {
        profile.Validate();
        if ((uint)index >= profile.TextureCount)
        {
            throw new NotSupportedException("Native cinematic texture selection leaves its authenticated texture count.");
        }
        uint manager = CoreDword(frame, 0x27973EC);
        uint rectangleBase = Directory(clut ? 0x9Cu : 0x98u, clut ? 4 : 0);
        uint mapBase = Directory(clut ? 0xA8u : 0xA4u, clut ? 12 : 8);
        uint rectangleAddress = checked(rectangleBase + (uint)index * 8);
        ReadOnlySpan<byte> rectangleBytes = profile.ReadStatic(rectangleAddress, 8);
        NativeBattleEffectTextureRectangle rectangle = new(BinaryPrimitives.ReadInt16LittleEndian(rectangleBytes), BinaryPrimitives.ReadInt16LittleEndian(rectangleBytes[2..]), BinaryPrimitives.ReadInt16LittleEndian(rectangleBytes[4..]), BinaryPrimitives.ReadInt16LittleEndian(rectangleBytes[6..]));
        byte map = profile.ReadStatic(checked(mapBase + (uint)index), 1)[0];
        int sourceIndex = clut ? map : map & 0x7F;
        uint file = CoreDword(frame, checked(0x2798A68u + (uint)sourceIndex * 4));
        uint directory = unchecked(file + ArenaDword(unchecked(file + (clut ? 8u : 0x14u))));
        uint relative = ArenaDword(unchecked(directory + (uint)index * 4));
        uint source = unchecked(directory + (clut ? relative : relative & 0xFFFFFF));
        NativeBattleTextureOrigin? origin = rectangle.Width > 0 && rectangle.Height > 0 ? new($"mag{profile.Effect:000}_b.{sourceIndex:00}", index, clut ? NativeBattleTextureRole.Clut : NativeBattleTextureRole.Pixels, profile.BitsPerPixel(index), rectangle.Width, rectangle.Height) : null;
        return new(rectangleAddress, rectangle, source, map, null) { Origin = origin };

        uint Directory(uint managerOffset, int descriptorOffset)
        {
            uint actual = CoreDword(frame, checked(manager + managerOffset));
            uint expected = checked(profile.InitializationAddress + profile.InitializationOffset(descriptorOffset));
            if (actual != expected)
            {
                throw new NotSupportedException("Native cinematic texture selection requires its selected current initialization directory.");
            }
            return actual;
        }

        uint ArenaDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));
    }
}
