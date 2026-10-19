using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicGridValue(NativeBattleWaterExecutionFrame Frame, uint Value);
public sealed record NativeBattleCinematicGridProjection(NativeBattleWaterExecutionFrame Frame, int QuarterDepth);
public sealed record NativeBattleCinematicGrid28Services(Func<NativeBattleWaterExecutionFrame, short, NativeBattleWaterExecutionFrame> TexturePage, Func<NativeBattleWaterExecutionFrame, short, NativeBattleCinematicGridValue> TextureClut, Func<NativeBattleWaterExecutionFrame, short, uint> ResolveSlot, Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> PrepareBasis, Func<NativeBattleWaterExecutionFrame, uint, NativeBattleWaterExecutionFrame> SetLighting, Func<NativeBattleWaterExecutionFrame, short, short, int, NativeBattleWaterExecutionFrame> SetDepthCue, Func<NativeBattleWaterExecutionFrame, uint, NativeBattleWaterExecutionFrame> SetFarColor, Func<NativeBattleWaterExecutionFrame, uint, uint, uint, uint, uint, uint, NativeBattleCinematicGridProjection> ProjectNormal, Func<NativeBattleWaterExecutionFrame, uint, uint, NativeBattleGfGeometryBatch, NativeBattleWaterExecutionFrame> Append);

