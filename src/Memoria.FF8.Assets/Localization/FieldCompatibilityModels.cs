using System.Collections.Immutable;

namespace Memoria.FF8.Assets.Localization;

public sealed record FieldCompatibilityText(int Ordinal, string Text, IReadOnlyList<string>? Tokens = null);

public sealed record FieldCompatibilityInput(string Field, string Language, string ResourceIdentity, byte[]? Jsm, IReadOnlyList<FieldCompatibilityText>? Bank, IReadOnlyList<string>? Errors = null, int? DeclaredTextCount = null);

public sealed record FieldCompatibilityRow(int Ordinal, string Text, ImmutableArray<string> Tokens);

public sealed record FieldCompatibilityPosition(int ObjectIndex, int ObjectLabel, int ScriptId, int InstructionOrdinal, int OperationIndex, int ByteOffset);

public sealed record FieldCompatibilityInstruction(FieldCompatibilityPosition Position, string Opcode, int EncodedValue, int Parameter);

/// <summary>Operands are in the EventEngine's original push order. Null operands require runtime evaluation.</summary>
public sealed record FieldCompatibilityContext(FieldCompatibilityPosition Position, string Opcode, ImmutableArray<int?> Operands, int? MessageId, bool IsMessage, bool Dynamic, ImmutableArray<int> CandidateOrdinals, FieldCompatibilityRow? Row, string PresentationStateFingerprint = "EntryAndControlFlowStateUnresolved");

public sealed record FieldCompatibilityInventory(string Field, string Language, string ResourceIdentity, string? JsmSha256, int? JsmLength, string ParserVersion, bool BankCountKnown, int? DeclaredTextCount, ImmutableArray<FieldCompatibilityRow> Bank, ImmutableArray<FieldCompatibilityInstruction> Instructions, ImmutableArray<FieldCompatibilityContext> Contexts, ImmutableArray<int> ReferencedOrdinals, ImmutableArray<int> UnreferencedOrdinals, ImmutableArray<int> EmptyOrdinals, ImmutableArray<string> Problems, string? ExecutableLayout)
{
    public bool ScriptValidated { get; init; }
    public bool BankValidated { get; init; }
    public bool StaticReferencesValidated { get; init; }
}

public sealed record FieldCompatibilityDivergence(string Reason, int? RawByteOffset, FieldCompatibilityPosition? EnglishPosition, FieldCompatibilityPosition? LocalPosition, ImmutableArray<FieldCompatibilityInstruction> EnglishPrecedingContext, ImmutableArray<FieldCompatibilityInstruction> LocalPrecedingContext, int? StructuralBoundaryOperationIndex = null);

public enum FieldCompatibilityKind
{
    ByteIdentical,
    StorageOnly,
    Divergent,
    Incomplete
}

public sealed record FieldCompatibilityMatch(int EnglishOrdinal, int LocalOrdinal, string Basis);

public sealed record FieldCompatibilityReviewCase(string Id, string Kind, ImmutableArray<FieldCompatibilityPosition> EnglishPositions, ImmutableArray<FieldCompatibilityPosition> LocalPositions, ImmutableArray<int> EnglishOrdinals, ImmutableArray<int> LocalOrdinals);

public sealed record FieldMessageIdentityEvidence(string Rule, bool ProjectionValidated, bool StreamEqual, bool OrdinalIdentityProven, int MatchedTokens, int EnglishBoundaryOperationIndex, int LocalBoundaryOperationIndex, FieldCompatibilityPosition? EnglishPosition, FieldCompatibilityPosition? LocalPosition);

public sealed record FieldCompatibilityResult(FieldCompatibilityKind Kind, FieldCompatibilityInventory English, FieldCompatibilityInventory Localized, FieldCompatibilityDivergence? FirstDivergence, ImmutableArray<FieldCompatibilityContext> EnglishSuffixContexts, ImmutableArray<FieldCompatibilityContext> LocalSuffixContexts, ImmutableArray<FieldCompatibilityMatch> ProvenOrdinalMatches, ImmutableArray<int> PotentialLocalOrdinals, ImmutableArray<int> ImplicatedEnglishOrdinals, int SemanticReviewCases, int ContextOnlyProblems, bool CandidateCountKnown, ImmutableArray<string> Reasons, ImmutableArray<FieldCompatibilityReviewCase> ReviewCases)
{
    public FieldMessageIdentityEvidence? MessageIdentityEvidence { get; init; }
}
