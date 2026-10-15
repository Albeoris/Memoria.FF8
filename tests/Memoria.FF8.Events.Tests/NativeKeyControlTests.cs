using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeKeyControlContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        foreach (int operand in new[] { 1, 65536, -1 })
        {
            byte[] content = SyntheticObjects([1], [
                [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 8)],
                [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.NOP, 0), (Jsm.Opcode.KEY, 0), (Jsm.Opcode.PSHN_L, 99), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.UCOFF, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, operand), (Jsm.Opcode.NOP, 0), (Jsm.Opcode.KEY, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.UCON, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.NOP, 0), (Jsm.Opcode.KEY, 0), (Jsm.Opcode.UCON, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8)]
            ]);
            EventEngine Create()
            {
                GameSession session = GameSession.CreateColdProcess();
                session.StartNewGame();
                session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
                session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
                session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
                EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 4 };
                engine.LoadField(74, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                engine.Step();
                return engine;
            }
            static JsonElement Caller(JsonElement state) => state.GetProperty("Fibers").EnumerateArray().Single(fiber => !fiber.GetProperty("Initialization").GetBoolean());
            static bool SequenceDisabled(EventEngine engine) => engine.Capture().GetProperty("UserControlSequenceDisabled").GetBoolean();
            void CheckRestore(EventEngine engine)
            {
                JsonElement saved = engine.Capture();
                EventEngine restored = Create();
                restored.Restore(saved);
                Require(saved.GetRawText() == restored.Capture().GetRawText(), "KEY divergent input/sequence state did not restore exactly into an independent engine.");
                engine.Step();
                restored.Step();
                Require(engine.Capture().GetRawText() == restored.Capture().GetRawText(), "KEY divergent input/sequence state changed its next ordinary callback after restore.");
            }

            EventEngine live = Create();
            JsonElement before = default;
            int keyCommands = 0;
            long firstKeyFrame = -1;
            bool offReset = false;
            live.CommandExecuted += command =>
            {
                JsonElement current = live.Capture();
                if (command.Name == nameof(Jsm.Opcode.KEY))
                {
                    JsonElement priorCaller = Caller(before);
                    JsonElement currentCaller = Caller(current);
                    int fullOperand = priorCaller.GetProperty("Stack")[0].GetInt32();
                    Require(command.Arguments.SequenceEqual([fullOperand]) && currentCaller.GetProperty("Stack").GetArrayLength() == 0 && JsonElement.DeepEquals(priorCaller.GetProperty("Results"), currentCaller.GetProperty("Results")), "KEY did not consume exactly its full DWORD or changed native results.");
                    Require(current.GetProperty("UserControlEnabled").GetBoolean() == (fullOperand != 0) && current.GetProperty("UserControlSequenceDisabled").GetBoolean() == before.GetProperty("UserControlSequenceDisabled").GetBoolean(), "KEY truncated its DWORD or changed the independent UCOFF/UCON sequence guard.");
                    Require(JsonElement.DeepEquals(before.GetProperty("Actors"), current.GetProperty("Actors")) && JsonElement.DeepEquals(before.GetProperty("Scene"), current.GetProperty("Scene")) && JsonElement.DeepEquals(before.GetProperty("FieldButtonInput"), current.GetProperty("FieldButtonInput")), "KEY changed actor state, party bindings or the current sampled input.");
                    if (keyCommands++ == 0)
                    {
                        firstKeyFrame = live.Frame;
                    }
                }
                if (command.Name == nameof(Jsm.Opcode.UCOFF))
                {
                    ActorState actor = live.Actors[0];
                    offReset = actor.AnimationId == actor.BaseAnimationId && actor.AnimationFrame == 0 && actor.NativePartyFlags is { AutoAnimation4: false } && SequenceDisabled(live);
                }
                before = current;
            };
            live.Step();
            Require(!live.IsUserControlEnabled && !SequenceDisabled(live) && CapturedVariables(live.Capture()).GetProperty("5000").GetInt32() == 99 && live.Frame == firstKeyFrame, "KEY0 did not disable input and continue immediately in the same callback.");
            CheckRestore(live);
            Require(offReset && !live.IsUserControlEnabled && SequenceDisabled(live), "KEY0 incorrectly suppressed UCOFF's full idle/phase reset and native party-mask update.");
            live.Step();
            Require(live.IsUserControlEnabled && SequenceDisabled(live) && live.Actors[0].NativePartyFlags is { AutoAnimation4: false }, $"KEY{operand} did not enable input independently of the disabled sequence and native actor mask.");
            CheckRestore(live);
            Require(live.IsUserControlEnabled && !SequenceDisabled(live) && live.Actors[0].NativePartyFlags is { AutoAnimation4: true }, "UCON failed to restore its disabled sequence after a nonzero KEY enabled input.");
            live.Step();
            Require(!live.IsUserControlEnabled && !SequenceDisabled(live) && live.Actors[0].NativePartyFlags is { AutoAnimation4: true } && keyCommands == 3 && live.LastFault is null, "Already-on UCON incorrectly overrode KEY0's independent disabled-input gate.");
        }
        Console.WriteLine("KEY313: full DWORD0/1/65536/-1, immediate consume without result/actor/input changes, independent UCOFF/UCON guards and exact divergent-state restore/continuation PASS.");
    }
}
