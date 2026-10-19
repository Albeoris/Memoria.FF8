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
public sealed record NativeBattleScriptedBackColor([property: JsonRequired] int Red, [property: JsonRequired] int Green, [property: JsonRequired] int Blue);

/// <summary>Process-retained cinematic lighting registers. Null fields denote unpublished, unknown registers.</summary>
public sealed record NativeBattleScriptedLightingMemory([property: JsonRequired] NativeBattleScriptedLightingMatrix? LightMatrix, [property: JsonRequired] NativeBattleScriptedLightingMatrix? ColorMatrix, [property: JsonRequired] NativeBattleScriptedBackColor? BackColor)
{
    public static NativeBattleScriptedLightingMemory Unknown() => new(null, null, null);

    public void Validate()
    {
        LightMatrix?.Validate();
        ColorMatrix?.Validate();
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleScriptedLightingMemory? ScriptedLighting { get; init; }
}
