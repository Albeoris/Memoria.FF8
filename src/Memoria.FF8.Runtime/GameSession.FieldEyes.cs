namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public void SetNativeFieldEyeOwners(NativeBattleRetainedMemory memory, NativeCrtRandomState? crtRandom)
    {
        memory.Validate();
        if (ReferenceEquals(State.NativeBattleMemory, memory) && State.CrtRandom == crtRandom)
        {
            return;
        }
        Change(State with { NativeBattleMemory = memory, CrtRandom = crtRandom });
    }
}
