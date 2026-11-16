using System.Text.Json;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverFieldHitPointSubtractionTests
{
    [Fact]
    public void Field_subtraction_publishes_the_bound_guest_row_without_the_additive_maximum_cap()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(root), "Select installed original FFVIII resources.");
        using GameAssets game = new(root!);
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals();
        GameplaySaveComponentDocument document = GameplaySaveSessionLoaderTests.Document(game, world: false) with { Characters = GameplaySaveCharacters.Capture(vitals) };
        NativeCharacterVital[] rows = document.Characters.Records.ToArray();
        rows[0] = rows[0] with { HitPoints = new(1, 1) };
        document = document with { Characters = document.Characters with { Records = rows } };
        string untouched = JsonSerializer.Serialize(rows[1]);
        using MemoryStream payload = new();
        GameplaySaveCodec.Write(payload, document);
        byte[] original = payload.ToArray();
        CombinedSessionSave envelope = new() { ActiveGame = SessionGame.Ff8, Ff8Payload = original, Ff8PayloadFormat = CrossoverSaveAdapter.Ff8Codec, MixedCombatBindings = [new(0, 0)] };
        using CrossoverGuestFieldVitals guest = new(root!, () => envelope, updated => envelope = updated);

        guest.SubtractHitPoints(0, 501);
        Assert.Equal((ushort)0, guest.Read().Hp);
        guest.AddHitPoints(0, 1);
        Assert.Equal((ushort)1, guest.Read().Hp);
        guest.SubtractHitPoints(0, short.MinValue);
        Assert.Equal((ushort)32769, guest.Read().Hp);
        GameplaySaveComponentDocument updated = CrossoverSaveAdapter.ReadFf8(envelope);
        Assert.Equal((ushort)32769, updated.Characters.Records[0].HitPoints.Minimum);
        Assert.Equal(untouched, JsonSerializer.Serialize(updated.Characters.Records[1]));
        Assert.Equal(original, payload.ToArray());
    }
}
