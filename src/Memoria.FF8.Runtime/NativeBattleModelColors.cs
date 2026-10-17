using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleModelColorOverride([property: JsonRequired] byte? Actor, [property: JsonRequired] uint? Color);

/// <summary>The two process-retained one-shot RGB/actor DWORDs atB8BA00/B8BA04, consumed in physical order by50A0A0.</summary>
public sealed record NativeBattleModelColorOverrides([property: JsonRequired] ImmutableArray<NativeBattleModelColorOverride> Slots)
{
    public static NativeBattleModelColorOverrides FromColdProcess() => new([new(255, 0), new(0, 0)]);

    public void Validate()
    {
        if (Slots.IsDefault || Slots.Length != 2 || Slots.Any(slot => slot is null || slot.Color > 0xFFFFFF))
        {
            throw new InvalidDataException("Native50A0A0 requires its two retained RGB/actor override slots.");
        }
    }

    public (NativeBattleModelColorOverrides State, NativeBattleGraphicsModel Model) Consume50A0CD(NativeBattleGraphicsModel model)
    {
        Validate();
        NativeBattleModelColorOverrides state = this;
        for (int index = 0; index < Slots.Length; index++)
        {
            NativeBattleModelColorOverride slot = state.Slots[index];
            byte actor = slot.Actor ?? throw new NotSupportedException("Native50A0D2 requires the observed pending-color actor BYTE; old missing state is unknown.");
            if (actor < 8 && actor == model.Index)
            {
                uint color = slot.Color ?? throw new NotSupportedException("Native50A0F7 requires this actor's actual pending RGB DWORD.");
                state = state with { Slots = state.Slots.SetItem(index, slot with { Actor = 255 }) };
                model = model with { Color40 = color };
            }
        }
        return (state, model);
    }
}

public sealed record NativeBattleModelColorStep(NativeBattleGraphicsModel Model, NativeBattleModelColorOverrides? Overrides);

/// <summary>502AB0 calls50A0A0 before50C110 and before body packets. Stage and one-shot colors are physical writers, not fade completion defaults.</summary>
public static class NativeBattleModelColors
{
    public static NativeBattleModelColorStep Advance50A0A0(NativeBattleGraphicsModel model, uint graphicsFlags, NativeBattleSummonColors colors, NativeBattleModelColorOverrides? overrides)
    {
        ushort flags2 = model.Flags2 ?? throw new NotSupportedException("Native502B56 requires the model WORD2 before50A0A0.");
        if ((flags2 & 4) != 0)
        {
            return new(model, overrides);
        }
        uint statuses = model.Flags8 ?? throw new NotSupportedException("Native50A0A0 requires its actual status DWORD8.");
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native50A0A9 requires its model position/vertical WORD owner.");
        model = model with { Transform = transform with { Vertical34 = 0 } };
        if ((model.Flags0 & 0x800) == 0)
        {
            model = (statuses & 0x2160510) != 0 ? NativeBattleModelStatusColors.Advance50A1C0(model, colors) : model with { Color40 = colors.Transition };
        }
        NativeBattleModelColorOverrides pending = overrides ?? throw new NotSupportedException("Native50A0CD requires its retained B8BA00/B8BA04 pending-color slots; migrate only an explicitly proved standalone owner.");
        (pending, model) = pending.Consume50A0CD(model);
        if ((model.Flags0 & 0x2000) == 0 && (statuses & 0x804) != 0 && (flags2 & 8) == 0)
        {
            model = NativeBattleModelStatusMotion.Advance50A370(model);
        }
        if ((model.Flags0 & 0x1000) == 0 && (statuses & 0x200) != 0 && (flags2 & 8) == 0)
        {
            model = NativeBattleModelStatusMotion.Advance50A5F0(model);
        }
        if ((graphicsFlags & 0x410) == 0)
        {
            if ((model.Flags0 & 0x4004) == 0 && (statuses & 0x82000) != 0)
            {
                throw new NotSupportedException("Native50A0A0 reached50A410 Doom/Petrify countdown text; its actual shared module producer is not implemented.");
            }
            //5711C0 draws into the current common packet heap; the Session invokes it here before50C110/body submission.
        }
        return new(model, pending);
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleModelColorOverrides? ModelColorOverrides { get; init; }
}
