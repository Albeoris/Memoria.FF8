using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAquaDeformation(ImmutableArray<byte> Mesh, NativeBattleActionMatrix Rotation, ImmutableArray<NativeBattleVector> Inputs)
{
    public ImmutableArray<short?> InputWs { get; init; }
}

public static partial class NativeBattleAquaParticles
{
    public static NativeBattleModulePoolTask AdvanceAnimation(NativeBattleModulePoolTask task)
    {
        task = task.WriteWord(0x50, unchecked((ushort)(task.ReadWord(0x50) + 1)));
        if (Word(task, 0x50) > Word(task, 0x52))
        {
            task = task.WriteWord(0x50, task.ReadWord(0x52)).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5));
            task = Next(task);
        }
        return task;
    }

    public static NativeBattleModulePoolTask AdvanceMotionRecord(NativeBattleModulePoolTask task, int index, bool gravity)
    {
        if (index is < 0 or > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        int position = 0x80 + index * 8;
        int velocity = 0xA0 + index * 8;
        if (gravity)
        {
            task = task.WriteWord(velocity + 2, unchecked((ushort)(task.ReadWord(velocity + 2) + 0x50)));
        }
        for (int lane = 0; lane < 3; lane++)
        {
            short value = Word(task, velocity + lane * 2);
            short damped = unchecked((short)(value - value / 8));
            task = task.WriteWord(velocity + lane * 2, unchecked((ushort)damped));
            task = task.WriteWord(position + lane * 2, unchecked((ushort)(Word(task, position + lane * 2) + damped / 16)));
        }
        return task;
    }

    public static NativeBattleModulePoolTask InitializeCasterBirth(NativeBattleModulePoolTask parent, NativeBattleModulePoolTask child, ref NativeCrtRandomState random, Action<NativeBattleActionMatrix, char, short>? observeRotation = null)
    {
        for (int index = 0; index < 4; index++)
        {
            int yaw = Draw(ref random) % 0x300 - 0x180;
            int pitch = Draw(ref random) % 0x180 - 0xC0;
            NativeBattleActionMatrix matrix = NativeBattleReflectController.ReadMatrix(parent, 0x58);
            observeRotation?.Invoke(matrix, 'Y', unchecked((short)yaw));
            matrix = NativeBattleCuragaMatrices.RotateY8DD8A0(matrix, unchecked((short)yaw));
            observeRotation?.Invoke(matrix, 'X', unchecked((short)pitch));
            matrix = NativeBattleCuragaMatrices.RotateX8DD7E0(matrix, unchecked((short)pitch));
            child = CopyPayload(parent, 0x58, child, 0xC0 + index * 32, 32);
            child = NativeBattleReflectController.WriteMatrix(child, 0xC0 + index * 32, matrix);
            child = CopyPayload(parent, 0x1C, child, 0x78, 8);
            child = child.WriteWord(0x148 + index * 2, (ushort)(Draw(ref random) & 0xFFF));
        }
        return child;
    }

    public static NativeBattleModulePoolTask InitializeTargetBirth(NativeBattleModulePoolTask parent, NativeBattleModulePoolTask child, bool spray, int boneCount, Func<short, int, NativeBattleModuleAnchor> readAnchor, ref NativeCrtRandomState random, Action<NativeBattleActionMatrix, char, short>? observeRotation = null, Action<NativeBattleActionMatrix, NativeBattleVector>? observeTransform = null)
    {
        ArgumentNullException.ThrowIfNull(readAnchor);
        if (boneCount is < 1 or > byte.MaxValue)
        {
            throw new InvalidDataException("The Aqua emitter requires its actual nonempty target skeleton.");
        }
        int count = spray ? 4 : 2;
        for (int index = 0; index < count; index++)
        {
            short selector = (short)(Draw(ref random) % boneCount);
            int fraction = Draw(ref random) & 0xFFF;
            NativeBattleModuleAnchor anchor = readAnchor(selector, fraction);
            child = WriteVector(child, 0x80 + index * 8, new(anchor.X, anchor.Y, anchor.Z));
        }
        for (int index = 0; index < count; index++)
        {
            int position = 0x80 + index * 8;
            short yaw;
            short pitch;
            if (spray)
            {
                child = child.WriteWord(position, unchecked((ushort)(Word(child, position) + (Draw(ref random) & 0x3FF) - 0x200)));
                child = child.WriteWord(position + 2, unchecked((ushort)(Word(child, position + 2) + (Draw(ref random) & 0x3FF) - 0x300)));
                child = child.WriteWord(position + 4, unchecked((ushort)(Word(child, position + 4) + (Draw(ref random) & 0x1FF) - 0x100)));
                pitch = (short)(Draw(ref random) & 0xFFF);
                yaw = (short)(Draw(ref random) & 0xFFF);
            }
            else
            {
                pitch = (short)((Draw(ref random) & 0x1FF) + 0xE00);
                yaw = (short)((Draw(ref random) & 0x3FF) - 0x200);
            }
            NativeBattleActionMatrix matrix = NativeBattleReflectController.ReadMatrix(parent, 0x58);
            observeRotation?.Invoke(matrix, 'Y', yaw);
            matrix = NativeBattleCuragaMatrices.RotateY8DD8A0(matrix, yaw);
            observeRotation?.Invoke(matrix, 'X', pitch);
            matrix = NativeBattleCuragaMatrices.RotateX8DD7E0(matrix, pitch);
            ushort sample = Draw(ref random);
            short travel = spray ? unchecked((short)-(sample & 0x3FF)) : unchecked((short)(-0x400 - (sample & 0x7FF)));
            NativeBattleVector point = new(0, 0, travel);
            observeTransform?.Invoke(matrix, point);
            NativeBattleVector velocity = NativeBattleCuragaMatrices.RotateVector45DF80(matrix, point);
            child = WriteVector(child, 0xA0 + index * 8, velocity);
        }
        return child;
    }

    public static NativeBattleAquaDeformation DeformWave(ImmutableArray<byte> mutableMesh, ImmutableArray<byte> source, short phase, short angle)
    {
        if (mutableMesh.IsDefault || mutableMesh.Length != 10600 || source.IsDefault || source.Length != 2504 || BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan().Slice(4)) != 312 || BinaryPrimitives.ReadInt32LittleEndian(mutableMesh.AsSpan().Slice(4)) != 312)
        {
            throw new InvalidDataException("The Aqua wave requires its original312 vertices and separate retained mesh.");
        }
        NativeBattleActionMatrix rotation = NativeBattleCuragaMatrices.RotateZ8DD960(NativeBattleCuragaMatrices.Identity8DD770(), angle);
        byte[] bytes = mutableMesh.ToArray();
        ImmutableArray<NativeBattleVector>.Builder inputs = ImmutableArray.CreateBuilder<NativeBattleVector>(312);
        ImmutableArray<short?>.Builder ws = ImmutableArray.CreateBuilder<short?>(312);
        for (int index = 0; index < 312; index++)
        {
            int offset = 8 + index * 8;
            NativeBattleVector point = new(BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan().Slice(offset)), BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan().Slice(offset + 2)), BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan().Slice(offset + 4)));
            inputs.Add(point);
            ws.Add(BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan().Slice(offset + 6)));
            NativeBattleVector transformed = NativeBattleCuragaMatrices.RotateVector45DF80(rotation, point);
            int waveAngle = (point.Z / 2 + phase) & 0xFFF;
            short amplitude = unchecked((short)(unchecked(unchecked(point.Z * 5) << 8) / 4096));
            int displacement = unchecked(NativeBattleCameraMath.Sine(waveAngle) * amplitude) / 4096;
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), unchecked((short)(transformed.X + displacement)));
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 2), transformed.Y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 4), transformed.Z);
            source.AsSpan().Slice(offset + 6, 2).CopyTo(bytes.AsSpan(offset + 6, 2));
        }
        return new(bytes.ToImmutableArray(), rotation, inputs.MoveToImmutable()) { InputWs = ws.MoveToImmutable() };
    }

    private static ushort? ReadPadding(NativeBattleModulePoolTask task, int matrixOffset)
    {
        int offset = matrixOffset + 18;
        return task.ReadUnknownPayloadRanges()?.Any(range => range.Offset < offset + 2 && range.Offset + range.Length > offset) == true ? null : task.ReadWord(offset);
    }

    private static NativeBattleModulePoolTask CopyPayload(NativeBattleModulePoolTask source, int sourceOffset, NativeBattleModulePoolTask destination, int destinationOffset, int length)
    {
        destination = destination.WriteBytes(destinationOffset, source.ReadRawPayloadSpan().Slice(sourceOffset - 12, length));
        if (source.ReadUnknownPayloadRanges() is { } unknown)
        {
            foreach (NativeBattleEffectUnknownRegion range in unknown)
            {
                int start = Math.Max(sourceOffset, range.Offset);
                int end = Math.Min(sourceOffset + length, range.Offset + range.Length);
                if (start < end)
                {
                    destination = destination.MarkPayloadUnknown(destinationOffset + start - sourceOffset, end - start);
                }
            }
        }
        return destination;
    }

    private static ushort Draw(ref NativeCrtRandomState random)
    {
        random = random.Draw(out ushort value);
        return value;
    }

    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleModulePoolTask Next(NativeBattleModulePoolTask task) => task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
    private static NativeBattleModulePoolTask WriteVector(NativeBattleModulePoolTask task, int offset, NativeBattleVector point) => task.WriteWord(offset, unchecked((ushort)point.X)).WriteWord(offset + 2, unchecked((ushort)point.Y)).WriteWord(offset + 4, unchecked((ushort)point.Z));
}
