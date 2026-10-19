using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>The cinematic vertical texture transfer over current metadata, arena source and rectangle-ring owners.</summary>
public static class NativeBattleCinematicScroll
{
    public static NativeBattleWaterExecutionFrame Draw(NativeBattleWaterExecutionFrame frame, INativeBattleScriptedTextureProfile profile, Func<NativeBattleWaterExecutionFrame, NativeBattleEffectTextureRectangle, uint, NativeBattleWaterExecutionFrame> enqueue)
    {
        ArgumentNullException.ThrowIfNull(enqueue);
        uint payloadAddress = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xB8, 4));
        byte[] raw = frame.Arena.Read(ArenaOffset(payloadAddress, 6), 6);
        NativeBattleWaterScrollPayload payload = new(BinaryPrimitives.ReadInt16LittleEndian(raw), BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(4)));
        NativeBattleCinematicTextureSelection source = NativeBattleCinematicTextureMetadata.ResolvePixelB664A0(frame, profile, payload.TextureId);
        frame = frame.WriteTemporaryDword(0xF8, (uint)(source.SourceMapByte & 0x80))
            .WriteTemporaryDword(0xFC, source.SourceAddress)
            .WriteTemporaryDword(0xF0, source.RectangleAddress)
            .WriteTemporaryDword(0xF4, source.TexturePage!.Value)
            .WriteTemporaryDword(0x64, source.SourceAddress)
            .WriteTemporaryDword(0x78, unchecked((uint)(int)source.Rectangle.Width))
            .WriteTemporaryDword(0x7C, unchecked((uint)(int)source.Rectangle.Height));
        short scroll = unchecked((short)SlotWord(frame, 0x96));
        int row = ComputeRow(source.Rectangle, scroll);
        frame = frame.WriteTemporaryDword(0x70, unchecked((uint)row));
        EmitGeometry(source.Rectangle, payload, source.SourceAddress, row, (rectangle, address) =>
        {
            uint runtime = CoreDword(frame, 0x279744C);
            byte counter = frame.Core.Read(unchecked(runtime + 0x43), 1)[0];
            int index = counter & 15;
            uint ring = 0x27974D4u + (uint)(index * 8);
            frame = frame with { Core = frame.Core.Write(unchecked(runtime + 0x43), [unchecked((byte)(counter + 1))]).Write(ring, EncodeRectangle(rectangle)) };
            frame = enqueue(frame, rectangle, address);
        });
        return frame;
    }

    internal static int ComputeRow(NativeBattleEffectTextureRectangle source, short scroll)
    {
        if (source.Width <= 0 || source.Height <= 0)
        {
            throw new NotSupportedException("Native texture scroll requires its actual positive source extent.");
        }
        return (int)scroll & (source.Height - 1);
    }

    internal static void EmitGeometry(NativeBattleEffectTextureRectangle source, NativeBattleWaterScrollPayload payload, uint sourceAddress, int row, Action<NativeBattleEffectTextureRectangle, uint> emit)
    {
        short width = source.Width;
        short height = source.Height;
        emit(new(payload.DestinationX, payload.DestinationY, width, unchecked((short)(height - row))), unchecked(sourceAddress + (uint)(width * row * 2)));
        if (row != 0)
        {
            emit(new(payload.DestinationX, unchecked((short)(payload.DestinationY + height - row)), width, unchecked((short)row)), sourceAddress);
        }
    }

    internal static byte[] EncodeRectangle(NativeBattleEffectTextureRectangle rectangle)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, rectangle.X);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), rectangle.Y);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), rectangle.Width);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(6), rectangle.Height);
        return bytes;
    }
}
