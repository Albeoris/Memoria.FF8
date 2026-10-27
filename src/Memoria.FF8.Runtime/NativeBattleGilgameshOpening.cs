namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGilgameshOpeningInitialization(NativeBattleEffectTaskPool Pool, NativeCrtRandomState Random);
public sealed record NativeBattleGilgameshOpeningStep(NativeBattleModulePoolTask UpdatedTask, NativeBattleGfGeometryBatch Batch, int Result);

/// <summary>592D00's mandatory five opening tasks and finite592E90/592F90/5930C0 draw/update leaves.</summary>
public static class NativeBattleGilgameshOpening
{
    public static NativeBattleGilgameshOpeningInitialization Initialize592D00(NativeBattleEffectTaskPool pool, NativeCrtRandomState random)
    {
        if (pool.DescriptorAddress != 0x21FFD58 || pool.BankAddress != 0x21FFD68 || pool.Stride != 0x24 || pool.Slots.Length != 100)
        {
            throw new InvalidDataException("Native592D00 requires its actual raw opening-task bank.");
        }
        pool.Validate();
        Add(0x592E90, [(0x0C, 0), (0x10, 0), (0x12, 0xE4A8), (0x14, 0), (0x1C, 0x2400), (0x20, 0x3000)], false);
        Add(0x592F90, [(0x0C, 0), (0x0E, 0), (0x10, 0), (0x12, 0xE4A8), (0x14, 0), (0x1A, 4), (0x1C, 0x2000), (0x20, 0x3000), (0x22, 0)], true);
        Add(0x592F90, [(0x0C, 0), (0x0E, 1), (0x10, 0), (0x12, 0xE4A8), (0x14, 0), (0x1A, 8), (0x1C, 0x2000), (0x20, 0x3000), (0x22, 0x600)], true);
        Add(0x5930C0, [(0x0C, 0), (0x0E, 0), (0x10, 0), (0x12, 0xCD38), (0x14, 0), (0x1A, 4), (0x1C, 0x3400), (0x22, 0x800)], true);
        Add(0x5930C0, [(0x0C, 0), (0x0E, 1), (0x10, 0), (0x12, 0xC950), (0x14, 0), (0x1A, 0xFFFC), (0x1C, 0x3000), (0x22, 0x800)], true);
        return new(pool, random);

        void Add(uint callback, (int Offset, ushort Value)[] fields, bool randomAngle)
        {
            int slot;
            (pool, slot) = pool.Append(callback);
            NativeBattleModulePoolTask task = pool.Slots[slot];
            foreach ((int offset, ushort value) in fields)
            {
                task = task.WriteWord(offset, value);
            }
            if (randomAngle)
            {
                random = random.Draw(out ushort value);
                task = task.WriteWord(0x18, (ushort)(value % 4096));
            }
            pool = pool.WriteSlot(slot, task);
        }
    }

    public static NativeBattleGilgameshOpeningStep Step(NativeBattleModulePoolTask task, NativeBattleGilgameshOpeningResources resources, NativeBattleActionMatrix worldView, NativeBattleCameraProjection projection, uint graphicsFlags, NativeBattleSummonColors? colors = null)
    {
        ValidateTask(task);
        resources.Validate();
        bool baseTask = task.Callback == 0x592E90;
        NativeBattleStaticMeshResource mesh = task.Callback switch
        {
            0x592E90 => resources.BaseMesh,
            0x592F90 => task.ReadWord(0x0E) == 0 ? resources.RingA : resources.RingB,
            0x5930C0 => task.ReadWord(0x0E) == 0 ? resources.SpireA : resources.SpireB,
            _ => throw new NotSupportedException("Native Gilgamesh opening callback is not one of its three finite leaves.")
        };
        NativeBattleActionMatrix matrix = Matrix(task, worldView);
        NativeBattleModuleMeshContext context = new(baseTask ? 0u : 0xC3u, 0, baseTask ? null : Signed(task, 0x22), null, null, null, 17, baseTask ? (byte)1 : (byte)2);
        NativeBattleGfGeometryBatch batch = NativeBattleModulePackets.EmitBatch572200(mesh.Bytes, 0, default, matrix, projection, context, colors ?? NativeBattleGteColorInterpolation.CodeConstants45F270);
        if ((graphicsFlags & 0x201) != 0)
        {
            return new(task, batch, 0);
        }
        if (!baseTask)
        {
            task = task.WriteWord(0x18, unchecked((ushort)(task.ReadWord(0x18) + task.ReadWord(0x1A))));
        }
        task = task.WriteWord(0x0C, unchecked((ushort)(task.ReadWord(0x0C) + 1)));
        return new(task, batch, Signed(task, 0x0C) >= 68 ? 2 : 0);
    }

    public static NativeBattleActionMatrix Matrix(NativeBattleModulePoolTask task, NativeBattleActionMatrix worldView)
    {
        ValidateTask(task);
        bool baseTask = task.Callback == 0x592E90;
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(0, baseTask ? (short)0 : Signed(task, 0x18), 0));
        int scale = Signed(task, 0x1C);
        int scaleY = task.Callback == 0x5930C0 ? scale : Signed(task, 0x20);
        local = NativeBattlePoseMatrices.Scale56BEF0(local, scale, scaleY, scale) with { X = Signed(task, 0x10), Y = Signed(task, 0x12), Z = Signed(task, 0x14) };
        return NativeBattleGfMatrices.Compose56C2F0(worldView, local);
    }

    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));

    private static void ValidateTask(NativeBattleModulePoolTask task)
    {
        if (task.Payload.IsDefault || task.PayloadLength != 24 || task.Callback is not (0x592E90 or 0x592F90 or 0x5930C0))
        {
            throw new InvalidDataException("Native Gilgamesh opening requires its original raw0x24-byte leaf task.");
        }
    }
}
