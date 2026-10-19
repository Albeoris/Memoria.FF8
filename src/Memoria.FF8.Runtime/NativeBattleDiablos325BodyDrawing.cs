using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDiablos325BodyDraw(NativeBattleEffectMemory Memory, NativeBattleGfGeometryBatch Packets);

public static class NativeBattleDiablos325BodyDrawing
{
    public static NativeBattleDiablos325BodyDraw Draw6574F0(NativeBattleEffectMemory memory, NativeBattleModulePoolTask task, NativeBattleCameraProjection projection, int height = -8192, short flags = 2, short distance = 128)
    {
        NativeBattleEffectArena source = memory.Arena ?? throw new NotSupportedException("Native Diablos body drawing requires its current model arena.");
        using NativeBattleEffectArenaTransaction? owned = source.Transaction is null ? new(source) : null;
        NativeBattleEffectArena arena = owned?.View ?? source;
        int skeleton = NativeBattleWaterExecutionFields.ArenaOffset(task.ReadDword(0x244), 1);
        int count = arena.Read(skeleton, 1)[0];
        short[] rotation = new short[9];
        for (int index = 0; index < rotation.Length; index++)
        {
            rotation[index] = unchecked((short)task.ReadWord(0x14C + index * 2));
        }
        NativeBattleActionMatrix model = new(rotation.ToImmutableArray(), unchecked((int)task.ReadDword(0x160)), unchecked((int)task.ReadDword(0x164)), unchecked((int)task.ReadDword(0x168)), 4096);
        NativeBattleActionMatrix[] bones = new NativeBattleActionMatrix[count];
        for (int index = 0; index < count; index++)
        {
            int offset = checked(skeleton + 32 + index * 48);
            NativeBattleActionMatrix bone = NativeBattleArenaPose.ReadMatrix(arena, offset);
            bones[index] = NativeBattlePoseMatrices.ComposeStageCamera([bone], model, 0)[0];
            arena = NativeBattleArenaPose.WriteMatrix(arena, offset, bones[index], true);
        }
        uint geometry = task.ReadDword(0x248);
        ImmutableArray<NativeBattleRawMesh> meshes = NativeBattleRawGeometryReader.Read((offset, length) => arena.Read(NativeBattleWaterExecutionFields.ArenaOffset(unchecked(geometry + (uint)offset), length), length), count);
        uint mask = task.ReadDword(0x188);
        int scratch = NativeBattleWaterExecutionFields.ArenaOffset(checked(NativeBattleDiablos325Entry.RequireState(memory).ModelBuffer5208 + 0xD94), 0);
        uint color = task.ReadDword(0x134);
        NativeBattleScriptedGraphicsMemory registers = memory.ScriptedGraphics ?? throw new NotSupportedException("Native Diablos body requires its current average-depth scale controls.");
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int index = 0; index < meshes.Length; index++)
        {
            if ((mask & (1u << (index & 31))) == 0)
            {
                continue;
            }
            NativeBattleRawMesh mesh = meshes[index];
            arena = NativeBattleDiablos325BodyProjection.Project657B50(arena, scratch, mesh.Vertices.Length, vertex => NativeBattleGfMatrices.Transform460840(bones[mesh.Vertices[vertex].Bone], mesh.Vertices[vertex].Position), projection, height, flags, distance);
            foreach (NativeBattleRawPolygon polygon in mesh.Polygons)
            {
                short zsf = (polygon.Quad ? registers.Zsf4 : registers.Zsf3) ?? throw new NotSupportedException("Native Diablos body requires its actual selected average-depth scale.");
                NativeBattleGfGeometryAttempt? packet = NativeBattleDiablos325BodyPackets.Emit6576A0(polygon.Bytes.AsSpan(), polygon.Quad, ReadVertex, color, zsf, allocated);
                if (packet is not null)
                {
                    attempts.Add(packet);
                    allocated = checked(allocated + packet.Data.Length);
                }
            }
        }
        NativeBattleDecodedPose pose = NativeBattleArenaPose.ReadCurrentPose(arena, skeleton, count);
        arena = NativeBattleArenaPose.RestoreBoneMatrices(arena, skeleton, pose);
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native Diablos body drawing requires its current temporary memory owner.");
        process = process with { Temporary5082B0Word4 = checked(NativeBattleDiablos325Entry.RequireState(memory).ModelBuffer5208 + 0xD94) };
        return new(memory with { Arena = owned?.Freeze() ?? arena, PrimitiveProcess = process }, new(attempts.ToImmutable(), allocated));

        NativeBattleProjectedVertex ReadVertex(int index)
        {
            byte[] bytes = arena.Read(checked(scratch + index * 8), 8);
            return new(BinaryPrimitives.ReadUInt32LittleEndian(bytes), BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)));
        }
    }
}
