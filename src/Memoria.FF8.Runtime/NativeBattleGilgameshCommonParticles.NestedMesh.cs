namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGilgameshNestedAppend(NativeBattleModulePoolTask UpdatedTask, NativeBattleEffectTaskPool Pool, NativeCrtRandomState Random, int Result);

public static partial class NativeBattleGilgameshCommonParticles
{
    public static NativeBattleGilgameshNestedAppend Step593C70(NativeBattleModulePoolTask task, NativeBattleEffectTaskPool pool, NativeCrtRandomState random, uint graphicsFlags)
    {
        ValidateTask(task);
        if (task.Callback != 0x593C70)
        {
            throw new NotSupportedException("Native593C70 requires its actual nested-mesh initializer callback.");
        }
        if ((graphicsFlags & 0x201) != 0)
        {
            return new(task, pool, random, 0);
        }
        int counter = Signed(task, 12);
        if (counter < 20 && counter % 8 == 1)
        {
            if (pool.DescriptorAddress != 0x21FFD58 || pool.BankAddress != 0x21FFD68 || pool.Stride != 0x24 || pool.Slots.Length != 100)
            {
                throw new InvalidDataException("Native593C70 requires its actual raw child task bank.");
            }
            pool.Validate();
            int slot;
            (pool, slot) = pool.Append(0x593D20);
            random = random.Draw(out ushort angle);
            NativeBattleModulePoolTask child = pool.Slots[slot].WriteWord(12, 0).WriteWord(16, 0).WriteWord(18, 0).WriteWord(20, 0).WriteWord(24, (ushort)(angle % 4096)).WriteWord(26, 50).WriteWord(28, 0x2400).WriteWord(32, 186).WriteWord(34, 186);
            pool = pool with { Slots = pool.Slots.SetItem(slot, child) };
        }
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return new(task, pool, random, Signed(task, 12) >= 30 ? 2 : 0);
    }

    public static NativeBattleGilgameshMeshAuxiliaryStep Step593D20(NativeBattleModulePoolTask task, NativeBattleGilgameshCommonParticleResources resources, NativeBattleActionMatrix worldView, NativeBattleCameraProjection projection, uint graphicsFlags, NativeBattleSummonColors? colors = null)
    {
        ValidateTask(task);
        if (task.Callback != 0x593D20)
        {
            throw new NotSupportedException("Native593D20 requires its actual finite nested-mesh callback.");
        }
        resources.Validate();
        int counter = Signed(task, 12);
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, Signed(task, 24), 0)), Signed(task, 28), Signed(task, 32), Signed(task, 28)) with { X = Signed(task, 16), Y = Signed(task, 18), Z = Signed(task, 20) };
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(worldView, local);
        NativeBattleModuleMeshContext context = new(counter >= 18 ? 0xF3u : 0x33u, 0, counter >= 18 ? 341 * (counter - 18) : null, null, null, null, 17, 2);
        NativeBattleGfGeometryBatch batch = NativeBattleModulePackets.EmitBatch572200(resources.NestedMesh.Bytes, 0, default, matrix, projection, context, colors ?? NativeBattleGteColorInterpolation.CodeConstants45F270);
        if ((graphicsFlags & 0x201) != 0)
        {
            return new(task, batch, 0);
        }
        int velocity = Signed(task, 34);
        task = task.WriteWord(24, unchecked((ushort)(task.ReadWord(24) + task.ReadWord(26))));
        task = task.WriteWord(32, unchecked((ushort)(task.ReadWord(32) + velocity)));
        task = task.WriteWord(34, unchecked((ushort)(velocity + velocity / 12)));
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return new(task, batch, Signed(task, 12) >= 30 ? 2 : 0);
    }
}
