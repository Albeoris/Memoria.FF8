using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect80Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect80Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect80Particles
{
    public static NativeBattleEffect80Frame Emit7B06A0(NativeBattleEffect80Frame frame, uint address, NativeBattleEffect80Resources resources, NativeBattleEffect80Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        short age = Signed(task, 0x24);
        if (age < 0x50 || age > 0x96)
        {
            return frame;
        }
        uint tableAddress = unchecked((uint)(BirthTableAddress + (age - 0x52) * 2));
        ushort enabled = tableAddress < BirthTableAddress ? frame.State.ReadWord(tableAddress) : BinaryPrimitives.ReadUInt16LittleEndian(resources.ReadStatic(tableAddress, 2).AsSpan());
        if (enabled == 0)
        {
            return frame;
        }
        NativeBattleVector first = RandomDirection(ref frame, 0x200);
        NativeBattleVector caster = Vector(inputs.ReadPosition8(frame, task.ReadByte(0x2C)));
        first = new(unchecked((short)(first.X + caster.X)), unchecked((short)(first.Y + caster.Y)), unchecked((short)(first.Z + caster.Z)));
        NativeBattleVector second = RandomDirection(ref frame, 0x100);
        short targetAge = unchecked((short)frame.State.ReadWord(TargetAddress + 0x50));
        int index = targetAge >= 120 ? 119 : targetAge;
        short height = BinaryPrimitives.ReadInt16LittleEndian(resources.ReadStatic(unchecked((uint)(AgeTableAddress + index * 2)), 2).AsSpan());
        int count = unchecked((short)-height) / 512;
        short endpointAge = unchecked((short)(targetAge + count));
        int endpointIndex = endpointAge >= 120 ? 119 : endpointAge;
        short endpointHeight = BinaryPrimitives.ReadInt16LittleEndian(resources.ReadStatic(unchecked((uint)(AgeTableAddress + endpointIndex * 2)), 2).AsSpan());
        NativeBattleVector endpoint = Vector(inputs.ReadPosition8(frame, task.ReadByte(0x2C)));
        endpoint = endpoint with { Y = unchecked((short)(endpoint.Y + endpointHeight)) };
        second = new(unchecked((short)(second.X + endpoint.X)), unchecked((short)(second.Y + endpoint.Y)), unchecked((short)(second.Z + endpoint.Z)));
        if (count > 0)
        {
            frame = BirthPair7B0860(frame, first, second, unchecked((short)count), resources, inputs.CapturePresentation);
        }
        return frame;
    }

    public static NativeBattleEffect80Frame BirthPair7B0860(NativeBattleEffect80Frame frame, NativeBattleVector start, NativeBattleVector end, short count, NativeBattleEffect80Resources resources, bool capturePresentation = false)
    {
        int step = 4096 / Math.Max(1, (int)count);
        for (int particle = 0; particle < 2; particle++)
        {
            NativeCrtRandomState random = frame.Random.Draw(out ushort angle);
            random = random.Draw(out ushort radiusDraw);
            int radius = (radiusDraw & 0x3FF) + 0x400;
            int x = NativeBattleCameraMath.Cosine(angle) * radius / 4096;
            int z = NativeBattleCameraMath.Sine(angle) * radius / 4096;
            random = random.Draw(out ushort speedDraw);
            int speed = (speedDraw << 8) >> 15;
            NativeBattleEffectArena descriptor = NativeBattleEffectArena.Clear().MarkUnknown(0, 48);
            descriptor = Point(descriptor, 8, start).Write(14, BitConverter.GetBytes((short)3000));
            descriptor = Point(descriptor, 16, end).Write(22, BitConverter.GetBytes((short)0));
            descriptor = Point(descriptor, 24, new(unchecked((short)((x * speed) >> 12)), 0, unchecked((short)((z * speed) >> 12)))).Write(30, BitConverter.GetBytes(unchecked((short)step)));
            descriptor = descriptor.Write(32, new byte[6]).Write(38, BitConverter.GetBytes((short)2)).Write(40, BitConverter.GetBytes((short)0x3000)).Write(42, BitConverter.GetBytes(unchecked((short)0xFE00))).Write(46, BitConverter.GetBytes((short)0x1000));
            random = random.Draw(out ushort driftX);
            random = random.Draw(out ushort driftY);
            random = random.Draw(out ushort driftZ);
            descriptor = Point(descriptor, 32, new(unchecked((short)((driftX & 0xFF) - 0x80)), unchecked((short)((driftY & 0xFF) - 0x80)), unchecked((short)((driftZ & 0xFF) - 0x80))));
            frame = frame with { Random = random };
            (frame, _) = NativeBattleEffect80Trails.Birth7AFDD0(frame, descriptor, resources, capturePresentation);
        }
        return frame;
    }

    private static NativeBattleVector RandomDirection(ref NativeBattleEffect80Frame frame, short distance)
    {
        NativeCrtRandomState random = frame.Random.Draw(out ushort y);
        random = random.Draw(out ushort x);
        frame = frame with { Random = random };
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770();
        matrix = NativeBattleCuragaMatrices.RotateY8DD8A0(matrix, (short)(y & 0xFFF));
        matrix = NativeBattleCuragaMatrices.RotateX8DD7E0(matrix, (short)(x & 0xFFF));
        return NativeBattleGfMatrices.RotateShort56C4F0(matrix, new(0, 0, distance));
    }

    private static NativeBattleEffectArena Point(NativeBattleEffectArena target, int offset, NativeBattleVector value) => target.Write(offset, BitConverter.GetBytes(value.X)).Write(offset + 2, BitConverter.GetBytes(value.Y)).Write(offset + 4, BitConverter.GetBytes(value.Z));
}


