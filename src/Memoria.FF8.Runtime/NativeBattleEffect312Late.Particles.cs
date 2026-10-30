using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect312Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect312Late
{
    private static NativeBattleEffectArena PackedBytes(NativeBattlePackedMatrix32 matrix)
    {
        NativeBattleEffectArena bytes = NativeBattleEffectArena.Clear().Write(0, matrix.Bytes.AsSpan());
        for (int index = 0; index < 32; index++)
        {
            if (!matrix.Known[index])
            {
                bytes = bytes.MarkUnknown(index, 1);
            }
        }
        return bytes;
    }

    private static short? Padding(NativeBattleEffectArena bytes, int offset)
    {
        (byte[] data, bool[] known) = bytes.ReadSnapshot(offset, 2);
        return known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(data) : null;
    }

    private static NativeBattleEffect312Step Sprites(NativeBattleEffect312Frame frame, uint address, NativeBattleEffect312Resources resources, NativeBattleEffect312Inputs inputs)
    {
        (frame, uint temporary48) = Allocate(frame, 72);
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = TaskWord(task, 0x0C);
        if (!Paused(frame, inputs) && age <= 16)
        {
            for (int emission = 0; emission < 2; emission++)
            {
                uint row = FindFree(frame, 0x223CCE8, 24);
                if (row == 0)
                {
                    break;
                }
                if (inputs.CapturePresentation)
                {
                    NativeBattlePilotVisualLifetimes.Allocated(frame.State.Storage, NativeBattleEffect312Profile.FirstRecordsAddress, checked((int)((row - NativeBattleEffect312Profile.FirstRecordsAddress) / NativeBattleEffect312Profile.FirstRecordStride)));
                }
                NativeBattleEffectArena particle = PutWord(Put(ReadBytes(frame, row, 24), 0, 1), 4, 0);
                frame = DrawRandom(frame, out int random);
                particle = PutWord(particle, 6, random % 3072 + 2560);
                particle = CopyBytes(ReadBytes(frame, address + 0x10, 8), 0, particle, 8, 8);
                frame = DrawRandom(frame, out random);
                particle = PutWord(particle, 8, Word(particle, 8) + random % 4800 - 2400);
                frame = DrawRandom(frame, out random);
                particle = PutWord(particle, 10, Word(particle, 10) - 400 - random % 2200);
                frame = DrawRandom(frame, out random);
                particle = PutWord(particle, 12, Word(particle, 12) + random % 2400 - 1200);
                frame = WriteBytes(frame, row, particle, 24);
            }
        }
        (frame, uint temporary180) = Allocate(frame, 180);
        NativeBattleEffectArena context = Put(PutWord(ReadBytes(frame, temporary180, 180), 0x24, 0), 0, 0xCD8EBC);
        NativeBattlePackedMatrix32 view = inputs.ReadView(frame);
        NativeBattleEffectArena matrixBytes = CopyBytes(PackedBytes(view), 0, ReadBytes(frame, temporary48, 72), 8, 32);
        frame = WriteBytes(frame, temporary48, matrixBytes, 72);
        NativeBattleEffectMemory memory = NativeBattleEffect315Gte.LoadRotation45DE50(frame.Memory, view);
        memory = NativeBattleEffect315Gte.LoadAlternateTranslation64DDA0(memory, view);
        frame = frame with { Memory = memory };
        int survivors = 0;
        for (int index = 0; index < 100; index++)
        {
            uint row = 0x223CCE8u + (uint)(index * 24);
            NativeBattleEffectArena particle = ReadBytes(frame, row, 24);
            if ((Dword(particle, 0) & 1) == 0)
            {
                continue;
            }
            NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.LoadVector(Registers(frame), 0, Vector(particle, 8), Padding(particle, 14));
            frame = Publish(frame, registers);
            memory = NativeBattleEffect315Gte.TransformAlternate4607E0(frame.Memory);
            memory = NativeBattleEffect315Gte.SetDiagonal64DE00(memory, unchecked((ushort)Word(particle, 6)));
            memory = NativeBattleEffect315Gte.MacToTranslation64DE40(memory);
            context = PutWord(context, 4, Word(particle, 4));
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(memory, context, resources.ReadSprite, inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame), pilotIdentity: inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(312, frame.State.Storage, NativeBattleEffect312Profile.FirstRecordsAddress, index) with { ResourceGeneration = 0xCD8EBC } : null);
            context = drawn.Temporary180;
            frame = WriteBytes(frame with { Memory = drawn.Memory }, temporary180, context, 180);
            frame = inputs.Execute(frame, new NativeBattleEffect312DrawBatch(NativeBattleEffect312Presentation.PreserveNativeDepths(drawn.Batch)));
            if (!Paused(frame, inputs))
            {
                particle = PutWord(particle, 4, Word(particle, 4) + 1);
                if (Word(context, 0x28) < 0)
                {
                    particle = Put(particle, 0, 0);
                }
                else
                {
                    survivors++;
                }
                frame = WriteBytes(frame, row, particle, 24);
            }
        }
        frame = WriteBytes(frame, temporary180, context, 180);
        frame = Release(Release(frame, 180), 72);
        if (Paused(frame, inputs))
        {
            return new(frame, 0);
        }
        frame = Write(frame, address, TaskPut(task, 0x0C, age + 1));
        return new(frame, survivors == 0 ? 2 : 0);
    }

    private static uint FindFree(NativeBattleEffect312Frame frame, uint start, int stride)
    {
        for (int index = 0; index < 100; index++)
        {
            uint address = start + (uint)(index * stride);
            if (frame.State.ReadDword(address) == 0)
            {
                return address;
            }
        }
        return 0;
    }

    private static NativeBattleEffect312Step Debris(NativeBattleEffect312Frame frame, uint address, NativeBattleEffect312Resources resources, NativeBattleEffect312Inputs inputs)
    {
        (frame, uint temporary88) = Allocate(frame, 88);
        (frame, uint temporary136) = Allocate(frame, 136);
        NativeBattleEffectArena descriptor = Put(Put(ReadBytes(frame, temporary88, 88), 8, 0), 0x1C, 0);
        NativeBattleEffectArena context = PutWord(ReadBytes(frame, temporary136, 136), 2, 0);
        NativeBattlePackedMatrix32 view = inputs.ReadView(frame);
        context = CopyBytes(PackedBytes(view), 0, context, 0x28, 32);
        int survivors = 0;
        for (int index = 0; index < 100; index++)
        {
            uint row = 0x223D648u + (uint)(index * 28);
            NativeBattleEffectArena particle = ReadBytes(frame, row, 28);
            if ((Dword(particle, 0) & 2) == 0)
            {
                continue;
            }
            uint mesh = BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(unchecked(0xCDF6A0u + Dword(particle, 8) * 4), 4));
            descriptor = Put(descriptor, 0, mesh);
            context = PutWord(PutWord(context, 0, Word(particle, 22)), 4, Word(particle, 26));
            NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(Word(context, 0), 0, Word(context, 4)));
            local = local with { X = Word(particle, 12), Y = Word(particle, 14), Z = Word(particle, 16) };
            short scale = Word(particle, 6);
            context = Put(Put(Put(context, 0x48, unchecked((uint)scale)), 0x4C, unchecked((uint)scale)), 0x50, unchecked((uint)scale));
            local = local with { Rotation = NativeBattlePoseMatrices.ScaleRotation56BEF0(local.Rotation, new(scale, scale, scale)) };
            NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(view.ReadMatrix(), local);
            short? padding = Padding(context, 0x1A);
            NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(composed, padding is { } pad ? unchecked((ushort)pad) : null);
            context = CopyBytes(PackedBytes(packed), 0, context, 8, 32);
            frame = WriteBytes(WriteBytes(frame, temporary136, context, 136), temporary88, descriptor, 88);
            frame = NativeBattleEffect312Mesh.Draw572200(LoadMatrix(frame, packed), temporary88, mesh, packed, resources, inputs, pilotIdentity: inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(312, frame.State.Storage, NativeBattleEffect312Profile.SecondRecordsAddress, index) with { ResourceGeneration = mesh } : null);
            descriptor = ReadBytes(frame, temporary88, 88);
            if (Paused(frame, inputs))
            {
                continue;
            }
            int particleAge = unchecked((short)(Word(particle, 4) + 1));
            particle = PutWord(particle, 4, particleAge);
            if (particleAge >= 16)
            {
                particle = Put(particle, 0, 0);
            }
            else
            {
                if (particleAge >= 12)
                {
                    particle = PutWord(particle, 6, scale - (scale >> 3));
                }
                int velocity = Word(particle, 18);
                particle = PutWord(PutWord(PutWord(PutWord(particle, 14, Word(particle, 14) + velocity), 18, velocity - (velocity >> 3)), 22, Word(particle, 22) + Word(particle, 20)), 26, Word(particle, 26) + Word(particle, 24));
                survivors++;
            }
            frame = WriteBytes(frame, row, particle, 28);
        }
        frame = WriteBytes(WriteBytes(frame, temporary136, context, 136), temporary88, descriptor, 88);
        frame = Release(Release(frame, 136), 88);
        if (Paused(frame, inputs))
        {
            return new(frame, 0);
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = TaskWord(task, 0x0C);
        if (age is >= 0 and <= 5)
        {
            for (int emission = 0; emission < 3; emission++)
            {
                uint row = FindFree(frame, 0x223D648, 28);
                if (row == 0)
                {
                    break;
                }
                if (inputs.CapturePresentation)
                {
                    NativeBattlePilotVisualLifetimes.Allocated(frame.State.Storage, NativeBattleEffect312Profile.SecondRecordsAddress, checked((int)((row - NativeBattleEffect312Profile.SecondRecordsAddress) / NativeBattleEffect312Profile.SecondRecordStride)));
                }
                NativeBattleEffectArena particle = PutWord(Put(ReadBytes(frame, row, 28), 0, 2), 4, 0);
                frame = DrawRandom(frame, out int random);
                particle = PutWord(particle, 6, random % 960 + 640);
                frame = DrawRandom(frame, out random);
                particle = Put(particle, 8, (uint)(random % 3));
                particle = CopyBytes(ReadBytes(frame, address + 16, 8), 0, particle, 12, 8);
                frame = DrawRandom(frame, out random);
                particle = PutWord(particle, 12, Word(particle, 12) + random % 4800 - 2400);
                frame = DrawRandom(frame, out random);
                particle = PutWord(particle, 14, -(random % 300));
                frame = DrawRandom(frame, out random);
                particle = PutWord(particle, 16, Word(particle, 16) + random % 2400 - 1200);
                frame = DrawRandom(frame, out random);
                particle = PutWord(particle, 18, -185 - random % 390);
                frame = DrawRandom(frame, out random);
                int yawVelocity = random % 120 + 60;
                particle = PutWord(particle, 20, (yawVelocity & 1) != 0 ? -yawVelocity : yawVelocity);
                frame = DrawRandom(frame, out random);
                int rollVelocity = random % 60 + 30;
                particle = PutWord(particle, 24, (rollVelocity & 1) != 0 ? -rollVelocity : rollVelocity);
                frame = DrawRandom(frame, out random);
                int yaw = random % 4096;
                particle = PutWord(PutWord(particle, 22, yaw), 26, unchecked((short)yaw) >> 1);
                frame = WriteBytes(frame, row, particle, 28);
            }
        }
        age = unchecked((short)(age + 1));
        frame = Write(frame, address, TaskPut(task, 12, age));
        return new(frame, age >= 4 && survivors == 0 ? 2 : 0);
    }
}
