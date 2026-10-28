using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePandemona291State([property: JsonRequired] uint? Pause2556258, [property: JsonRequired] uint? Action255625C, [property: JsonRequired] uint? Caster2556260, [property: JsonRequired] int? Ground25562E0, [property: JsonRequired] int? Center25562E8, [property: JsonRequired] uint? Scratch25562EC, [property: JsonRequired] uint? Flash25562F0, [property: JsonRequired] uint? ModelBuffer25562F4, [property: JsonRequired] uint? PacketCursor25562DC, [property: JsonRequired] uint? StaticLoadState13BBBE8)
{
    public System.Collections.Immutable.ImmutableArray<NativeBattlePandemona291Animation>? Animations { get; init; }
    public bool AnimationsKnownCold { get; init; }

    public static NativeBattlePandemona291State FromColdProcess() => new(0, 0, 0, 0, 0, 0, 0, 0, 0, 3) { AnimationsKnownCold = true };
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattlePandemona291State? Pandemona291
    {
        get => effectState.Pandemona291;
        init => effectState = effectState with { Pandemona291 = value };
    }
}

public sealed record NativeBattlePandemona291Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);
