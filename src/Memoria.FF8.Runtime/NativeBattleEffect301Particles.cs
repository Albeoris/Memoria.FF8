using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect301Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect301Lower;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect301Particles
{
    public static NativeBattleEffect301Frame Birth66EB10(NativeBattleEffect301Frame frame, NativeBattleEffect301Resources resources, NativeBattleEffect301Inputs inputs) => Birth(frame, true, resources, inputs);
    public static NativeBattleEffect301Frame Birth66ED70(NativeBattleEffect301Frame frame, NativeBattleEffect301Resources resources, NativeBattleEffect301Inputs inputs) => Birth(frame, false, resources, inputs);

    private static NativeBattleEffect301Frame Birth(NativeBattleEffect301Frame frame, bool mesh, NativeBattleEffect301Resources resources, NativeBattleEffect301Inputs inputs)
    {
        frame = frame with { State = frame.State.BindSources(resources) };
        short count = ReadWord(ref frame, resources);
        if (count < 0)
        {
            throw new InvalidDataException("The particle script requires its current nonnegative record count.");
        }
        for (int index = 0; index < count; index++)
        {
            (frame, uint address) = Append(frame, mesh ? 0x25076F8u : 0x25076E8u, mesh ? 0x66EC50u : 0x66EE60u);
            if (address == 0)
            {
                throw new InvalidDataException($"The current mandatory particle append has exhausted its physical pool at script cursor 0x{frame.State.ReadDword(0x103DC9C):X8}.");
            }
            uint model = frame.State.ReadDword(0x25076E0);
            NativeBattleModulePoolTask task = Read(frame, address);
            for (int axis = 0; axis < 3; axis++)
            {
                NativeBattleEffectArena current = inputs.ReadModelArena9C(frame, model);
                short delta = ReadWord(ref frame, resources);
                task = task.WriteWord(12 + axis * 2, unchecked((ushort)(Word301(current, 0x54 + axis * 4) + delta)));
            }
            task = task.WriteWord(18, 0);
            for (int axis = 0; axis < 3; axis++)
            {
                task = task.WriteBytes(20 + axis, [unchecked((byte)ReadWord(ref frame, resources))]);
            }
            task = task.WriteBytes(23, [2]);
            if (mesh)
            {
                for (int field = 24; field < 28; field++)
                {
                    task = task.WriteBytes(field, [unchecked((byte)ReadWord(ref frame, resources))]);
                }
            }
            frame = Write(frame, address, task);
        }
        return frame;
    }

    private static short ReadWord(ref NativeBattleEffect301Frame frame, NativeBattleEffect301Resources resources)
    {
        uint pointer = frame.State.ReadDword(0x103DC9C);
        short value = resources.ReadScriptWord(pointer);
        frame = frame with { State = frame.State.WriteDword(0x103DC9C, checked(pointer + 2)) };
        return value;
    }

    public static NativeBattleEffect301Step AdvanceMesh(NativeBattleEffect301Frame frame, uint address, NativeBattleEffect301Resources resources, NativeBattleEffect301Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        (frame, uint temporary) = Allocate(frame, 152);
        short first = unchecked((short)(SByte(task, 24) << 4));
        short second = unchecked((short)(SByte(task, 25) << 4));
        NativeBattleActionMatrix y = NativeBattlePandemona291Restoration.Rotation701270(second);
        NativeBattleActionMatrix x = NativeBattlePandemona291Restoration.Rotation701220(first);
        NativeBattleEffectArena context = Context(frame, temporary, 152);
        context = WriteRotation(context, 0x78, y);
        context = WriteRotation(context, 0x58, x);
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Multiply56C090(x, y) with { X = Signed(task, 12), Y = Signed(task, 14), Z = Signed(task, 16) };
        context = WriteRotation(context, 0x58, local);
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
        context = WriteRotation(context, 0x58, composed);
        for (int axis = 0; axis < 3; axis++)
        {
            context = Put301(context, 0x6C + axis * 4, unchecked((uint)(axis == 0 ? composed.X : axis == 1 ? composed.Y : composed.Z)));
        }
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(composed, null));
        context = Put301(Put301(context, 0, 0x103D314), 28, 0);
        frame = PublishContext(frame, temporary, context, 152);
        frame = DrawStatic301(frame, temporary, resources, inputs);
        frame = Release(frame, 152);
        task = Read(frame, address);
        task = task.WriteBytes(24, [unchecked((byte)(task.ReadBytes(24, 1)[0] + task.ReadBytes(26, 1)[0]))]).WriteBytes(25, [unchecked((byte)(task.ReadBytes(25, 1)[0] + task.ReadBytes(27, 1)[0]))]);
        task = task.WriteWord(12, unchecked((ushort)(Signed(task, 12) + (SByte(task, 20) << 4)))).WriteWord(16, unchecked((ushort)(Signed(task, 16) + (SByte(task, 22) << 4))));
        int velocity = SByte(task, 21);
        task = task.WriteWord(14, unchecked((ushort)(Signed(task, 14) + (velocity << 2))));
        if (Signed(task, 14) >= 0)
        {
            velocity = (-velocity) >> 1;
        }
        task = task.WriteBytes(21, [unchecked((byte)(velocity + SByte(task, 23)))]);
        short next = unchecked((short)(Signed(task, 18) + 1));
        return new(Write(frame, address, task.WriteWord(18, unchecked((ushort)next))), next >= 16 ? 2 : 0);
    }

    public static NativeBattleEffect301Step AdvanceSprite(NativeBattleEffect301Frame frame, uint address, NativeBattleEffect301Resources resources, NativeBattleEffect301Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleEffectArena point = TaskView(task, 12, 8);
        (byte[] bytes, bool[] known) = point.ReadSnapshot(6, 2);
        short? w = known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), new(Signed(task, 12), Signed(task, 14), Signed(task, 16)), w, 4096, -1024) };
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena context = PutWord301(PutWord301(Put301(Context(frame, temporary, 180), 0, 0x103D118), 4, task.ReadWord(18)), 0x24, 0);
        frame = PublishContext(frame, temporary, context, 180);
        NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(frame.Memory, context, resources.Sprite, inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame), 17, 2, pilotIdentity: NativeBattleEffect32Presentation.Resource(frame.PilotIdentity, NativeBattleEffect301Resources.SpriteAddresses[0]));
        frame = PublishContext(frame with { Memory = drawn.Memory }, temporary, drawn.Temporary180, 180);
        frame = inputs.Execute(frame, new NativeBattleEffect301DrawBatch(drawn.Batch, 0x250773C));
        frame = Release(frame, 180);
        task = Read(frame, address);
        for (int axis = 0; axis < 3; axis++)
        {
            task = task.WriteWord(12 + axis * 2, unchecked((ushort)(Signed(task, 12 + axis * 2) + SByte(task, 20 + axis))));
        }
        short next = unchecked((short)(Signed(task, 18) + 1));
        return new(Write(frame, address, task.WriteWord(18, unchecked((ushort)next))), next >= 16 ? 2 : 0);
    }

    private static int SByte(NativeBattleModulePoolTask task, int offset) => unchecked((sbyte)task.ReadBytes(offset, 1)[0]);
    private static NativeBattleEffectArena WriteRotation(NativeBattleEffectArena context, int offset, NativeBattleActionMatrix matrix)
    {
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(matrix, null);
        return CopyBytes(PackedArena(packed), 0, context, offset, 20);
    }
}
