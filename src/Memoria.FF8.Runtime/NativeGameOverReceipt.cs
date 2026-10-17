using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>An accepted native loss request at the end of its issuing field callback.</summary>
public sealed record NativeGameOverReceipt([property: JsonRequired] int Version, [property: JsonRequired] int FieldId, [property: JsonRequired] string ProgramHash, [property: JsonRequired] string? ExecutableHash, [property: JsonRequired] int ObjectId, [property: JsonRequired] int ScriptId, [property: JsonRequired] int Operation, [property: JsonRequired] long Frame, [property: JsonRequired] byte RequestKind)
{
    public void Validate()
    {
        if (Version != 1 || FieldId is < 0 or > ushort.MaxValue || ProgramHash is not { Length: 64 } hash || !hash.All(Uri.IsHexDigit) || ExecutableHash is not null && ExecutableHash != NativeFieldRuntimeProfile.FF8EnExecutableHash || ObjectId < 0 || ScriptId < 0 || Operation < 0 || Frame < 1 || RequestKind != 4)
        {
            throw new InvalidDataException("Invalid native game-over request receipt.");
        }
    }
}
