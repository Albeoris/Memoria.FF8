using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDiablos325WingsDraw(NativeBattleEffectMemory Memory, NativeBattleModulePoolTask Task, NativeBattleGfGeometryBatch Packets);

public static class NativeBattleDiablos325Wings
{
    private static readonly int[] Lengths = [12, 12, 20, 24, 20, 24, 28, 36];

    public static NativeBattleDiablos325WingsDraw Advance654811(NativeBattleDiablos325Frame frame, NativeBattleModulePoolTask task, NativeBattleCameraProjection projection)
    {
        ushort counter = task.ReadWord(0xC);
        if (counter is < 91 or > 102)
        {
            throw new NotSupportedException("Native Diablos paired wings require their current reached timeline window.");
        }
        NativeBattleDiablos325State state = NativeBattleDiablos325Entry.RequireState(frame.Memory);
        if (counter == 91)
        {
            foreach (int offset in new[] { 0x14, 0x54 })
            {
                task = task.WriteDword(offset, checked(state.ModelBuffer5208 + 0x3505C));
                task = task.WriteDword(offset + 4, checked(state.ModelBuffer5208 + 0x3704C));
                task = task.WriteDword(offset + 8, checked(state.ModelBuffer5208 + 0x3505C));
            }
        }
        int ratio = (counter - 91) * 4096 / 12;
        int product = unchecked(2 * ratio);
        int index = product >> 12;
        int fraction = product & 0xFFF;
        NativeBattleEffectArena source = frame.Memory.Arena ?? throw new NotSupportedException("Native Diablos paired wings require their current arena.");
        using NativeBattleEffectArenaTransaction? owned = source.Transaction is null ? new(source) : null;
        NativeBattleEffectArena arena = owned?.View ?? source;
        uint vertices = checked(state.ModelBuffer5208 + 0xD94);
        uint geometry = checked(state.ModelBuffer5208 + 0x4C244);
        arena = NativeBattleDiablos325WingMorph.Vertices6590E0(arena, task.ReadDword(0x14 + index * 4), task.ReadDword(0x18 + index * 4), fraction, vertices);
        arena = NativeBattleDiablos325WingMorph.Polygons659200(arena, task.ReadDword(0x54 + index * 4), task.ReadDword(0x58 + index * 4), fraction, geometry);
        ImmutableArray<byte> data = ReadGeometry(arena, geometry);
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Diablos paired wings require their current view.");
        NativeBattleActionMatrix local = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, -8192, unchecked((short)state.FarZ51DC), 4096);
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(view, local);
        NativeBattleMeshPacketContext context = new(0x2000, null, null, null, null, null, 0, 17, 2);
        NativeBattleGfGeometryBatch batch = NativeBattleGfGeometryPackets.EmitBatch(data, 0, default, matrix, projection, context, null, Vertex, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(325, task, 0x6590E0));
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Diablos paired wings require their current renderer controls.");
        NativeBattleModelRendererRegisters registers = process.ModelRendererRegisters ?? new(null, null, null, null, null);
        process = process with { ModelRendererRegisters = registers with { FarRed = null, FarGreen = null, FarBlue = null } };
        return new(frame.Memory with { Arena = owned?.Freeze() ?? arena, PrimitiveProcess = process }, task, batch);

        NativeBattleVector Vertex(int offset)
        {
            byte[] xyz = arena.Read(NativeBattleWaterExecutionFields.ArenaOffset(unchecked(vertices + (uint)offset), 6), 6);
            return new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4)));
        }
    }

    internal static ImmutableArray<byte> ReadGeometry(NativeBattleEffectArena arena, uint source)
    {
        uint cursor = unchecked(source + Read(source));
        for (int group = 0; group < 8; group++)
        {
            int count = unchecked((int)Read(cursor));
            cursor = unchecked(cursor + 4);
            if (count > 0)
            {
                cursor = checked(cursor + (uint)(count * Lengths[group]));
            }
        }
        int length = checked((int)(cursor - source));
        return arena.Read(NativeBattleWaterExecutionFields.ArenaOffset(source, length), length).ToImmutableArray();

        uint Read(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(NativeBattleWaterExecutionFields.ArenaOffset(address, 4), 4));
    }
}
