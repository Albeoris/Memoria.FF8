using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect326Controller
{
    private static NativeBattleEffectMemory IrToTranslation64DE90(NativeBattleEffectMemory memory)
    {
        NativeBattleGeometryRegisters geometry = memory.GeometryRegisters ?? throw new NotSupportedException("Effect326 requires its actual translation controls.");
        NativeBattleScriptedColorRegisters colors = memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect326 requires its actual transformed IR registers.");
        if (colors.Ir.Any(value => value is null))
        {
            throw new NotSupportedException("Effect326 cannot publish unobserved IR translation lanes.");
        }
        return memory with { GeometryRegisters = geometry with { Translation = colors.Ir } };
    }

    private static int ParticleRoll(ref NativeBattleEffect326Frame frame)
    {
        NativeCrtRandomState next = frame.Random.Draw(out ushort value);
        frame = frame with { Random = next };
        return value;
    }

    private static NativeBattleEffectArena ParticleSlice(NativeBattleEffectArena source, int offset, int length) => CopyBytes(source, offset, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    private static NativeBattleEffectArena ParticleWord(NativeBattleEffectArena source, int offset, int value) => source.Write(offset, BitConverter.GetBytes(unchecked((ushort)value)));
    private static short ParticleSigned(NativeBattleEffectArena source, int offset) => BinaryPrimitives.ReadInt16LittleEndian(source.Read(offset, 2));
    private static uint ParticleDword(NativeBattleEffectArena source, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(source.Read(offset, 4));
}
