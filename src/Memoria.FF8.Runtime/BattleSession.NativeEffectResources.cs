namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private Func<ushort, NativeBattleEffectBinding>? nativeEffectResourceResolver;

    /// <summary>Attaches immutable original file input for the next actual50AF20 request; never hydrates an active saved effect.</summary>
    public void AttachNativeEffectResourceResolver(Func<ushort, NativeBattleEffectBinding> resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        nativeEffectResourceResolver = resolver;
    }
}

/// <summary>Host materialization after native selected-index/null-entry fallback and before571870 initialization.</summary>
public static class NativeBattleEffectResourceResolver
{
    public static NativeBattleOriginalEffectState MaterializeSelected(NativeBattleOriginalEffectState selected, Func<ushort, NativeBattleEffectBinding>? resolve)
    {
        ArgumentNullException.ThrowIfNull(selected);
        if (resolve is null)
        {
            return selected;
        }
        NativeBattleEffectBinding original = selected.Binding;
        NativeBattleEffectBinding materialized = resolve(original.Effect) ?? throw new InvalidDataException("Native50AF20 resource resolver returned no original selected binding.");
        if (materialized.Effect != original.Effect || materialized.Entry != original.Entry || materialized.Initializer != original.Initializer || materialized.InitializerIsRet != original.InitializerIsRet || materialized.InitializerResource != original.InitializerResource)
        {
            throw new InvalidDataException("Native50AF20 resource resolver changed the original selected effect metadata.");
        }
        return selected with { Binding = materialized };
    }
}
