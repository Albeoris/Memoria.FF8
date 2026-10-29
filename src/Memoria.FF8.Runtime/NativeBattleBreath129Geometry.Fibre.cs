using System.Numerics;
using static Memoria.FF8.Runtime.NativeBattleBreath129Controller;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBreath129FibreStack(uint CrtSavedEdi);
public sealed record NativeBattleBreath129FibrePreparation(NativeBattleBreath129Frame Frame, uint Address, NativeBattleBreath129FibreStack Stack);

public static partial class NativeBattleBreath129Geometry
{
    public static NativeBattleBreath129Frame GenerateFibre6B68C0(NativeBattleBreath129Frame frame, uint taskAddress, NativeBattleBreath129Resources resources, NativeBattleBreath129Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, taskAddress);
        NativeBattleBreath129BodyStep body = inputs.ReadBody(frame, task.ReadDword(28));
        frame = body.Frame;
        NativeBattleEffectArena pairs = resources.ReadView(task.ReadDword(20), 8);
        int first = BitConverter.ToInt16(pairs.Read(0, 2));
        int firstFrames = BitConverter.ToInt16(pairs.Read(2, 2));
        int second = BitConverter.ToInt16(pairs.Read(4, 2));
        int secondFrames = BitConverter.ToInt16(pairs.Read(6, 2));
        int progress = unchecked((short)task.ReadWord(14));
        uint triangles = task.ReadDword(24);
        NativeBattleBreath129SampleStep a = Sample6B6FE0(frame, body.Source, checked(triangles + (uint)(first * 24)), firstFrames, progress, resources, inputs);
        NativeBattleBreath129SampleStep b = Sample6B6FE0(a.Frame, body.Source, checked(triangles + (uint)(second * 24)), secondFrames, progress, resources, inputs);
        NativeBattleBreath129FibrePreparation prepared = PrepareFibre6B68C0(b.Frame, taskAddress, a.Point8, a.Normal8, b.Point8, b.Normal8, resources, inputs);
        frame = DrawFibre6B6CC0(prepared.Frame, prepared.Address, resources, inputs with { PilotIdentity = inputs.CapturePresentation ? NativeBattleBreath129VisualIdentity.Fibre(prepared.Frame, taskAddress, body.Source.GeometryAddress) : null }, prepared.Stack);
        return Release(frame, 0x100);
    }

    public static NativeBattleBreath129FibrePreparation PrepareFibre6B68C0(NativeBattleBreath129Frame frame, uint taskAddress, NativeBattleEffectArena firstPoint8, NativeBattleEffectArena firstNormal8, NativeBattleEffectArena secondPoint8, NativeBattleEffectArena secondNormal8, NativeBattleBreath129Resources resources, NativeBattleBreath129Inputs inputs)
    {
        NativeBattleVector first = Vector(firstPoint8);
        NativeBattleVector second = Vector(secondPoint8);
        NativeBattleGeometryRegisterStep registers = Registers(frame);
        registers = registers with { Colors = registers.Colors with { Ir = [second.X - first.X, second.Y - first.Y, second.Z - first.Z] } };
        registers = NativeBattleReflectBeam135Geometry.Square45F930(registers);
        int squared = unchecked(registers.Colors.Mac[0]!.Value + registers.Colors.Mac[1]!.Value + registers.Colors.Mac[2]!.Value);
        uint bits = unchecked((uint)squared);
        int leading = BitOperations.LeadingZeroCount(squared < 0 ? ~bits : bits);
        registers = registers with { Geometry = registers.Geometry with { Lzcs = bits, Lzcr = checked((uint)leading) } };
        frame = Publish(frame, registers);
        int even = leading & ~1;
        int selector = even > 24 ? unchecked(squared << (even - 24)) : squared >> (24 - even);
        if (resources.SqrtWords256.Length != 256 || (uint)selector >= 256)
        {
            throw new NotSupportedException("Breath fibre length reaches outside its actual addressed square-root table.");
        }
        int lengthQ12 = unchecked(resources.SqrtWords256[selector] << ((31 - even) >> 1));
        int length = lengthQ12 >> 12;
        NativeBattleModulePoolTask task = Read(frame, taskAddress);
        int normalFactor = lengthQ12 >> (task.ReadByte(18) & 31);
        frame = WeightFibre(frame, Vector(firstNormal8), normalFactor, false, inputs);
        NativeBattleVector firstControl = AddFibre(first, FibreIr(frame));
        frame = WeightFibre(frame, Vector(secondNormal8), normalFactor, false, inputs);
        NativeBattleVector secondControl = AddFibre(second, FibreIr(frame));
        (frame, uint address) = Allocate(frame, 0x100);
        NativeBattleEffectArena records = Context(frame, address, 0x100);
        records = NativeBattleEffectNodeContextMemory.Copy(firstPoint8, 0, records, 0, 8);
        records = NativeBattleEffectNodeContextMemory.Copy(secondPoint8, 0, records, 0xF0, 8);
        records = records.Write(6, BitConverter.GetBytes(task.ReadWord(32))).Write(14, BitConverter.GetBytes(task.ReadWord(34))).Write(0xF6, BitConverter.GetBytes(task.ReadWord(122))).Write(0xFE, BitConverter.GetBytes(task.ReadWord(124)));
        for (int index = 1; index < 15; index++)
        {
            int ratio = index * 4096 / 15;
            int inverse = 4096 - ratio;
            frame = WeightFibre(frame, first, inverse, false, inputs);
            frame = WeightFibre(frame, firstControl, ratio, true, inputs);
            NativeBattleVector left = FibreIr(frame);
            frame = WeightFibre(frame, second, ratio, false, inputs);
            frame = WeightFibre(frame, secondControl, inverse, true, inputs);
            NativeBattleVector right = FibreIr(frame);
            frame = WeightFibre(frame, left, inverse, false, inputs);
            frame = WeightFibre(frame, right, ratio, true, inputs);
            NativeBattleVector point = FibreIr(frame);
            int offset = index * 16;
            records = records.Write(offset, BitConverter.GetBytes(point.X)).Write(offset + 2, BitConverter.GetBytes(point.Y)).Write(offset + 4, BitConverter.GetBytes(point.Z)).Write(offset + 6, BitConverter.GetBytes(task.ReadWord(32 + index * 6))).Write(offset + 14, BitConverter.GetBytes(task.ReadWord(34 + index * 6)));
        }
        uint working = frame.State.ReadDword(0x252115C);
        for (int index = 1; index < 15; index++)
        {
            int amplitude = unchecked(Word(frame, working + 0x35C0u + (uint)(index * 2)) * length) >> 12;
            for (int axis = 0; axis < 3; axis++)
            {
                NativeCrtRandomState random = frame.Random.Draw(out ushort sample);
                frame = frame with { Random = random };
                int displacement = unchecked((sample - 0x4000) * amplitude) >> 20;
                int offset = index * 16 + axis * 2;
                short value = BitConverter.ToInt16(records.Read(offset, 2));
                records = records.Write(offset, BitConverter.GetBytes(unchecked((short)(value + displacement))));
            }
        }
        return new(PublishContext(frame, address, records, 0x100), address, new(address));
    }

    private static NativeBattleBreath129Frame WeightFibre(NativeBattleBreath129Frame frame, NativeBattleVector vector, int factor, bool accumulate, NativeBattleBreath129Inputs inputs)
    {
        NativeBattleGeometryRegisterStep registers = Registers(frame);
        registers = registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)factor) } };
        registers = NativeBattleGeometry.LoadIrColumn(registers, unchecked((ushort)vector.X), unchecked((ushort)vector.Y), unchecked((ushort)vector.Z));
        return Publish(frame, NativeBattleRaldo19BallRendering.Weighted(registers, accumulate, inputs.Colors));
    }

    private static NativeBattleVector FibreIr(NativeBattleBreath129Frame frame)
    {
        NativeBattleGeometryRegisterStep registers = Registers(frame);
        return new(unchecked((short)registers.Colors.Ir[0]!.Value), unchecked((short)registers.Colors.Ir[1]!.Value), unchecked((short)registers.Colors.Ir[2]!.Value));
    }
    private static NativeBattleVector AddFibre(NativeBattleVector first, NativeBattleVector second) => new(unchecked((short)(first.X + second.X)), unchecked((short)(first.Y + second.Y)), unchecked((short)(first.Z + second.Z)));
}
