namespace Memoria.FF8.Resources.Text;

/// <summary>Edits immutable profile identities without changing other profile namespaces.</summary>
public static class NativeFontProfileOperations
{
    public static NativeFontProfile Duplicate(NativeFontProfile source)
    {
        ArgumentNullException.ThrowIfNull(source);
        string id = Guid.NewGuid().ToString("D");
        return source with
        {
            Id = id,
            DisplayName = source.DisplayName + " copy",
            Assignments = source.Assignments.Select(assignment => assignment.ProfileId == source.Id ? assignment with { ProfileId = id } : assignment).ToArray()
        };
    }
}
