using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeActorVisibilityContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        (Jsm.Opcode[] Commands, bool[] Saved, bool[] Collision)[] cases =
        [
            ([Jsm.Opcode.THROUGHON, Jsm.Opcode.SHOW], [false, false], [false, true]),
            ([Jsm.Opcode.HIDE, Jsm.Opcode.SHOW], [false, false], [false, true]),
            ([Jsm.Opcode.THROUGHON, Jsm.Opcode.HIDE, Jsm.Opcode.SHOW], [false, true, true], [false, false, false]),
            ([Jsm.Opcode.HIDE, Jsm.Opcode.HIDE, Jsm.Opcode.SHOW], [false, true, true], [false, false, false]),
            ([Jsm.Opcode.HIDE, Jsm.Opcode.THROUGHOFF, Jsm.Opcode.SHOW], [false, false, false], [false, true, true]),
            ([Jsm.Opcode.HIDE, Jsm.Opcode.THROUGHON, Jsm.Opcode.SHOW], [false, false, false], [false, false, true]),
            ([Jsm.Opcode.TALKOFF, Jsm.Opcode.PUSHOFF, Jsm.Opcode.HIDE, Jsm.Opcode.SHOW], [false, false, false, false], [true, true, false, true])
        ];
        foreach (var test in cases)
        {
            List<(Jsm.Opcode, int)> main = [(Jsm.Opcode.LBL, 1)];
            foreach (Jsm.Opcode command in test.Commands)
            {
                main.AddRange([(command, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0)]);
            }
            main.AddRange([(Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 0)]);
            byte[] content = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.RET, 0)], main.ToArray()]);
            EventEngine Create()
            {
                GameSession session = GameSession.CreateColdProcess();
                session.StartNewGame();
                session.InitializeNewGameCharacterVitals(vitals.DeepCopy());
                session.InitializeNewGameEconomy(economy.DeepCopy());
                EventEngine engine = new(session)
                {
                    AnimationFrameCount = (_, _) => 4
                };
                engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                return engine;
            }
            EventEngine live = Create();
            Require(live.Actors[0].NativeSavedCollisionDisabled16 == false, "Fresh52BC00 actor DWORD352 mask16 did not retain its actual zero allocation.");
            int observed = 0;
            live.CommandExecuted += command =>
            {
                if (command.Name is not ("HIDE" or "SHOW" or "THROUGHON" or "THROUGHOFF" or "TALKOFF" or "PUSHOFF"))
                {
                    return;
                }
                ActorState actor = live.Actors[0];
                Require(actor.NativeSavedCollisionDisabled16 == test.Saved[observed] && actor.CollisionEnabled == test.Collision[observed], $"Native{command.Name} lost current588 or saved DWORD352 mask16 at phase{observed}.");
                if (command.Name is "HIDE" or "SHOW")
                {
                    bool visible = command.Name == "SHOW";
                    Require(actor.Visible == visible && actor.PushEnabled == visible && actor.TalkEnabled == visible, $"Native{command.Name} did not write352 mask8,585 and587 independently of their prior values.");
                }
                observed++;
            };
            EventEngine? independent = null;
            for (int tick = 0; tick < 40 && observed < test.Commands.Length; tick++)
            {
                live.Step();
                if (independent is not null)
                {
                    independent.Step();
                    Require(JsonElement.DeepEquals(live.Capture(), independent.Capture()), "Independent HIDE/SHOW restore changed the next native opcode, current contacts or saved mask16.");
                }
                independent = Create();
                independent.Restore(live.Capture());
            }
            Require(observed == test.Commands.Length, "Native visibility component did not execute every original command.");
            JsonElement valid = live.Capture();
            JsonNode unknown = JsonNode.Parse(valid.GetRawText())!;
            unknown["Actors"]![0]!["NativeSavedCollisionDisabled16"] = null;
            try
            {
                live.Restore(JsonSerializer.SerializeToElement(unknown));
                throw new InvalidOperationException("Unknown saved collision mask16 was inferred from current visibility or collision.");
            }
            catch (InvalidDataException)
            {
            }
            Require(JsonElement.DeepEquals(valid, live.Capture()), "Rejected unknown mask16 partially changed the current VM.");
        }

        // An unallocated component does not acquire native flags from a HIDE/SHOW call.
        foreach (Jsm.Opcode command in new[] { Jsm.Opcode.HIDE, Jsm.Opcode.SHOW })
        {
            GameSession session = new(new GameState());
            EventEngine unallocated = new(session);
            unallocated.LoadField(600, SyntheticProgram((Jsm.Opcode.LBL, 0), (command, 0), (Jsm.Opcode.RET, 0)), []);
            try
            {
                unallocated.Step();
                throw new InvalidOperationException("HIDE/SHOW manufactured an unknown physical mask16.");
            }
            catch (EventScriptException)
            {
            }
            JsonElement saved = unallocated.Capture();
            JsonElement fiber = saved.GetProperty("Fibers")[0];
            Require(unallocated.LastFault?.Operation == 1 && fiber.GetProperty("Position").GetInt32() == 1 && fiber.GetProperty("Stack").GetArrayLength() == 0 && unallocated.Actors[0] is { Visible: true, CollisionEnabled: true, PushEnabled: true, TalkEnabled: true, NativeSavedCollisionDisabled16: null }, "Unknown HIDE/SHOW owner changed the original PC, operands or current contact flags.");
        }
        Console.WriteLine("Native HIDE/SHOW: fresh saved16, collision restore, repeated HIDE, hidden THROUGH writes, talk/push resets, independent restore and unknown-owner refusals pass.");
    }
}
