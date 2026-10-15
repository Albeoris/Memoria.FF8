using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record RandomEncounterRequest([property: JsonRequired] SceneKind Scene, [property: JsonRequired] int FieldId, [property: JsonRequired] long Frame, [property: JsonRequired] ushort Formation, [property: JsonRequired] string SourceHash);

/// <summary>Explicit playthrough policy and the process history executed while that policy is enabled.</summary>
public sealed record RandomEncounterSessionState([property: JsonRequired] bool Enabled, [property: JsonRequired] NativeRandomEncounterState Native, [property: JsonRequired] RandomEncounterRequest? Pending)
{
    public void Validate()
    {
        Native.Validate();
        if (Pending is { } request && (!Enabled || request.Scene is not (SceneKind.Field or SceneKind.WorldMap) || request.Frame < 0 || request.FieldId < 0 || request.SourceHash.Length != 64 || !request.SourceHash.All(Uri.IsHexDigit)))
        {
            throw new InvalidDataException("Random battle request lacks its owning scene and original resource identity.");
        }
    }
}
