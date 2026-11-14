namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState SetNativeQuezacotlTargets6C5300(NativeBattleGraphicsState graphics, NativeBattleOriginalEffectState effect, bool hidden)
    {
        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new InvalidDataException("Native6C5300/6C5350 lost its actual action header.");
        NativeBattleActionHeaderSlot header = nativeActionPackets.ReadHeader(parameters.Header);
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Native6C5300/6C5350 requires its actual target contact pointer.");
        for (int index = 0; index < header.ReadContactCount(); index++)
        {
            int target = nativeActionPackets.ReadContact(first + index).Primary.ActorIndex;
            NativeBattleGraphicsModel model = graphics.ActorModels.Get(target);
            graphics = graphics with { ActorModels = graphics.ActorModels.Set(target, model with { Flags0 = hidden ? (ushort)(model.Flags0 | 4) : (ushort)(model.Flags0 & ~4) }) };
        }
        return graphics;
    }
}
