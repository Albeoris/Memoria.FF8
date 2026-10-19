using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleSandStorm176Controller
{
    private sealed partial class Execution
    {
        private static int Mul(int a, int b) => unchecked(a * b) >> 12;
        private static int Sin(int value) => NativeBattleCameraMath.Sine(value);
        private static int Cos(int value) => NativeBattleCameraMath.Cosine(value);

        private void InitializeFunnel(bool second)
        {
            uint packets = second ? 0x236E3C8u : 0x23693C8u;
            for (int page = 0; page < 2; page++)
            {
                for (int ring = 0; ring < 8; ring++)
                {
                    int lower = unchecked((byte)(ring * 31));
                    int upper = ring == 7 ? 255 : unchecked((byte)(lower + 31));
                    int left = 63;
                    int right = 0;
                    for (int segment = 0; segment < 8; segment++)
                    {
                        left ^= 63;
                        right ^= 63;
                        for (int copy = 0; copy < 2; copy++)
                        {
                            uint packet = packets + (uint)(page * 0x2800 + ring * 0x500 + segment * 80 + copy * 40);
                            D(packet, 0x09000000);
                            D(packet + 4, 0x2E808080);
                            Byte(packet + 12, left);
                            Byte(packet + 28, left);
                            Byte(packet + 20, right);
                            Byte(packet + 36, right);
                            Byte(packet + 13, 255 - upper);
                            Byte(packet + 21, 255 - upper);
                            Byte(packet + 29, 255 - lower);
                            Byte(packet + 37, 255 - lower);
                            W(packet + 14, page == 0 ? 0x3F54 : 0x3F94);
                            W(packet + 22, page == 0 ? 0xB9 : 0xB8);
                        }
                    }
                }
            }
            uint rings = second ? 0x23743F8u : 0x23742A0u;
            for (int ring = 0; ring < 17; ring++)
            {
                uint address = rings + (uint)(ring * 20);
                W(address, 0);
                W(address + 2, ring * 100 / 16);
                W(address + 4, 0);
                D(address + 16, 300);
            }
            uint head = second ? 0x23733D8u : 0x23733C8u;
            for (int axis = 0; axis < 3; axis++)
            {
                W(head + (uint)(axis * 2), 0);
                W(head + 8 + (uint)(axis * 2), 0);
            }
        }

        private void Byte(uint address, int value) => SetModule(State.Module.Write(checked((int)(address - ModuleBase)), new[] { unchecked((byte)value) }));

        private static int[] Parameters(int age)
        {
            int t;
            if (age < 26)
            {
                t = (age << 12) / 26;
                return [Mul(t, 200) + 100, Mul(t, 500) + 300, Mul(t, 100), 4096, 0, 0];
            }
            if (age < 46)
            {
                t = ((age - 26) << 12) / 20;
                return [Mul(t, 600) + 300, Mul(t, 700) + 800, Mul(t, 100) + 100, Mul(t, -2176) + 4096, Mul(t, 1024), 0];
            }
            if (age < 56)
            {
                t = ((age - 46) << 12) / 10;
                return [Mul(4096 - Sin((age - 46) * 102 + 1024), 5100) + 900, Mul(t, 300) + 1500, Mul(t, 800) + 200, Mul(t, -512) + 1920, Mul(t, 1024) + 1024, 0];
            }
            if (age < 72)
            {
                t = ((age - 56) << 12) / 16;
                return [6000, 1800, 1000, Mul(t, -256) + 1408, 2048, Mul(t, 256)];
            }
            if (age < 88)
            {
                t = ((age - 72) << 12) / 16;
                return [Mul(t, 4000) + 6000, Mul(t, 2600) + 1800, Mul(t, -300) + 1000, 1152, Mul(t, -1024) + 2048, 256];
            }
            throw new InvalidDataException("Native Sand Storm funnel passed its terminal counter.");
        }

        private int Funnel(ref NativeBattleModulePoolTask task)
        {
            int age = S(task, 12);
            int[] parameters = Parameters(age);
            bool second = S(task, 14) != 0;
            NativeBattleVector origin = TaskPosition(task, 16);
            short x = origin.X;
            short z = origin.Z;
            if (age >= 26)
            {
                int fraction = ((age - 26) << 12) / 38;
                x = unchecked((short)(Mul(W(0x2374298) - origin.X, fraction) + origin.X));
                z = unchecked((short)(Mul(W(0x237429C) - origin.Z, fraction) + origin.Z));
                NativeBattleActionMatrix world = inputs.ReadModel(Caster).GeometryWorldMatrix ?? throw new NotSupportedException("Native Sand Storm sway requires its caster's retained native matrix.");
                NativeBattleVector sway = NativeBattleGfMatrices.RotateShort56C4F0(world, new(unchecked((short)Mul(Sin(S(task, 32)), S(task, 28))), 0, 0));
                x = unchecked((short)(x + sway.X));
                z = unchecked((short)(z + sway.Z));
                if (!inputs.DrawOnly)
                {
                    short speed = S(task, 34);
                    task = task.WriteWord(28, unchecked((ushort)(S(task, 28) + S(task, 30)))).WriteWord(32, unchecked((ushort)(S(task, 32) + speed))).WriteWord(34, unchecked((ushort)(speed - (speed >> 5))));
                }
            }
            uint point = second ? 0x23685B0u : 0x23685A8u;
            W(point, x);
            W(point + 4, z);
            W(point + 6, parameters[1]);
            uint rings = second ? 0x23743F8u : 0x23742A0u;
            uint head = second ? 0x23733D8u : 0x23733C8u;
            for (int ring = 16; ring >= 0; ring--)
            {
                uint address = rings + (uint)(ring * 20);
                for (int axis = 0; axis < 3; axis++)
                {
                    W(address + 8 + (uint)(axis * 2), ring > 0 ? Roll() % 128 + W(address - 20 + 8 + (uint)(axis * 2)) - 64 : W(head + (uint)(axis * 2)));
                }
                if (ring == 0)
                {
                    for (int axis = 0; axis < 3; axis++)
                    {
                        W(head + 8 + (uint)(axis * 2), W(head + 8 + (uint)(axis * 2)) + Roll() % 512 - 256);
                    }
                    for (int axis = 0; axis < 3; axis++)
                    {
                        W(head + (uint)(axis * 2), W(head + (uint)(axis * 2)) + (unchecked(Sin(W(head + 8 + (uint)(axis * 2))) << 9) >> 12));
                    }
                }
                int fraction = (ring << 12) / 16;
                NativeBattleVector angles = new(unchecked((short)Mul(Sin(W(address + 8)), 288)), unchecked((short)Mul(Sin(W(address + 10)), 288)), unchecked((short)Mul(Sin(W(address + 12)), 288)));
                int radius = (unchecked(fraction * 7 << 9) >> 12) + 512;
                NativeBattleVector moved = NativeBattleGfMatrices.RotateShort56C4F0(NativeBattleGfMatrices.Euler56CE30(angles), new(unchecked((short)(unchecked(-radius * parameters[2]) >> 12)), 0, 0));
                W(address, moved.X);
                W(address + 2, unchecked((short)(moved.Y >> 4)) - Mul(fraction, parameters[0]));
                W(address + 4, moved.Z);
                int phase = Mul(fraction, parameters[4]) + parameters[5];
                int wave = 4096 - Mul(4096 - parameters[3], Sin(Sin(phase) >> 2));
                D(address + 16, unchecked((uint)Mul(wave, parameters[1])));
            }
            NativeBattleActionMatrix funnelMatrix = State.FunnelMatrix ?? throw new NotSupportedException("Native Sand Storm requires its retained funnel matrix.");
            funnelMatrix = funnelMatrix with { X = x, Y = 0, Z = z };
            current = current with { Memory = current.Memory with { SandStorm176 = State with { FunnelMatrix = funnelMatrix } } };
            DrawFunnel(rings, second ? 0x236E3C8u : 0x23693C8u, second ? -age * 100 : age * 100, age < 16 ? age * 8 : age >= 80 ? (88 - age) << 4 : 128, NativeBattleGfMatrices.Compose56C2F0(inputs.Projection.View ?? throw new NotSupportedException("Native Sand Storm requires its published camera."), funnelMatrix));
            if (inputs.DrawOnly)
            {
                return 0;
            }
            task = task.WriteWord(12, unchecked((ushort)(age + 1)));
            return age + 1 >= 88 ? 2 : 0;
        }

        private void DrawFunnel(uint rings, uint packets, int angle, int alpha, NativeBattleActionMatrix matrix)
        {
            ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
            NativeBattleVector[] previous = RingVertices(rings, angle);
            uint color = 0x2E000000u | (uint)(alpha | alpha << 8 | alpha << 16);
            for (int ring = 1; ring < 17; ring++)
            {
                angle += 64;
                NativeBattleVector[] vertices = RingVertices(rings + (uint)(ring * 20), angle);
                for (int segment = 0; segment < 8; segment++)
                {
                    uint address = packets + (uint)(((ring - 1) * 32 + segment * 2 + parity) * 40);
                    byte[] data = State.Module.Read(checked((int)(address - ModuleBase)), 40);
                    NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[40];
                    BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), color);
                    Array.Fill(mask, NativeBattleGfPacketByteState.Known, 4, 4);
                    NativeBattleModuleProjectedVertex[] projected = [NativeBattleModuleProjection.Project45FE10(matrix, vertices[segment], inputs.Projection), NativeBattleModuleProjection.Project45FE10(matrix, vertices[segment + 1], inputs.Projection), NativeBattleModuleProjection.Project45FE10(matrix, previous[segment], inputs.Projection), NativeBattleModuleProjection.Project45FE10(matrix, previous[segment + 1], inputs.Projection)];
                    NativeBattleGeometryPrimitive? primitive = null;
                    if (((projected[0].Flags | projected[1].Flags | projected[2].Flags) & 0x60000) == 0)
                    {
                        for (int corner = 0; corner < 4; corner++)
                        {
                            int offset = 8 + corner * 8;
                            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), projected[corner].Screen);
                            Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, 4);
                        }
                        int bucket = 17 + (projected.Sum(vertex => (int)vertex.Depth) >> 6);
                        ImmutableArray<uint> words = Enumerable.Range(0, 10).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(index * 4))).ToImmutableArray();
                        primitive = new(bucket, words, projected.Select(vertex => (int)vertex.Depth).ToImmutableArray());
                    }
                    attempts.Add(new(checked((int)(address - ModuleBase)), data.ToImmutableArray(), mask.ToImmutableArray(), primitive));
                }
                previous = vertices;
            }
            Emit(new NativeBattleSandStorm176Funnel(new(attempts.ToImmutable(), 0)));
        }

        private NativeBattleVector[] RingVertices(uint ring, int angle)
        {
            NativeBattleVector[] points = new NativeBattleVector[9];
            int radius = unchecked((int)D(ring + 16));
            for (int index = 0; index < 8; index++)
            {
                int phase = angle + index * 512;
                points[index] = new(unchecked((short)(unchecked((short)Mul(Cos(phase), radius)) + W(ring))), W(ring + 2), unchecked((short)(unchecked((short)Mul(Sin(phase), radius)) + W(ring + 4))));
            }
            points[8] = points[0];
            return points;
        }
    }
}
