using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect301Controller;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect301Lower
{
    public static NativeBattleEffect301Step Advance(NativeBattleEffect301Frame frame, uint address, NativeBattleEffect301Resources resources, NativeBattleEffect301Inputs inputs)
    {
        return Read(frame, address).Callback switch
        {
            0x66EC50 => NativeBattleEffect301Particles.AdvanceMesh(frame, address, resources, inputs),
            0x66EE60 => NativeBattleEffect301Particles.AdvanceSprite(frame, address, resources, inputs),
            _ => throw new NotSupportedException("The effect requires its selected lower callback.")
        };
    }

    public static NativeBattleEffect301Frame DrawModel66E300(NativeBattleEffect301Frame frame, uint model, int age, int thresholdY, uint scratch, NativeBattleEffect301Inputs inputs) => inputs.Execute(frame, new NativeBattleEffect301DrawBody66E300(model, age, thresholdY, scratch));
    public static short Word301(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadInt16LittleEndian(value.Read(offset, 2));
    public static uint Dword301(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(value.Read(offset, 4));
    public static NativeBattleEffectArena Put301(NativeBattleEffectArena value, int offset, uint number) => value.Write(offset, BitConverter.GetBytes(number));
    public static NativeBattleEffectArena PutWord301(NativeBattleEffectArena value, int offset, int number) => value.Write(offset, BitConverter.GetBytes(unchecked((short)number)));
    public static uint Main301(NativeBattleEffect301Frame frame) => frame.State.ReadDword(0x2507754);
    public static NativeBattleEffect301Frame DrawStatic301(NativeBattleEffect301Frame frame, uint temporary, NativeBattleEffect301Resources resources, NativeBattleEffect301Inputs inputs)
    {
        NativeBattleEffectArena context = Context(frame, temporary, 88);
        NativeBattleStaticMeshContext88Step result = NativeBattleStaticMeshContext88.DrawLoaded(frame.Memory, resources.ReadMesh(Dword301(context, 0)), inputs.ReadProjection(frame), context, inputs.Colors, 17, 2);
        frame = PublishContext(frame with { Memory = result.Memory }, temporary, result.Context, 88);
        return inputs.Execute(frame, new NativeBattleEffect301DrawBatch(result.Batch, 0x250773C));
    }
}
