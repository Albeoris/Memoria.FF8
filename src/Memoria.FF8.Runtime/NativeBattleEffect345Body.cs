using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect345Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect345Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect345Body
{
    public static NativeBattleEffect345Frame InitializeAppearance70C810(NativeBattleEffect345Frame frame, uint address, NativeBattleEffect345Resources resources, NativeBattleEffect345Inputs inputs)
    {
        frame = NativeBattleEffect345Controller.InitializeAppearance70C840(frame, AppearanceContext, AppearanceProjectedRows, AppearanceDescriptor, address + 0x30);
        return Write(frame, address, Read(frame, address).WriteByte(0x26, (byte)(Read(frame, address).ReadByte(0x26) | 8)));
    }

    public static NativeBattleEffect345Frame ApplyMaterial70C590(NativeBattleEffect345Frame frame, uint address, NativeBattleEffect345Resources resources, NativeBattleEffect345Inputs inputs, short mode = 1)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleEffectArena scratch = NativeBattleEffectNodeContextMemory.Copy(TaskView(task, 0x30, 0x80), 0, NativeBattleEffectArena.CreateUnknown(0x80), 0, 0x80);
        NativeBattleEffectArena model = Material70C5B0(scratch, unchecked((short)task.ReadWord(0x13A)), mode, inputs.ReadModelColors(frame).Color40);
        return Write(frame, address, CopyTask(task, 0x30, model, 0x80));
    }

    public static NativeBattleEffectArena Material70C5B0(NativeBattleEffectArena model, short fade, short mode, uint color)
    {
        if (fade == 0)
        {
            return Put32(model, 0x28, color).Write(0x2B, [0]).Write(7, [0]);
        }
        if (mode == 1)
        {
            color = (color & 0xFF000000) | ((color & 0xFEFEFE) >> 1);
        }
        if (mode is 1 or 2 or 3)
        {
            model = model.Write(0x2B, [2]);
        }
        if (mode == 3)
        {
            model = model.Write(7, [(byte)(unchecked((byte)(255 - (byte)(255 * fade / 4096))) >> 2)]);
        }
        for (int lane = 0; lane < 3; lane++)
        {
            byte channel = (byte)(color >> (lane * 8));
            model = model.Write(0x28 + lane, [unchecked((byte)(channel - channel * fade / 4096))]);
        }
        return model;
    }

    public static NativeBattleEffect345Frame ApplyColor70C6B0(NativeBattleEffect345Frame frame, uint address, NativeBattleEffect345Resources resources, NativeBattleEffect345Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        short fade = unchecked((short)task.ReadWord(0x13C));
        uint color = inputs.ReadModelColors(frame).Color44;
        for (int lane = 0; lane < 3; lane++)
        {
            byte channel = (byte)(color >> (lane * 8));
            task = task.WriteByte(0x5C + lane, unchecked((byte)(channel - channel * fade / 4096)));
        }
        return Write(frame, address, task);
    }

    public static NativeBattleEffect345Frame Draw707820(NativeBattleEffect345Frame frame, uint contextAddress, ImmutableArray<byte> mesh, uint meshAddress, NativeBattleEffect345Resources resources, NativeBattleEffect345Inputs inputs)
    {
        NativeBattleGeometryRegisterStep registers = Registers(frame);
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect345 mesh drawing requires its current geometry controls.");
        NativeBattleModelRendererRegisters far = frame.Memory.PrimitiveProcess?.ModelRendererRegisters ?? throw new NotSupportedException("Effect345 mesh drawing requires its current far-color controls.");
        NativeBattleEffectArena context = ReadBytes(frame, contextAddress, 104);
        uint flags = Dword345(context, 0x14);
        uint selected = (flags & 0x2000) != 0 ? Dword345(context, 4) : checked(meshAddress + 8);
        (NativeBattleVector Vertex, short? W) ReadSelected(int offset)
        {
            NativeBattleEffectArena point = inputs.ReadPhysical(frame, checked(selected + (uint)offset), 8);
            return (Vector(point), NullableWord(point, 6));
        }
        short? padding = registers.Geometry.Rotation20[18] is byte low && registers.Geometry.Rotation20[19] is byte high ? unchecked((short)(low | high << 8)) : null;
        NativeBattleActorWaveMeshStep drawn = NativeBattleActorWaveMesh.EmitBatch(mesh, meshAddress, NativeBattleGeometry.CaptureMatrix(registers.Geometry), inputs.ReadProjection(frame), context, registers, far, controls.Dqa, controls.Dqb, controls.Zsf4, inputs.Colors, padding, controls.Zsf3, selectedVerticesAddress: (flags & 0x2000) != 0 ? selected : null, readSelectedVertex: (flags & 0x2000) != 0 ? ReadSelected : null);
        frame = Publish(frame, drawn.Registers);
        frame = frame with { Memory = frame.Memory with { PrimitiveProcess = frame.Memory.PrimitiveProcess! with { ModelRendererRegisters = drawn.FarControls } } };
        frame = WriteBytes(frame, contextAddress, drawn.Context104, 104);
        return inputs.Execute(frame, new NativeBattleEffect345DrawBatch(drawn.Batch, PrimaryCursorGlobal));
    }

    public static NativeBattleEffect345Frame Draw70CEC0(NativeBattleEffect345Frame frame, uint address, NativeBattlePackedMatrix32 basis, NativeBattleEffect345Resources resources, NativeBattleEffect345Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if ((task.ReadByte(0x26) & 4) != 0)
        {
            return frame;
        }
        NativeBattleActionMatrix world = NativeBattlePoseMatrices.Scale56BEF0(basis.ReadMatrix() with { X = unchecked((short)task.ReadWord(0x1C)), Y = unchecked((short)task.ReadWord(0x1E)), Z = unchecked((short)task.ReadWord(0x20)) }, unchecked((int)task.ReadDword(0x30)), unchecked((int)task.ReadDword(0x34)), unchecked((int)task.ReadDword(0x38)));
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), world);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(composed, null));
        uint temporary;
        (frame, temporary) = Allocate(frame, 104);
        NativeBattleEffectArena context = ReadBytes(frame, temporary, 104);
        context = NativeBattleActorWaveMesh.PrepareContext(context, task.ReadDword(0x4C), 0, unchecked((short)task.ReadWord(0x50)), task.ReadDword(0x40));
        context = Put32(context, 8, task.ReadDword(0x40));
        context = Put32(context, 12, unchecked((uint)(int)(short)task.ReadWord(0x50)));
        context = Put32(context, 0x10, unchecked((uint)(int)(short)task.ReadWord(0x52)));
        context = Put32(context, 0x14, 0xF0);
        frame = WriteBytes(frame, temporary, context, 104);
        uint meshAddress = task.ReadDword(0x4C);
        frame = Draw707820(frame, temporary, resources.ReadStatic(meshAddress, NativeBattleEffect345Resources.MeshLength), meshAddress, resources, inputs);
        return Release(frame, 104);
    }

    private static NativeBattleEffectArena Put16(NativeBattleEffectArena arena, int offset, ushort value) => arena.Write(offset, BitConverter.GetBytes(value));
    private static NativeBattleEffectArena Put32(NativeBattleEffectArena arena, int offset, uint value) => arena.Write(offset, BitConverter.GetBytes(value));
    private static short? NullableWord(NativeBattleEffectArena arena, int offset)
    {
        (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset, 2);
        return known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
    }
}
