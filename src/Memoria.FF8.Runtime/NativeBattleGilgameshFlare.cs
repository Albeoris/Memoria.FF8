namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGilgameshFlareStep(NativeBattleModulePoolTask UpdatedTask, NativeBattleGfGeometryBatch Batch, int Result);

/// <summary>591470's direct screen-space Gouraud flare and signed task integration.</summary>
public static class NativeBattleGilgameshFlare
{
    public static NativeBattleGilgameshFlareStep Step591470(NativeBattleModulePoolTask task, NativeBattleGilgameshFlareResources resources, NativeBattleCameraProjection projection, uint graphicsFlags)
    {
        ValidateTask(task);
        resources.Validate();
        NativeBattleStaticMeshResource source = task.ReadWord(0x0E) == 0 ? resources.SourceA : resources.SourceB;
        NativeBattleActionMatrix matrix = Matrix(task);
        NativeBattleGfGeometryBatch batch = NativeBattleModulePackets.EmitBatch572200(source.Bytes, 0, default, matrix, projection with { FocalLength = 0x120 }, new(0x33, null, null, null, null, null, 17, 2));
        if ((graphicsFlags & 0x201) != 0)
        {
            return new(task, batch, 0);
        }
        if (Signed(task, 0x0C) < 6)
        {
            int velocity = Signed(task, 0x1E);
            task = task.WriteWord(0x1C, unchecked((ushort)(Signed(task, 0x1C) + velocity)));
            task = task.WriteWord(0x1E, unchecked((ushort)(velocity - velocity / 16)));
        }
        task = task.WriteWord(0x0C, unchecked((ushort)(task.ReadWord(0x0C) + 1)));
        return new(task, batch, Signed(task, 0x0C) >= 8 ? 2 : 0);
    }

    public static NativeBattleActionMatrix Matrix(NativeBattleModulePoolTask task)
    {
        ValidateTask(task);
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler56CE30(new(0, 0, Signed(task, 0x18)));
        return NativeBattlePoseMatrices.Scale56BEF0(matrix, Signed(task, 0x1C), Signed(task, 0x20), Signed(task, 0x20)) with { X = Signed(task, 0x10), Y = Signed(task, 0x12), Z = 0x120 };
    }

    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));

    private static void ValidateTask(NativeBattleModulePoolTask task)
    {
        if (task.PayloadLength != 0x24 - 12)
        {
            throw new InvalidDataException("Native591470 requires its original raw0x24-byte task.");
        }
    }
}
