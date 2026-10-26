using System.Numerics;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Logical translation progress keyed by native part index; selected model geometry remains resource data.</summary>
public sealed record NativeModelPartResourceState([property: JsonRequired] Dictionary<int, NativeModelPartTranslation> Translations)
{
    public static NativeModelPartResourceState Create(NativeModelPartsDefinition definition)
    {
        definition.Validate();
        return new NativeModelPartResourceState([]);
    }

    public void Validate(NativeModelPartsDefinition definition)
    {
        definition.Validate();
        if (Translations is null || Translations.Any(pair => (uint)pair.Key >= definition.Parts.Count || pair.Value is null || pair.Value.Mode is not (1 or 4) || pair.Value.Duration == 0))
        {
            throw new InvalidDataException("Native model-part translations require existing part indices, supported modes and nonzero durations.");
        }
        if (Translations.Count > 0)
        {
            definition.RequireDisjointTranslationGeometry();
        }
    }

    public NativeModelPartResourceState StartTranslation(NativeModelPartsDefinition definition, int partIndex, short x, short y, short z, short duration)
    {
        Validate(definition);
        if ((uint)partIndex >= definition.Parts.Count)
        {
            throw new NotSupportedException("Native model-part command requires its registered part index.");
        }
        if (Translations.TryGetValue(partIndex, out NativeModelPartTranslation? current) && current.Mode == 1)
        {
            //532180 returns-1;359/360 still consume their selector and return2.
            return this;
        }
        if (duration == 0)
        {
            throw new NotSupportedException("Native animated part translation cannot divide by zero.");
        }
        NativeModelPartDefinition part = definition.Parts[partIndex];
        short startX = current?.TargetX ?? part.TargetX;
        short startY = current?.TargetY ?? part.TargetY;
        short startZ = current?.TargetZ ?? part.TargetZ;
        NativeModelPartTranslation translation = new(1, startX, startY, startZ, unchecked((short)(startX + x)), unchecked((short)(startY + y)), unchecked((short)(startZ + z)), duration, 1);
        Dictionary<int, NativeModelPartTranslation> translations = new(Translations)
        {
            [partIndex] = translation
        };
        NativeModelPartResourceState result = this with { Translations = translations };
        result.Validate(definition);
        return result;
    }

    /// <summary>One actual533CD0 enabled-instance visit, preserving per-skin signed WORD arithmetic and ordering.</summary>
    public NativeModelPartStep Step(NativeModelPartsDefinition definition)
    {
        Validate(definition);
        if (Translations.Count == 0)
        {
            return new NativeModelPartStep(this, new Dictionary<int, Vector3>());
        }
        Dictionary<int, NativeModelPartTranslation> states = new(Translations);
        Dictionary<int, Vector3> translations = [];
        foreach ((int partIndex, NativeModelPartTranslation state) in Translations.OrderBy(pair => pair.Key))
        {
            NativeModelPartDefinition part = definition.Parts[partIndex];
            short counter = state.Counter;
            short mode = state.Mode;
            for (int skin = part.FirstSkinBinding; skin < part.FirstSkinBinding + part.SkinBindingCount; skin++)
            {
                if (mode == 1)
                {
                    //530C30 narrows each interpolated delta before separately adding the signed start vector.
                    short x = unchecked((short)(counter * (state.TargetX - state.StartX) / state.Duration));
                    short y = unchecked((short)(counter * (state.TargetY - state.StartY) / state.Duration));
                    short z = unchecked((short)(counter * (state.TargetZ - state.StartZ) / state.Duration));
                    translations[skin] = new Vector3(x, y, z) + new Vector3(state.StartX, state.StartY, state.StartZ);
                    counter = unchecked((short)(counter + 1));
                    if (state.Duration < counter)
                    {
                        mode = 4;
                    }
                }
                else
                {
                    translations[skin] = new Vector3(state.TargetX, state.TargetY, state.TargetZ);
                }
            }
            states[partIndex] = state with { Counter = counter, Mode = mode };
        }
        return new NativeModelPartStep(this with { Translations = states }, translations);
    }
}

public sealed record NativeModelPartTranslation([property: JsonRequired] short Counter, [property: JsonRequired] short StartX, [property: JsonRequired] short StartY, [property: JsonRequired] short StartZ, [property: JsonRequired] short TargetX, [property: JsonRequired] short TargetY, [property: JsonRequired] short TargetZ, [property: JsonRequired] short Duration, [property: JsonRequired] short Mode);

public sealed record NativeModelPartStep(NativeModelPartResourceState State, IReadOnlyDictionary<int, Vector3> SkinTranslations);
