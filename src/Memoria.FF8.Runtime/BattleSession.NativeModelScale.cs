using System.Buffers.Binary;

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
        if (effect.Binding.Effect == NativeBattleEffect101Profile.Effect && pointer == NativeBattleEffect101Profile.ScaleAddress)
        {
            NativeBattleEffect101State state = NativeEffectMemory?.Effect101 ?? throw new NotSupportedException("Native502AB0 requires effect101's retained scale vector owner; migrate the selected older snapshot explicitly.");
            return new NativeBattleModelScaleVector(unchecked((int)state.ReadDword(pointer)), unchecked((int)state.ReadDword(pointer + 4)), unchecked((int)state.ReadDword(pointer + 8)));
        }
        if (effect.Binding.Effect == NativeBattleEffect79Profile.Effect && pointer == NativeBattleEffect79Profile.BodyContextAddress + 0xAC)
        {
            NativeBattleEffect79State state = NativeEffectMemory?.Effect79 ?? throw new NotSupportedException("Native502AB0 requires effect79's current scale vector.");
            return new NativeBattleModelScaleVector(unchecked((int)state.ReadDword(pointer)), unchecked((int)state.ReadDword(pointer + 4)), unchecked((int)state.ReadDword(pointer + 8)));
        }
        if (effect.Binding.Effect == NativeBattleEffect65Profile.Effect && pointer == NativeBattleEffect65Profile.ScaleAddress)
        {
            NativeBattleEffect65Resources resources = effect.Binding.Effect65Resources ?? throw new NotSupportedException("Native502AB0 requires effect65's original scale vector.");
            ReadOnlySpan<byte> vector = resources.ReadStatic(pointer, 12).AsSpan();
            return new NativeBattleModelScaleVector(BinaryPrimitives.ReadInt32LittleEndian(vector), BinaryPrimitives.ReadInt32LittleEndian(vector.Slice(4)), BinaryPrimitives.ReadInt32LittleEndian(vector.Slice(8)));
        }
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
