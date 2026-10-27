namespace Memoria.FF8.Runtime;

/// <summary>Typed Curaga entry point for the shared signed-byte sprite animation kernel.</summary>
public static class NativeBattleCuragaSpriteAnimation
{
    public static NativeBattleCuragaFrame Advance880CE0(NativeBattleCuragaFrame frame, uint contextTask, int node, sbyte resourceKind)
    {
        if (resourceKind is < 0 or > 2)
        {
            return frame;
        }
        using NativeBattleCuragaNodeMemory memory = new(frame, contextTask);
        NativeBattleEffectNodeLifecycle.AdvanceSpriteAnimation(memory, node, resourceKind);
        return memory.Flush();
    }
}
