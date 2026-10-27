using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeBlinkEyesContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(689);
        byte[] content = SyntheticObjects([1], [
            [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 3), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 777), (Jsm.Opcode.PSHN_L, 260), (Jsm.Opcode.BLINKEYES, 0), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.POPI_L, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
        string hash = Convert.ToHexString(SHA256.HashData(content));
        NativeFieldRegistrationResources resources = field.NativeRegistration! with { ProgramHash = hash, Loader = field.NativeRegistration!.Loader! with { Program = new("protocol/jsm", hash, content.ToImmutableArray()) } };
        (GameSession Session, EventEngine Engine) Create()
        {
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
            session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
            session.InitializeNewGameSalaryTable(assets.ReadNativeSalaryTable());
            session.RequestField(689, 0);
            EventEngine engine = new(session)
            {
                AnimationFrameCount = field.GetAnimationFrameCount,
                ReadNativeEyes = model => field.Models[model].NativeEyes,
                ReadNativeFieldRegistrationResources = () => resources
            };
            engine.LoadField(689, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return (session, engine);
        }
        var host = Create();
        EventEngine live = host.Engine;
        live.Step();
        NativeCrtRandomState initial = live.Scene.NativeEyeProcess!.CrtRandom!;
        live.Step();
        Require(live.Actors[0].NativeEyeBlinkParameter611 == 4 && live.Actors[0].NativeEyes is { Mode: 0x80000, BoundModelId: 3 }, "BLINKEYES lost lowBYTE611 or registered actor/model ownership.");
        NativeCrtRandomState expected = initial.Draw(out _).Draw(out _);
        Require(live.Scene.NativeEyeProcess!.CrtRandom == expected, "BLINKEYES did not consume exactly two shared CRT draws before its first graphics callback.");
        NativeCrtRandomState sharedDraw = host.Session.State.CrtRandom!.Draw(out _);
        host.Session.SetNativeFieldEyeOwners(host.Session.CurrentNativeBattleMemory!, sharedDraw);
        NativeFieldEyeProcess liveBeforeCapture = live.Scene.NativeEyeProcess!;
        JsonElement saved = live.Capture();
        NativeFieldEyeProcess capturedProcess = saved.GetProperty("Scene").GetProperty("NativeEyeProcess").Deserialize<NativeFieldEyeProcess>()!;
        Require(capturedProcess.CrtRandom == sharedDraw, "Eye snapshot replaced an intervening draw from the current shared CRT owner.");
        Require(ReferenceEquals(liveBeforeCapture, live.Scene.NativeEyeProcess), "Eye checkpoint replaced its running scene owner.");
        Require(ReferenceEquals(host.Session.CurrentNativeBattleMemory, liveBeforeCapture.Memory), "Eye checkpoint replaced its running retained memory.");
        Require(saved.GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ScriptId").GetInt32() == 1).GetProperty("Stack")[0].GetInt32() == 777, "BLINKEYES consumed an unrelated retained DWORD.");
        EventEngine restored = Create().Engine;
        restored.Restore(saved);
        for (int index = 0; index < 20; index++)
        {
            live.Step();
            restored.Step();
            Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "BLINKEYES CPU counters, VRAM copies or shared CRT diverged after independent restore.");
        }
        Console.WriteLine("Native5262C0/532340/532930: real registered Rinoa resources, lowBYTE611, shared CRT, preserved operand and independent graphics callbacks.");
    }
}