/// <summary>A textured grid with projected normals, interpolated colors and per-cell ordering.</summary>
public static class NativeBattleCinematicGrid28
{
    public static NativeBattleWaterExecutionFrame Draw(NativeBattleWaterExecutionFrame frame, NativeBattleCinematicGrid28Services services)
    {
        ArgumentNullException.ThrowIfNull(services);
        uint payload = SlotDword(0xB8);
        frame = frame.WriteTemporaryDword(0x60, unchecked((uint)SignedWord(unchecked(payload + 4))));
        frame = frame.WriteTemporaryDword(0x64, unchecked((uint)SignedWord(unchecked(payload + 6))));
        short texture = SignedWord(unchecked(payload + 2));
        frame = services.TexturePage(frame, texture);
        frame = frame.WriteTemporaryDword(0x68, frame.ReadTemporaryDword(0xFC));
        NativeBattleCinematicGridValue clut = services.TextureClut(frame, texture);
        frame = clut.Frame.WriteTemporaryDword(0x6C, clut.Value);
        int columns = unchecked((int)frame.ReadTemporaryDword(0x60));
        int rows = unchecked((int)frame.ReadTemporaryDword(0x64));
        int firstU = unchecked(-(columns / 2 << 4) - ((SlotWord(frame, 0x98) >> 4) & 0xF0));
        frame = frame.WriteTemporaryDword(0x70, unchecked((uint)firstU));
        int firstV = unchecked(-(rows / 2 << 4) - ((SlotWord(frame, 0x94) >> 4) & 0xF0));
        frame = frame.WriteTemporaryDword(0x74, unchecked((uint)firstV));
        frame = frame.WriteTemporaryDword(0x78, unchecked((uint)((columns + 1) * 12)));
        uint reference = services.ResolveSlot(frame, SignedWord(payload));
        frame = frame.WriteTemporaryDword(0x7C, (ReadSlot(frame, 0xDE, 1)[0] & 0x20) != 0 ? SlotDword(0xC0) : uint.MaxValue);
        ushort savedScale = SlotWord(frame, 0x8E);
        frame = WriteSlotWord(frame, 0x8E, 0x1000);
        frame = services.PrepareBasis(frame);
        frame = WriteSlotWord(frame, 0x8E, savedScale);
        frame = services.SetLighting(frame, SlotDword(0xCC));
        short far = SignedWord(unchecked(payload + 10));
        short near = SignedWord(unchecked(payload + 8));
        frame = services.SetDepthCue(frame, near, far, 512);
        frame = services.SetFarColor(frame, ReferenceColor(unchecked(reference + 0xCC)));
        uint work = ManagerDword(0x74);
        rows = unchecked((int)frame.ReadTemporaryDword(0x64));
        frame = frame.WriteTemporaryWord(0xC0, unchecked((ushort)(ReadSlot(frame, 0x94, 1)[0] - (rows / 2 << 8))));
        int initialZ = unchecked(ReadSlot(frame, 0x98, 1)[0] - (unchecked((int)frame.ReadTemporaryDword(0x60)) / 2 << 8));
        frame = frame.WriteTemporaryWord(0xC2, 0);
        frame = frame.WriteTemporaryWord(0xC8, unchecked((ushort)(SlotWord(frame, 0x96) << 4)));
        frame = frame.WriteTemporaryWord(0xCA, 0);
        frame = frame.WriteTemporaryWord(0xCC, 0);
        frame = frame.WriteTemporaryDword(0xE0, 0x3C808080);
        uint command = (SlotDword(0xCC) & 0x02000000) | 0x3C000000;
        for (int row = 0; row <= rows; row++)
        {
            frame = frame.WriteTemporaryWord(0xC4, unchecked((ushort)initialZ));
            columns = unchecked((int)frame.ReadTemporaryDword(0x60));
            for (int column = 0; column <= columns; column++)
            {
                uint temporary = CoreDword(frame, 0x2797624);
                NativeBattleCinematicGridProjection projection = services.ProjectNormal(frame, unchecked(temporary + 0xC0), unchecked(temporary + 0xC8), unchecked(temporary + 0xE0), work, unchecked(work + 8), unchecked(temporary + 0xFC));
                frame = projection.Frame;
                uint color = Dword(unchecked(work + 8));
                StoreWord(unchecked(work + 4), unchecked((ushort)projection.QuarterDepth));
                StoreDword(unchecked(work + 8), color | command);
                work = unchecked(work + 12);
                frame = frame.WriteTemporaryWord(0xC4, unchecked((ushort)(frame.ReadTemporaryWord(0xC4) + 0x100)));
            }
            frame = frame.WriteTemporaryWord(0xC0, unchecked((ushort)(frame.ReadTemporaryWord(0xC0) + 0x100)));
        }
        uint top = ManagerDword(0x74);
        uint bottom = unchecked(top + frame.ReadTemporaryDword(0x78));
        uint output = ManagerDword(0x7C);
        rows = unchecked((int)frame.ReadTemporaryDword(0x64));
        int v = unchecked((int)frame.ReadTemporaryDword(0x74));
        for (int row = 0; row < rows; row++)
        {
            columns = unchecked((int)frame.ReadTemporaryDword(0x60));
            int u = unchecked((int)frame.ReadTemporaryDword(0x70));
            for (int column = 0; column < columns; column++)
            {
                byte[] data = new byte[52];
                data[3] = 12;
                PutDword(4, Dword(unchecked(top + 8)));
                PutDword(0x10, Dword(unchecked(top + 20)));
                PutDword(0x1C, Dword(unchecked(bottom + 8)));
                PutDword(0x28, Dword(unchecked(bottom + 20)));
                PutWord(0x1A, unchecked((ushort)frame.ReadTemporaryDword(0x68)));
                PutWord(0xE, unchecked((ushort)frame.ReadTemporaryDword(0x6C)));
                data[0x24] = data[0xC] = unchecked((byte)u);
                u = unchecked(u + 16);
                data[0x30] = data[0x18] = (u & 0xF0) == 0 ? (byte)255 : unchecked((byte)u);
                data[0x19] = data[0xD] = unchecked((byte)v);
                data[0x31] = data[0x25] = (unchecked(v + 16) & 0xF0) == 0 ? (byte)255 : unchecked((byte)(v + 16));
                PutDword(8, Dword(top));
                PutDword(0x14, Dword(unchecked(top + 12)));
                PutDword(0x20, Dword(bottom));
                PutDword(0x2C, Dword(unchecked(bottom + 12)));
                int depth = unchecked((int)frame.ReadTemporaryDword(0x7C));
                if (depth < 0)
                {
                    depth = (SignedWord(unchecked(bottom + 16)) + SignedWord(unchecked(bottom + 4)) + SignedWord(unchecked(top + 16)) + SignedWord(unchecked(top + 4))) / 4;
                }
                uint ordering = unchecked(frame.ReadTemporaryDword(0x14C) + ((uint)depth & 0xFFFFFFFC));
                ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Range(0, 52).Select(index => index >= 3 && index is not (0x26 or 0x27 or 0x32 or 0x33) ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Untouched).ToImmutableArray();
                ImmutableArray<uint> words = Enumerable.Range(0, 13).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(index * 4))).ToImmutableArray();
                NativeBattleGeometryPrimitive primitive = new(0, words, []) { PhysicalWriteMask = mask };
                frame = services.Append(frame, output, ordering, new([new(0, data.ToImmutableArray(), mask, primitive)], 52));
                top = unchecked(top + 12);
                bottom = unchecked(bottom + 12);
                output = unchecked(output + 52);

                void PutDword(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);
                void PutWord(int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset), value);
            }
            v = unchecked(v + 16);
            top = unchecked(top + 12);
            bottom = unchecked(bottom + 12);
        }
        return WriteCoreDword(frame, unchecked(CoreDword(frame, 0x27973EC) + 0x7C), output);

        uint ReferenceColor(uint address)
        {
            if (address >= frame.TemporaryFrame.Address && (long)address - frame.TemporaryFrame.Address <= 0x17C)
            {
                return frame.ReadTemporaryDword(checked((int)(address - frame.TemporaryFrame.Address)));
            }
            if (address >= NativeBattleScriptedEffectMemory.Address && (long)address - NativeBattleScriptedEffectMemory.Address <= NativeBattleScriptedEffectMemory.Length - 4)
            {
                return CoreDword(frame, address);
            }
            return Dword(address);
        }

        uint SlotDword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, offset, 4));
        uint ManagerDword(uint offset) => CoreDword(frame, unchecked(CoreDword(frame, 0x27973EC) + offset));
        byte[] Read(uint address, int length) => frame.Arena.Read(ArenaOffset(address, length), length);
        short SignedWord(uint address) => BinaryPrimitives.ReadInt16LittleEndian(Read(address, 2));
        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));

        void StoreWord(uint address, ushort value)
        {
            byte[] data = new byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(data, value);
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 2), data) };
        }

        void StoreDword(uint address, uint value)
        {
            byte[] data = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(data, value);
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 4), data) };
        }
    }
}
