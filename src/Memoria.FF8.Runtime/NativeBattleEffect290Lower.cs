using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect290Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect290Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect290Lower
{
    public static NativeBattleEffect290Step Advance(NativeBattleEffect290Frame frame, uint address, NativeBattleEffect290Resources resources, NativeBattleEffect290Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback == 0x676400)
        {
            NativeBattleActionMatrix local = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], Signed(task, 12), Signed(task, 14), Signed(task, 16), 4096);
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
            NativeBattleEffectArena caller = PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, null));
            for (int axis = 0; axis < 3; axis++)
            {
                caller = Put290(caller, 0x20 + axis * 4, unchecked((uint)(int)Signed(task, 0x12)));
            }
            caller = Put290(Put290(Put290(caller, 0x2C, 1), 0x30, unchecked((uint)-128)), 0x34, checked(frame.State.ReadDword(ArenaGlobal) + MorphOffset));
            NativeBattleEffect290Step result = NativeBattleEffect290Programme.Advance(frame, address + 0x14, 0x110, caller, resources, inputs);
            return new(result.Frame, result.Result == 0 ? 2 : 0);
        }
        if (task.Callback is not (0x677550 or 0x6775F0))
        {
            throw new NotSupportedException("The effect requires its selected lower callback.");
        }
        bool second = task.Callback == 0x6775F0;
        NativeBattleEffectArena point = TaskView(task, 12, 8);
        (byte[] bytes, bool[] known) = point.ReadSnapshot(0, 8);
        short? w = known[6] && known[7] ? BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(6)) : null;
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), Vector(point), w, second ? (short)6000 : (short)2048, -1024) };
        frame = Publish(frame, Registers(frame) with { Colors = Registers(frame).Colors with { NormalInput = bytes.ToImmutableArray(), NormalInputKnown = known.ToImmutableArray() } });
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena context = PutWord290(PutWord290(Put290(Context(frame, temporary, 180), 0, second ? SecondSpriteAddress : FirstSpriteAddress), 4, Signed(task, 0x12)), 0x24, 0);
        NativeBattleModuleSpriteDraw draw = NativeBattleModuleSpriteDrawing.Draw571C80(frame.Memory, context, resources.Sprite, inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame), 17, 2, pilotIdentity: inputs.CapturePresentation ? NativeBattleOffsetBodyVisuals.At(290, frame.Effect.Pools, address, second ? SecondSpriteAddress : FirstSpriteAddress) : null);
        frame = PublishContext(frame with { Memory = draw.Memory }, temporary, draw.Temporary180, 180);
        frame = inputs.Execute(frame, new NativeBattleEffect290DrawBatch(draw.Batch, CursorGlobal));
        frame = Release(frame, 180);
        task = Read(frame, address);
        for (int axis = 0; axis < 3; axis++)
        {
            task = task.WriteWord(12 + axis * 2, unchecked((ushort)(Signed(task, 12 + axis * 2) + unchecked((sbyte)TaskView(task, 0x14 + axis, 1).Read(0, 1)[0]))));
        }
        task = task.WriteWord(0x12, unchecked((ushort)(Signed(task, 0x12) + 1)));
        return new(Write(frame, address, task), Signed(task, 0x12) >= (second ? 13 : 12) ? 2 : 0);
    }

    public static (NativeBattleEffect290Frame Frame, uint Address) TryBirthTarget(NativeBattleEffect290Frame frame, NativeBattleEffect290Resources resources) => Append(frame, TargetDescriptor, 0x676400);

    public static NativeBattleEffect290Frame FinishBirthTarget(NativeBattleEffect290Frame frame, uint address, NativeBattleEffectArena point8, short scale, NativeBattleEffect290Resources resources)
    {
        NativeBattleModulePoolTask task = CopyTask(Read(frame, address), 12, point8, 8).WriteWord(0x12, unchecked((ushort)scale));
        frame = Write(frame, address, task);
        return NativeBattleEffect290Programme.Initialize(frame, address + 0x14, FirstProgrammeAddress, 0x110, resources);
    }

    public static NativeBattleEffectArena PutWord290(NativeBattleEffectArena data, int offset, int value) => data.Write(offset, BitConverter.GetBytes(unchecked((short)value)));
    public static NativeBattleEffectArena Put290(NativeBattleEffectArena data, int offset, uint value) => data.Write(offset, BitConverter.GetBytes(value));
    public static NativeBattlePackedMatrix32 Packed(NativeBattleEffectArena data)
    {
        (byte[] bytes, bool[] known) = data.ReadSnapshot(0, 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }
    public static NativeBattleGeometryRegisterStep LoadPacked(NativeBattleGeometryRegisterStep step, NativeBattleEffectArena data, int slot, int offset)
    {
        (byte[] bytes, bool[] known) = data.ReadSnapshot(offset, 8);
        short? w = known[6] && known[7] ? BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(6)) : null;
        step = NativeBattleGeometry.LoadVector(step, slot, Vector(data, offset), w);
        return step with { Colors = step.Colors with { NormalInput = bytes.ToImmutableArray(), NormalInputKnown = known.ToImmutableArray() } };
    }
}
