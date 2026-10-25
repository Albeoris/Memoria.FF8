using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeMenuDrawPointMaskContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeDrawPointParticleTables particles = NativeDrawPointParticleTableReader.Read(File.ReadAllBytes(Path.Combine(gameDirectory, "FF8_EN.exe")));
        foreach (int ability in new[] { 0, 78, 79, 80, 81, 82 })
        {
            foreach (int mask in new[] { 0, 1, 255 })
            {
                int expectedSource = ability == 79 ? 1 : 0;
                int expectedMask = expectedSource | mask;
                byte[] content = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 116), (Jsm.Opcode.PSHN_L, 249), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 19), (Jsm.Opcode.PSHN_L, mask), (Jsm.Opcode.SETDRAWPOINT, 0), (Jsm.Opcode.PSHN_L, 13), (Jsm.Opcode.Unknown16, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.MENUNORMAL, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 500), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
                (GameSession Session, EventEngine Engine) Create()
                {
                    GameSession session = GameSession.CreateColdProcess();
                    session.StartNewGame();
                    NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
                    NativeCharacterVital[] records = vitals.Records.ToArray();
                    records[0] = records[0] with { HitPoints = new NativeHpWord(0, 0), Status = 1, Junction = records[0].Junction with { Abilities = (uint)ability << (8 * (ability % 4)) } };
                    records[1] = records[1] with { Junction = records[1].Junction with { Abilities = 79 } };
                    session.InitializeNewGameCharacterVitals(vitals with { Records = records, CombatParty = [0, 255, 255] });
                    session.SetEventMemory(session.EventMemory.Write(88, 1, NativeScalarValue.Exact(1)));
                    session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
                    session.InitializeNewGameShotAvailability(assets.ReadNewGameShotAvailability());
                    session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
                    EventEngine engine = new(session)
                    {
                        AnimationFrameCount = (_, _) => 4,
                        DrawPointParticleTables = particles
                    };
                    engine.LoadField(74, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                    // Explicitly initialize this declared component's transient pool before synchronous setup.
                    engine.RestartNativeDrawPointPresentationForMigration();
                    return (session, engine);
                }

                void EnterMenuReturn(EventEngine engine)
                {
                    Require(engine.PartyMenu is { Phase: NativePartyMenuPhase.Requested }, "Declared menu request was not reached.");
                    engine.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 2));
                    Require(engine.PartyMenu is { Phase: NativePartyMenuPhase.Active }, "Declared menu did not execute its real entry callbacks.");
                    Require(engine.Apply(new PlayerAction(PlayerActionKind.Cancel)).Accepted, "Ordinary normal-menu cancellation was refused.");
                    engine.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 4));
                    Require(engine.PartyMenu is { Phase: NativePartyMenuPhase.FieldResumeInitialization }, "Menu return did not retain its actual kind6 callback boundary.");
                }

                (GameSession session, EventEngine engine) = Create();
                engine.Step();
                Require(engine.Scene.DrawPointReset is { SourceByte88: 1 } reset && reset.SampledDisplayMask == (1 | mask) && engine.Scene.NativeDrawPointDisplayMask == (1 | mask) && engine.Scene.DrawPointEnabled == 1 && engine.Scene.DrawPointIndex == 12, "Declared SETDRAWPOINT did not execute its real byte/index/word producers.");
                JsonElement setup = JsonSerializer.SerializeToElement(engine.Scene.DrawPointReset);
                engine.Step();
                EnterMenuReturn(engine);
                JsonElement pending = engine.Capture();
                (_, EventEngine restored) = Create();
                restored.Restore(pending);
                Require(JsonElement.DeepEquals(pending, restored.Capture()), "Active draw-point menu tail did not restore independently.");
                engine.Apply(new PlayerAction(PlayerActionKind.Wait));
                restored.Apply(new PlayerAction(PlayerActionKind.Wait));
                Require(engine.LastFault is null && engine.PartyMenu is null && session.State.Variables[500] == 77 && engine.Scene.NativeDrawPointDisplayMask == expectedMask && engine.Scene.DrawPointEnabled == 1 && engine.Scene.DrawPointDisplayParameter == mask && engine.Scene.DrawPointIndex == 12, $"Kind6 changed ability {ability}, mask {mask}: source flags {session.CharacterVitals!.GetEncounterAbilityFlags()}, mask {engine.Scene.NativeDrawPointDisplayMask}, phase {engine.PartyMenu?.Phase}, fault {engine.LastFault}.");
                Require(JsonElement.DeepEquals(setup, JsonSerializer.SerializeToElement(engine.Scene.DrawPointReset)) && JsonElement.DeepEquals(engine.Capture(), restored.Capture()), "Kind6 invented another474750 reset, rewrote sampled source/anchors or changed restored continuation.");
                Require(session.EventMemory.Cells?.TryGetValue(88, out NativeMemoryValue? source) == true && source.Value.IsExact && source.Value.RequireExact() == expectedSource, "Kind6 did not publish its actual newly rebuilt source BYTE88.");

                JsonElement valid = engine.Capture();
                foreach (string invalid in mask == 0 ? new[] { "missing", "unknown", "width" } : new[] { "missing", "unknown", "bits", "width" })
                {
                    JsonNode malformed = JsonNode.Parse(valid.GetRawText())!;
                    if (invalid == "missing")
                    {
                        malformed["Scene"]!.AsObject().Remove("NativeDrawPointDisplayMask");
                    }
                    else
                    {
                        malformed["Scene"]!["NativeDrawPointDisplayMask"] = invalid == "unknown" ? null : JsonValue.Create(invalid == "bits" ? 0 : 256);
                    }
                    try
                    {
                        restored.Restore(JsonSerializer.SerializeToElement(malformed));
                        throw new InvalidOperationException("Invalid current native draw-point word was accepted.");
                    }
                    catch (Exception exception) when (exception is JsonException or InvalidDataException)
                    {
                    }
                    Require(JsonElement.DeepEquals(valid, restored.Capture()), "Invalid current display word changed the VM before refusal.");
                }

                byte[] inactive = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.MENUNORMAL, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
                engine.LoadField(75, inactive, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                Require(engine.Scene.DrawPointEnabled == 0 && engine.Scene.DrawPointDisplayParameter == 0 && engine.Scene.DrawPointReset is null && engine.Scene.NativeDrawPointDisplayMask == expectedMask, "Fresh native field reset invented a WORD1CDBFEA write while clearing its dispatch bytes.");
                engine.Step();
                engine.Step();
                EnterMenuReturn(engine);
                engine.Apply(new PlayerAction(PlayerActionKind.Wait));
                Require(engine.LastFault is null && engine.Scene.NativeDrawPointDisplayMask == expectedMask && engine.Scene.DrawPointEnabled == 0 && engine.Scene.DrawPointReset is null, "Inactive menu tail wrote a new display word or particle reset instead of skipping4747A0.");
            }
        }
        Console.WriteLine("Native menu draw-point tail: equipped abilities78..82, KO inclusion/reserve exclusion, rebuilt Move-Find BYTE88 and activeB240 source OR, preserves sampled reset/anchors/index, retains physical word across inactive load/tail, independent restore and invalid required word refusal passed.");
    }
}
