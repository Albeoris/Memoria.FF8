namespace Albeoris.Games.FF8.Jsm.Messages;

/// <summary>Line selection parameters supplied to a field question instruction.</summary>
public sealed record FieldMessageChoices(Int32 FirstLine, Int32 LastLine, Int32 BeginLine, Int32 CancelLine);
