using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Identity evidence only. The caller must check the parsed state and perform an explicit upgrade.</summary>
public sealed record LegacySavedWorldCheckpointProof(string Sha256, int ExpectedFieldId, long ExpectedFrame);

public static class LegacySavedWorldProof
{
    public static GameState Upgrade(ReadOnlySpan<byte> originalBytes)
    {
        LegacySavedWorldCheckpointProof proof = Resolve(originalBytes) ?? throw new NotSupportedException("This exact checkpoint has no verified legacy New Game saved-world provenance.");
        JsonSerializerOptions options = new() { Converters = { new JsonStringEnumConverter() } };
        GameState state = JsonSerializer.Deserialize<GameState>(originalBytes, options) ?? throw new InvalidDataException("Empty checkpoint.");
        _ = new GameSession(state);
        if (state.SavedWorld is not null || state.Scene != SceneKind.Field || state.FieldId != proof.ExpectedFieldId || state.World is not null || state.WorldArrival is not null || state.EventState is not JsonElement vm || vm.GetProperty("FieldId").GetInt32() != proof.ExpectedFieldId || vm.GetProperty("Frame").GetInt64() != proof.ExpectedFrame)
        {
            throw new InvalidDataException("The exact legacy proof disagrees with its checkpoint state.");
        }
        return state with { SavedWorld = NativeSavedWorldState.CreateVerifiedLegacyNewGame(proof.Sha256) };
    }

    /// <summary>Hashes original checkpoint bytes; unrelated legacy files never receive initial saved-world defaults.</summary>
    public static LegacySavedWorldCheckpointProof? Resolve(ReadOnlySpan<byte> originalBytes)
    {
        string sha256 = Convert.ToHexStringLower(SHA256.HashData(originalBytes));
        return sha256 switch
        {
            NativeSavedWorldState.LegacyCheckpoint265Sha256 => new LegacySavedWorldCheckpointProof(sha256, 137, 265),
            NativeSavedWorldState.LegacyCheckpoint300Sha256 => new LegacySavedWorldCheckpointProof(sha256, 137, 300),
            _ => null
        };
    }
}
