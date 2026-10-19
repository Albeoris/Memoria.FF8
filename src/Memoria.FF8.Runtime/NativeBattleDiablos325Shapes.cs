using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDiablos325ShapeDraw(NativeBattleEffectMemory Memory, NativeBattleModulePoolTask Task, NativeBattleGfGeometryBatch Packets);

public static class NativeBattleDiablos325Shapes
{
    public static NativeBattleDiablos325ShapeDraw Advance654899(NativeBattleDiablos325Frame frame, NativeBattleModulePoolTask task, NativeBattleCameraProjection projection)
    {
        ushort counter = task.ReadWord(0xC);
        if (counter is >= 238 and <= 297)
        {
            return Advance654AAC(frame, task, projection);
        }
        if (counter is < 103 or > 202)
        {
            throw new NotSupportedException("Native Diablos current shape cycle requires its reached timeline window.");
        }
        NativeBattleDiablos325State state = NativeBattleDiablos325Entry.RequireState(frame.Memory);
        if (counter == 103 && state.Pause5180 == 0)
        {
            uint[] offsets = [0x3903C, 0x3B02C, 0x3D01C, 0x3F00C, 0x40FFC, 0x42FEC, 0x44FDC, 0x46FCC, 0x48FBC];
            for (int index = 0; index < offsets.Length; index++)
            {
                task = task.WriteDword(0x14 + index * 4, checked(state.ModelBuffer5208 + offsets[index]));
            }
        }
        int selected = counter < 173 ? (counter - 103) % 9 : 0;
        return Draw659680(frame.Memory, task, task.ReadDword(0x14 + selected * 4), new(0, -8192, unchecked((short)state.FarZ51DC)), projection);
    }

    public static NativeBattleDiablos325ShapeDraw Advance654AAC(NativeBattleDiablos325Frame frame, NativeBattleModulePoolTask task, NativeBattleCameraProjection projection)
    {
        ushort counter = task.ReadWord(0xC);
        if (counter is < 238 or > 297)
        {
            throw new ArgumentOutOfRangeException(nameof(task));
        }
        NativeBattleDiablos325State state = NativeBattleDiablos325Entry.RequireState(frame.Memory);
        if (counter == 238 && state.Pause5180 == 0)
        {
            for (int index = 0; index < 13; index++)
            {
                task = task.WriteDword(0x14 + index * 4, checked(state.ModelBuffer5208 + 0x3505Cu + (uint)index * 0x1FF0));
            }
        }
        int selected = (counter - 238) % 13;
        return Draw659680(frame.Memory, task, task.ReadDword(0x14 + selected * 4), new(90, -8792, unchecked((short)(state.FarZ51DC - 160))), projection);
    }

    public static NativeBattleDiablos325ShapeDraw Draw659680(NativeBattleEffectMemory memory, NativeBattleModulePoolTask task, uint source, NativeBattleVector position, NativeBattleCameraProjection projection)
    {
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native Diablos shape drawing requires its current arena bytes.");
        ImmutableArray<byte> data = NativeBattleDiablos325Wings.ReadGeometry(arena, source);
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Diablos shape drawing requires its current camera basis.");
        NativeBattleActionMatrix local = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], position.X, position.Y, position.Z, 4096);
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(view, local);
        NativeBattleMeshPacketContext context = new(0, null, null, null, null, null, 0, 17, 2);
        NativeBattleGfGeometryBatch packets = NativeBattleGfGeometryPackets.EmitBatch(data, 0, default, matrix, projection, context, null);
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native Diablos shape drawing requires its current renderer controls.");
        NativeBattleModelRendererRegisters registers = process.ModelRendererRegisters ?? new(null, null, null, null, null);
        process = process with { ModelRendererRegisters = registers with { FarRed = null, FarGreen = null, FarBlue = null } };
        return new(memory with { PrimitiveProcess = process }, task, packets);
    }
}
