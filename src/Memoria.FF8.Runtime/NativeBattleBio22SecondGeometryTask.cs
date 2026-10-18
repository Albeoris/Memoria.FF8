using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBio22SecondGeometryFrame(NativeBattleModulePoolTask Task, NativeBattleModulePoolTask Primary, NativeBattleEffectMemory Memory, NativeCrtRandomState Random);
public sealed record NativeBattleBio22ParticleAppend(uint ParentAddress, NativeBattleVector Position, NativeBattleVector Velocity, NativeBattleVector Acceleration)
{
    public const uint Descriptor = 0x270E540;
    public const uint Callback = 0x89C930;
    public const int Size = 0x70;
}
public sealed record NativeBattleBio22OverlayAppend(uint ParentAddress, uint ProgramAddress)
{
    public const uint Descriptor = 0x27044E0;
    public const uint Callback = 0x89C650;
    public const int Size = 0x3C;
}
public sealed record NativeBattleBio22SecondGeometryInputs(Func<NativeBattleBio22SecondGeometryFrame, NativeBattleBio22Geometry, bool, NativeBattleBio22SecondGeometryFrame> Draw, Func<NativeBattleBio22SecondGeometryFrame, NativeBattleBio22ParticleAppend, NativeBattleBio22SecondGeometryFrame> AppendParticle, Func<NativeBattleBio22SecondGeometryFrame, NativeBattleBio22OverlayAppend, NativeBattleBio22SecondGeometryFrame> AppendOverlay);
public sealed record NativeBattleBio22SecondGeometryStep(NativeBattleBio22SecondGeometryFrame Frame, uint Result)
{
    [JsonIgnore]
    public bool CompleteRequested => Result == 2;
}

