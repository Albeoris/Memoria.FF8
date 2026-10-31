using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleXatmDeath276Controller
{
    private sealed partial class Execution
    {
        private int TryAppend(int pool, uint callback) => Frame.Effect.Pools[pool].Slots.Any(task => (task.Flags & 1) == 0) ? Append(pool, callback) : -1;
        private static short TableWord(ImmutableArray<byte> data, int offset) => BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan().Slice(offset, 2));
        private static int TableInt(ImmutableArray<byte> data, int offset) => BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan().Slice(offset, 4));
        private static NativeBattleModulePoolTask Position(NativeBattleModulePoolTask task, int offset, NativeBattleVector point) => W(W(W(task, offset, point.X), offset + 2, point.Y), offset + 4, point.Z);
        private int Event(int index)
        {
            NativeBattleModulePoolTask task = Read(6, index);
            NativeBattleXatmDeath276Table table = resources.Events.Single(item => item.Address == task.ReadDword(16));
            ImmutableArray<byte> data = table.Data;
            int tick = unchecked((int)task.ReadDword(12));
            if (tick < data[0])
            {
                for (int remaining = data[1]; remaining > 0;)
                {
                    int count = TableInt(data, 0x78);
                    int first = unchecked(Random() * count) >> 15;
                    int second = unchecked(Random() * count) >> 15;
                    while (first == second)
                    {
                        second = unchecked(Random() * count) >> 15;
                    }
                    if (!NativeBattleXatmDeath276Arcs.CanSpawn(table.Triangles, first, second))
                    {
                        continue;
                    }
                    int arc = TryAppend(5, 0x686A10);
                    if (arc >= 0)
                    {
                        Put(5, arc, NativeBattleXatmDeath276Arcs.Initialize(Read(5, arc), inputs.ReadModelAddress(), checked(table.TriangleAddress + (uint)(first * 24)), checked(table.TriangleAddress + (uint)(second * 24)), 12, 7));
                    }
                    remaining--;
                }
            }
            int end = data[2];
            if (tick >= (end >> 1) && tick < end)
            {
                for (int offset = 0x16; data[offset] != 255; offset += 6)
                {
                    if (data[offset + 4] > (Random() & 255))
                    {
                        SpawnSparks(data[offset] & 0x7F, data[offset + 5], 2, 1, 0);
                    }
                }
            }
            if (unchecked((uint)(tick - end + 4)) < 4)
            {
                uint late = unchecked(3u - (uint)(tick - end + 4));
                for (int offset = 0x16; data[offset] != 255; offset += 6)
                {
                    if (data[offset + 1] != 0 && data[offset + 1] < late)
                    {
                        SpawnSparks(data[offset] & 0x7F, data[offset + 3], 1, 1, data[0x15]);
                    }
                }
                if (data[0x14] != 255)
                {
                    int count = (Random() & 3) + 6;
                    NativeBattleVector position = inputs.ReadBone(data[0x14], 0);
                    for (int ordinal = 0; ordinal < count; ordinal++)
                    {
                        int smoke = TryAppend(1, 0x686000);
                        if (smoke < 0)
                        {
                            continue;
                        }
                        NativeBattleModulePoolTask child = Position(Read(1, smoke), 12, position);
                        child = W(child, 18, 0);
                        child = W(child, 20, (Random() & 0x1FF) - 0x100);
                        child = W(child, 24, (Random() & 0x1FF) - 0x100);
                        child = W(child, 22, (Random() & 0x1FF) - 0x12C);
                        Put(1, smoke, W(child, 26, (Random() & 0x7FF) + 0x800));
                    }
                }
            }
            if (tick == end)
            {
                NativeBattleEffectTaskPool directors = Frame.Effect.Pools[7];
                NativeBattleModulePoolTask director = directors.Slots[0];
                uint address = directors.BankAddress;
                int cursor = checked((int)(director.ReadDword(0x48) - address));
                for (int offset = 0x16; data[offset] != 255; offset += 6)
                {
                    byte bone = data[offset];
                    if (bone < 128)
                    {
                        director = director.WriteByte(cursor++, bone);
                    }
                }
                director = director.WriteDword(0x48, checked(address + (uint)cursor));
                Put(7, 0, director);
                if (TableWord(data, 14) != 0)
                {
                    Execute(new NativeBattleXatmDeath276Burst(table, TableWord(data, 14), TableWord(data, 16), TableWord(data, 18)));
                }
                if (data[0x15] == 0)
                {
                    return 2;
                }
            }
            if (tick == data[8])
            {
                int prim = TryAppend(4, 0x6860B0);
                if (prim >= 0)
                {
                    NativeBattleVector position = inputs.ReadBone(data[9], TableWord(data, 10));
                    NativeBattleModulePoolTask child = Position(Read(4, prim), 14, position);
                    child = W(child, 12, 0);
                    Put(4, prim, W(child, 20, TableWord(data, 12)));
                    Execute(new NativeBattleXatmDeath276InitializePrim(prim));
                }
            }
            if (data[3] != 255 && tick == data[3])
            {
                Execute(new NativeBattleXatmDeath276Fade(data[4], data[5], data[6], 128));
            }
            Put(6, index, task.WriteDword(12, unchecked(task.ReadDword(12) + 1)));
            return 0;
        }

        private void SpawnSparks(int bone, int size, int step, int count, byte falling)
        {
            int vertices = inputs.ReadBoneVertexCount(bone);
            for (int ordinal = 0; ordinal < count; ordinal++)
            {
                int spark = TryAppend(3, 0x6866F0);
                if (spark < 0)
                {
                    return;
                }
                int selected = unchecked(Random() * vertices) >> 15;
                NativeBattleModulePoolTask task = Position(Read(3, spark), 16, inputs.ReadBoneVertex(bone, selected));
                task = W(task, 22, size << 6);
                task = W(task, 12, 0).WriteByte(14, unchecked((byte)step)).WriteByte(15, falling);
                Put(3, spark, task);
            }
        }

        private int Moving(int index, bool smoke)
        {
            int pool = smoke ? 1 : 2;
            NativeBattleModulePoolTask task = Read(pool, index);
            DrawSprite(task, smoke ? 0x10D2EA0u : 0x10D304Cu, 12, Word(task, 18) >> 1, Word(task, 26));
            short velocity = Word(task, 22);
            task = Add(Add(Add(task, 14, velocity), 12, Word(task, 20)), 16, Word(task, 24));
            task = Add(task, 18, 1);
            task = W(task, 22, velocity + (smoke ? 16 : -5));
            Put(pool, index, task);
            return Word(task, 18) < (smoke ? 16 : 28) ? 0 : 2;
        }

        private int Spark(int index)
        {
            NativeBattleModulePoolTask task = Read(3, index);
            DrawSprite(task, 0x10D31C4, 16, Word(task, 12), Word(task, 22));
            if (Word(task, 12) == 5)
            {
                byte specification = task.ReadByte(15);
                int first = specification >> 4;
                for (int count = specification & 15; count > 0; count--)
                {
                    int fall = TryAppend(2, 0x686820);
                    if (fall < 0)
                    {
                        continue;
                    }
                    NativeBattleModulePoolTask child = Read(2, fall).WriteDword(12, task.ReadDword(16)).WriteDword(16, task.ReadDword(20));
                    int offset = -128 - (Random() & 255);
                    child = W(child, 18, first);
                    child = Add(child, 14, offset);
                    child = W(child, 20, (Random() & 0x7F) - 0x40);
                    int depth = (Random() & 0x7F) - 0x40;
                    child = W(child, 22, -50);
                    child = W(child, 24, depth);
                    Put(2, fall, W(child, 26, (Random() & 0x7FF) + 0x1000));
                }
            }
            task = Add(task, 12, task.ReadByte(14));
            Put(3, index, task);
            return Word(task, 12) < 14 ? 0 : 2;
        }

        private void DrawSprite(NativeBattleModulePoolTask task, uint address, int offset, int phase, short size)
        {
            NativeBattleModuleResource resource = resources.Sprites.Single(item => item.Address == address);
            ushort frame = unchecked((ushort)phase);
            NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, Phase = frame, SpriteFrame = resource.ProjectedFrame571C80(frame), Anchor = new(Word(task, offset), Word(task, offset + 2), Word(task, offset + 4), Word(task, offset + 6)), Scale = size, DepthOffset = -(size >> 2), SpriteFlags = 0 };
            if (inputs.CapturePresentation)
            {
                draw = draw with { PilotIdentity = NativeBattleSelectedProgrammeVisuals.Resource(NativeBattlePilotVisualLifetimes.Identity(276, task), address) };
            }
            Execute(new NativeBattleXatmDeath276Sprite(draw));
        }
    }
}

