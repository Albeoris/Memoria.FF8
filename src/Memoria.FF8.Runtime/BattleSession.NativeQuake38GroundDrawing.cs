using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private sealed partial class NativeQuake38Execution
    {
        private short Zsf3 => Memory.ScriptedGraphics?.Zsf3 ?? throw new NotSupportedException("Native Quake requires its current initialized ZSF3 register.");
        private short Zsf4 => Memory.ScriptedGraphics?.Zsf4 ?? throw new NotSupportedException("Native Quake requires its current initialized ZSF4 register.");

        private void DrawEdge(int offset, int edge, int first, int second, int centroid)
        {
            NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(session.NativeFileStaging!, 24);
            session.NativeFileStaging = temporary.Staging;
            ImmutableArray<byte> entry = Memory.Arena!.Read(offset, 112).ToImmutableArray();
            short phase = state.Word(0x279299E);
            short height = 0;
            if (phase > 2)
            {
                byte zone = entry[88 + edge];
                if (zone >= 10)
                {
                    throw new InvalidDataException("Native Quake edge leaves its actual wave bank.");
                }
                height = state.Word(checked(0x278C912u + (uint)zone * 16));
                int[] selectors = [first, second, centroid];
                for (int index = 0; index < 3; index++)
                {
                    byte[] vector = entry.AsSpan().Slice(selectors[index] * 8, 8).ToArray();
                    short y = unchecked((short)(BinaryPrimitives.ReadInt16LittleEndian(vector.AsSpan(2)) + height));
                    BinaryPrimitives.WriteInt16LittleEndian(vector.AsSpan(2), y);
                    session.NativeFileStaging = session.NativeFileStaging.Write(temporary.Address + checked((uint)index * 8), vector);
                }
            }
            if (phase > 4)
            {
                Emit(NativeBattleQuake38GroundPackets.Face8D9BE0(entry, first, second, centroid, height, Projection, Zsf3, Identity(0)));
                foreach ((int a, int b, int bit) in new[] { (second, first, 1), (centroid, second, 2), (first, centroid, 4) })
                {
                    if ((entry[96 + edge] & bit) == 0)
                    {
                        continue;
                    }
                    NativeBattleVector left = Position(a);
                    NativeBattleVector right = Position(b);
                    NativeBattleTemporaryAllocation side = NativeBattleTemporaryMemory.Allocate5082B0(session.NativeFileStaging!, 32);
                    byte[] vectors = new byte[32];
                    int[] selectors = [a, b, a, b];
                    for (int index = 0; index < 4; index++)
                    {
                        entry.AsSpan().Slice(selectors[index] * 8, 8).CopyTo(vectors.AsSpan(index * 8));
                        short y = BinaryPrimitives.ReadInt16LittleEndian(vectors.AsSpan(index * 8 + 2));
                        y = unchecked((short)(y + (index < 2 ? height : 16)));
                        BinaryPrimitives.WriteInt16LittleEndian(vectors.AsSpan(index * 8 + 2), y);
                    }
                    session.NativeFileStaging = side.Staging.Write(side.Address, vectors);
                    Emit(NativeBattleQuake38GroundBorders.Side8DA3E0(entry, left, right, a, b, height, Projection, Zsf4, Identity(bit)));
                    session.NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(session.NativeFileStaging!, 32);
                }
            }
            if (phase is 3 or 4)
            {
                (int First, int Second, int Bit)[] borders = [(second, first, 1), (centroid, second, 2), (first, centroid, 4)];
                for (int index = 0; index < borders.Length; index++)
                {
                    var border = borders[index];
                    if ((entry[96 + edge] & border.Bit) != 0)
                    {
                        Emit(NativeBattleQuake38GroundBorders.Crack8D9F70(entry, Position(border.First), Position(border.Second), edge, index, state.Word(0x278C8EA), state.Read(0x27929A0, 40), resources, Projection, Zsf4, Identity(8 + index)));
                    }
                }
                if (phase == 4)
                {
                    for (int index = 0; index < borders.Length; index++)
                    {
                        var border = borders[index];
                        if ((entry[96 + edge] & border.Bit) == 0)
                        {
                            continue;
                        }
                        NativeBattleQuake38WallDraw wall = NativeBattleQuake38GroundBorders.Wall8DA200(entry, Position(border.First), Position(border.Second), edge, index, state.Word(0x2792D70), state.Word(0x2792D6A), state.Read(0x2792E38, 40), resources, Projection, Zsf4, Identity(12 + index));
                        entry = wall.Entry;
                        Memory.Arena!.Write(offset, entry.AsSpan());
                        Emit(wall.Batch);
                        if (wall.SpawnChip)
                        {
                            SpawnChip(wall.First, wall.UpperFirst, checked(offset + edge * 3 + index + 76));
                        }
                    }
                }
            }
            session.NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(session.NativeFileStaging!, 24);

            NativeBattlePilotVisualIdentity Identity(int primitive) => NativeBattlePilotVisualLifetimes.Identity(38, Memory.Arena!, 0x8D9BE0, offset, edge * 16 + primitive);

            NativeBattleVector Position(int selector)
            {
                short x = BinaryPrimitives.ReadInt16LittleEndian(entry.AsSpan().Slice(selector * 8));
                short y = unchecked((short)(BinaryPrimitives.ReadInt16LittleEndian(entry.AsSpan().Slice(selector * 8 + 2)) + height));
                short z = BinaryPrimitives.ReadInt16LittleEndian(entry.AsSpan().Slice(selector * 8 + 4));
                return new(x, y, z);
            }
        }

        private void SpawnChip(NativeBattleVector first, NativeBattleVector second, int counter)
        {
            if (Memory.Arena!.ReadByte(counter) != 0 || state.Word(0x2792D70) >= 12 || (Random() & 3) != 0)
            {
                return;
            }
            state = state.SetWord(0x2792D6A, unchecked((short)(state.Word(0x2792D6A) + 1)));
            uint child = Spawn(state.Dword(0x27929C8), 3, 0x8DB630, 172);
            NativeBattleModulePoolTask task = hierarchy.Read(child);
            task = SetWord(task, 28, (first.X + second.X) / 2);
            task = SetWord(task, 30, (first.Y + second.Y) / 2 - (Random() & 0x1FF));
            task = SetWord(task, 32, (first.Z + second.Z) / 2);
            task = task.WriteDword(168, checked(0x20DFAB8u + (uint)counter + 1));
            hierarchy = hierarchy.Write(child, task);
        }
    }
}
