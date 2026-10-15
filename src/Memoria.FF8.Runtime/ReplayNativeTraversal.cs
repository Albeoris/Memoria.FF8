namespace Memoria.FF8.Runtime;

/// <summary>A controlled native ladder endpoint requested through its current issuing program; no old position or callback count.</summary>
public sealed record ReplayNativeTraversal(int Version, ReplayScriptGoal Producer, byte Mode, bool Forward, bool Inferred)
{
    public void Validate()
    {
        if (Version != 1 || Producer is null || Mode is not (3 or 4) || Producer.FieldId is < 0 or > ushort.MaxValue || Producer.ObjectId < 0 || Producer.ScriptId < 0 || Producer.Operation < 0 || Producer.Code is < 0 or > ushort.MaxValue || Producer.Inferred != Inferred || !IsHash(Producer.ProgramHash) || Producer.ExecutableHash is null && !Inferred || Producer.ExecutableHash is not null && !IsHash(Producer.ExecutableHash))
        {
            throw new InvalidDataException("Native traversal requires a current issuing site, supported ladder mode and authenticated endpoint intent.");
        }
    }

    private static bool IsHash(string? hash)
    {
        return hash is { Length: 64 } && hash.All(Uri.IsHexDigit);
    }
}
