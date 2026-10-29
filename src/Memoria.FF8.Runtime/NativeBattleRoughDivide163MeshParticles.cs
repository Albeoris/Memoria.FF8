using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRoughDivide163Controller
{
    public static NativeBattleRoughDivide163ChildStep AdvanceMeshParticles(NativeBattleRoughDivide163Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRoughDivide163Inputs inputs, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        uint callback = task.Callback;
        uint flag = callback switch
        {
            0x5F6CA0 => 1,
            0x5F7000 => 2,
            0x5F81D0 => 8,
            0x5F8D10 => 4,
            _ => throw new NotSupportedException("Native Rough Divide mesh-particle callback is unsupported.")
        };
        uint bank = callback == 0x5F8D10 ? 0x23CBDF8u : 0x23CAB38u;
        uint mesh = callback switch
        {
            0x5F6CA0 => 0xD9D314,
            0x5F81D0 => 0xD9FE54,
            _ => 0xD9E1DC
        };
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        int counter = Word(task, 12);
        bool spawn = callback == 0x5F81D0 ? counter < 10 : counter <= (callback == 0x5F8D10 ? 5 : 10);
        if (!paused && spawn)
        {
            int requested = callback == 0x5F6CA0 ? 4 : callback == 0x5F7000 ? 3 : callback == 0x5F8D10 ? 6 : 1;
            for (int index = 0; index < requested; index++)
            {
                uint record = FreeParticle(frame.Staging, bank, 150, 32);
                if (record == 0)
                {
                    break;
                }
                NativeBattleSquallFinisherVisualIdentity.Allocated(163, task, record);
                NativeBattleFileStaging staging = WriteWord(WriteDword(frame.Staging, record, flag), record + 4, 0);
                if (callback == 0x5F81D0)
                {
                    uint source = checked(0x23CE318u + (uint)counter * 56);
                    staging = WriteWord(staging, record + 6, unchecked((ushort)counter));
                    staging = Copy(staging, source, record + 8, 8);
                    int speed = unchecked((short)((counter + 2) << 9)) / 3;
                    staging = WriteWord(WriteWord(staging, record + 18, unchecked((ushort)speed)), record + 22, unchecked((ushort)speed));
                    int dx = ReadWord(staging, source + 56) - ReadWord(staging, source);
                    int dz = ReadWord(staging, source + 60) - ReadWord(staging, source + 4);
                    var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(dx, 0, dz);
                    int squared = NormalizedSquare(dx, 0, dz);
                    if (squared < 0)
                    {
                        throw new NotSupportedException("Native5F81D0 reaches an unsupported exceptional signed56BEC0 square root.");
                    }
                    int length = unchecked((int)(long)Math.Sqrt(squared));
                    NativeBattleModuleAxisAngle alignment = NativeBattleModuleAlignment.Between571480(-4096, 0, 0, direction.X, direction.Y, direction.Z);
                    NativeBattleActionMatrix basis = new(NativeBattleModuleAlignment.Rotation5714F0(alignment.Angle, alignment.Axis), 0, 0, 0, 4096);
                    for (int component = 0; component < 9; component++)
                    {
                        staging = WriteWord(staging, source + 24 + (uint)component * 2, unchecked((ushort)basis.Rotation[component]));
                    }
                    staging = WriteWord(staging, record + 16, unchecked((ushort)(unchecked(length << 12) / 700)));
                }
                else
                {
                    uint origin = callback == 0x5F8D10 ? 0x23CE280u : 0x23CD318u;
                    int scale = callback == 0x5F6CA0 ? Rand() % 640 + 640 : callback == 0x5F7000 ? Rand() % 512 + 512 : Rand() % 2560 + 256;
                    staging = WriteWord(staging, record + 6, unchecked((ushort)scale));
                    staging = Copy(staging, origin, record + 8, 8);
                    if (callback == 0x5F8D10)
                    {
                        staging = WriteWord(staging, record + 8, unchecked((ushort)(ReadWord(staging, record + 8) + Rand() % 4000 - 2000)));
                        staging = WriteWord(staging, record + 10, unchecked((ushort)(-100 - Rand() % 300)));
                        staging = WriteWord(staging, record + 12, unchecked((ushort)(ReadWord(staging, record + 12) + Rand() % 4000 - 2000)));
                        staging = WriteWord(staging, record + 14, unchecked((ushort)scale));
                        staging = WriteWord(staging, record + 18, unchecked((ushort)(-60 - Rand() % 420)));
                    }
                    else
                    {
                        if (callback == 0x5F7000)
                        {
                            staging = WriteWord(staging, record + 14, unchecked((ushort)scale));
                        }
                        int range = callback == 0x5F6CA0 ? 8192 : 5120;
                        int dx = Rand() % range - range / 2;
                        int dy = -(Rand() % 2048);
                        int dz = Rand() % range - range / 2;
                        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(dx, dy, dz);
                        if (callback == 0x5F6CA0)
                        {
                            int offset = Rand() % 40 + 30;
                            staging = WriteWord(staging, record + 8, unchecked((ushort)(ReadWord(staging, record + 8) + ((offset * direction.X) >> 12))));
                            staging = WriteWord(staging, record + 10, unchecked((ushort)(ReadWord(staging, record + 10) + ((offset * direction.Y) >> 12))));
                            staging = WriteWord(staging, record + 12, unchecked((ushort)(ReadWord(staging, record + 12) + ((offset * direction.Z) >> 12))));
                        }
                        int speed = callback == 0x5F6CA0 ? Rand() % 240 + 220 : Rand() % 220 + 40;
                        staging = WriteWord(staging, record + 16, unchecked((ushort)((speed * direction.X) >> 12)));
                        staging = WriteWord(staging, record + 18, unchecked((ushort)((speed * direction.Y) >> 12)));
                        staging = WriteWord(staging, record + 20, unchecked((ushort)((speed * direction.Z) >> 12)));
                        if (callback == 0x5F6CA0)
                        {
                            staging = WriteWord(staging, record + 24, unchecked((ushort)direction.X));
                            staging = WriteWord(staging, record + 26, unchecked((ushort)direction.Y));
                            staging = WriteWord(staging, record + 28, unchecked((ushort)direction.Z));
                        }
                    }
                    if (callback != 0x5F6CA0)
                    {
                        staging = WriteWord(staging, record + 24, unchecked((ushort)(Rand() % 4096)));
                        int speed = callback == 0x5F7000 ? Rand() % 40 + 30 : Rand() % 150 + 100;
                        if ((speed & 1) != 0)
                        {
                            speed = -speed;
                        }
                        staging = WriteWord(staging, record + 26, unchecked((ushort)speed));
                        staging = WriteWord(staging, record + 28, unchecked((ushort)(Rand() % 4096)));
                        staging = WriteWord(staging, record + 30, unchecked((ushort)(callback == 0x5F7000 ? Rand() % 60 + 20 : Rand() % 130 + 60)));
                    }
                }
                frame = frame with { Staging = staging };
            }
        }
        int alive = 0;
        uint drawFlags = callback is 0x5F7000 or 0x5F8D10 ? 0u : 0x33u;
        int? fade = null;
        for (int index = 0; index < 150; index++)
        {
            uint record = bank + (uint)index * 32;
            NativeBattleFileStaging staging = frame.Staging;
            if ((ReadDword(staging, record) & flag) == 0)
            {
                continue;
            }
            short age = ReadWord(staging, record + 4);
            NativeBattleActionMatrix world;
            if (callback == 0x5F6CA0)
            {
                NativeBattleModuleAxisAngle alignment = NativeBattleModuleAlignment.Between571480(0, -4096, 0, ReadWord(staging, record + 24), ReadWord(staging, record + 26), ReadWord(staging, record + 28));
                world = new(NativeBattleModuleAlignment.Rotation5714F0(alignment.Angle, alignment.Axis), 0, 0, 0, 4096);
                int scale = ReadWord(staging, record + 6);
                world = NativeBattlePoseMatrices.Scale56BEF0(world, scale, scale, scale);
                if (age >= 1)
                {
                    drawFlags = 0xF3;
                    fade = 682 * age - 682;
                }
            }
            else if (callback == 0x5F81D0)
            {
                uint source = checked(0x23CE330u + (uint)ReadWord(staging, record + 6) * 56);
                world = ReadMatrix(staging, source, true);
                world = NativeBattlePoseMatrices.Scale56BEF0(world, ReadWord(staging, record + 16), ReadWord(staging, record + 18), 0);
                if (age >= 6)
                {
                    drawFlags = 0xF3;
                    fade = 682 * (age - 6);
                }
            }
            else
            {
                world = NativeBattleGfMatrices.Euler56CE30(new(ReadWord(staging, record + 24), 0, ReadWord(staging, record + 28)));
                int scale = ReadWord(staging, record + 6);
                world = NativeBattlePoseMatrices.Scale56BEF0(world, scale, scale, scale);
                if (age >= (callback == 0x5F7000 ? 1 : 8))
                {
                    int factor = callback == 0x5F7000 ? 4681 - 585 * age : 9552 - 682 * age;
                    staging = WriteWord(staging, record + 6, unchecked((ushort)((ReadWord(staging, record + 14) * factor) >> 12)));
                }
            }
            world = world with { X = ReadWord(staging, record + 8), Y = callback == 0x5F81D0 ? 0 : ReadWord(staging, record + 10), Z = ReadWord(staging, record + 12) };
            frame = frame with { Staging = staging };
            frame = DrawParticleMesh(frame, inputs, mesh, NativeBattlePoseMatrices.Compose56C2F0(view, world), projection, colors, new(drawFlags, 0, fade, null, null, null, 17, 2), NativeBattleSquallFinisherVisualIdentity.Particle(163, task, record, mesh));
            if (paused)
            {
                continue;
            }
            int lifetime = callback == 0x5F8D10 ? 14 : callback == 0x5F81D0 ? 12 : 7;
            if (age >= lifetime)
            {
                frame = frame with { Staging = WriteDword(frame.Staging, record, 0) };
                continue;
            }
            staging = WriteWord(frame.Staging, record + 4, unchecked((ushort)(age + 1)));
            if (callback == 0x5F81D0)
            {
                short speed = ReadWord(staging, record + 22);
                staging = WriteWord(staging, record + 18, unchecked((ushort)(ReadWord(staging, record + 18) + speed)));
                staging = WriteWord(staging, record + 22, unchecked((ushort)(speed - speed / 5)));
            }
            else if (callback == 0x5F8D10)
            {
                short speed = ReadWord(staging, record + 18);
                staging = WriteWord(staging, record + 10, unchecked((ushort)(ReadWord(staging, record + 10) + speed)));
                staging = WriteWord(staging, record + 18, unchecked((ushort)(speed - (speed >> 4))));
            }
            else
            {
                for (uint axis = 0; axis < 6; axis += 2)
                {
                    short speed = ReadWord(staging, record + 16 + axis);
                    staging = WriteWord(staging, record + 8 + axis, unchecked((ushort)(ReadWord(staging, record + 8 + axis) + speed)));
                    staging = WriteWord(staging, record + 16 + axis, unchecked((ushort)(speed - (speed >> (callback == 0x5F6CA0 ? 2 : 3)))));
                }
            }
            if (callback is 0x5F7000 or 0x5F8D10)
            {
                staging = WriteWord(staging, record + 24, unchecked((ushort)(ReadWord(staging, record + 24) + ReadWord(staging, record + 26))));
                staging = WriteWord(staging, record + 28, unchecked((ushort)(ReadWord(staging, record + 28) + ReadWord(staging, record + 30))));
            }
            frame = frame with { Staging = staging };
            alive++;
        }
        if (paused)
        {
            return new(frame, pool, 0);
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
        return new(frame, pool.WriteSlot(slot, task), alive == 0 ? 2 : 0);

        int Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
    }

    private static NativeBattleRoughDivide163Frame DrawParticleMesh(NativeBattleRoughDivide163Frame frame, NativeBattleRoughDivide163Inputs inputs, uint address, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattleModuleMeshContext context, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleStaticMeshResource source = frame.Memory.RoughDivide163?.RetainedResources?.Meshes.Single(mesh => mesh.Address == address) ?? throw new NotSupportedException("Native Rough Divide requires its actually retained mesh resources.");
        ImmutableArray<byte> bytes = frame.Staging.Read(address, source.Bytes.Length).ToImmutableArray();
        return inputs.Execute(frame, new NativeBattleRoughDivide163Geometry(NativeBattleModulePackets.EmitBatch572200(bytes, 0, default, matrix, projection, context, colors, pilotIdentity: pilotIdentity)));
    }

    private static int NormalizedSquare(int x, int y, int z)
    {
        double square = (float)((double)z * (float)z + (double)(float)y * (float)y + (double)x * x);
        return unchecked((int)((uint)BitConverter.DoubleToInt64Bits(square + 4503601774854144d) + 0x80000000u));
    }

    private static NativeBattleActionMatrix ReadMatrix(NativeBattleFileStaging staging, uint address, bool rotationOnly = false)
    {
        ImmutableArray<short> rotation = Enumerable.Range(0, 9).Select(index => ReadWord(staging, address + (uint)index * 2)).ToImmutableArray();
        return rotationOnly ? new(rotation, 0, 0, 0, 4096) : new(rotation, unchecked((int)ReadDword(staging, address + 20)), unchecked((int)ReadDword(staging, address + 24)), unchecked((int)ReadDword(staging, address + 28)), 4096);
    }
}
