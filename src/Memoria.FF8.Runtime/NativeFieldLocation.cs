using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeFieldLocationPose([property: JsonRequired] short X, [property: JsonRequired] short Y, [property: JsonRequired] ushort Triangle, [property: JsonRequired] byte Direction)
{
    public static NativeFieldLocationPose FromPhysicalWords(int fixedX, int fixedY, ushort triangle, byte direction)
    {
        return new NativeFieldLocationPose(unchecked((short)(fixedX >> 12)), unchecked((short)(fixedY >> 12)), triangle, direction);
    }
}
public sealed record NativeFieldLocationSlot([property: JsonRequired] byte? RuntimeBinding, [property: JsonRequired] NativeFieldLocationPose? Pose);

/// <summary>Actual470250 saved-field words. An unowned physical read remains unknown; it never receives a zero pose.</summary>
public sealed record NativeFieldLocation([property: JsonRequired] ushort FieldId, [property: JsonRequired] ushort? PreviousFieldId, [property: JsonRequired] ushort NativeKind, [property: JsonRequired] ImmutableArray<NativeFieldLocationSlot> Slots, [property: JsonRequired] string ProgramHash, [property: JsonRequired] string ExecutableHash, [property: JsonRequired] long Frame, [property: JsonRequired] int ObjectId, [property: JsonRequired] int ScriptId, [property: JsonRequired] int Operation)
{
    public void Validate()
    {
        if (FieldId < 72 || NativeKind != 1 || Slots.IsDefault || Slots.Length != 3 || Slots.Any(slot => slot is null || slot.Pose is not null && (slot.RuntimeBinding is null or >= 48)) || ProgramHash is not { Length: 64 } || !ProgramHash.All(Uri.IsHexDigit) || !NativeFieldRuntimeProfile.IsSupportedImage(ExecutableHash) || Frame < 0 || ObjectId < 0 || ScriptId < 0 || Operation < 0)
        {
            throw new InvalidDataException("Native saved field location lacks its actual470250 field/menu request and three physical read results.");
        }
    }
}
