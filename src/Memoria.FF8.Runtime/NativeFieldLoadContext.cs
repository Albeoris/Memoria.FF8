using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public enum NativeFieldLoadOrigin
{
    NewGame,
    FieldRequest,
    WorldReturn,
    BattleDefeat
}

/// <summary>The executed request whose native loader copies its module-entry reason into event BYTE87.</summary>
public sealed record NativeFieldLoadContext(int Version, int FieldId, NativeFieldLoadOrigin Origin)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDefeatReturnBranch? DefeatBranch { get; init; }

    [JsonIgnore]
    public byte Reason => Origin switch
    {
        NativeFieldLoadOrigin.NewGame => 0,
        NativeFieldLoadOrigin.BattleDefeat => 0,
        NativeFieldLoadOrigin.FieldRequest => 1,
        NativeFieldLoadOrigin.WorldReturn => 2,
        _ => throw new InvalidDataException("Unknown native field allocation origin.")
    };

    public static NativeFieldLoadContext NewGame() => new(1, 74, NativeFieldLoadOrigin.NewGame);
    public static NativeFieldLoadContext FromFieldRequest(int fieldId) => new(1, fieldId, NativeFieldLoadOrigin.FieldRequest);
    public static NativeFieldLoadContext FromWorldReturn(int fieldId) => new(1, fieldId, NativeFieldLoadOrigin.WorldReturn);

    public static NativeFieldLoadContext FromBattleDefeat(NativeBattleDefeatReturnBranch branch) => new(1, 75, NativeFieldLoadOrigin.BattleDefeat) { DefeatBranch = branch };

    public void Validate()
    {
        if (Version != 1 || FieldId is < 0 or > ushort.MaxValue || !Enum.IsDefined(Origin) || Origin == NativeFieldLoadOrigin.NewGame && FieldId != 74 || (Origin == NativeFieldLoadOrigin.BattleDefeat ? FieldId != 75 || DefeatBranch is null || DefeatBranch.ResumePreviousScene : DefeatBranch is not null))
        {
            throw new InvalidDataException("Native field allocation requires its supported original request and WORD destination.");
        }
    }
}
