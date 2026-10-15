using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeDrawPointSourceByteContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        byte[] Content(int mask) => SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, -1494), (Jsm.Opcode.PSHN_L, 670), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 3), (Jsm.Opcode.MENUNORMAL, 0), (Jsm.Opcode.PSHM_B, 88), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.MENUNORMAL, 0), (Jsm.Opcode.PSHM_B, 88), (Jsm.Opcode.POPI_L, 4), (Jsm.Opcode.PSHN_L, mask), (Jsm.Opcode.SETDRAWPOINT, 0), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8)]]);

        (GameSession Session, EventEngine Engine) Create(int mask = 0)
        {
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
            session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
            session.InitializeNewGameShotAvailability(assets.ReadNewGameShotAvailability());
            session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
            EventEngine engine = new(session);
            engine.LoadField(74, Content(mask), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            session.MarkReplayUnavailable("Declared BYTE88/menu/draw-point component; no original journey is claimed.");
            return (session, engine);
        }

        void CloseNormalMenu(EventEngine engine)
        {
            Require(engine.PartyMenu is { Kind: NativeFieldMenuKind.Normal, Phase: NativePartyMenuPhase.Requested }, "Declared menu did not reach its actual request.");
            engine.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 2));
            Require(engine.PartyMenu is { Phase: NativePartyMenuPhase.Active }, "Declared normal menu did not execute bootstrap and entry.");
            Require(engine.Apply(new PlayerAction(PlayerActionKind.Cancel)).Accepted, "Ordinary menu cancellation was refused.");
            engine.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 3));
            Require(engine.PartyMenu is { Phase: NativePartyMenuPhase.FieldReentry }, "Normal menu did not finish its native no-change exit.");
            engine.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 2));
            Require(engine.LastFault is null, "Native kind6 BYTE88 publication or resumed consumer refused.");
        }

        (GameSession session, EventEngine engine) = Create();
        engine.Step();
        engine.Step();
        CloseNormalMenu(engine);
        Require(session.EventMemory.Cells?.TryGetValue(88, out NativeMemoryValue? source) == true && source.Width == 1 && source.Value.IsExact && source.Value.RequireExact() == 0, "Executed495EF0/52B4F0 lost BYTE88 width or retained a bare scalar mirror.");
        JsonElement beforeSecondReturn = engine.Capture();
        (GameSession restoredSession, EventEngine restored) = Create();
        restored.Restore(beforeSecondReturn);
        Require(JsonElement.DeepEquals(beforeSecondReturn, restored.Capture()), "Exact typed BYTE88 did not restore independently.");
        CloseNormalMenu(engine);
        CloseNormalMenu(restored);
        Require(JsonElement.DeepEquals(engine.Capture(), restored.Capture()), "Second kind6 overwrite or draw-point continuation differed after restore.");
        Require(engine.Scene.DrawPointReset is { SampledDisplayMask: 0, SourceByte88: 0 } && engine.Scene.DrawPointEnabled == 1 && engine.Scene.DrawPointDisplayParameter == 0, "SETDRAWPOINT lowBYTE0 failed after the genuine menu source producer.");
        JsonElement snapshot = engine.Capture();
        JsonElement results = snapshot.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1).GetProperty("Results");
        Require(results.GetProperty("3").GetInt32() == 0 && results.GetProperty("4").GetInt32() == 0, "BYTE88 reads did not consume the exact width-preserved source.");
        restored.Restore(snapshot);
        foreach (string invalid in new[] { "source", "mask" })
        {
            JsonNode malformed = JsonNode.Parse(snapshot.GetRawText())!;
            if (invalid == "source")
            {
                malformed["Scene"]!["DrawPointReset"]!.AsObject().Remove("SourceByte88");
            }
            else
            {
                malformed["Scene"]!["DrawPointReset"]!["SampledDisplayMask"] = 1;
            }
            try
            {
                restored.Restore(JsonSerializer.SerializeToElement(malformed));
                throw new InvalidOperationException("Missing sampled source or invalid native OR receipt was accepted.");
            }
            catch (Exception exception) when (exception is JsonException or InvalidDataException)
            {
            }
            Require(JsonElement.DeepEquals(snapshot, restored.Capture()), "Invalid draw-point mask/source changed the VM before restore refusal.");
        }
        engine.Step();
        restored.Step();
        Require(JsonElement.DeepEquals(engine.Capture(), restored.Capture()), "Draw-point continuation or restore recreated a bare BYTE88 mirror.");

        JsonElement stateBeforeRefusal = JsonSerializer.SerializeToElement(session.State);
        foreach (Dictionary<int, NativeMemoryValue> invalid in new[]
        {
            new Dictionary<int, NativeMemoryValue> { [88] = new(1, NativeScalarValue.Exact(256)) },
            new Dictionary<int, NativeMemoryValue> { [87] = new(2, NativeScalarValue.Exact(0)), [88] = new(1, NativeScalarValue.Exact(0)) },
            new Dictionary<int, NativeMemoryValue> { [116] = new(1, NativeScalarValue.Exact(0)) }
        })
        {
            try
            {
                session.SetNativeEventOwners(session.WorldControls! with { Menu209 = 99 }, eventMemory: new NativeEventMemory(1, invalid));
                throw new InvalidOperationException("Invalid prospective typed memory was accepted.");
            }
            catch (InvalidDataException)
            {
            }
            Require(JsonElement.DeepEquals(stateBeforeRefusal, JsonSerializer.SerializeToElement(session.State)), "Composite native publication changed controls or another owner before typed-memory refusal.");
        }
        Dictionary<int, NativeMemoryValue> valid = new() { [88] = new(1, NativeScalarValue.Exact(0)), [5000] = new(2, NativeScalarValue.Exact(32768)) };
        session.SetNativeEventOwners(session.WorldControls!, eventMemory: new NativeEventMemory(1, valid));
        valid.Clear();
        Require(session.EventMemory.Cells?.Count == 2 && session.EventMemory.Cells[5000].Width == 2, "Valid nonoverlapping exact native memory was refused or retained a mutable caller dictionary.");
        foreach (int mask in new[] { 256, -256, 2, 254, 255, 0x1234 })
        {
            (_, EventEngine selected) = Create(mask);
            selected.Step();
            selected.Step();
            CloseNormalMenu(selected);
            CloseNormalMenu(selected);
            byte expected = unchecked((byte)mask);
            Require(selected.Scene.DrawPointReset is { SourceByte88: 0 } reset && reset.SampledDisplayMask == expected && selected.Scene.DrawPointDisplayParameter == expected && selected.Scene.DrawPointEnabled == 1, "SETDRAWPOINT changed its lowBYTE truncation, unsigned OR or unconditional B240=1.");
        }
        Console.WriteLine("Native BYTE88: two executed normal-menu kind6 producers, no scalar mirror, zero/even/odd display byte OR, truncation/read/independent restore and composite typed-memory atomic refusal passed.");
    }
}
