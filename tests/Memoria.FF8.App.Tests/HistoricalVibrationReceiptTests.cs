using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class HistoricalVibrationReceiptTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Completed_request_does_not_require_its_old_bank_to_restore_the_independent_queue(bool unavailable)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeVibrationBank bank = assets.ReadNativeVibrationBank();
        byte[] script = GameplaySaveFieldScriptsTests.Encode([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.SETVIBRATE, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
        EventEngine Create()
        {
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.SetCharacterVitals(assets.ReadNewGameCharacterVitals());
            EventEngine engine = new(session) { ReadNativeVibrationBank = () => bank };
            engine.LoadField(600, script, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        EventEngine source = Create();
        source.Step();
        source.Step();
        Assert.NotNull(source.Scene.VibrationRequest);
        JsonElement saved = source.Capture();
        EventEngine copy = Create();
        copy.ReadNativeVibrationBank = unavailable ? null : () => bank with { SourceHash = new string('A', 64) };
        copy.Restore(saved);
        Assert.True(JsonElement.DeepEquals(saved, copy.Capture()));
        for (int tick = 0; tick < 4; tick++)
        {
            source.Step();
            copy.Step();
            Assert.True(JsonElement.DeepEquals(source.Capture(), copy.Capture()));
        }
        JsonObject malformed = JsonNode.Parse(saved.GetRawText())!.AsObject();
        malformed["Scene"]!["VibrationRequest"]!["EffectivePriority"] = 0;
        JsonElement before = copy.Capture();
        Assert.Throws<InvalidDataException>(() => copy.Restore(JsonSerializer.SerializeToElement(malformed)));
        Assert.True(JsonElement.DeepEquals(before, copy.Capture()));
    }
}
