namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffectBinding? Entry21DFEC0
    {
        get => effectState.Entry21DFEC0;
        init => effectState = effectState with { Entry21DFEC0 = value };
    }
    public NativeBattleEffectBinding? Entry21DFEC4
    {
        get => effectState.Entry21DFEC4;
        init => effectState = effectState with { Entry21DFEC4 = value };
    }
    public NativeBattleEffectBinding? Entry21DFEC8
    {
        get => effectState.Entry21DFEC8;
        init => effectState = effectState with { Entry21DFEC8 = value };
    }
    public byte? Parameter82 { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public uint? MagicTextureBufferAddress1D99A88 { get; init; }

    public NativeBattleEffectMemory PublishEntry50AF20(NativeBattleEffectBinding binding, bool destination21DFEC0)
    {
        if (binding.Effect is < 1 or > 400 || binding.Entry == 0)
        {
            throw new InvalidDataException("Native50AF20 requires its actual selected nonzero module entry.");
        }
        return destination21DFEC0 ? this with { Entry21DFEC0 = binding, MagicTextureBufferAddress1D99A88 = NativeBattleEffectNodeMemory.ArenaAddress } : this with { Entry21DFEC4 = binding, MagicTextureBufferAddress1D99A88 = NativeBattleEffectNodeMemory.ArenaAddress };
    }

    public NativeBattleEffectMemory CopyEntry50B830()
    {
        return this with { Entry21DFEC8 = Entry21DFEC4 ?? throw new NotSupportedException("Native50B830 requires its actual retained21DFEC4 entry before copying21DFEC8.") };
    }

    public NativeBattleEffectMemory SetDirectParameters(uint callback, byte command, ushort action, byte effectLowByte)
    {
        if (callback == 0x50BEE0)
        {
            return this with { Parameter80 = (byte)(command == 0xED ? 2 : 1) };
        }
        if (callback != 0x50BDC0)
        {
            throw new ArgumentOutOfRangeException(nameof(callback));
        }
        if (action <= 5)
        {
            return this with { Parameter80 = (byte)(action + 2) };
        }
        if (action is 0xFFFC or 0xFFFA)
        {
            return this with { Parameter80 = 8, Parameter82 = action == 0xFFFC ? (byte)0 : unchecked((byte)(effectLowByte - 5)) };
        }
        return this;
    }

    public NativeBattleOriginalEffectState RequireDirectEntry21DFEC4(NativeBattleOriginalEffectState? current)
    {
        NativeBattleEffectBinding retained = Entry21DFEC4 ?? throw new NotSupportedException("Native direct effect service requires the observed21DFEC4 callback; the current module cannot establish it.");
        if (current is null || current.Binding.Effect != retained.Effect || current.Binding.Entry != retained.Entry || current.Binding.Initializer != retained.Initializer)
        {
            throw new NotSupportedException("Native retained21DFEC4 differs from the current loaded module; its independently retained resource globals are required before direct invocation.");
        }
        return current;
    }
}
