using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeBattleMusicByteContract()
    {
        foreach (int raw in new[] { -1, 0x1234, 256, int.MinValue })
        {
            byte[] code = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, raw), (Jsm.Opcode.SETBATTLEMUSIC, 0), (Jsm.Opcode.PSHM_B, 203), (Jsm.Opcode.POPI_L, 1), (Jsm.Opcode.PSHSM_B, 203), (Jsm.Opcode.POPI_L, 2), (Jsm.Opcode.PSHN_L, raw ^ 0x80), (Jsm.Opcode.POPM_B, 203), (Jsm.Opcode.PSHM_B, 203), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.RET, 0));
            GameSession session = new(new GameState());
            EventEngine engine = new(session);
            engine.LoadField(100, code, []);
            Require(engine.Scene.NativeBattleMusic203 is null && !engine.Variables.ContainsKey(203), "Ordinary field load invented a battle-music BYTE203 value.");
            for (int tick = 0; tick < 12 && !engine.Capture().GetProperty("Fibers")[0].GetProperty("Results").TryGetProperty("3", out _); tick++)
            {
                engine.Step();
            }
            JsonElement results = engine.Capture().GetProperty("Fibers")[0].GetProperty("Results");
            Require(results.GetProperty("1").GetInt32() == unchecked((byte)raw) && results.GetProperty("2").GetInt32() == unchecked((sbyte)raw) && results.GetProperty("3").GetInt32() == unchecked((byte)(raw ^ 0x80)), "SET/POPM_B203 lost low-BYTE narrowing, signed/unsigned reads or shared ownership.");
            Require(engine.Scene.NativeBattleMusic203 == unchecked((byte)(raw ^ 0x80)) && engine.Variables[203] == engine.Scene.NativeBattleMusic203 && !engine.EventMemory.Cells.ContainsKey(203), "BYTE203 was stored in a generic memory cell beside its scene owner.");
            JsonElement saved = engine.Capture();
            EventEngine clone = new(new GameSession(new GameState()));
            clone.LoadField(100, code, []);
            clone.Restore(saved);
            Require(JsonElement.DeepEquals(saved, clone.Capture()), "Canonical battle music failed independent JSON restore.");
            NativeEventMemory duplicate = session.EventMemory.Write(203, 1, 0);
            ExpectMemoryOwnershipFailure<InvalidDataException>(() => session.SetEventMemory(duplicate), "A known BYTE203 owner accepted a generic duplicate.");
            engine.LoadField(101, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)), []);
            Require(engine.Scene.NativeBattleMusic203 == unchecked((byte)(raw ^ 0x80)), "Field load erased canonical battle music.");
        }

        byte[] readCode = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 1234), (Jsm.Opcode.PSHM_B, 203), (Jsm.Opcode.RET, 0));
        EventEngine old = new(new GameSession(new GameState()));
        old.LoadField(100, readCode, []);
        JsonObject legacy = JsonNode.Parse(old.Capture().GetRawText())!.AsObject();
        legacy["Scene"]!.AsObject().Remove("NativeBattleMusic203");
        legacy["Scene"]!["BattleMusic"] = 77;
        old.Restore(JsonSerializer.SerializeToElement(legacy));
        Require(old.Scene.NativeBattleMusic203 is null && !old.Variables.ContainsKey(203), "Missing canonical BYTE203 was inferred from the obsolete BattleMusic shadow.");
        try
        {
            old.Step();
            throw new InvalidOperationException("Unknown BYTE203 read bypassed canonical ownership.");
        }
        catch (EventScriptException) when (old.LastFault?.Opcode == "PSHM_B")
        {
        }
        JsonElement unknownFiber = old.Capture().GetProperty("Fibers")[0];
        Require(unknownFiber.GetProperty("Position").GetInt32() == 2 && unknownFiber.GetProperty("Stack")[0].GetInt32() == 1234, "Unknown BYTE203 read consumed an operand or advanced its native PC.");

        foreach ((Jsm.Opcode opcode, int address) in new[] { (Jsm.Opcode.PSHM_W, 202), (Jsm.Opcode.PSHM_L, 201), (Jsm.Opcode.PSHSM_W, 203), (Jsm.Opcode.POPM_W, 202), (Jsm.Opcode.POPM_L, 203) })
        {
            EventEngine crossing = new(new GameSession(new GameState()));
            crossing.LoadField(100, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 1234), (opcode, address), (Jsm.Opcode.RET, 0)), []);
            try
            {
                crossing.Step();
                throw new InvalidOperationException("Cross-owner battle music access was admitted.");
            }
            catch (EventScriptException) when (crossing.LastFault?.Opcode == opcode.ToString())
            {
            }
            JsonElement fiber = crossing.Capture().GetProperty("Fibers")[0];
            Require(fiber.GetProperty("Position").GetInt32() == 2 && fiber.GetProperty("Stack")[0].GetInt32() == 1234 && crossing.Scene.NativeBattleMusic203 is null, "Rejected WORD/DWORD access changed its PC, operand or unknown BYTE203.");
        }

        GameSession aliased = new(new GameState());
        aliased.SetEventMemory(aliased.EventMemory.Write(203, 1, 77));
        ExpectMemoryOwnershipFailure<InvalidDataException>(() =>
        {
            EventEngine engine = new(aliased);
            engine.LoadField(100, readCode, []);
        }, "Unknown BYTE203 ownership accepted a pre-existing generic scalar alias.");
        Require(NativeEventMemoryOwnership.SceneRegions(NativeEventSceneMemoryOwners.None).Count == 0, "Root-only state acquired a scene owner.");
        using JsonDocument emptyScene = JsonDocument.Parse("{}");
        var regions = NativeEventMemoryOwnership.SceneRegions(NativeEventMemoryOwnership.SceneOwners(emptyScene.RootElement));
        Require(NativeEventMemoryOwnership.Resolve(203, 1, regions) == NativeEventMemoryOwner.BattleMusic, "An old Scene with unknown BYTE203 failed to reserve its canonical owner.");

        GameSession fresh = CreateNativeMusicSession();
        EventEngine bootstrap = new(fresh);
        bootstrap.LoadField(74, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        Require(bootstrap.Scene.NativeBattleMusic203 == 5 && bootstrap.Variables[203] == 5 && !bootstrap.EventMemory.Cells.ContainsKey(203), "Verified470560/52CA90 New Game reset failed to publish battle music5 into the sole BYTE203 owner.");
        Console.WriteLine("Battle music BYTE203: canonical nullable scene owner; SET/POPM byte aliases, signed reads, cross-owner/legacy refusal, JSON/field carry and authenticated New Game5.");
    }
}
