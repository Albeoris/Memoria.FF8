using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRayBomb10Controller
{
    private sealed partial class Execution
    {
        private int Director(int index)
        {
            if (DrawOnly)
            {
                return 0;
            }
            int counter = Word(Read(index), 12);
            int caster = checked((int)Process.Caster);
            NativeBattleActionMatrix matrix = inputs.ReadMatrix(caster);
            if (counter == 0)
            {
                NativeBattleVector anchor = inputs.ReadBone(caster, 240, 0x680);
                NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(matrix, new(0, 0x320, -930));
                NativeBattleVector centre = inputs.ReadCentre(Frame.Effect.Parameters?.TargetMask7A ?? throw new NotSupportedException("Native627F10 requires its actual target mask."));
                Process = Process with { Muzzle = new(unchecked((short)(anchor.X + offset.X)), unchecked((short)(anchor.Y + offset.Y)), unchecked((short)(anchor.Z + offset.Z)), Process.Muzzle.W), Centre = new(centre.X, centre.Y, centre.Z, Process.Centre.W) };
            }
            if (counter == 1)
            {
                for (int ordinal = 0; ordinal < 2; ordinal++)
                {
                    int child = Append(0x6290F0);
                    NativeBattleModulePoolTask task = W(Read(child), 12, 0);
                    task = W(task, 14, Rand() % 6 + ordinal);
                    int size = Rand() % 0x320 + 0x190;
                    task = W(task, 28, 0x1000);
                    task = W(task, 30, 0x66);
                    task = W(task, 32, 0);
                    Put(child, W(task, 24, size));
                }
            }
            if (counter is >= 1 and <= 30)
            {
                for (int ordinal = 0; ordinal < 6; ordinal++)
                {
                    int particle = FreeParticle();
                    if (particle < 0)
                    {
                        break;
                    }
                    P32(particle, 0, 1);
                    P16(particle, 4, 0);
                    P16(particle, 6, (Rand() & 0x3FF) + 0x1000);
                    NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(matrix, TableVector(resources.DustOffsets, ordinal));
                    P16(particle, 8, Process.Muzzle.X + offset.X);
                    P16(particle, 10, Process.Muzzle.Y + offset.Y);
                    P16(particle, 12, Process.Muzzle.Z + offset.Z);
                    P16(particle, 14, Process.Muzzle.W);
                    NativeBattleVector direction = NativeBattleGfMatrices.RotateShort56C4F0(matrix, TableVector(resources.DustDirections, ordinal));
                    var normal = NativeBattleActionGeometryRules.NormalizeVector56BC50(direction.X, direction.Y, direction.Z);
                    int speed = Rand() % 0x8C + 0x96;
                    P16(particle, 16, unchecked(unchecked((short)normal.X) * speed) >> 12);
                    P16(particle, 18, unchecked(unchecked((short)normal.Y) * speed) >> 12);
                    P16(particle, 20, unchecked(unchecked((short)normal.Z) * speed) >> 12);
                }
            }
            if (counter == 16)
            {
                int child = Append(0x629000);
                Put(child, W(W(W(Read(child), 12, 0), 28, 0x3333), 30, 0x1D4));
            }
            if (counter == 34)
            {
                int child = Append(0x628B00);
                NativeBattleVector delta = new(unchecked((short)(Process.Centre.X - Process.Muzzle.X)), 0, unchecked((short)(Process.Centre.Z - Process.Muzzle.Z)));
                NativeBattleVector target = NativeBattleGfMatrices.RotateShort56C4F0(NativeBattleGfMatrices.Euler56CD50(new(0, -0x400, 0)), delta);
                NativeBattleModulePoolTask task = Read(child);
                task = W(task, 12, 0);
                task = W(task, 16, target.X + Process.Muzzle.X);
                task = W(task, 18, Process.Centre.Y);
                task = W(task, 20, target.Z + Process.Muzzle.Z);
                task = W(task, 24, 0x46);
                task = W(task, 26, 0xA0);
                Put(child, W(task, 32, 0x800));
            }
            if (counter is >= 59 and <= 64)
            {
                for (int position = 0; position < 5; position++)
                {
                    int count = (Rand() & 1) + 1;
                    for (int ordinal = count; ordinal > 0; ordinal--)
                    {
                        int child = Append(0x6289B0);
                        NativeBattleModulePoolTask task = W(Read(child), 12, 0);
                        int delay = (Rand() & 7) + count;
                        task = Point(task, Process.Centre);
                        task = W(task, 14, delay);
                        task = W(task, 16, Word(task, 16) + unchecked((short)(Rand() % 0x320 + TableShort(resources.PuffX, position))) - 0x190);
                        task = W(task, 18, 0);
                        task = W(task, 20, Word(task, 20) + unchecked((short)(Rand() % 0x320 + TableShort(resources.PuffZ, position))) - 0x12C);
                        task = W(task, 24, Rand() & 0xFFF);
                        task = W(task, 28, Rand() % 0xE00 + 0x800);
                        Put(child, W(task, 32, (Rand() & 3) + 4));
                    }
                }
            }
            if (counter == 81)
            {
                int child = Append(0x628870);
                NativeBattleModulePoolTask task = W(Point(Read(child), Process.Centre), 12, 0);
                task = W(task, 28, 0x3924);
                task = W(task, 32, 0);
                Put(child, W(task, 34, 0x130C));
            }
            if (counter is >= 81 and <= 84)
            {
                for (int position = 0; position < 5; position++)
                {
                    int count = (Rand() & 1) + 2;
                    int scale = count * 400;
                    for (int ordinal = 0; ordinal < count; ordinal++)
                    {
                        int child = Append(0x628700);
                        NativeBattleModulePoolTask task = W(Read(child), 12, 0);
                        task = W(task, 14, (Rand() & 7) + ordinal + 11);
                        task = Point(task, Process.Centre);
                        task = W(task, 16, Word(task, 16) + unchecked((short)(Rand() % 0xE10 + TableShort(resources.DebrisX, position))) - 0x708);
                        task = W(task, 18, -(Rand() % 60));
                        task = W(task, 20, Word(task, 20) + unchecked((short)(Rand() % 0xE10 + TableShort(resources.DebrisZ, position))) - 0x708);
                        task = W(task, 22, -30 - Rand() % 0x1F4);
                        task = W(task, 24, Rand() & 1);
                        int fragmentScale = Rand() % 0x500 + 0x100;
                        task = W(task, 30, Word(task, 22));
                        task = W(task, 26, fragmentScale);
                        task = W(task, 28, scale);
                        task = W(task, 32, Rand() % 100 + 0x28);
                        Put(child, W(task, 34, Word(task, 32) >> 2));
                    }
                }
            }
            if (counter == 59)
            {
                int child = Append(0x6296F0, 0);
                Write(0, child, W(Frame.Effect.Pools[0].Slots[child], 12, 0));
            }
            if (counter == 82)
            {
                NativeBattleActionHeaderSlot header = inputs.Packets.Headers[0];
                int first = header.FirstContactIndex ?? throw new NotSupportedException("Native627F10 requires its first header contact pointer.");
                int count = header.ReadByte(16);
                for (int actor = 0; actor < 3; actor++)
                {
                    if ((inputs.ReadModel(actor).Flags0 & 2) == 0)
                    {
                        continue;
                    }
                    int child = Append(0x629200);
                    NativeBattleModulePoolTask task = W(Read(child), 12, 0);
                    task = W(task, 14, actor);
                    task = W(task, 24, (Rand() % 3 + 1) << 12);
                    int amplitude = -2500 - Rand() % 1000;
                    int target = -1;
                    for (int contact = 0; contact < count; contact++)
                    {
                        if (inputs.Packets.ReadContact(first + contact).Primary.ActorIndex == actor)
                        {
                            target = contact;
                            break;
                        }
                    }
                    task = W(task, 26, target);
                    Put(child, W(task, 32, target < 0 ? amplitude >> 1 : amplitude));
                }
            }
            if (counter <= 8)
            {
                Execute(new NativeBattleRayBomb10Flash(counter << 8));
            }
            else if (counter >= 152)
            {
                Execute(new NativeBattleRayBomb10Flash((160 - counter) << 8));
            }
            if (counter == 10)
            {
                Execute(new NativeBattleRayBomb10Sound(resources.MuzzleSound, 0, Process.Muzzle));
            }
            if (counter == 81)
            {
                Execute(new NativeBattleRayBomb10Sound(resources.BlastSound, 0x100, Process.Centre));
            }
            Put(index, Add(Read(index), 12, 1));
            if (Word(Read(index), 12) <= 160)
            {
                return 0;
            }
            Execute(new NativeBattleRayBomb10Flash(0));
            return 2;
        }

        private static short TableShort(ImmutableArray<byte> table, int index) => BinaryPrimitives.ReadInt16LittleEndian(table.AsSpan().Slice(index * 4, 2));
        private static NativeBattleVector TableVector(ImmutableArray<byte> table, int index) => new(BinaryPrimitives.ReadInt16LittleEndian(table.AsSpan().Slice(index * 8, 2)), BinaryPrimitives.ReadInt16LittleEndian(table.AsSpan().Slice(index * 8 + 2, 2)), BinaryPrimitives.ReadInt16LittleEndian(table.AsSpan().Slice(index * 8 + 4, 2)));
    }
}
