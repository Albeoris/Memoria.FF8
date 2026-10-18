namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleModelScaleVector? ReadNativeModelScaleVector502AB0(NativeBattleGraphicsState graphics, NativeBattleGraphicsModel model)
    {
        uint pointer = model.ScaleVectorAddress48 ?? throw new NotSupportedException("Native502AB0 requires its retained model scale pointer; migrate the named older snapshot explicitly.");
        if (pointer == 0)
        {
            return null;
        }
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native502AB0 scale pointer has no live effect owner.");
        if (effect.Binding.Effect != 40 || pointer < 0x30)
        {
            throw new NotSupportedException("Native502AB0 scale pointer requires its implemented live vector owner.");
        }
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(effect.Pools).Read(pointer - 0x30);
        if ((task.Flags & 1) == 0)
        {
            throw new InvalidDataException("Native502AB0 scale pointer references a retired Scan task.");
        }
        return new NativeBattleModelScaleVector(unchecked((int)task.ReadDword(0x30)), unchecked((int)task.ReadDword(0x34)), unchecked((int)task.ReadDword(0x38)));
    }
}
