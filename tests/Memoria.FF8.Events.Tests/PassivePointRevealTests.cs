using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void PassivePointRevealContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeDrawPointParticleTables particles = NativeDrawPointParticleTableReader.Read(File.ReadAllBytes(Path.Combine(gameDirectory, "FF8_EN.exe")));
        foreach (bool active in new[] { false, true })
        {
            foreach (int parameter in new[] { 0, 1 })
            {
                GameSession session = GameSession.CreateColdProcess();
                session.StartNewGame();
                NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
                vitals.GuardianForces[3] = vitals.GuardianForces[3] with { Flags = 1, HitPoints = 580, LearnedBank = new(0, 1UL << 15), Progression = null, LastProjection = null, MoveFindDisabled = !active };
                session.InitializeNewGameCharacterVitals(vitals);
                session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
                session.SetEventMemory(session.EventMemory.Write(88, 1, NativeScalarValue.Exact(active ? 0 : 1)));
                byte[] content = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 56), (Jsm.Opcode.PSHN_L, -501), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 3), (Jsm.Opcode.PSHN_L, parameter), (Jsm.Opcode.SETDRAWPOINT, 0), (Jsm.Opcode.PSHN_L, 14), (Jsm.Opcode.Unknown16, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHM_B, 88), (Jsm.Opcode.POPM_B, 500), (Jsm.Opcode.PSHN_L, 14), (Jsm.Opcode.DRAWPOINT, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 501), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
                EventEngine engine = new(session)
                {
                    AnimationFrameCount = (_, _) => 4,
                    DrawPointParticleTables = particles,
                    ReadNativeDrawPointCatalog = assets.ReadNativeDrawPointCatalog
                };
                engine.LoadField(74, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                engine.RestartNativeDrawPointPresentationForMigration();
                engine.Step();
                Require(session.EventMemory.Read(88, 1).RequireExact() == (active ? 1 : 0), "Passive source did not replace the stale equipped-ability source.");
                Require(engine.Scene.NativeDrawPointDisplayMask == ((active ? 1 : 0) | parameter), "Hidden/ordinary draw visibility did not use active GF passive.");
                JsonElement setup = JsonSerializer.SerializeToElement(engine.Scene.DrawPointReset);
                NativeEventRandomState random = session.EventRandom!;
                engine.Step();
                Require(session.State.Variables[500] == (active ? 1 : 0), "Native hidden-save consumer did not see the passive reveal byte.");
                if (!active && parameter == 0)
                {
                    Require(engine.CurrentDialogue is null && engine.Scene.DrawPointInteraction is null && session.EventRandom == random && session.State.Variables[501] == 77, "Unavailable hidden point consumed random state, opened dialogue or prevented script completion.");
                }
                else
                {
                    Require(engine.CurrentDialogue is { Channel: 6 } && engine.Scene.DrawPointInteraction is not null && session.EventRandom != random, "Available hidden or ordinary point did not reach the original DRAWPOINT interaction.");
                }
                Require(JsonElement.DeepEquals(setup, JsonSerializer.SerializeToElement(engine.Scene.DrawPointReset)), "Live passive source rewrote historical point setup.");
                JsonElement saved = engine.Capture();
                engine.Restore(saved);
                Require(JsonElement.DeepEquals(saved, engine.Capture()), "Passive reveal changed the exact restored snapshot before a callback.");
                if (!active && parameter == 0)
                {
                    session.SetNativeEventOwners(session.WorldControls!, characterVitals: session.CharacterVitals!.SetGuardianForcePassiveAbilityEnabled(3, 79, true));
                    engine.Step();
                    Require(session.EventMemory.Read(88, 1).RequireExact() == 1 && engine.Scene.NativeDrawPointDisplayMask == 1, "Enabling the GF passive did not reveal the already-loaded point on its next callback.");
                    Require(JsonElement.DeepEquals(setup, JsonSerializer.SerializeToElement(engine.Scene.DrawPointReset)), "Passive refresh rewrote the original setup receipt.");
                }
            }
        }
        Console.WriteLine("Passive point reveal: stale source refresh, original hidden-save byte consumer, hidden draw visibility/interaction, ordinary draw access and unchanged setup receipt passed.");
    }
}
