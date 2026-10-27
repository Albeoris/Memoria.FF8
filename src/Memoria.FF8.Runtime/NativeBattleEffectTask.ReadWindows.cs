namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleModulePoolTask
{
    internal NativeBattleReadWindow OpenPayloadReadWindow()
    {
        NativeBattleTaskBankOwner current = RequireOwner();
        NativeBattleEffectArena payload = NativeBattleEffectArena.BorrowWorking(current.Storage, Start + 12, PayloadLength);
        return payload.OpenReadWindow(0, PayloadLength);
    }
}
