using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect318Controller;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect318Lower
{
    public static NativeBattleEffect318Step Advance(NativeBattleEffect318Frame frame, uint address, NativeBattleEffect318Resources resources, NativeBattleEffect318Inputs inputs)
    {
        return Read(frame, address).Callback switch
        {
            0x65F100 => NativeBattleEffect318Sprite.Advance(frame, address, resources, inputs),
            0x65F2B0 => AdvanceBone(frame, address, inputs),
            _ => throw new NotSupportedException("The effect requires its selected lower callback.")
        };
    }

    public static NativeBattleEffect318Frame Birth65F250(NativeBattleEffect318Frame frame, int boneIndex, int delay, int stepZ, int stepXY, NativeBattleEffect318Inputs inputs)
    {
        (frame, uint address) = Append(frame, 0x2505438, 0x65F2B0);
        if (address == 0)
        {
            throw new InvalidDataException("The selected bone task requires its current available pool slot.");
        }
        uint pointer = inputs.ResolveBoneRecord(frame, frame.State.ReadDword(0x2505424), boneIndex);
        NativeBattleModulePoolTask task = Read(frame, address).WriteDword(12, pointer).WriteWord(16, unchecked((ushort)delay)).WriteWord(24, unchecked((ushort)stepXY)).WriteWord(20, unchecked((ushort)stepZ)).WriteWord(18, 0).WriteWord(22, 0);
        return Write(frame, address, task);
    }

    public static NativeBattleEffect318Step AdvanceBone(NativeBattleEffect318Frame frame, uint address, NativeBattleEffect318Inputs inputs)
    {
        NativeBattleBoneScaleTaskStep step = NativeBattleBoneScaleTask65F2B0.Advance(Read(frame, address));
        NativeBattleModulePoolTask task = step.Task;
        frame = Write(frame, address, task);
        if (step.Result == 2)
        {
            return new(frame, 2);
        }
        frame = inputs.Execute(frame, new NativeBattleEffect318ScaleBone56BEF0(task.ReadDword(12), Signed(task, 22), Signed(task, 18)));
        return new(frame, 0);
    }

    public static NativeBattleEffect318Frame DrawMesh572200(NativeBattleEffect318Frame frame, uint temporary88, NativeBattleEffect318Resources resources, NativeBattleEffect318Inputs inputs)
    {
        NativeBattleEffectArena context = Context(frame, temporary88, 88);
        NativeBattleStaticMeshContext88Step draw = NativeBattleStaticMeshContext88.DrawLoaded(frame.Memory, resources.ReadMesh(Dword318(context, 0)), inputs.ReadProjection(frame), context, inputs.Colors, 17, 2);
        frame = PublishContext(frame with { Memory = draw.Memory }, temporary88, draw.Context, 88);
        return inputs.Execute(frame, new NativeBattleEffect318DrawBatch(draw.Batch, 0x250541C));
    }

    public static NativeBattleEffectArena Direction65F0A0(NativeBattleEffectArena angles8, NativeBattleEffectArena retained8)
    {
        int first = Word318(angles8, 0);
        int second = Word318(angles8, 2);
        int cosine = NativeBattleCameraMath.Cosine(first);
        retained8 = PutWord318(retained8, 2, -NativeBattleCameraMath.Sine(first));
        retained8 = PutWord318(retained8, 0, unchecked(NativeBattleCameraMath.Sine(second) * cosine) >> 12);
        return PutWord318(retained8, 4, unchecked(-NativeBattleCameraMath.Cosine(second) * cosine) >> 12);
    }

    public static NativeBattleEffectArena Direction65F7E0(NativeBattleEffectArena angles8, NativeBattleEffectArena retained8)
    {
        int first = Word318(angles8, 0);
        int third = Word318(angles8, 4);
        int negativeSine = -NativeBattleCameraMath.Sine(first);
        retained8 = PutWord318(retained8, 4, NativeBattleCameraMath.Cosine(first));
        retained8 = PutWord318(retained8, 0, unchecked(NativeBattleCameraMath.Sine(third) * negativeSine) >> 12);
        return PutWord318(retained8, 2, unchecked(-NativeBattleCameraMath.Cosine(third) * negativeSine) >> 12);
    }

    public static uint Dword318(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(value.Read(offset, 4));
    public static short Word318(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadInt16LittleEndian(value.Read(offset, 2));
    public static NativeBattleEffectArena Put318(NativeBattleEffectArena value, int offset, uint number) => value.Write(offset, BitConverter.GetBytes(number));
    public static NativeBattleVector Vector318(NativeBattleEffectArena value) => new(Word318(value, 0), Word318(value, 2), Word318(value, 4));

    public static NativeBattleEffectArena PutWord318(NativeBattleEffectArena value, int offset, int number) => value.Write(offset, BitConverter.GetBytes(unchecked((short)number)));
}
