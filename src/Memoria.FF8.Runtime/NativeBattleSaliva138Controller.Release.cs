using static Memoria.FF8.Runtime.NativeBattleSaliva138Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleSaliva138Controller
{
    public static NativeBattleSaliva138Frame ReleaseReturnedBanks(NativeBattleSaliva138Frame frame)
    {
        if (frame.Effect.ReturnedDescriptor != 0)
        {
            throw new InvalidDataException("Saliva bank release requires its actual completed returned root.");
        }
        foreach (uint descriptor in new[] { CurveDescriptor, MovingSpriteDescriptor })
        {
            NativeBattleEffectTaskTransfer detached = NativeBattleEffectTaskMemory.DetachArenaBank(frame.Memory, frame.Effect, descriptor);
            frame = frame with { Memory = detached.Memory, Effect = detached.Effect };
        }
        NativeBattleEffectTaskTransfer transferred = NativeBattleEffectTaskMemory.TransferInactiveArenaBanks(frame.Memory, frame.Effect);
        return frame with { Memory = transferred.Memory, Effect = transferred.Effect };
    }
}