public static class NativeBattleBio22SecondGeometryTask
{
    public static NativeBattleBio22SecondGeometryStep Advance89C270(uint selfAddress, NativeBattleBio22SecondGeometryFrame frame, NativeBattleBio22Resources resources, NativeBattleBio22SecondGeometryInputs inputs)
    {
        NativeBattleModulePoolTask task = frame.Task;
        if (task.Payload.IsDefault || task.Payload.Length != 0x98 - 12 || task.Callback != 0x89C270)
        {
            throw new InvalidDataException("Native Bio second geometry requires its actual hierarchy task.");
        }
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                uint scale = unchecked((uint)S(frame.Primary, 0x86));
                task = task.WriteDword(0x1C, frame.Primary.ReadDword(0x78)).WriteDword(0x20, frame.Primary.ReadDword(0x7C)).WriteWord(0x46, frame.Primary.ReadWord(0x82)).WriteDword(0x4C, 0x1619888).WriteWord(0x84, 0x400).WriteDword(0x6C, 0x16189E8).WriteDword(0x70, 0x161E8B0).WriteWord(0x50, 0).WriteDword(0x30, scale).WriteDword(0x34, scale).WriteDword(0x38, scale);
                frame = MorphDraw(frame with { Task = task }, resources, inputs);
                frame = frame with { Task = frame.Task.WriteByte(0x29, 1) };
                break;
            case 1:
                task = task.WriteWord(0x84, W(task.ReadWord(0x84) + 0x400));
                if (S(task, 0x84) >= 0x1000)
                {
                    task = task.WriteWord(0x74, 4).WriteWord(0x76, 0).WriteWord(0x84, 0).WriteDword(0x4C, 0x161F750).WriteDword(0x6C, Pointer(resources, 4)).WriteDword(0x70, Pointer(resources, 0)).WriteByte(0x29, 2);
                }
                frame = MorphDraw(frame with { Task = task }, resources, inputs);
                break;
            case 2:
                task = task.WriteWord(0x84, W(task.ReadWord(0x84) + 0x400));
                if (S(task, 0x84) >= 0x1000)
                {
                    NativeCrtRandomState random = frame.Random.Draw(out ushort value);
                    int second = unchecked((short)(task.ReadWord(0x76) + value % 3 + 1));
                    while (second >= 4)
                    {
                        second -= 4;
                    }
                    task = task.WriteWord(0x74, task.ReadWord(0x76)).WriteWord(0x76, W(second));
                    task = task.WriteDword(0x6C, Pointer(resources, S(task, 0x74))).WriteDword(0x70, Pointer(resources, S(task, 0x76))).WriteWord(0x8A, W(task.ReadWord(0x8A) + 1)).WriteWord(0x84, 0);
                    frame = frame with { Random = random };
                }
                frame = MorphDraw(frame with { Task = task }, resources, inputs);
                if (S(frame.Task, 0x8A) >= 5)
                {
                    frame = Emit89C750(selfAddress, frame, 0, inputs.AppendParticle);
                    frame = frame with { Primary = frame.Primary.WriteWord(0x80, 1) };
                    frame = inputs.AppendOverlay(frame, new(selfAddress, 0x162693C));
                    frame = frame with { Task = frame.Task.WriteByte(0x29, 3) };
                }
                break;
            case 3:
                frame = Emit89C750(selfAddress, frame, 1, inputs.AppendParticle);
                frame = frame with { Task = frame.Task.WriteByte(0x29, 4) };
                break;
            case 4:
                frame = Emit89C750(selfAddress, frame, 2, inputs.AppendParticle);
                frame = frame with { Task = frame.Task.WriteByte(0x29, 5) };
                break;
            case 5:
                frame = frame with { Task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)).WriteByte(0x29, 6) };
                break;
            case 6:
                break;
            default:
                throw new NotSupportedException("Native Bio second geometry phase leaves its selected callback table.");
        }
        frame = Pulsate(frame);
        byte flags = frame.Task.ReadByte(0x26);
        task = frame.Task.WriteWord(0x24, W(frame.Task.ReadWord(0x24) + 1));
        return new(frame with { Task = task }, (flags & 1) != 0 && task.ReadByte(0x28) == 0 ? 2u : 0u);
    }

    public static NativeBattleBio22SecondGeometryFrame Emit89C750(uint selfAddress, NativeBattleBio22SecondGeometryFrame frame, short selector, Func<NativeBattleBio22SecondGeometryFrame, NativeBattleBio22ParticleAppend, NativeBattleBio22SecondGeometryFrame> append)
    {
        if (selector is < 0 or > 2)
        {
            throw new NotSupportedException("Native Bio particle emission requires one of its three reached selectors.");
        }
        NativeBattleModulePoolTask task = frame.Task;
        NativeBattleBio22Geometry mesh = Current(frame.Memory, task.ReadDword(0x4C));
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4)) / 8 - 1;
        if (count <= 0)
        {
            return frame;
        }
        NativeBattleActionMatrix world = NativeBattleGfMatrices.Euler56CD50(new(S(task, 0x44), S(task, 0x46), S(task, 0x48)));
        world = world with { X = S(task, 0x1C), Y = S(task, 0x1E), Z = S(task, 0x20) };
        world = NativeBattlePoseMatrices.Scale56BEF0(world, unchecked((int)task.ReadDword(0x30)), unchecked((int)task.ReadDword(0x34)), unchecked((int)task.ReadDword(0x38)));
        for (int index = 0; index < count; index++)
        {
            int offset = checked(8 + selector * 16 + index * 0x40);
            NativeBattleVector source = new(BinaryPrimitives.ReadInt16LittleEndian(mesh.Bytes.AsSpan().Slice(offset)), BinaryPrimitives.ReadInt16LittleEndian(mesh.Bytes.AsSpan().Slice(offset + 2)), BinaryPrimitives.ReadInt16LittleEndian(mesh.Bytes.AsSpan().Slice(offset + 4)));
            NativeBattleVector position = NativeBattleGfMatrices.Transform460840(world, source);
            NativeBattleVector velocity = new((short)(unchecked((short)(position.X - S(task, 0x1C))) / 8), (short)(unchecked((short)(position.Y - S(task, 0x1E))) / 8), (short)(unchecked((short)(position.Z - S(task, 0x20))) / 8));
            NativeBattleVector acceleration = new((short)-(velocity.X / 16), (short)-(velocity.Y / 16), (short)-(velocity.Z / 16));
            frame = append(frame, new(selfAddress, position, velocity, acceleration));
        }
        return frame;
    }

    private static NativeBattleBio22SecondGeometryFrame MorphDraw(NativeBattleBio22SecondGeometryFrame frame, NativeBattleBio22Resources resources, NativeBattleBio22SecondGeometryInputs inputs)
    {
        uint address = frame.Task.ReadDword(0x4C);
        NativeBattleBio22Geometry current = Current(frame.Memory, address);
        NativeBattleBio22Geometry next = current.Morph89B0D0(Source(resources, frame.Task.ReadDword(0x6C)), Source(resources, frame.Task.ReadDword(0x70)), S(frame.Task, 0x84));
        if (!next.Bytes.Equals(current.Bytes))
        {
            next.ValidateForAddress(address);
        }
        NativeBattleEffectMemory memory = address == 0x1619888 ? frame.Memory with { Bio22ThirdGeometry = next } : frame.Memory with { Bio22FourthGeometry = next };
        return inputs.Draw(frame with { Memory = memory }, next, frame.Primary.ReadWord(0x88) == 1);
    }

    private static NativeBattleBio22SecondGeometryFrame Pulsate(NativeBattleBio22SecondGeometryFrame frame)
    {
        NativeBattleModulePoolTask task = frame.Task;
        NativeCrtRandomState random = frame.Random;
        int[] masks = [0x7F, 0x3F, 0x3F];
        int[] additions = [0x80, 0x40, 0x10];
        for (int axis = 0; axis < 3; axis++)
        {
            random = random.Draw(out ushort value);
            int offset = 0x64 + axis * 2;
            task = task.WriteWord(offset, W((task.ReadWord(offset) + (value & masks[axis]) + additions[axis]) & 0x7FF));
        }
        int q = S(frame.Primary, 0x86);
        for (int axis = 0; axis < 3; axis++)
        {
            int addition = unchecked(q / 4 * NativeBattleCameraMath.Sine(S(task, 0x64 + axis * 2))) / 4096;
            task = task.WriteDword(0x30 + axis * 4, unchecked((uint)(q + addition)));
        }
        random = random.Draw(out ushort yaw1);
        task = task.WriteWord(0x44, W((task.ReadWord(0x44) + (yaw1 & 0x1F) + 0x20) & 0xFFF));
        random = random.Draw(out ushort yaw2);
        task = task.WriteWord(0x46, W((task.ReadWord(0x46) + (yaw2 & 0x1F) + 0x20) & 0xFFF));
        return frame with { Task = task, Random = random };
    }

    private static NativeBattleBio22Geometry Current(NativeBattleEffectMemory memory, uint address) => address switch
    {
        0x1619888 => memory.Bio22ThirdGeometry ?? throw new NotSupportedException("Native Bio requires its retained third geometry owner."),
        0x161F750 => memory.Bio22FourthGeometry ?? throw new NotSupportedException("Native Bio requires its retained fourth geometry owner."),
        _ => throw new NotSupportedException("Native Bio second geometry pointer leaves its two physical owners.")
    };

    private static uint Pointer(NativeBattleBio22Resources resources, int index)
    {
        if (index < 0 || index >= 5 || resources.SecondFamilyMorphPointers.Data.IsDefault || resources.SecondFamilyMorphPointers.Data.Length != 20)
        {
            throw new InvalidDataException("Native Bio second source index leaves its five-entry pointer directory.");
        }
        return BinaryPrimitives.ReadUInt32LittleEndian(resources.SecondFamilyMorphPointers.Data.AsSpan().Slice(index * 4));
    }

    private static ImmutableArray<byte> Source(NativeBattleBio22Resources resources, uint address)
    {
        foreach (NativeBattleEffectHeapAllocation source in resources.SecondFamilyInitialSources.Concat(resources.SecondFamilyMorphSources))
        {
            if (source.Identity == $"FF8_EN:{address:X}")
            {
                return source.Data;
            }
        }
        throw new NotSupportedException("Native Bio second morph source pointer has no authenticated current resource.");
    }

    private static short S(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static ushort W(int value) => unchecked((ushort)value);
}
