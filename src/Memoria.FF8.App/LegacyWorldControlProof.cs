using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

public static class LegacyWorldControlProof
{
    public static GameState Upgrade(ReadOnlySpan<byte> originalBytes)
    {
        string sha256 = Convert.ToHexStringLower(SHA256.HashData(originalBytes));
        if (!NativeWorldControlState.IsApprovedLegacyCheckpoint(sha256))
        {
            throw new NotSupportedException("This exact checkpoint has no verified native world-control flag provenance.");
        }
        JsonSerializerOptions options = new() { Converters = { new JsonStringEnumConverter() } };
        GameState state = JsonSerializer.Deserialize<GameState>(originalBytes, options) ?? throw new InvalidDataException("Empty checkpoint.");
        _ = new GameSession(state);
        if (state.WorldControls is not null || state.SavedWorld is null || state.Scene != SceneKind.Field || state.FieldId != 172 || state.World is not null || state.WorldArrival is not null || state.EventState is not JsonElement vm || vm.GetProperty("FieldId").GetInt32() != 172 || vm.GetProperty("Frame").GetInt64() != 0)
        {
            throw new InvalidDataException("The exact control flag proof disagrees with its checkpoint state.");
        }
        return state with { WorldControls = NativeWorldControlState.CreateVerifiedLegacyNewGame(sha256) };
    }
}
