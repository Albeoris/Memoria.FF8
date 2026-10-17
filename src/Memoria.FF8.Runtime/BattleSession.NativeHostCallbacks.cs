namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    /// <summary>Executed47CF60 callbacks, including entry, command waits and outcome presentation.</summary>
    public long? NativeHostCallbacks { get; private set; } = 0;
}
