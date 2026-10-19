using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleDiablos325Puffs
{
    public static NativeBattleDiablos325Frame SpawnModel658D90(NativeBattleDiablos325Frame frame, NativeBattleModulePoolTask task)
    {
        NativeBattleEffectArena source = frame.Memory.Arena ?? throw new NotSupportedException("Native Diablos model puffs require their current model arena.");
        NativeBattleDiablos325State state = NativeBattleDiablos325Entry.RequireState(frame.Memory);
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Diablos model puffs require their actual temporary memory owner.");
        uint mask = task.ReadDword(0x188);
        process = process with { Temporary5082B0Word4 = mask };
        short[] rotation = Enumerable.Range(0, 9).Select(index => unchecked((short)task.ReadWord(0x14C + index * 2))).ToArray();
        NativeBattleActionMatrix world = new(rotation.ToImmutableArray(), unchecked((int)task.ReadDword(0x160)), unchecked((int)task.ReadDword(0x164)), unchecked((int)task.ReadDword(0x168)), 4096);
        int skeleton = NativeBattleWaterExecutionFields.ArenaOffset(task.ReadDword(0x244), 1);
        int count = source.Read(skeleton, 1)[0];
        ImmutableArray<NativeBattleActionMatrix> bones = Enumerable.Range(0, count).Select(index => NativeBattleGfMatrices.Compose56C2F0(world, NativeBattleArenaPose.ReadMatrix(source, skeleton + 32 + index * 48))).ToImmutableArray();
        uint geometry = task.ReadDword(0x248);
        ImmutableArray<NativeBattleRawMesh> meshes = NativeBattleRawGeometryReader.Read((offset, length) => source.Read(NativeBattleWaterExecutionFields.ArenaOffset(unchecked(geometry + (uint)offset), length), length), count);
        NativeBattleEffectArenaWriter writer = new(source);
        int vertexIndex = 0;
        int particles = 0;
        short centerX = unchecked((short)task.ReadWord(0x160));
        short centerY = unchecked((short)(task.ReadWord(0x164) - 512));
        short centerZ = unchecked((short)task.ReadWord(0x168));
        for (int mesh = 0; mesh < meshes.Length && particles < 128; mesh++)
        {
            if ((mask & (1u << (mesh & 31))) == 0)
            {
                continue;
            }
            foreach (NativeBattleRawVertex vertex in meshes[mesh].Vertices)
            {
                if ((vertexIndex & 7) == 0)
                {
                    NativeBattleVector point = NativeBattleGfMatrices.Transform460840(bones[vertex.Bone], vertex.Position);
                    int phase = vertexIndex >> 2;
                    byte[] bytes = new byte[16];
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0xFD67A0);
                    BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), point.X);
                    BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(6), point.Y);
                    BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), point.Z);
                    bytes[10] = unchecked((byte)((phase & 31) - 16));
                    bytes[11] = unchecked((byte)(64 - (phase & 15)));
                    bytes[12] = unchecked((byte)((point.X - centerX) >> 4));
                    bytes[13] = unchecked((byte)((point.Y - centerY) >> 4));
                    bytes[14] = unchecked((byte)((point.Z - centerZ) >> 4));
                    bytes[15] = unchecked((byte)(64 + phase % 3));
                    writer.Write(BaseOffset + particles * 16, bytes);
                    particles++;
                    if (particles == 128)
                    {
                        break;
                    }
                }
                vertexIndex++;
            }
        }
        return frame with { Memory = frame.Memory with { Arena = writer.Freeze(), PrimitiveProcess = process, Diablos325 = state with { Mode51F0 = 3 } } };
    }
}
