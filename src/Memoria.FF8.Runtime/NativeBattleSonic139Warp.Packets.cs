using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleSonic139Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleSonic139Warp
{
    public static NativeBattleSonic139Frame DrawGouraud6A97C0(NativeBattleSonic139Frame frame, uint temporary, NativeBattleSonic139Inputs inputs) => DrawTriangles(frame, temporary, false, inputs);
    public static NativeBattleSonic139Frame DrawTextured6A9640(NativeBattleSonic139Frame frame, uint temporary, NativeBattleSonic139Inputs inputs) => DrawTriangles(frame, temporary, true, inputs);

    private static NativeBattleSonic139Frame DrawTriangles(NativeBattleSonic139Frame frame, uint temporary, bool textured, NativeBattleSonic139Inputs inputs)
    {
        NativeBattleEffectArena context = Context(frame, temporary, 76);
        uint stream = Word32(context, 20);
        uint count = Dword(frame, stream);
        stream = checked(stream + 4);
        if (count > NativeBattleEffectArena.Length / 20)
        {
            throw new InvalidDataException("Native Sonic triangles leave their bounded face bank.");
        }
        uint cursor = Cursor(frame);
        for (uint faceIndex = 0; faceIndex < count; faceIndex++)
        {
            NativeBattleEffectArena face = ReadBytes(frame, stream, 20);
            context = Context(frame, temporary, 76);
            uint records = Word32(context, 4);
            for (int corner = 0; corner < 3; corner++)
            {
                ushort offset = BinaryPrimitives.ReadUInt16LittleEndian(face.Read(4 + corner * 2, 2));
                NativeBattleEffectArena record = ReadBytes(frame, checked(records + (uint)offset * 4), 12);
                context = NativeBattleEffectNodeContextMemory.Copy(record, 0, context, 28 + corner * 12, 12);
            }
            stream = checked(stream + 20);
            context = context.Write(20, BitConverter.GetBytes(stream));
            frame = PublishContext(frame, temporary, context, 76);
            if (textured && (context.Read(36, 1)[0] & context.Read(48, 1)[0] & context.Read(60, 1)[0]) != 0)
            {
                continue;
            }
            int allocation = textured ? 32 : 28;
            NativeBattleEffectArena packet = ReadBytes(frame, cursor, allocation).Write(0, BitConverter.GetBytes(textured ? 0x07000000u : 0x06000000u));
            if (textured)
            {
                packet = packet.Write(4, BitConverter.GetBytes(0x26808080u));
                short firstU = BinaryPrimitives.ReadInt16LittleEndian(context.Read(32, 2));
                int textureX = unchecked((int)Word32(context, 12));
                int textureY = unchecked((int)Word32(context, 16));
                ushort page = unchecked((ushort)((((textureX + firstU) >> 6) & 15) | ((textureY & 512) << 2) | 64 | ((textureY >> 4) & 16)));
                packet = packet.Write(22, BitConverter.GetBytes(page));
                byte baseU = unchecked((byte)(firstU & 0xC0));
                for (int corner = 0; corner < 3; corner++)
                {
                    int record = 28 + corner * 12;
                    packet = packet.Write(8 + corner * 8, context.Read(record, 4)).Write(12 + corner * 8, new byte[] { unchecked((byte)(context.Read(record + 4, 1)[0] - baseU)), context.Read(record + 6, 1)[0] });
                }
            }
            else
            {
                NativeBattleScriptedColorRegisters colors = frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native Sonic Gouraud packets require their current color registers.");
                uint cue = colors.Ir0 ?? throw new NotSupportedException("Native Sonic Gouraud packets require their actual cue DWORD.");
                NativeBattleModelRendererRegisters far = frame.Memory.PrimitiveProcess?.ModelRendererRegisters ?? throw new NotSupportedException("Native Sonic Gouraud packets require their actual far controls.");
                for (int corner = 0; corner < 3; corner++)
                {
                    uint source = Word32(face, corner == 0 ? 0 : 8 + corner * 4);
                    NativeBattleScriptedDpcsResult shaded = NativeBattleScriptedDpcs.Transform45F270(colors, source, cue, far, inputs.Colors);
                    colors = shaded.Registers;
                    packet = packet.Write(4 + corner * 8, BitConverter.GetBytes(shaded.PackedRgb)).Write(8 + corner * 8, context.Read(32 + corner * 12, 4));
                }
                packet = packet.Write(7, new byte[] { 0x32 });
                frame = frame with { Memory = frame.Memory with { ScriptedColorRegisters = colors } };
            }
            uint table = Word32(context, 24);
            uint baseTable = TableBase(frame);
            if (table < baseTable || (table - baseTable) % 4 != 0 || (table - baseTable) / 4 >= NativeBattlePrimitiveOrdering.BattleTableLength)
            {
                throw new InvalidDataException("Native Sonic triangle context leaves its actual selected ordering table.");
            }
            frame = Append(frame, cursor, allocation, allocation, checked((int)((table - baseTable) / 4)), 1, packet, inputs);
            cursor = checked(cursor + (uint)allocation);
        }
        frame = PublishContext(frame, temporary, Context(frame, temporary, 76).Write(20, BitConverter.GetBytes(stream)), 76);
        return SetCursor(frame, cursor);
    }

    private static uint Word32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
}
