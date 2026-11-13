namespace Memoria.FF8.Runtime;

/// <summary>The source-model transitions of the original 50B2A0 invocation service.</summary>
public static class NativeBattleGuardianInvocation
{
    public static NativeBattleGraphicsModel EnterSource(NativeBattleGraphicsModel model)
    {
        return model with { Flags8 = (model.Flags8 ?? throw new InvalidDataException("Native50B2A0 requires the caster DWORD8.")) & ~0x800000u };
    }

    public static NativeBattleGraphicsModel HideSource(NativeBattleGraphicsModel model)
    {
        if ((model.Flags0 & 4) == 0)
        {
            throw new InvalidOperationException("Native50B2A0 must wait for the source disappearance service.");
        }
        return model with { Flags0 = (ushort)((model.Flags0 & 0xFFF5) | 8), EffectCounter97 = 0, ResourceFlags44 = (ushort)((model.ResourceFlags44 ?? throw new InvalidDataException("Native50B2A0 requires the caster resource WORD44.")) & ~0x20) };
    }

    public static NativeBattleGraphicsModel RestoreSource(NativeBattleGraphicsModel model)
    {
        return model with { Flags0 = (ushort)((model.Flags0 & 0xFFF1) | 2), Flags2 = (ushort)((model.Flags2 ?? throw new InvalidDataException("Native50B2A0 requires the caster WORD2.")) | 4) };
    }
}
