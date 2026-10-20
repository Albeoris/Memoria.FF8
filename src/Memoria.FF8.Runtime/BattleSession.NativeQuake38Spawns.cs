using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private sealed partial class NativeQuake38Execution
    {
        private void SpawnDebris()
        {
            ImmutableArray<byte> entry = RandomEntry();
            int centroid = entry[110] == 1 ? 3 : entry[110] == 2 ? 4 : 0;
            if (centroid == 0)
            {
                return;
            }
            NativeBattleVector point = Mean(entry, centroid);
            if (point.X <= state.Word(0x2792E06) || point.X >= state.Word(0x2792E04) || point.Z <= state.Word(0x2792E2E) || point.Z >= state.Word(0x2792E2C))
            {
                return;
            }
            state = state.SetWord(0x279299C, unchecked((short)(state.Word(0x279299C) + 1)));
            uint address = Spawn(state.Dword(0x27929C8), 4, 0x8DA900, 176);
            NativeBattleModulePoolTask task = hierarchy.Read(address);
            task = Vector(task, 28, point).MarkPayloadUnknown(34, 2);
            SourceVertex(0, 0);
            SourceVertex(1, 1);
            SourceVertex(2, centroid);
            task = task.WriteWord(160, BinaryPrimitives.ReadUInt16LittleEndian(entry.AsSpan().Slice(48))).WriteWord(162, BinaryPrimitives.ReadUInt16LittleEndian(entry.AsSpan().Slice(50)));
            task = task.WriteDword(164, BinaryPrimitives.ReadUInt32LittleEndian(entry.AsSpan().Slice(40))).WriteDword(168, BinaryPrimitives.ReadUInt32LittleEndian(entry.AsSpan().Slice(44))).WriteDword(172, BinaryPrimitives.ReadUInt32LittleEndian(entry.AsSpan().Slice(44))).WriteByte(171, entry[43]);
            task = SetWord(task, 30, ZoneHeight(point));
            hierarchy = hierarchy.Write(address, task);

            void SourceVertex(int destination, int source)
            {
                NativeBattleVector vertex = EntryVector(entry, source);
                int x = (point.X - vertex.X) * (Random() % 3584 + 512) / 4096;
                int z = (point.Z - vertex.Z) * (Random() % 3584 + 512) / 4096;
                short y = unchecked((short)(point.Y - vertex.Y));
                task = Vector(task, 92 + destination * 8, new(unchecked((short)x), y, unchecked((short)z)));
                int lowerX = unchecked((short)x) * (Random() % 3584 + 512) / 4096;
                int lowerZ = unchecked((short)z) * (Random() % 3584 + 512) / 4096;
                short lowerY = unchecked((short)(y + Random() % 768 + 128));
                task = Vector(task, 116 + destination * 8, new(unchecked((short)lowerX), lowerY, unchecked((short)lowerZ)));
                task = task.WriteByte(148 + destination, entry[100 + source]).WriteByte(151 + destination, entry[100 + source]).WriteByte(154 + destination, entry[105 + source]).WriteByte(157 + destination, entry[105 + source]);
            }
        }

        private void SpawnGroundSprites(bool dust, int count)
        {
            NativeBattleVector? selected = GroundPoint();
            if (selected is not { } first)
            {
                return;
            }
            uint counter = dust ? 0x2792E18u : 0x2792D6Cu;
            state = state.SetWord(counter, unchecked((short)(state.Word(counter) + 1)));
            uint address = Spawn(state.Dword(0x27929C8), 3, dust ? 0x8DB410u : 0x8DB030u, 172);
            NativeBattleModulePoolTask task = hierarchy.Read(address);
            task = Row(task, 0, first with { Y = ZoneHeight(first) });
            for (int index = 1; index < count; index++)
            {
                NativeBattleVector? next = GroundPoint();
                if (next is not { } point)
                {
                    continue;
                }
                short height = ZoneHeight(point);
                short jitter = unchecked((short)(Random() & 0x3FF));
                height = unchecked((short)(dust ? height - jitter : height + jitter - 512));
                task = Row(task, index, point with { Y = height });
            }
            hierarchy = hierarchy.Write(address, task);
        }

        private NativeBattleVector? GroundPoint()
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                ImmutableArray<byte> entry = RandomEntry();
                int centroid = entry[110] == 1 ? 3 : entry[110] == 2 ? 4 : 0;
                if (centroid == 0)
                {
                    continue;
                }
                NativeBattleVector point = Mean(entry, centroid);
                if (point.X < state.Word(0x2792E06) || point.X > state.Word(0x2792E04) || point.Z < state.Word(0x2792E2E) || point.Z > state.Word(0x2792E2C))
                {
                    continue;
                }
                short x = unchecked((short)(point.X + (Random() & 0x7FF) - 1024));
                short z = unchecked((short)(point.Z + (Random() & 0x7FF) - 1024));
                return new(x, point.Y, z);
            }
            return null;
        }

        private ImmutableArray<byte> RandomEntry()
        {
            short count = state.Word(0x278C8EC);
            if (count <= 0)
            {
                throw new NotSupportedException("Native Quake random primitive requires its actual positive ground primitive count.");
            }
            // The original random selector admits entry0 and excludes entrycount.
            int index = Random() % count;
            return Memory.Arena!.Read(checked(EntryOffset + index * 112), 112).ToImmutableArray();
        }

        private short ZoneHeight(NativeBattleVector point)
        {
            byte zone = NativeBattleQuake38Zones.Read8D9A80(resources.ZoneMap, point.X, point.Z, state.Word(0x278C8E8), state.Word(0x278C8E6), state.Word(0x2792D6E));
            if (zone >= 10)
            {
                throw new InvalidDataException("Native Quake spawn leaves its actual wave bank.");
            }
            return state.Word(checked(0x278C912u + (uint)zone * 16));
        }

        private static NativeBattleVector Mean(ImmutableArray<byte> entry, int centroid)
        {
            NativeBattleVector first = EntryVector(entry, 0);
            NativeBattleVector second = EntryVector(entry, 1);
            NativeBattleVector center = EntryVector(entry, centroid);
            return new(unchecked((short)((first.X + second.X + center.X) / 3)), unchecked((short)((first.Y + second.Y + center.Y) / 3)), unchecked((short)((first.Z + second.Z + center.Z) / 3)));
        }

        private static NativeBattleVector EntryVector(ImmutableArray<byte> entry, int selector) => new(BinaryPrimitives.ReadInt16LittleEndian(entry.AsSpan().Slice(selector * 8)), BinaryPrimitives.ReadInt16LittleEndian(entry.AsSpan().Slice(selector * 8 + 2)), BinaryPrimitives.ReadInt16LittleEndian(entry.AsSpan().Slice(selector * 8 + 4)));
        private static NativeBattleModulePoolTask Vector(NativeBattleModulePoolTask task, int offset, NativeBattleVector vector) => task.WriteWord(offset, unchecked((ushort)vector.X)).WriteWord(offset + 2, unchecked((ushort)vector.Y)).WriteWord(offset + 4, unchecked((ushort)vector.Z));
        private static NativeBattleModulePoolTask Row(NativeBattleModulePoolTask task, int index, NativeBattleVector vector) => Vector(task, 88 + index * 12, vector).MarkPayloadUnknown(94 + index * 12, 2).WriteWord(96 + index * 12, 1);
    }
}
