namespace Albeoris.Games.FF8.Jsm.Messages;

/// <summary>Identifies a context component that has incompatible definitions.</summary>
public enum FieldMessageContextComponent
{
    Position,
    Size,
    Choices,
}

/// <summary>Reports that one message identifier is used with incompatible presentation parameters.</summary>
public sealed record FieldMessageContextConflict(Int32 MessageId, FieldMessageContextComponent Component);

/// <summary>Contains the merged message contexts and any components omitted because they were ambiguous.</summary>
public sealed class FieldMessageContextAnalysis
{
    internal FieldMessageContextAnalysis(IReadOnlyList<FieldMessageContext> contexts, IReadOnlyList<FieldMessageContextConflict> conflicts)
    {
        Contexts = Array.AsReadOnly(contexts.ToArray());
        Conflicts = Array.AsReadOnly(conflicts.ToArray());
    }

    public IReadOnlyList<FieldMessageContext> Contexts { get; }

    public IReadOnlyList<FieldMessageContextConflict> Conflicts { get; }
}
