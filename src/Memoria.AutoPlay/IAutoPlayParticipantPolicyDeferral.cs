namespace Memoria.AutoPlay;

public interface IAutoPlayParticipantPolicyDeferral
{
    string TargetId { get; }
    string SourceConditionSignature { get; }
    string GuardFingerprint { get; }
    IReadOnlyList<AutoPlayChoiceSelection> Selections { get; }
    string? ChoiceKey { get; }
    int FirstOption { get; }
    int OptionCount { get; }
}
