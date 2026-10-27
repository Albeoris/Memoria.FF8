namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGilgameshFinalRingStep(NativeBattleModulePoolTask UpdatedTask, NativeBattleGfGeometryBatch Batch, int Result);

/// <summary>591F50's delayed finite ring, exact screen-independent view composition and signed scale damping.</summary>
public static class NativeBattleGilgameshFinalRing
{
    public static NativeBattleGilgameshFinalRingStep Step591F50(NativeBattleModulePoolTask task, NativeBattleStaticMeshResource resource, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, uint graphicsFlags, NativeBattleSummonColors? colors = null)
    {
        ValidateTask(task);
        bool paused = (graphicsFlags & 0x201) != 0;
        if (Signed(task, 0x0E) > 0)
        {
            return new(paused ? task : task.WriteWord(0x0E, unchecked((ushort)(task.ReadWord(0x0E) - 1))), new([], 0), 0);
        }
        int counter = Signed(task, 0x0C);
        uint flags = counter >= 4 ? 0xF3u : 0x33u;
        int? fade = counter >= 4 ? unchecked(341 * (counter - 4)) : null;
        NativeBattleGfGeometryBatch batch = NativeBattleModulePackets.EmitBatch572200(resource.Bytes, 0, default, Matrix(task, view), projection, new(flags, 0, fade, null, null, null, 17, 2), colors ?? NativeBattleGteColorInterpolation.CodeConstants45F270);
        if (paused)
        {
            return new(task, batch, 0);
        }
        int xVelocity = Signed(task, 0x1E);
        int yVelocity = Signed(task, 0x22);
        task = task.WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) + xVelocity)));
        task = task.WriteWord(0x1E, unchecked((ushort)(xVelocity - xVelocity / 4)));
        task = task.WriteWord(0x20, unchecked((ushort)(task.ReadWord(0x20) + yVelocity)));
        task = task.WriteWord(0x22, unchecked((ushort)(yVelocity - yVelocity / 4)));
        task = task.WriteWord(0x0C, unchecked((ushort)(task.ReadWord(0x0C) + 1)));
        return new(task, batch, Signed(task, 0x0C) >= 16 ? 2 : 0);
    }

    public static NativeBattleActionMatrix Matrix(NativeBattleModulePoolTask task, NativeBattleActionMatrix view)
    {
        ValidateTask(task);
        int x = Signed(task, 0x1C);
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, Signed(task, 0x18), 0)), x, Signed(task, 0x20), x) with { X = Signed(task, 0x10), Y = 0, Z = Signed(task, 0x14) };
        return NativeBattleGfMatrices.Compose56C2F0(view, local);
    }

    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));

    private static void ValidateTask(NativeBattleModulePoolTask task)
    {
        if (task.Payload.IsDefault || task.PayloadLength != 24)
        {
            throw new InvalidDataException("Native591F50 requires its original raw0x24-byte task.");
        }
    }
}
