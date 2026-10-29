using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>Textured Gouraud packets over native per-vertex colors or per-vertex intensity factors.</summary>
public static class NativeBattleCinematicVertexColorPackets
{
    public static (NativeBattleWaterExecutionFrame Frame, uint Next) Emit(NativeBattleWaterExecutionFrame frame, uint group, uint projected, uint colors, bool intensity, Func<NativeBattleWaterExecutionFrame, uint, short, NativeBattleCinematicGridValue> shade, Func<NativeBattleWaterExecutionFrame, uint, uint, NativeBattleGfGeometryBatch, NativeBattleWaterExecutionFrame> append, IReadOnlyDictionary<uint, System.Numerics.Vector3>? pilotWorld = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        short type = unchecked((short)Word(group));
        bool quad = type is 18 or 19;
        bool faceColors = type is 9 or 19;
        if (type is not (8 or 9 or 18 or 19) || intensity && !faceColors)
        {
            throw new NotSupportedException("Native vertex-color mesh requires its selected textured triangle or quad group.");
        }
        int count = unchecked((short)Word(group + 2));
        int corners = quad ? 4 : 3;
        int faceStride = (quad, faceColors) switch
        {
            (false, false) => 20,
            (false, true) => 28,
            (true, false) => 24,
            _ => 36
        };
        int indices = faceColors ? quad ? 24 : 18 : quad ? 12 : 10;
        int uv = faceColors ? corners * 4 : 4;
        int packetLength = quad ? 52 : 40;
        uint pageFlags = ReadSlot(frame, 0x92, 1)[0] & 0x60u;
        uint command = (BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xCC, 4)) & 0x02000000) | (quad ? 0x3C000000u : 0x34000000u);
        short selectedDepth = unchecked((short)SlotWord(frame, 0xC0));
        uint fixedOrdering = intensity ? unchecked(frame.ReadTemporaryDword(0x14C) + (uint)selectedDepth) : selectedDepth != 0 ? unchecked(frame.ReadTemporaryDword(0x14C) + ((uint)selectedDepth & 0x3FFC)) : 0;
        frame = frame.WriteTemporaryDword(0xF0, pageFlags).WriteTemporaryDword(0xF4, command).WriteTemporaryDword(0xF8, fixedOrdering);
        uint output = CoreDword(frame, unchecked(CoreDword(frame, 0x27973EC) + 0x7C));
        uint face = unchecked(group + 4);
        for (int faceIndex = 0; faceIndex < count; faceIndex++, face = unchecked(face + (uint)faceStride))
        {
            ushort[] offsets = new ushort[corners];
            uint[] screens = new uint[corners];
            for (int corner = 0; corner < 3; corner++)
            {
                offsets[corner] = Word(face + (uint)(indices + corner * 2));
                screens[corner] = Dword(projected + offsets[corner]);
            }
            if (NativeBattleModulePackets.Nclip(screens[0], screens[1], screens[2]) <= 0)
            {
                continue;
            }
            byte[] data = new byte[packetLength];
            NativeBattleGfPacketByteState[] known = new NativeBattleGfPacketByteState[packetLength];
            data[3] = quad ? (byte)12 : (byte)9;
            known[3] = NativeBattleGfPacketByteState.Known;
            StoreWord(14, Word(face + (uint)(indices + corners * 2)));
            StoreWord(26, unchecked((ushort)(Word(face + (uint)(indices + corners * 2 + 2)) | pageFlags)));
            for (int corner = 0; corner < 3; corner++)
            {
                StoreDword(8 + corner * 12, screens[corner]);
            }
            if (quad)
            {
                offsets[3] = Word(face + (uint)(indices + 6));
                screens[3] = Dword(projected + offsets[3]);
                StoreDword(44, screens[3]);
            }
            for (int corner = 0; corner < corners; corner++)
            {
                uint color = Dword(colors + (uint)(offsets[corner] >> 1));
                if (faceColors)
                {
                    uint material = Dword(face + (uint)(corner * 4));
                    NativeBattleCinematicGridValue result = intensity ? shade(frame, material, unchecked((short)color)) : shade(frame, color, unchecked((byte)material));
                    frame = result.Frame;
                    color = result.Value;
                }
                StoreDword(4 + corner * 12, corner == 0 ? color | command : color);
            }
            for (int corner = 0; corner < corners; corner++)
            {
                StoreWord(12 + corner * 12, Word(face + (uint)(uv + corner * 2)));
            }
            int[] depths = new int[4];
            uint ordering = fixedOrdering;
            if (!intensity)
            {
                for (int corner = 0; corner < corners; corner++)
                {
                    depths[corner] = Word(projected + offsets[corner] + 4);
                }
                if (ordering == 0)
                {
                    int sum = unchecked(depths[0] + depths[1] + depths[2] + depths[3]);
                    uint offset = unchecked((uint)((sum / corners >> 2) & ~3));
                    ordering = unchecked(frame.ReadTemporaryDword(0x14C) + offset);
                }
            }
            ImmutableArray<uint> words = Enumerable.Range(0, data.Length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(index * 4))).ToImmutableArray();
            ImmutableArray<NativeBattleGfPacketByteState> mask = known.ToImmutableArray();
            NativeBattleGeometryPrimitive primitive = new(0, words, depths.ToImmutableArray()) { PhysicalWriteMask = mask };
            if (pilotIdentity is { } identity && pilotWorld is not null && offsets.All(offset => pilotWorld.ContainsKey(projected + offset)))
            {
                primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked((int)face) }, pilotWorld[projected + offsets[0]], pilotWorld[projected + offsets[1]], pilotWorld[projected + offsets[2]], quad ? pilotWorld[projected + offsets[3]] : default, checked((byte)corners)) { DepthDivisor = 4 } };
            }
            frame = append(frame, output, ordering, new([new(0, data.ToImmutableArray(), mask, primitive)], packetLength));
            output = unchecked(output + (uint)packetLength);

            void StoreWord(int offset, ushort value)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset), value);
                Array.Fill(known, NativeBattleGfPacketByteState.Known, offset, 2);
            }

            void StoreDword(int offset, uint value)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);
                Array.Fill(known, NativeBattleGfPacketByteState.Known, offset, 4);
            }
        }
        frame = WriteCoreDword(frame, unchecked(CoreDword(frame, 0x27973EC) + 0x7C), output);
        return (frame, face);

        ushort Word(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(ArenaOffset(address, 2), 2));
        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));
    }
}
