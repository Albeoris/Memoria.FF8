using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect324Controller;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect324Lower
{
    public static NativeBattleEffect324Step Advance(NativeBattleEffect324Frame frame, uint address, NativeBattleEffect324Resources resources, NativeBattleEffect324Inputs inputs)
    {
        return Read(frame, address).Callback switch
        {
            0x596E30 => NativeBattleEffect324Records.Advance(frame, address, 1, resources, inputs),
            0x597130 => NativeBattleEffect324Records.Advance(frame, address, 2, resources, inputs),
            0x597410 => NativeBattleEffect324Palette.Advance(frame, address, resources, inputs),
            0x597520 => NativeBattleEffect324History.Advance(frame, address, inputs),
            0x597870 => NativeBattleEffect324Eye.Advance(frame, address, resources, inputs),
            _ => throw new NotSupportedException("The effect requires its selected lower callback.")
        };
    }

    public static uint Model324(int actor) => checked(0x1D972C0u + (uint)actor * 0x9C);
    public static uint Caster324(NativeBattleEffect324Frame frame) => Model324(checked((int)frame.State.ReadDword(0x2203C4C)));
    public static bool Paused324(NativeBattleEffect324Frame frame, NativeBattleEffect324Inputs inputs) => (inputs.ReadFlags(frame) & 0x201) != 0;
    public static short Word324(NativeBattleEffectArena bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.Read(offset, 2));
    public static uint Dword324(NativeBattleEffectArena bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.Read(offset, 4));
    public static NativeBattleEffectArena Put324(NativeBattleEffectArena bytes, int offset, uint value) => bytes.Write(offset, BitConverter.GetBytes(value));
    public static NativeBattleEffectArena PutWord324(NativeBattleEffectArena bytes, int offset, int value) => bytes.Write(offset, BitConverter.GetBytes(unchecked((short)value)));
    public static NativeBattleEffect324Frame WriteModel324(NativeBattleEffect324Frame frame, uint model, int offset, NativeBattleEffectArena data, int length, NativeBattleEffect324Inputs inputs) => inputs.Execute(frame, new NativeBattleEffect324WriteModelBytes(model, offset, data, length));

    public static NativeBattleEffect324Frame Color324(NativeBattleEffect324Frame frame, uint source, uint farColor, uint factor, NativeBattleEffect324Inputs inputs, out uint color)
    {
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("The actor color requires its current primitive controls.");
        NativeBattleModelRendererRegisters far = (process.ModelRendererRegisters ?? throw new NotSupportedException("The actor color requires its current far controls.")).PublishModelColor(farColor);
        NativeBattleScriptedDpcsResult result = NativeBattleScriptedDpcs.Transform45F270(frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("The actor color requires current arithmetic registers."), source, factor, far, inputs.Colors);
        color = result.PackedRgb;
        return frame with { Memory = frame.Memory with { PrimitiveProcess = process with { ModelRendererRegisters = far }, ScriptedColorRegisters = result.Registers } };
    }

    public static NativeBattleEffect324Frame DrawSprite324(NativeBattleEffect324Frame frame, uint temporary, NativeBattleEffect324Resources resources, NativeBattleEffect324Inputs inputs)
    {
        NativeBattleModuleSpriteDraw result = NativeBattleModuleSpriteDrawing.Draw571C80(frame.Memory, Context(frame, temporary, 180), resources.Sprite, inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame), 17, 2);
        frame = PublishContext(frame with { Memory = result.Memory }, temporary, result.Temporary180, 180);
        return inputs.Execute(frame, new NativeBattleEffect324DrawBatch(result.Batch, 0x221BC60));
    }
}
