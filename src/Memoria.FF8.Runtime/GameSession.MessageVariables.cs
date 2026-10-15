using System.Text.Json;

namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public NativeMessageVariables? MessageVariables => State.MessageVariables;
    public bool CanStoreMessageVariables => true;

    public void SetMessageVariables(NativeMessageVariables bank)
    {
        ArgumentNullException.ThrowIfNull(bank);
        State = State with { MessageVariables = bank };
    }

    private static void ValidateMessageVariableCheckpoint(GameState state, JsonElement? vm)
    {
        if (vm is not JsonElement snapshot)
        {
            return;
        }
        if (!snapshot.TryGetProperty("MessageVariables", out JsonElement value))
        {
            throw new InvalidDataException("Saved VM lacks canonical process message-variable metadata; migrate this save explicitly.");
        }
        NativeMessageVariables? saved = value.ValueKind == JsonValueKind.Null ? null : value.Deserialize<NativeMessageVariables>(JsonOptions);
        if (saved != state.MessageVariables)
        {
            throw new InvalidDataException("Root and VM message-variable banks disagree.");
        }
    }
}
