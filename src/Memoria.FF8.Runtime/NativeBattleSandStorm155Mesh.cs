using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleSandStorm155Mesh
{
    public static (NativeBattleSandStorm155Frame Frame, int Result) Advance607210(NativeBattleSandStorm155Frame frame, uint address, NativeBattleSandStorm155Resources resources, NativeBattleSandStorm155Inputs inputs, uint graphicsFlags)
    {
        NativeBattleModulePoolTask task = NativeBattleSandStorm155Controller.Read(frame, address);
        short age = Signed(task, 12);
        if (age < 30)
        {
            int fraction = unchecked(age << 12) / 29;
            short anchorX = Word(frame, 0x23F2958);
            short anchorZ = Word(frame, 0x23F295C);
            short x = unchecked((short)(anchorX + (unchecked((Word(frame, 0x23F2938) - anchorX) * fraction) >> 12)));
            short z = unchecked((short)(anchorZ + (unchecked((Word(frame, 0x23F293C) - anchorZ) * fraction) >> 12)));
            short lateral = unchecked((short)(unchecked(NativeBattleCameraMath.Sine(Signed(task, 0x20)) * Signed(task, 0x1C)) >> 12));
            NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(inputs.ReadModelWorld(frame, checked((int)frame.State.ReadDword(0x23F2950))), new(lateral, 0, 0));
            frame = PutWord(frame, 0x23F2948, unchecked((short)(x + offset.X)));
            frame = PutWord(frame, 0x23F294C, unchecked((short)(z + offset.Z)));
        }
        else
        {
            int sample = age - 30;
            if (sample >= 34)
            {
                sample -= 34;
            }
            frame = frame with { State = frame.State.WriteView(0x23F2948, frame.State.ReadView(checked(0x23F2960u + (uint)(sample * 8)), 8), 8) };
        }
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(0, Signed(task, 0x18), 0)) with { X = Word(frame, 0x23F2948), Y = 0, Z = Word(frame, 0x23F294C) };
        NativeBattleActionMatrix first = NativeBattlePoseMatrices.Scale56BEF0(local, 8192, Signed(task, 0x12), 8192);
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), first);
        frame = Load(frame, composed, true);
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging ?? throw new NotSupportedException("Sand Storm requires its actual addressed mesh context."), 88);
        uint temporary = allocation.Address;
        NativeBattleEffectArena context = NativeBattleTemporaryMemory.ReadView(allocation.Staging, temporary, 88);
        uint flags = 0x2033;
        int cue = 0;
        if (age < 6)
        {
            cue = unchecked(4096 - age * 682);
            flags = 0x20F3;
        }
        else if (age >= 52)
        {
            cue = unchecked((age - 52) << 9);
            flags = 0x20F3;
        }
        context = context.Write(0, BitConverter.GetBytes(0xDB8150u)).Write(4, BitConverter.GetBytes(NativeBattleSandStorm155Geometry.SelectedVertices)).Write(8, BitConverter.GetBytes(0u)).Write(12, BitConverter.GetBytes(cue)).Write(0x1C, BitConverter.GetBytes(flags));
        frame = frame with { Staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, temporary, context, 88) };
        frame = inputs.Execute(frame, new NativeBattleSandStorm155DrawMesh88(temporary, resources.Mesh.Address) { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(155, task, 0) with { ResourceGeneration = resources.Mesh.Address } });
        NativeBattleActionMatrix second = NativeBattlePoseMatrices.Scale56BEF0(local, 6600, Signed(task, 0x12), 6600);
        composed = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), second);
        frame = Load(frame, composed, false);
        context = NativeBattleTemporaryMemory.ReadView(frame.Staging!, temporary, 88);
        cue = BinaryPrimitives.ReadInt32LittleEndian(context.Read(12, 4));
        flags = BinaryPrimitives.ReadUInt32LittleEndian(context.Read(0x1C, 4));
        cue = unchecked(cue + unchecked(4096 - cue) / 2);
        context = context.Write(12, BitConverter.GetBytes(cue)).Write(0x1C, BitConverter.GetBytes(flags | 0xC0));
        frame = frame with { Staging = NativeBattleTemporaryMemory.PublishView(frame.Staging!, temporary, context, 88) };
        frame = inputs.Execute(frame, new NativeBattleSandStorm155DrawMesh88(temporary, resources.Mesh.Address) { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(155, task, 1) with { ResourceGeneration = resources.Mesh.Address } });
        frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(frame.Staging!, 88) };
        if ((graphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        task = NativeBattleSandStorm155Controller.Read(frame, address);
        if (age < 10)
        {
            short speed = Signed(task, 0x16);
            task = task.WriteWord(0x12, unchecked((ushort)(task.ReadWord(0x12) + speed))).WriteWord(0x16, unchecked((ushort)(speed - (speed >> 4))));
        }
        short angularSpeed = Signed(task, 0x22);
        task = task.WriteWord(0x18, unchecked((ushort)(task.ReadWord(0x18) + task.ReadWord(0x1A)))).WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) + task.ReadWord(0x1E)))).WriteWord(0x20, unchecked((ushort)(task.ReadWord(0x20) + angularSpeed))).WriteWord(0x22, unchecked((ushort)(angularSpeed - (angularSpeed >> 5))));
        frame = NativeBattleSandStorm155Controller.Write(frame, address, task);
        if (age == 29)
        {
            frame = NativeBattleSandStorm155Curve.PrepareSamples60752A(frame);
        }
        task = NativeBattleSandStorm155Controller.Read(frame, address).WriteWord(12, unchecked((ushort)(NativeBattleSandStorm155Controller.Read(frame, address).ReadWord(12) + 1)));
        return (NativeBattleSandStorm155Controller.Write(frame, address, task), Signed(task, 12) >= 60 ? 2 : 0);
    }

    private static NativeBattleSandStorm155Frame Load(NativeBattleSandStorm155Frame frame, NativeBattleActionMatrix matrix, bool translation)
    {
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(matrix, null);
        NativeBattleGeometryRegisters geometry = NativeBattleGeometry.LoadMatrix(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Sand Storm requires its actual current geometry registers."), matrix, true, translation, null);
        geometry = geometry with { Rotation20 = Enumerable.Range(0, 20).Select(index => packed.Known[index] ? (byte?)packed.Bytes[index] : null).ToImmutableArray() };
        return frame with { Memory = frame.Memory with { GeometryRegisters = geometry } };
    }

    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static short Word(NativeBattleSandStorm155Frame frame, uint address) => BinaryPrimitives.ReadInt16LittleEndian(frame.State.ReadView(address, 2).Read(0, 2));
    private static NativeBattleSandStorm155Frame PutWord(NativeBattleSandStorm155Frame frame, uint address, short value) => frame with { State = frame.State.Write(address, BitConverter.GetBytes(value)) };
}
