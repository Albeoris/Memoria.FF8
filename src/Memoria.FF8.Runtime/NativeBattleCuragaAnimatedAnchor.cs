namespace Memoria.FF8.Runtime;

public static partial class NativeBattleCuragaNodeKinematics
{
    public static NativeBattleCuragaFrame FollowAnimatedAnchor8822A0(NativeBattleCuragaFrame frame, uint contextTask, int node)
    {
        NativeBattleCuragaNodeMemory memory = new(frame, contextTask);
        NativeBattleCuragaExecutableState bank = frame.Memory.CuragaExecutable ?? throw new NotSupportedException("Native8822A0 requires its retained executable channels.");
        byte[] source = ReadSource(memory, bank, unchecked((sbyte)memory.Byte(node + 0x6A)));
        FollowAnimatedAnchor(memory, node, source, bank);
        return memory.Flush();
    }

    internal static void FollowAnimatedAnchor(NativeBattleCuragaNodeMemory memory, int node, ReadOnlySpan<byte> source, NativeBattleCuragaExecutableState bank)
    {
        NativeBattleEffectAnimatedAnchor.Advance(memory, node, source.ToArray(), bank.Read);
    }
}
