using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicLeviathanPacketsResult(NativeBattleWaterExecutionFrame Frame, NativeBattleGfGeometryBatch Packets);

public static class NativeBattleCinematicLeviathanPackets
{
    public static NativeBattleCinematicLeviathanPacketsResult Emit(NativeBattleWaterExecutionFrame frame, Func<uint, int> resolveBucket, IReadOnlyDictionary<uint, System.Numerics.Vector3>? pilotWorld = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        uint model = frame.ReadTemporaryDword(0x84);
        uint manager = CoreDword(frame, 0x27973EC);
        uint start = CoreDword(frame, unchecked(manager + 0x7C));
        frame = frame.WriteTemporaryDword(0xF4, start).WriteTemporaryDword(0xFC, ReadSlot(frame, 0xDE, 1)[0]).WriteTemporaryDword(0xE0, frame.ReadTemporaryDword(0x138));
        uint command = (BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xCC, 4)) & 0x02FFFFFF) | 0x24000000;
        frame = frame.WriteTemporaryDword(0xE8, command).WriteTemporaryWord(0xD0, Word(model + 0x48)).WriteTemporaryWord(0xD2, Word(model + 0x4A));
        uint face = Dword(model + 0x40);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        Draw(false, unchecked((short)frame.ReadTemporaryWord(0xD0)));
        command = (frame.ReadTemporaryDword(0xE8) & 0x02FFFFFF) | 0x2C000000;
        frame = frame.WriteTemporaryDword(0xE8, command);
        Draw(true, unchecked((short)frame.ReadTemporaryWord(0xD2)));
        frame = WriteCoreDword(frame, unchecked(manager + 0x7C), unchecked(start + (uint)allocated));
        return new(frame, new(attempts.ToImmutable(), allocated));

        void Draw(bool quad, int count)
        {
            int corners = quad ? 4 : 3;
            int firstLength = quad ? 56 : 48;
            int secondLength = quad ? 40 : 32;
            for (int index = 0; index < count; index++)
            {
                uint work = frame.ReadTemporaryDword(0x68);
                int[] offsets = new int[corners];
                uint[] screens = new uint[corners];
                for (int corner = 0; corner < 3; corner++)
                {
                    offsets[corner] = (Word(unchecked(face + (uint)(corner * 2))) & 0xFFF) * 8;
                    screens[corner] = Dword(unchecked(work + (uint)offsets[corner]));
                }
                if (NativeBattleModulePackets.Nclip(screens[0], screens[1], screens[2]) >= 0)
                {
                    if (quad)
                    {
                        offsets[3] = (Word(face + 6) & 0xFFF) * 8;
                        screens[3] = Dword(unchecked(work + (uint)offsets[3]));
                    }
                    int[] depths = offsets.Select(offset => (int)unchecked((short)Word(unchecked(work + (uint)offset + 4)))).ToArray();
                    int average = depths.Aggregate(0, (sum, depth) => unchecked(sum + depth)) / corners;
                    uint table = unchecked(frame.ReadTemporaryDword(0x14C) + (uint)((average >> 4) * 4));
                    int bucket = resolveBucket(table);
                    ImmutableArray<int> metadata = quad ? depths.ToImmutableArray() : [depths[0], depths[1], depths[2], 0];
                    SkinWrites first = new(firstLength, allocated);
                    SkinWrites second = new(secondLength, checked(allocated + firstLength));
                    second.Byte(3, quad ? (byte)9 : (byte)7);
                    first.Byte(3, quad ? (byte)13 : (byte)11);
                    first.Word(8, 0);
                    first.Word(firstLength - 4, 0);
                    first.Word(firstLength - 8, 0xE2000000);
                    for (int corner = 0; corner < corners; corner++)
                    {
                        first.Word(16 + 8 * corner, screens[corner]);
                        second.Word(8 + 8 * corner, screens[corner]);
                        uint uv = unchecked(frame.ReadTemporaryDword(0x6C) + (uint)offsets[corner]);
                        first.Byte(20 + 8 * corner, Byte(uv));
                        first.Byte(21 + 8 * corner, Byte(uv + 2));
                    }
                    if (quad)
                    {
                        second.Word16(12, Word(face + 8));
                        second.Word16(20, Word(face + 12));
                        second.Word16(28, Word(face + 16));
                        second.Word16(36, Word(face + 18));
                        second.Word16(22, Word(face + 14));
                        second.Word16(14, Word(face + 10));
                    }
                    else
                    {
                        second.Word16(12, Word(face + 8));
                        second.Word16(20, Word(face + 12));
                        second.Word16(28, Word(face + 6));
                        second.Word16(22, Word(face + 14));
                        second.Word16(14, Word(face + 10));
                    }
                    second.Word(4, frame.ReadTemporaryDword(0xE8));
                    first.Word16(30, unchecked((ushort)frame.ReadTemporaryDword(0x78)));
                    first.Word16(22, unchecked((ushort)frame.ReadTemporaryDword(0x7C)));
                    first.Word(12, frame.ReadTemporaryDword(0x80) | (quad ? 0x2C000000u : 0x24000000u));
                    first.Word(4, frame.ReadTemporaryDword(0x88));
                    NativeBattleGeometryPrimitive firstPrimitive = new(bucket, first.Words(), metadata);
                    NativeBattleGeometryPrimitive secondPrimitive = new(bucket, second.Words(), metadata);
                    if (pilotIdentity is { } identity && pilotWorld is not null && offsets.All(offset => pilotWorld.ContainsKey(unchecked(work + (uint)offset))))
                    {
                        System.Numerics.Vector3[] world = offsets.Select(offset => pilotWorld[unchecked(work + (uint)offset)]).ToArray();
                        NativeBattlePilotVisualGeometry geometry = new(identity with { Primitive = unchecked((int)face * 2) }, world[0], world[1], world[2], quad ? world[3] : default, checked((byte)corners));
                        firstPrimitive = firstPrimitive with { PilotGeometry = geometry };
                        secondPrimitive = secondPrimitive with { PilotGeometry = geometry with { Identity = geometry.Identity with { Primitive = unchecked(geometry.Identity.Primitive + 1) } } };
                    }
                    attempts.Add(first.Attempt(firstPrimitive));
                    attempts.Add(second.Attempt(secondPrimitive));
                    allocated = checked(allocated + firstLength + secondLength);
                }
                face = unchecked(face + (quad ? 20u : 16u));
            }
        }
        ushort Word(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(ArenaOffset(address, 2), 2));
        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));
        byte Byte(uint address) => frame.Arena.Read(ArenaOffset(address, 1), 1)[0];
    }
}
