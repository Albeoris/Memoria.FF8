namespace Memoria.FF8.Runtime;

public static partial class NativeBattleFatedCircle162Controller
{
    public static NativeBattleFatedCircle162ChildStep Advance5FA540(NativeBattleFatedCircle162Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleFatedCircle162Inputs inputs, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors) => AdvanceMeshParticles(frame, pool, slot, inputs, view, projection, colors, 2, null);
    public static NativeBattleFatedCircle162ChildStep Advance5FABD0(NativeBattleFatedCircle162Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleFatedCircle162Inputs inputs, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors) => AdvanceMeshParticles(frame, pool, slot, inputs, view, projection, colors, 4, null);
    public static NativeBattleFatedCircle162ChildStep Advance5FAF50(NativeBattleFatedCircle162Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleFatedCircle162Inputs inputs, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattleCameraPose camera) => AdvanceMeshParticles(frame, pool, slot, inputs, view, projection, colors, 8, camera);

    private static NativeBattleFatedCircle162ChildStep AdvanceMeshParticles(NativeBattleFatedCircle162Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleFatedCircle162Inputs inputs, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, uint flag, NativeBattleCameraPose? camera)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        bool motes = flag == 2;
        bool shells = flag == 4;
        bool streaks = flag == 8;
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        uint mesh = motes ? 0xDA3E48u : shells ? 0xDA4118u : 0xDA4308u;
        uint drawFlags = motes ? 3u : 0x33u;
        int? fade = null;
        int alive = 0;
        (int X, int Y, int Z) cameraDirection = default;
        if (streaks)
        {
            NativeBattleCameraPose pose = camera ?? throw new NotSupportedException("Native5FAF50 requires its actual current camera eye and target WORDs.");
            var normalized = NativeBattleActionGeometryRules.NormalizeVector56BC50(pose.Target.X - pose.Eye.X, pose.Target.Y - pose.Eye.Y, pose.Target.Z - pose.Eye.Z);
            cameraDirection = (normalized.X, normalized.Y, normalized.Z);
        }
        for (int index = 0; index < 80; index++)
        {
            uint address = 0x23CEA40u + (uint)index * 24;
            if ((unchecked((uint)ReadDword(frame.Staging, address)) & flag) == 0)
            {
                continue;
            }
            short age = ReadWord(frame.Staging, address + 4);
            int dx = ReadWord(frame.Staging, address + (shells ? 8u : 16u));
            int dy = ReadWord(frame.Staging, address + (shells ? 10u : 18u));
            int dz = ReadWord(frame.Staging, address + (shells ? 12u : 20u));
            var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(dx, dy, dz);
            NativeBattleModuleAxisAngle alignment = streaks ? NativeBattleModuleAlignment.Between571480(-4096, 0, 0, direction.X, direction.Y, direction.Z) : NativeBattleModuleAlignment.Between571480(0, -4096, 0, direction.X, direction.Y, direction.Z);
            NativeBattleActionMatrix world = new(NativeBattleModuleAlignment.Rotation5714F0(alignment.Angle, alignment.Axis), 0, 0, 0, 4096);
            int scale = ReadWord(frame.Staging, address + 6);
            if (shells)
            {
                NativeBattleVector axis = new(alignment.Axis.Z, alignment.Axis.Y, alignment.Axis.X);
                NativeBattleActionMatrix second = new(NativeBattleModuleAlignment.Rotation5714F0(ReadWord(frame.Staging, address + 14), axis), 0, 0, 0, 4096);
                world = NativeBattleGfMatrices.Multiply56C090(second, world);
                world = NativeBattlePoseMatrices.Scale56BEF0(world, scale, scale, scale) with { X = Word(task, 16), Y = Word(task, 18), Z = Word(task, 20) };
            }
            else if (motes)
            {
                world = NativeBattlePoseMatrices.Scale56BEF0(world, 1024, scale, 1024) with { X = ReadWord(frame.Staging, address + 8), Y = ReadWord(frame.Staging, address + 10), Z = ReadWord(frame.Staging, address + 12) };
            }
            else
            {
                world = NativeBattlePoseMatrices.Scale56BEF0(world, scale, 1408, 1408) with { X = unchecked(ReadWord(frame.Staging, address + 8) + ((800 * cameraDirection.X) >> 12)), Y = unchecked(ReadWord(frame.Staging, address + 10) + ((800 * cameraDirection.Y) >> 12)), Z = unchecked(ReadWord(frame.Staging, address + 12) + ((800 * cameraDirection.Z) >> 12)) };
            }
            if (age >= (streaks ? 1 : 4))
            {
                drawFlags |= 0xC0;
                fade = streaks ? (age << 11) - 2048 : (age - 4) << 10;
            }
            frame = DrawMesh(frame, inputs, mesh, NativeBattlePoseMatrices.Compose56C2F0(view, world), projection, colors, new(drawFlags, 0, fade, null, null, null, 17, 2));
            if (paused)
            {
                continue;
            }
            age = unchecked((short)(age + 1));
            frame = frame with { Staging = WriteWord(frame.Staging, address + 4, unchecked((ushort)age)) };
            if (age >= (streaks ? 3 : 8))
            {
                frame = frame with { Staging = WriteDword(frame.Staging, address, 0) };
                continue;
            }
            NativeBattleFileStaging staging = frame.Staging;
            if (shells)
            {
                staging = WriteWord(staging, address + 6, unchecked((ushort)(scale + scale / 6)));
                staging = WriteWord(staging, address + 14, unchecked((ushort)(ReadWord(staging, address + 14) + ReadWord(staging, address + 22))));
            }
            else
            {
                if (motes)
                {
                    staging = WriteWord(staging, address + 6, unchecked((ushort)(scale - (scale >> 4))));
                }
                for (int axis = 0; axis < 3; axis++)
                {
                    uint component = (uint)axis * 2;
                    short velocity = ReadWord(staging, address + 16 + component);
                    staging = WriteWord(staging, address + 8 + component, unchecked((ushort)(ReadWord(staging, address + 8 + component) + velocity)));
                    if (streaks)
                    {
                        staging = WriteWord(staging, address + 16 + component, unchecked((ushort)(velocity + (velocity >> 4))));
                    }
                }
            }
            frame = frame with { Staging = staging };
            alive++;
        }
        if (paused)
        {
            return new(frame, pool, 0);
        }
        int spawnUntil = motes ? 14 : shells ? 1 : 10;
        if (task.ReadWord(12) <= spawnUntil)
        {
            NativeBattleVector offset = new(0, 0, 0);
            if (streaks)
            {
                byte actor = frame.Memory.FatedCircle162?.Parameters?.Actor78 ?? throw new InvalidDataException("Native5FAF50 requires its entered caster.");
                NativeBattleActionMatrix body = inputs.ReadModel(actor).GeometryWorldMatrix ?? throw new NotSupportedException("Native5FAF50 requires its actual actor matrix64.");
                offset = NativeBattleGfMatrices.RotateShort56C4F0(body, new(0, 0, -2400));
            }
            int count = motes ? 3 : shells ? 10 : 8;
            for (int index = 0; index < count; index++)
            {
                int free = FreeParticle(frame.Staging, 0x23CEA40, 80);
                if (free < 0)
                {
                    break;
                }
                uint address = 0x23CEA40u + (uint)free * 24;
                NativeBattleFileStaging staging = WriteDword(frame.Staging, address, flag);
                staging = WriteWord(staging, address + 4, 0);
                staging = WriteWord(staging, address + 6, unchecked((ushort)(streaks ? Rand() % 5120 + 2048 : Rand() % 1536 + 1024)));
                if (shells)
                {
                    int x = Rand() % 4096 - 2048;
                    int y = Rand() % 4096 - 2048;
                    int z = Rand() % 4096 - 2048;
                    var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
                    staging = WriteWord(WriteWord(WriteWord(staging, address + 8, unchecked((ushort)direction.X)), address + 10, unchecked((ushort)direction.Y)), address + 12, unchecked((ushort)direction.Z));
                    staging = WriteWord(staging, address + 14, unchecked((ushort)(Rand() % 4096)));
                    staging = WriteWord(staging, address + 22, unchecked((ushort)(Rand() % 30 + 10)));
                }
                else if (motes)
                {
                    staging = staging.Write(address + 8, task.ReadRawPayloadSpan().Slice(4, 8));
                    int x = Rand() % 4096 - 2048;
                    int y = Rand() % 4096 - 2048;
                    int z = Rand() % 4096 - 2048;
                    var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
                    int radius = Rand() % 800 + 800;
                    staging = WriteWord(staging, address + 8, unchecked((ushort)(ReadWord(staging, address + 8) + (unchecked(direction.X * radius) >> 12))));
                    staging = WriteWord(staging, address + 10, unchecked((ushort)(ReadWord(staging, address + 10) + (unchecked(direction.Y * radius) >> 12))));
                    staging = WriteWord(staging, address + 12, unchecked((ushort)(ReadWord(staging, address + 12) + (unchecked(direction.Z * radius) >> 12))));
                    int speed = Rand() % 130 + 50;
                    staging = WriteWord(staging, address + 16, unchecked((ushort)(unchecked(-unchecked(direction.X * speed)) >> 12)));
                    staging = WriteWord(staging, address + 18, unchecked((ushort)(unchecked(-unchecked(direction.Y * speed)) >> 12)));
                    staging = WriteWord(staging, address + 20, unchecked((ushort)(unchecked(-unchecked(direction.Z * speed)) >> 12)));
                }
                else
                {
                    staging = WriteWord(staging, address + 8, unchecked((ushort)(Word(task, 16) + offset.X)));
                    staging = WriteWord(staging, address + 10, unchecked((ushort)(Word(task, 18) + offset.Y)));
                    staging = WriteWord(staging, address + 12, unchecked((ushort)(Word(task, 20) + offset.Z)));
                    staging = WriteWord(staging, address + 8, unchecked((ushort)(ReadWord(staging, address + 8) + Rand() % 500 - 250)));
                    staging = WriteWord(staging, address + 10, unchecked((ushort)(ReadWord(staging, address + 10) + Rand() % 500 - 250)));
                    staging = WriteWord(staging, address + 12, unchecked((ushort)(ReadWord(staging, address + 12) + Rand() % 500 - 250)));
                    var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(Word(task, 16) - ReadWord(staging, address + 8), Word(task, 18) - ReadWord(staging, address + 10), Word(task, 20) - ReadWord(staging, address + 12));
                    int speed = Rand() % 100 + 60;
                    staging = WriteWord(staging, address + 16, unchecked((ushort)(unchecked(direction.X * speed) >> 12)));
                    staging = WriteWord(staging, address + 18, unchecked((ushort)(unchecked(direction.Y * speed) >> 12)));
                    staging = WriteWord(staging, address + 20, unchecked((ushort)(unchecked(direction.Z * speed) >> 12)));
                }
                frame = frame with { Staging = staging };
            }
        }
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return new(frame, pool.WriteSlot(slot, task), Word(task, 12) < (streaks ? 3 : 8) || alive != 0 ? 0 : 2);

        int Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
    }
}
