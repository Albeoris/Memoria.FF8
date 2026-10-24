using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect321Controller;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect321Lower
{
    public static NativeBattleEffect321Step Advance(NativeBattleEffect321Frame frame, uint address, NativeBattleEffect321Resources resources, NativeBattleEffect321Inputs inputs)
    {
        return Read(frame, address).Callback switch
        {
            0x597E30 => NativeBattleEffect321Rings.Advance(frame, address, false, resources, inputs),
            0x597F90 => NativeBattleEffect321Records.Advance(frame, address, false, resources, inputs),
            0x598230 => NativeBattleEffect321Debris.Advance(frame, address, resources, inputs),
            0x598570 => AdvanceRequest(frame, address, inputs),
            0x5985C0 => NativeBattleEffect321Actor.Advance(frame, address, inputs),
            0x5988B0 => NativeBattleEffect321Records.Advance(frame, address, true, resources, inputs),
            0x598B90 => NativeBattleEffect321Rings.Advance(frame, address, true, resources, inputs),
            _ => throw new NotSupportedException("The effect requires its selected lower callback.")
        };
    }

    public static NativeBattleEffect321Step AdvanceRequest(NativeBattleEffect321Frame frame, uint address, NativeBattleEffect321Inputs inputs)
    {
        frame = inputs.Execute(frame, new NativeBattleEffect321PcRender47CF50(0, 255, 255, 255, 63));
        if (Paused(frame, inputs))
        {
            return new(frame, 0);
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        short next = unchecked((short)(Signed(task, 12) + 1));
        return new(Write(frame, address, task.WriteWord(12, unchecked((ushort)next))), next >= 20 ? 2 : 0);
    }

    public static bool Paused(NativeBattleEffect321Frame frame, NativeBattleEffect321Inputs inputs) => (inputs.ReadFlags(frame) & 0x201) != 0;
    public static short Word(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadInt16LittleEndian(value.Read(offset, 2));
    public static uint Dword(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(value.Read(offset, 4));
    public static NativeBattleEffectArena Put(NativeBattleEffectArena value, int offset, uint number) => value.Write(offset, BitConverter.GetBytes(number));
    public static NativeBattleEffectArena PutWord(NativeBattleEffectArena value, int offset, int number) => value.Write(offset, BitConverter.GetBytes(unchecked((short)number)));
    public static int Random(ref NativeBattleEffect321Frame frame)
    {
        frame = frame with { Random = frame.Random.Draw(out ushort value) };
        return value;
    }
    public static NativeBattleEffect321Frame DrawStatic(NativeBattleEffect321Frame frame, uint temporary, NativeBattleEffect321Resources resources, NativeBattleEffect321Inputs inputs)
    {
        NativeBattleEffectArena context = Context(frame, temporary, 88);
        NativeBattleStaticMeshContext88Step result = NativeBattleStaticMeshContext88.DrawLoaded(frame.Memory, resources.ReadMesh(Dword(context, 0)), inputs.ReadProjection(frame), context, inputs.Colors, 17, 2);
        frame = PublishContext(frame with { Memory = result.Memory }, temporary, result.Context, 88);
        return inputs.Execute(frame, new NativeBattleEffect321DrawBatch(result.Batch, 0x2239528));
    }
    public static NativeBattleEffect321Frame DrawSprite(NativeBattleEffect321Frame frame, uint temporary, NativeBattleEffect321Resources resources, NativeBattleEffect321Inputs inputs)
    {
        NativeBattleModuleSpriteDraw result = NativeBattleModuleSpriteDrawing.Draw571C80(frame.Memory, Context(frame, temporary, 180), resources.Sprite, inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame), 17, 2);
        frame = PublishContext(frame with { Memory = result.Memory }, temporary, result.Temporary180, 180);
        return inputs.Execute(frame, new NativeBattleEffect321DrawBatch(result.Batch, 0x2239528));
    }
}
