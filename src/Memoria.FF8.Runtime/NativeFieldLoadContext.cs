using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public enum NativeFieldLoadOrigin
{
    NewGame,
    FieldRequest,
    WorldReturn
}

/// <summary>The executed request whose native loader copies its module-entry reason into event BYTE87.</summary>
public sealed record NativeFieldLoadContext(int Version, int FieldId, NativeFieldLoadOrigin Origin)
{
    [JsonIgnore]
    public byte Reason => Origin switch
    {
        NativeFieldLoadOrigin.NewGame => 0,
        NativeFieldLoadOrigin.FieldRequest => 1,
        NativeFieldLoadOrigin.WorldReturn => 2,
        _ => throw new InvalidDataException("Unknown native field allocation origin.")
    };

    public static NativeFieldLoadContext NewGame() => new(1, 74, NativeFieldLoadOrigin.NewGame);
    public static NativeFieldLoadContext FromFieldRequest(int fieldId) => new(1, fieldId, NativeFieldLoadOrigin.FieldRequest);
    public static NativeFieldLoadContext FromWorldReturn(int fieldId) => new(1, fieldId, NativeFieldLoadOrigin.WorldReturn);

    public void Validate()
    {
        if (Version != 1 || FieldId is < 0 or > ushort.MaxValue || !Enum.IsDefined(Origin) || Origin == NativeFieldLoadOrigin.NewGame && FieldId != 74)
        {
            throw new InvalidDataException("Native field allocation requires its supported original request and WORD destination.");
        }
    }
}
