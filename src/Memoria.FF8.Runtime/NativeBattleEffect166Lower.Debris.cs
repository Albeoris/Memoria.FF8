using static Memoria.FF8.Runtime.NativeBattleEffect166Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect166Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect166Lower
{
    public static NativeBattleEffect166Step AdvanceDebris(NativeBattleEffect166Frame frame, uint address, NativeBattleEffect166Resources resources, NativeBattleEffect166Inputs inputs)
    {
        (frame, uint drawAddress) = Allocate(frame, 88);
        (frame, uint workAddress) = Allocate(frame, 136);
        NativeBattleEffectArena work = Context(frame, workAddress, 136);
        work = CopyBytes(PackedArena(inputs.ReadView(frame)), 0, work, 0x28, 32).Write(2, BitConverter.GetBytes((ushort)0));
        NativeBattleEffectArena draw = Context(frame, drawAddress, 88).Write(8, BitConverter.GetBytes(0u)).Write(0x1C, BitConverter.GetBytes(0u));
        int survivors = 0;
        for (int index = 0; index < SecondRecordCount; index++)
        {
            uint recordAddress = SecondRecordsAddress + (uint)(index * SecondRecordStride);
            NativeBattleEffectArena record = ReadBytes(frame, recordAddress, SecondRecordStride);
            if ((DwordDebris(record, 0) & 2) == 0)
            {
                continue;
            }
            int selector = checked((int)DwordDebris(record, 8));
            if ((uint)selector >= (uint)resources.MeshTable.Length)
            {
                throw new InvalidDataException("Effect166 debris requires its actual mesh selector.");
            }
            uint source = resources.MeshTable[selector];
            work = work.Write(0, record.Read(0x16, 2)).Write(4, record.Read(0x1A, 2));
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler56CE30(new(unchecked((short)WordDebris(record, 0x16)), 0, unchecked((short)WordDebris(record, 0x1A)))) with { X = WordDebris(record, 12), Y = WordDebris(record, 14), Z = WordDebris(record, 16) };
            int scale = WordDebris(record, 6);
            for (int lane = 0; lane < 3; lane++)
            {
                work = work.Write(0x48 + lane * 4, BitConverter.GetBytes(scale));
            }
            matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, scale, scale, scale);
            matrix = NativeBattleGfMatrices.Compose56C2F0(Packed(work, 0x28).ReadMatrix(), matrix);
            NativeBattleEffectArena raw = PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, null));
            raw = CopyBytes(work, 0x1A, raw, 18, 2);
            work = CopyBytes(raw, 0, work, 8, 32);
            frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(matrix, null));
            draw = draw.Write(0, BitConverter.GetBytes(source));
            NativeBattleStaticMeshContext88Step drawn = NativeBattleStaticMeshContext88.DrawLoaded(frame.Memory, resources.ReadMesh(source), inputs.ReadProjection(frame), draw, inputs.Colors, 17, 2, pilotIdentity: inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(166, Read(frame, address), index) with { ResourceGeneration = unchecked((long)source * 397 ^ NativeBattlePilotVisualLifetimes.Identity(166, frame.State.Storage, SecondRecordsAddress, index).Generation) } : null);
            draw = drawn.Context;
            frame = frame with { Memory = drawn.Memory };
            frame = PublishContext(frame, drawAddress, draw, 88);
            frame = PublishContext(frame, workAddress, work, 136);
            frame = inputs.Execute(frame, new NativeBattleEffect166DrawBatch(drawn.Batch, CursorGlobal));
            if ((inputs.ReadFlags(frame) & 0x201) != 0)
            {
                continue;
            }
            int next = unchecked((short)(WordDebris(record, 4) + 1));
            record = record.Write(4, BitConverter.GetBytes((short)next));
            if (next >= 16)
            {
                record = record.Write(0, BitConverter.GetBytes(0u));
            }
            else
            {
                if (next >= 12)
                {
                    int currentScale = WordDebris(record, 6);
                    record = record.Write(6, BitConverter.GetBytes(unchecked((short)(currentScale - (currentScale >> 3)))));
                }
                int velocity = WordDebris(record, 0x12);
                record = record.Write(14, BitConverter.GetBytes(unchecked((short)(WordDebris(record, 14) + velocity)))).Write(0x12, BitConverter.GetBytes(unchecked((short)(velocity - (velocity >> 3))))).Write(0x16, BitConverter.GetBytes(unchecked((short)(WordDebris(record, 0x16) + WordDebris(record, 0x14))))).Write(0x1A, BitConverter.GetBytes(unchecked((short)(WordDebris(record, 0x1A) + WordDebris(record, 0x18)))));
                survivors++;
            }
            frame = WriteBytes(frame, recordAddress, record, SecondRecordStride);
        }
        frame = PublishContext(frame, workAddress, work, 136);
        frame = Release(frame, 136);
        frame = PublishContext(frame, drawAddress, draw, 88);
        frame = Release(frame, 88);
        if ((inputs.ReadFlags(frame) & 0x201) != 0)
        {
            return new(frame, 0);
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 12);
        if (age >= 0 && age <= 5)
        {
            for (int birth = 0; birth < 3; birth++)
            {
                int free = -1;
                for (int index = 0; index < SecondRecordCount; index++)
                {
                    if (frame.State.ReadDword(SecondRecordsAddress + (uint)(index * SecondRecordStride)) == 0)
                    {
                        free = index;
                        break;
                    }
                }
                if (free < 0)
                {
                    break;
                }
                uint recordAddress = SecondRecordsAddress + (uint)(free * SecondRecordStride);
                NativeBattleEffectArena record = ReadBytes(frame, recordAddress, SecondRecordStride).Write(0, BitConverter.GetBytes(2u)).Write(4, BitConverter.GetBytes((ushort)0));
                record = record.Write(6, BitConverter.GetBytes((short)(Roll(ref frame) % 960 + 640)));
                record = record.Write(8, BitConverter.GetBytes(Roll(ref frame) % 3));
                record = CopyBytes(TaskView(task, 0x10, 8), 0, record, 12, 8);
                record = record.Write(12, BitConverter.GetBytes(unchecked((short)(WordDebris(record, 12) + Roll(ref frame) % 4800 - 2400))));
                record = record.Write(14, BitConverter.GetBytes(unchecked((short)-(Roll(ref frame) % 300))));
                record = record.Write(16, BitConverter.GetBytes(unchecked((short)(WordDebris(record, 16) + Roll(ref frame) % 2400 - 1200))));
                record = record.Write(0x12, BitConverter.GetBytes(unchecked((short)(-185 - Roll(ref frame) % 390))));
                int angularX = Roll(ref frame) % 120 + 60;
                if ((angularX & 1) != 0)
                {
                    angularX = -angularX;
                }
                record = record.Write(0x14, BitConverter.GetBytes((short)angularX));
                int angularZ = Roll(ref frame) % 60 + 30;
                if ((angularZ & 1) != 0)
                {
                    angularZ = -angularZ;
                }
                record = record.Write(0x18, BitConverter.GetBytes((short)angularZ));
                int angle = Roll(ref frame) % 4096;
                record = record.Write(0x16, BitConverter.GetBytes((short)angle)).Write(0x1A, BitConverter.GetBytes((short)(angle >> 1)));
                frame = WriteBytes(frame, recordAddress, record, SecondRecordStride);
                if (inputs.CapturePresentation)
                {
                    NativeBattlePilotVisualLifetimes.Allocated(frame.State.Storage, SecondRecordsAddress, free);
                }
            }
        }
        short nextAge = unchecked((short)(age + 1));
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)nextAge)));
        return new(frame, nextAge >= 4 && survivors == 0 ? 2 : 0);
    }

    private static int WordDebris(NativeBattleEffectArena arena, int offset) => System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static uint DwordDebris(NativeBattleEffectArena arena, int offset) => System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
}
