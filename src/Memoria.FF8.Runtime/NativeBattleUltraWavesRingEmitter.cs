using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleUltraWavesEmitterStep(NativeBattleEffectMemory Memory, NativeBattleFileStaging Staging, NativeBattleModulePoolTask Task, NativeBattleEffectArena Particles, int Result);

public static class NativeBattleUltraWavesRingEmitter
{
    public static NativeBattleUltraWavesEmitterStep Advance(NativeBattleUltraWavesEmitterStep step, ushort effect, NativeBattleStaticMeshResource ring, ImmutableArray<uint> palette, NativeBattlePackedMatrix32 camera, NativeBattleCameraProjection projection, NativeBattleSummonColors constants, bool paused, Func<NativeBattleUltraWavesEmitterStep, NativeBattleGfGeometryBatch, NativeBattleUltraWavesEmitterStep> deliver, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        int divisor = effect switch
        {
            154 => 6,
            184 => 7,
            _ => throw new NotSupportedException("Ultra Waves emitter requires its native scale-growth policy.")
        };
        if (palette.IsDefault || palette.Length != 16)
        {
            throw new InvalidDataException("Ultra Waves emitter requires its actual sixteen-color table.");
        }
        NativeBattleTemporaryAllocation contextAllocation = NativeBattleTemporaryMemory.Allocate5082B0(step.Staging, 108);
        NativeBattleTemporaryAllocation matrixAllocation = NativeBattleTemporaryMemory.Allocate5082B0(contextAllocation.Staging, 136);
        step = step with { Staging = matrixAllocation.Staging };
        NativeBattleEffectArena context = View(step.Staging, contextAllocation.Address, 108).Write(0, BitConverter.GetBytes(ring.Address)).Write(8, BitConverter.GetBytes(0u)).Write(0x1C, BitConverter.GetBytes(0x33u));
        NativeBattleEffectArena temporary = View(step.Staging, matrixAllocation.Address, 136).Write(0x58, BitConverter.GetBytes(0)).Write(0x5C, BitConverter.GetBytes(0)).Write(0x60, BitConverter.GetBytes(-4096));
        NativeBattleVector direction = new(Word(step.Task, 24), Word(step.Task, 26), Word(step.Task, 28));
        for (int axis = 0; axis < 3; axis++)
        {
            int value = axis == 0 ? direction.X : axis == 1 ? direction.Y : direction.Z;
            temporary = temporary.Write(0x68 + axis * 4, BitConverter.GetBytes(value));
        }
        int alive = 0;
        for (int particle = 0; particle < 100; particle++)
        {
            int at = particle * 24;
            if ((Read(at) & step.Task.ReadWord(14)) == 0)
            {
                continue;
            }
            int life = Read(at + 4);
            NativeBattleActionMatrix matrix = new(NativeBattleCuragaMatrices.Identity8DD770().Rotation, Read(at + 8), Read(at + 10), Read(at + 12), 4096);
            temporary = Dword(temporary, 0x1C, matrix.X);
            temporary = Dword(temporary, 0x20, matrix.Y);
            temporary = Dword(temporary, 0x24, matrix.Z);
            NativeBattleModuleAxisAngle alignment = NativeBattleModuleAlignment.Between571480(0, 0, -4096, direction.X, direction.Y, direction.Z);
            for (int axis = 0; axis < 3; axis++)
            {
                int value = axis == 0 ? alignment.Axis.X : axis == 1 ? alignment.Axis.Y : alignment.Axis.Z;
                temporary = Dword(temporary, 0x78 + axis * 4, value);
            }
            matrix = matrix with { Rotation = NativeBattleModuleAlignment.Rotation5714F0(alignment.Angle, alignment.Axis) };
            temporary = WriteRotation(temporary, 8, matrix.Rotation);
            NativeBattleActionMatrix spin = new(NativeBattleModuleAlignment.Rotation5714F0(Read(at + 14), direction), 0, 0, 0, 4096);
            temporary = WriteRotation(temporary, 0x28, spin.Rotation);
            spin = NativeBattleGfMatrices.Multiply56C090(spin, matrix);
            temporary = WriteRotation(temporary, 0x28, spin.Rotation).MarkUnknown(0x3A, 2);
            NativeBattleVector radial = new(Read(at + 22), 0, 0);
            temporary = temporary.Write(0, BitConverter.GetBytes(unchecked((short)radial.X))).Write(2, BitConverter.GetBytes((short)0)).Write(4, BitConverter.GetBytes((short)0));
            (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(spin, radial);
            temporary = temporary.Write(0, BitConverter.GetBytes(unchecked((short)x))).Write(2, BitConverter.GetBytes(unchecked((short)y))).Write(4, BitConverter.GetBytes(unchecked((short)z)));
            matrix = matrix with { X = unchecked(matrix.X + unchecked((short)x)), Y = unchecked(matrix.Y + unchecked((short)y)), Z = unchecked(matrix.Z + unchecked((short)z)) };
            temporary = Dword(temporary, 0x1C, matrix.X);
            temporary = Dword(temporary, 0x20, matrix.Y);
            temporary = Dword(temporary, 0x24, matrix.Z);
            int scale = Read(at + 6);
            for (int axis = 0; axis < 3; axis++)
            {
                temporary = Dword(temporary, 0x48 + axis * 4, scale);
            }
            matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, scale, scale, scale);
            temporary = WriteRotation(temporary, 8, matrix.Rotation);
            matrix = NativeBattleGfMatrices.Compose56C2F0(camera.ReadMatrix(), matrix);
            NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(matrix, null);
            temporary = temporary.Write(8, packed.Bytes.AsSpan()).MarkUnknown(0x1A, 2);
            NativeBattleGeometryRegisterStep registers = new(step.Memory.GeometryRegisters ?? throw new NotSupportedException("Ultra Waves emitter requires its current geometry registers."), step.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Ultra Waves emitter requires its current arithmetic registers."));
            step = step with { Memory = step.Memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix, true, true, null) } };
            int color = Read(at + 2);
            if ((uint)color >= (uint)palette.Length)
            {
                throw new InvalidDataException("Ultra Waves particle leaves its actual palette.");
            }
            context = context.Write(0x20, BitConverter.GetBytes(palette[color]));
            if (life >= 6)
            {
                context = context.Write(12, BitConverter.GetBytes(unchecked((life - 6) * 682))).Write(0x1C, BitConverter.GetBytes(U32(context, 0x1C) | 0xC0));
            }
            NativeBattleUltraWavesRingDraw drawn = NativeBattleUltraWavesRingDrawing.Draw(step.Memory, context, ring, projection, constants, pilotIdentity: pilotIdentity is { } identity ? NativeBattleUltraWavesVisualLifetimes.Identity(identity, step.Task, particle) : null);
            context = drawn.Temporary108;
            step = step with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(step.Staging, contextAllocation.Address, context, 108) };
            step = step with { Staging = NativeBattleTemporaryMemory.PublishView(step.Staging, matrixAllocation.Address, temporary, 136) };
            step = deliver(step, drawn.Batch);
            if (paused)
            {
                continue;
            }
            Put(at + 4, life + 1);
            if (life + 1 >= 12)
            {
                Put(at, 0);
                continue;
            }
            Put(at + 2, ((byte)Read(at + 2) + 1) & 15);
            Put(at + 6, unchecked(scale + scale / divisor));
            for (int axis = 0; axis < 3; axis++)
            {
                Put(at + 8 + axis * 2, unchecked(Read(at + 8 + axis * 2) + Read(at + 16 + axis * 2)));
            }
            Put(at + 14, unchecked(Read(at + 14) + 512));
            Put(at + 22, unchecked(Read(at + 22) + 10));
            alive++;
        }
        step = step with { Staging = NativeBattleTemporaryMemory.PublishView(step.Staging, contextAllocation.Address, context, 108) };
        step = step with { Staging = NativeBattleTemporaryMemory.PublishView(step.Staging, matrixAllocation.Address, temporary, 136) };
        step = step with { Staging = NativeBattleTemporaryMemory.Release5082D0(NativeBattleTemporaryMemory.Release5082D0(step.Staging, 136), 108) };
        if (paused)
        {
            return step with { Result = 0 };
        }
        int age = Word(step.Task, 12);
        if (age is >= 0 and <= 20)
        {
            for (int particle = 0; particle < 100; particle++)
            {
                int at = particle * 24;
                if (Read(at) != 0)
                {
                    continue;
                }
                Put(at, step.Task.ReadWord(14));
                Put(at + 2, Word(step.Task, 34));
                Put(at + 4, 0);
                Put(at + 6, 768);
                step = step with { Particles = step.Particles.Write(at + 8, step.Task.ReadBytes(16, 6)) };
                Put(at + 14, Word(step.Task, 32));
                for (int axis = 0; axis < 3; axis++)
                {
                    Put(at + 16 + axis * 2, unchecked(Word(step.Task, 24 + axis * 2) * 150) >> 12);
                }
                Put(at + 22, 100);
                if (pilotIdentity.HasValue)
                {
                    NativeBattleUltraWavesVisualLifetimes.Allocated(step.Task, particle);
                }
                break;
            }
        }
        step = step with { Task = step.Task.WriteWord(32, unchecked((ushort)(Word(step.Task, 32) + 256))).WriteWord(34, (ushort)((unchecked((byte)Word(step.Task, 34)) - 1) & 15)).WriteWord(12, unchecked((ushort)(age + 1))), Result = age + 1 >= 12 && alive == 0 ? 2 : 0 };
        return step;

        short Read(int offset) => BinaryPrimitives.ReadInt16LittleEndian(step.Particles.Read(offset, 2));
        void Put(int offset, int value) => step = step with { Particles = step.Particles.Write(offset, BitConverter.GetBytes(unchecked((short)value))) };
    }

    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static uint U32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattleEffectArena Dword(NativeBattleEffectArena arena, int offset, int value) => arena.Write(offset, BitConverter.GetBytes(value));
    private static NativeBattleEffectArena View(NativeBattleFileStaging staging, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    private static NativeBattleEffectArena WriteRotation(NativeBattleEffectArena arena, int offset, ImmutableArray<short> rotation)
    {
        byte[] bytes = new byte[18];
        for (int index = 0; index < 9; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 2), rotation[index]);
        }
        return arena.Write(offset, bytes);
    }
}
