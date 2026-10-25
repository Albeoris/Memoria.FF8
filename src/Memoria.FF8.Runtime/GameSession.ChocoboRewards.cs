namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public ChocoboRewardGrantResult ClaimRemainingChocoboRewards()
    {
        ChocoboRewardGrantResult result = ChocoboRewardTransactions.ClaimRemaining(State);
        State = result.State;
        return result;
    }
}
