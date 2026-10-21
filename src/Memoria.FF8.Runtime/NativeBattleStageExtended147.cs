namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleStageSource
{
    public NativeBattleEffectHeapAllocation? TextureAnimation147 { get; init; }
}

public sealed record NativeBattleStage147State
{
    [System.Text.Json.Serialization.JsonRequired]
    public short Subphase { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public short Counter { get; init; }
}

public static class NativeBattleStage147
{
    public const uint CallbackAddress = 0x511EF0;

    public static short Descent511EF0(short initialZ, short counter) => unchecked((short)((unchecked(-6000 * ((counter << 12) / 20)) >> 12) + initialZ + 6000));
}
