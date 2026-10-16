namespace Memoria.FF8.Runtime;

/// <summary>1D96DB0 and1D96DC0 are independent retained globals;501160 resets the number but preserves the source pointer.</summary>
public sealed record NativeBattleSummonStreamState(int? Number, uint? SourceAddress)
{
    public static NativeBattleSummonStreamState FromColdProcess() => new(0, 0);
    public static NativeBattleSummonStreamState Unknown() => new(null, null);

    public NativeBattleSummonStreamState Reset501160() => this with { Number = 0 };

    public NativeBattleSummonStreamState Publish501860(uint sourceAddress, int leadingNumber)
    {
        return new(unchecked(leadingNumber + 690000), sourceAddress);
    }

    public NativeBattleModuleSound Play5018C0(uint pan, uint volume)
    {
        int number = Number ?? throw new NotSupportedException("Native5018C0 requires its actual501860 stream-number global.");
        return new(unchecked((uint)number), 0, null) { RawPan = pan, Volume = volume };
    }

    /// <summary>501160's initialized2D branch of501920; the separately enabled original3D sound backend is unsupported.</summary>
    public NativeBattleModuleSound Play501920(NativeBattleModuleAnchor position, NativeBattleCameraProjection projection, uint volume)
    {
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(projection);
        int number = Number ?? throw new NotSupportedException("Native501920 requires its actual501860 stream-number global.");
        return new(unchecked((uint)number), 0, position) { Pan = projection.ReadPan(new(position.X, position.Y, position.Z)), Volume = volume };
    }
}

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleSummonStreamState? SummonStream { get; init; }
}
