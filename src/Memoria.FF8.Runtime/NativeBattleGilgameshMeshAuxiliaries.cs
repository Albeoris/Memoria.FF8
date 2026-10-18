namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGilgameshMeshAuxiliaryInitialization(NativeBattleEffectTaskPool Pool, NativeCrtRandomState Random);
public sealed record NativeBattleGilgameshMeshAuxiliaryStep(NativeBattleModulePoolTask UpdatedTask, NativeBattleGfGeometryBatch Batch, int Result);

/// <summary>593550/594160's mandatory finite mesh children and593600/594210's exact draw/update contracts.</summary>
public static class NativeBattleGilgameshMeshAuxiliaries
{
    public static NativeBattleGilgameshMeshAuxiliaryInitialization Initialize593550(NativeBattleEffectTaskPool pool, NativeCrtRandomState random)
    {
        ValidatePool(pool);
        random = random.Draw(out ushort value);
        int baseAngle = value % 4096;
        for (int index = 0; index < 4; index++)
        {
            int slot;
            (pool, slot) = pool.Append(0x593600);
            int angle = baseAngle + index * 1024;
            NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(12, 0).WriteWord(16, unchecked((ushort)(NativeBattleCameraMath.Sine(angle) * 1000 >> 12))).WriteWord(18, unchecked((ushort)-15000)).WriteWord(20, unchecked((ushort)(NativeBattleCameraMath.Cosine(angle) * 1000 >> 12))).WriteWord(24, unchecked((ushort)angle)).WriteWord(28, 1024).WriteWord(34, 90);
            pool = pool with { Slots = pool.Slots.SetItem(slot, task) };
        }
        return new(pool, random);
    }

    public static NativeBattleGilgameshMeshAuxiliaryInitialization Initialize594160(NativeBattleEffectTaskPool pool, NativeCrtRandomState random, NativeBattleGilgameshMeshAuxiliaryResources resources)
    {
        ValidatePool(pool);
        resources.Validate();
        foreach (NativeBattleGilgameshMeshAuxiliaryRow row in resources.Rows)
        {
            int slot;
            (pool, slot) = pool.Append(0x594210);
            random = random.Draw(out ushort angle);
            random = random.Draw(out ushort angularVelocity);
            random = random.Draw(out ushort yScale);
            int velocity = row.ScaleSource / 8;
            NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(12, 0).WriteWord(14, unchecked((ushort)row.Delay)).WriteWord(16, 0).WriteWord(18, unchecked((ushort)row.Y)).WriteWord(20, 0).WriteWord(24, (ushort)(angle % 4096)).WriteWord(26, (ushort)(angularVelocity % 60 + 20)).WriteWord(30, unchecked((ushort)velocity)).WriteWord(28, unchecked((ushort)(2 * velocity))).WriteWord(32, unchecked((ushort)(yScale % 1536 + row.YScaleBase)));
            pool = pool with { Slots = pool.Slots.SetItem(slot, task) };
        }
        return new(pool, random);
    }

    public static NativeBattleGilgameshMeshAuxiliaryStep Step(NativeBattleModulePoolTask task, NativeBattleGilgameshMeshAuxiliaryResources resources, NativeBattleActionMatrix worldView, NativeBattleCameraProjection projection, uint graphicsFlags, NativeBattleSummonColors? colors = null)
    {
        ValidateTask(task);
        bool delayed = task.Callback == 0x594210;
        bool paused = (graphicsFlags & 0x201) != 0;
        if (delayed && Signed(task, 14) > 0)
        {
            return new(paused ? task : task.WriteWord(14, unchecked((ushort)(task.ReadWord(14) - 1))), new([], 0), 0);
        }
        resources.Validate();
        int counter = Signed(task, 12);
        int? fade = delayed ? counter < 4 ? unchecked((4 - counter) << 10) : counter >= 6 ? unchecked((counter - 6) << 9) : null : counter < 6 ? unchecked(4096 - 682 * counter) : null;
        NativeBattleModuleMeshContext context = new(fade is null ? 0x33u : 0xF3u, 0, fade, null, null, null, 17, 2);
        NativeBattleGfGeometryBatch batch = NativeBattleModulePackets.EmitBatch572200((delayed ? resources.DelayedMesh : resources.RisingMesh).Bytes, 0, default, Matrix(task, worldView), projection, context, colors ?? NativeBattleGteColorInterpolation.CodeConstants45F270);
        if (paused)
        {
            return new(task, batch, 0);
        }
        int scale = Signed(task, 28);
        if (delayed)
        {
            int velocity = Signed(task, 30);
            task = task.WriteWord(24, unchecked((ushort)(task.ReadWord(24) + task.ReadWord(26))));
            task = task.WriteWord(28, unchecked((ushort)(scale + velocity)));
            task = task.WriteWord(30, unchecked((ushort)(velocity - velocity / 12)));
        }
        else
        {
            int velocity = Signed(task, 34);
            task = task.WriteWord(18, unchecked((ushort)(Signed(task, 18) + velocity)));
            task = task.WriteWord(28, unchecked((ushort)(scale + (scale >> 6))));
            task = task.WriteWord(34, unchecked((ushort)(velocity + velocity / 12)));
        }
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return new(task, batch, Signed(task, 12) >= (delayed ? 14 : 30) ? 2 : 0);
    }

    public static NativeBattleActionMatrix Matrix(NativeBattleModulePoolTask task, NativeBattleActionMatrix worldView)
    {
        ValidateTask(task);
        int scale = Signed(task, 28);
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, Signed(task, 24), 0)), scale, task.Callback == 0x594210 ? Signed(task, 32) : scale, scale) with { X = Signed(task, 16), Y = Signed(task, 18), Z = Signed(task, 20) };
        return NativeBattleGfMatrices.Compose56C2F0(worldView, local);
    }

    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));

    private static void ValidateTask(NativeBattleModulePoolTask task)
    {
        if (task.Payload.IsDefault || task.Payload.Length != 24 || task.Callback is not (0x593600 or 0x594210))
        {
            throw new InvalidDataException("Native Gilgamesh finite mesh requires its original raw0x24-byte child task.");
        }
    }

    private static void ValidatePool(NativeBattleEffectTaskPool pool)
    {
        if (pool.DescriptorAddress != 0x21FFD58 || pool.BankAddress != 0x21FFD68 || pool.Stride != 36 || pool.Slots.Length != 100)
        {
            throw new InvalidDataException("Native Gilgamesh mesh initialization requires its actual raw child bank.");
        }
        pool.Validate();
    }
}
