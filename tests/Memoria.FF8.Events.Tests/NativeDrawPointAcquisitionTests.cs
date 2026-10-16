using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeDrawPointAcquisitionContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeDrawPointCatalog catalog = assets.ReadNativeDrawPointCatalog();
        NativeDrawPointParticleTables particles = NativeDrawPointParticleTableReader.Read(File.ReadAllBytes(Path.Combine(gameDirectory, "FF8_EN.exe")));
        byte drawAbility = checked((byte)(20 + Array.IndexOf(catalog.CommandIds.ToArray(), (byte)6)));
        int regeneratingIndex = Enumerable.Range(0, 256).First(index => (catalog.PointBytes[index] & 0x40) != 0 && (catalog.PointBytes[index] & 63) is > 0 and < 57);
        foreach ((int index, bool full, bool cancel) in new[] { (13, false, true), (13, true, false), (13, false, false), (regeneratingIndex, false, false) })
        {
            int magic = catalog.PointBytes[index] & 63;
            byte[] content = SyntheticObjects([1, (8 << 7) | 1], [
                [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.SETPARTY, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.PSHN_L, 200), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.SETDRAWPOINT, 0), (Jsm.Opcode.PSHN_L, index + 1), (Jsm.Opcode.Unknown16, 0), (Jsm.Opcode.RET, 8)],
                [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.UCOFF, 0), (Jsm.Opcode.PSHN_L, 53), (Jsm.Opcode.POPI_L, 0), (Jsm.Opcode.PSHN_L, index + 1), (Jsm.Opcode.DRAWPOINT, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.UCON, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 500), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)],
                [(Jsm.Opcode.LBL, 8), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 200), (Jsm.Opcode.PSHN_L, 200), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.RET, 8)],
                [(Jsm.Opcode.LBL, 9), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
            (GameSession Session, EventEngine Engine) Create()
            {
                GameSession session = GameSession.CreateColdProcess();
                session.StartNewGame();
                session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
                session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
                NativeCharacterVitals initial = session.CharacterVitals!;
                NativeCharacterVital[] records = initial.Records.ToArray();
                NativeMagicStock[] inventory = records[0].MagicInventory.ToArray();
                inventory[0] = new NativeMagicStock((byte)magic, full ? (byte)100 : (byte)98);
                // Declared component stock/command bytes exercise native acquisition, not an original gameplay input journal.
                records[0] = records[0] with { Junction = records[0].Junction with { Commands = drawAbility }, MagicInventory = inventory };
                session.SetCharacterVitals(initial with { Records = records });
                EventEngine engine = new(session)
                {
                    AnimationFrameCount = (_, _) => 4,
                    ReadNativeDrawPointCatalog = assets.ReadNativeDrawPointCatalog,
                    DrawPointParticleTables = particles,
                    ResolveNativeDrawPointDirection = FieldNativeScriptDirectionProbe.ResolveFacing,
                    ResolveGlobalSoundResource = sample =>
                    {
                        GlobalAudioSampleDescriptor descriptor = GlobalAudioFormat.Read(gameDirectory, sample);
                        return new GlobalSoundResource(sample, GlobalSoundBackendStatus.Muted, new GlobalSoundLoopPolicy(descriptor.PlaybackFlags, descriptor.LoopStartPcmByte, descriptor.LoopEndPcmByte, descriptor.WaveFormat.SampleRate, 16, descriptor.WaveFormat.Channels, 1087));
                    }
                };
                engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                // Explicit component visual initialization precedes the declared SETDRAWPOINT anchor reset.
                engine.RestartNativeDrawPointPresentationForMigration();
                return (session, engine);
            }
            (GameSession session, EventEngine engine) = Create();
            void StepRestored()
            {
                JsonElement state = engine.Capture();
                (_, EventEngine restored) = Create();
                restored.Restore(state);
                Require(JsonElement.DeepEquals(state, restored.Capture()), "Eligible draw-point stage did not independently restore exactly.");
                engine.Step();
                restored.Step();
                Require(JsonElement.DeepEquals(engine.Capture(), restored.Capture()), "Eligible draw-point stage continuation changed after independent restore.");
            }
            short? Phase() => engine.Actors[0].NativeMovementAccelerationTarget;
            while (engine.Scene.DrawPointInteraction is null && engine.Frame < 8)
            {
                engine.Step();
            }
            byte expectedEligible = (byte)(1 << Array.IndexOf(session.CharacterVitals!.CombatParty, (byte)0));
            Require(engine.Scene.DrawPointInteraction is { Message: 2, RandomStateBefore: 1, RandomStateAfter: 1103527590 } issued && issued.PointIndex == index && issued.EligibleCombatSlots == expectedEligible && issued.Quantity > 2 && Phase() == 1 && engine.CurrentDialogue?.Text == catalog.FormatMessage(2, magic), "Eligible draw point lost its original message, equipped command, point index, sampled quantity or first phase.");
            short quantity = engine.Scene.DrawPointInteraction!.Quantity;
            Require(engine.Scene.GlobalSoundRequests.Count(request => request.OriginalEffectId == 66) == 1 && session.EventRandom!.State == 1103527590, "Eligible draw point omitted native sound66 or drew script RNG more than once.");
            StepRestored();
            Require(engine.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted, "Eligible discovery message refused ordinary acknowledgement.");
            StepRestored();
            Require(Phase() == 2 && engine.CurrentDialogue is null, "Acknowledged eligible draw skipped its separate selection preparation callback.");
            StepRestored();
            Require(Phase() == 3 && engine.CurrentDialogue is { FirstChoice: 1, LastChoice: 2, InitialChoice: 2, CancelChoice: 1 } selection && selection.Text == catalog.FormatSelection([0], session.CharacterVitals!, magic, expectedEligible), "Draw selection lost its original character/cancel rows or actual choice metadata.");
            StepRestored();
            if (cancel)
            {
                Require(engine.Apply(new PlayerAction(PlayerActionKind.Cancel)).Accepted, "Original draw selection Cancel refused.");
                StepRestored();
                Require(engine.Scene.DrawPointInteraction is null && session.CharacterVitals!.Records[0].MagicInventory[0].Count == 98 && session.EventRandom!.DrawPoints!.Depletion.All(value => value == 0) && session.State.Variables[500] == 77 && engine.IsUserControlEnabled, "Canceled selection awarded/depleted or retained its operand/control wait.");
                continue;
            }
            Require(engine.Apply(new PlayerAction(PlayerActionKind.Choose, 2)).Accepted, "Explicit character draw selection refused.");
            StepRestored();
            Require(Phase() == 4, "Character choice skipped original phase4 body resolution.");
            StepRestored();
            Require(Phase() == 5 && engine.NativeDrawPointAcquisitionActive && engine.Scene.GlobalSoundRequests.Count(request => request.OriginalEffectId == 67) == 1, "Eligible selection skipped actual native particles or native sound67.");
            long launched = engine.Frame;
            StepRestored();
            while (Phase() == 5 && engine.Frame - launched < 64)
            {
                engine.Step();
            }
            Require(Phase() == 6 && !engine.NativeDrawPointAcquisitionActive && engine.Frame - launched >= 38 && session.CharacterVitals!.Records[0].MagicInventory[0].Count == (full ? 100 : 98), "Acquisition did not wait for all eight native spark records before stock writes.");
            StepRestored();
            Require(session.EventRandom!.State == 1103527590 && engine.Scene.DrawPointInteraction!.Quantity == quantity, "Draw acquisition or wait resampled script RNG/quantity.");
            if (full)
            {
                Require(Phase() == 1 && engine.Scene.DrawPointInteraction is { Message: 6, Acquired: 0 } && session.MessageVariables!.At(7) == 0 && session.CharacterVitals!.Records[0].MagicInventory[0].Count == 100, "Full native inventory changed stock or failed to return to phase1 after message6.");
                Require(engine.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted, "Failed acquisition message refused acknowledgement.");
                StepRestored();
                StepRestored();
                Require(Phase() == 3 && engine.Scene.DrawPointInteraction!.Quantity == quantity && session.EventRandom.State == 1103527590, "Failed acquisition retried discovery/RNG instead of original selection.");
                Require(engine.Apply(new PlayerAction(PlayerActionKind.Cancel)).Accepted, "Retried draw selection Cancel refused.");
                StepRestored();
                Require(engine.Scene.DrawPointInteraction is null && session.EventRandom.DrawPoints!.Depletion.All(value => value == 0), "Failed draw/cancel depleted the original packed point.");
            }
            else
            {
                Require(Phase() == 7 && engine.Scene.DrawPointInteraction is { Message: 5, Acquired: 2 } && session.MessageVariables!.At(7) == 2 && session.CharacterVitals!.Records[0].MagicInventory[0] == new NativeMagicStock((byte)magic, 100), "Successful native acquisition did not stop at100 and publish the actual accepted count to message variable7.");
                StepRestored();
                Require(engine.CurrentDialogue!.Text.Contains("2", StringComparison.Ordinal), "Successful draw text did not expand canonical message variable7.");
                Require(session.EventRandom.DrawPoints!.Depletion.All(value => value == 0), "Successful draw depleted before final message acknowledgement.");
                Require(engine.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted, "Successful acquisition message refused acknowledgement.");
                StepRestored();
                int depletion = (session.EventRandom.DrawPoints.Depletion[index / 4] >> (2 * (index & 3))) & 3;
                Require(engine.Scene.DrawPointInteraction is null && depletion == ((catalog.PointBytes[index] & 0x40) != 0 ? 2 : 3) && engine.CurrentDialogue is null && session.State.Variables[500] == 77 && engine.IsUserControlEnabled, "Acknowledged successful draw lost native depletion2/3, operand completion or same-callback successor.");
                JsonElement owner = engine.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1);
                Require(owner.GetProperty("Results").GetProperty("0").GetInt32() == 53 && owner.GetProperty("Results").GetProperty("3").GetInt32() == 53, "Successful draw invented result0=1 instead of preserving the issuing result register.");
            }
        }
        Console.WriteLine("Eligible DRAWPOINT: original discovery/selection, explicit Cancel, actual spark completion, full-stock retry, accepted-unit cap/message variable7, depletion2/3 and independent restore at phases1..7 passed.");
    }
}








