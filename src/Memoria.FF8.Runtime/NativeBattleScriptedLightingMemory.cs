using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>The five packed matrix control DWORDs, including the unused upper padding WORD and its known mask.</summary>
public sealed record NativeBattleScriptedLightingMatrix([property: JsonRequired] ImmutableArray<byte> Bytes, [property: JsonRequired] ImmutableArray<bool> Known)
{
    [JsonIgnore]
    public ImmutableArray<short> Rotation
    {
        get
        {
            Validate();
            if (Known.Take(18).Any(known => !known))
            {
                throw new NotSupportedException("Native lighting matrix requires its actual rotation register values.");
            }
            byte[] bytes = Bytes.ToArray();
            return Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(index * 2))).ToImmutableArray();
        }
    }

    public NativeBattleActionMatrix ToActionMatrix() => new(Rotation, 0, 0, 0, 4096);

    public void Validate()
    {
        if (Bytes.IsDefault || Known.IsDefault || Bytes.Length != 20 || Known.Length != 20)
        {
            throw new InvalidDataException("Native lighting matrix requires five complete packed control DWORDs and their known mask.");
        }
    }
}

/// <summary>The back-color control DWORDs after the native signed input multiplication by sixteen.</summary>
public sealed record NativeBattleScriptedBackColor([property: JsonRequired] int Red, [property: JsonRequired] int Green, [property: JsonRequired] int Blue)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<bool>? Known { get; init; }

    public static NativeBattleScriptedBackColor Unknown() => new(0, 0, 0) { Known = [false, false, false] };

    public void Validate()
    {
        if (Known is { } known && (known.IsDefault || known.Length != 3))
        {
            throw new InvalidDataException("Native back-color controls require three lane knowledge flags.");
        }
    }

    public int? ReadLane(int index)
    {
        Validate();
        if ((uint)index >= 3)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        if (Known is { } known && !known[index])
        {
            return null;
        }
        return index == 0 ? Red : index == 1 ? Green : Blue;
    }

    public NativeBattleScriptedBackColor WriteLane(int index, int value)
    {
        Validate();
        if ((uint)index >= 3)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        ImmutableArray<bool> known = (Known ?? [true, true, true]).SetItem(index, true);
        return index switch
        {
            0 => this with { Red = value, Known = known },
            1 => this with { Green = value, Known = known },
            _ => this with { Blue = value, Known = known }
        };
    }

    public NativeBattleScriptedBackColor RequireKnown()
    {
        Validate();
        if (Known is { } known && known.Any(value => !value))
        {
            throw new NotSupportedException("Native back-color consumption requires all three actual control lanes.");
        }
        return this;
    }
}

/// <summary>Process-retained cinematic lighting registers. Null fields denote unpublished, unknown registers.</summary>
public sealed record NativeBattleScriptedLightingMemory([property: JsonRequired] NativeBattleScriptedLightingMatrix? LightMatrix, [property: JsonRequired] NativeBattleScriptedLightingMatrix? ColorMatrix, [property: JsonRequired] NativeBattleScriptedBackColor? BackColor)
{
    public static NativeBattleScriptedLightingMemory Unknown() => new(null, null, null);

    public void Validate()
    {
        LightMatrix?.Validate();
        ColorMatrix?.Validate();
        BackColor?.Validate();
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleScriptedLightingMemory? ScriptedLighting { get; init; }
}
