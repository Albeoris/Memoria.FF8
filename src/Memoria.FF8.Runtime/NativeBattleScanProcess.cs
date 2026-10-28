using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScanProcess([property: JsonRequired] NativeBattleScanState? Controller, [property: JsonRequired] NativeBattleScanInputPublicationState Input, [property: JsonRequired] uint PhysicalButtons1CD01FC, [property: JsonRequired] bool InputInitialized, [property: JsonRequired] NativeBattleScanByteOwner InteractiveInput269AAA8)
{
    [JsonRequired]
    public NativeBattleScanTextProcess Text { get; init; } = NativeBattleScanTextProcess.FromColdProcess();

    [JsonRequired]
    public NativeBattleScanPresentationMemory Presentation { get; init; } = NativeBattleScanPresentationMemory.FromColdProcess();

    [JsonRequired]
    public NativeBattleScanByteOwner Mouse1D2B2E0 { get; init; } = Known(16);

    [JsonRequired]
    public NativeBattleScanResultMemory Results { get; init; } = NativeBattleScanResultMemory.FromColdProcess();

    [JsonRequired]
    public ushort Complete269D93A { get; init; }

    [JsonRequired]
    public ImmutableDictionary<uint, NativeBattleScanByteOwner> MutableMeshes { get; init; } = ImmutableDictionary<uint, NativeBattleScanByteOwner>.Empty;

    public static NativeBattleScanProcess FromColdProcess() => new(null, new(new([Known(196), Known(196)]), [Known(36), Known(36)], Known(4), Known(12)), 0, false, Known(48));

    public void Validate()
    {
        Input.Validate();
        InteractiveInput269AAA8.RequireLength(48);
        Text.Validate();
        Presentation.Validate();
        Mouse1D2B2E0.RequireLength(16);
        Results.Validate();
    }

    private static NativeBattleScanByteOwner Known(int length) => new(new byte[length].ToImmutableArray(), Enumerable.Repeat(true, length).ToImmutableArray());
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleScanProcess? Scan
    {
        get => effectState.Scan;
        init => effectState = effectState with { Scan = value };
    }
}
