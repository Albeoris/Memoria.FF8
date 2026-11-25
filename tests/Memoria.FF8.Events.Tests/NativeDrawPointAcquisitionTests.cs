using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
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
        FieldAssets field = assets.LoadField(600);
        NativeFieldEyeProcess coldMemory = new NativeFieldEyeProcess(1, assets.ReadColdProcessNativeBattleMemory(), NativeCrtRandomState.FromColdProcess()).CaptureSnapshot();
        foreach ((int index, int initialCount, bool cancel, int bonus, bool shared, int receiver, bool equipped, bool fieldOnlyLeader) in new[]
        {
            (13, 97, false, 0, false, 0, true, true),
            (13, 98, true, 0, false, 0, true, false),
            (13, 100, false, 0, false, 0, true, false),
            (13, 98, false, 0, false, 0, true, false),
            (regeneratingIndex, 98, false, 0, false, 0, true, false),
            (13, 1, false, 5, false, 0, true, false),
            (13, 1, false, 100, false, 0, true, false),
            (13, 98, true, 100, false, 0, true, false),
            (13, 100, false, 100, false, 0, true, false),
            (13, 98, false, 0, true, 0, true, false),
            (regeneratingIndex, 98, false, 100, true, 0, true, false),
            (13, 100, true, 0, true, 0, true, false),
            (13, 98, false, 0, true, 1, true, true),
            (13, 98, true, 0, true, 0, false, false),
            (13, 98, false, 0, true, 1, false, true)
        })
        {
            bool full = initialCount == 100;
            bool missingPersonalReceiver = !shared && fieldOnlyLeader;
            int magic = catalog.PointBytes[index] & 63;
            byte[] content = SyntheticObjects([1, (8 << 7) | 1], [
                [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, receiver == 0 ? 255 : receiver), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.SETPARTY, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.PSHN_L, 200), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.SETDRAWPOINT, 0), (Jsm.Opcode.PSHN_L, index + 1), (Jsm.Opcode.Unknown16, 0), (Jsm.Opcode.RET, 8)],
                [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.UCOFF, 0), (Jsm.Opcode.PSHN_L, 53), (Jsm.Opcode.POPI_L, 0), (Jsm.Opcode.PSHN_L, index + 1), (Jsm.Opcode.DRAWPOINT, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.UCON, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 500), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)],
                [(Jsm.Opcode.LBL, 8), (Jsm.Opcode.PSHN_L, receiver), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 200), (Jsm.Opcode.PSHN_L, 200), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.RET, 8)],
                [(Jsm.Opcode.LBL, 9), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
            // Register the original model resources against the declared test program for checkpoint restore.
            NativeFieldRegistrationResources registration = field.NativeRegistration!;
            NativeFieldRegistrationLoaderInputs loader = registration.Loader!;
            string programHash = Convert.ToHexString(SHA256.HashData(content));
            registration = registration with { ProgramHash = programHash, Loader = loader with { Program = loader.Program with { Sha256 = programHash, Data = content.ToImmutableArray() } } };
            (GameSession Session, EventEngine Engine) Create()
            {
                GameSession session = GameSession.CreateColdProcess(coldMemory.RestoreOwned().Memory);
                session.StartNewGame();
                session.SetGameplayPreferences(session.State.GameplayPreferences with { Tuning = new GameplayTuning { DrawBonus = bonus } });
                session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
                session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
                NativeCharacterVitals initial = session.CharacterVitals!;
                NativeCharacterVital[] records = initial.Records.ToArray();
                NativeMagicStock[] inventory = records[0].MagicInventory.ToArray();
                inventory[0] = new NativeMagicStock((byte)magic, checked((byte)initialCount));
                // Declared component stock/command bytes exercise native acquisition, not an original gameplay input journal.
                records[0] = records[0] with { MagicInventory = inventory };
                if (equipped)
                {
                    records[receiver] = records[receiver] with { Junction = records[receiver].Junction with { Commands = drawAbility } };
                }
                session.SetCharacterVitals(initial with { Records = records });
                if (shared)
                {
                    session.SetGameplayPreferences(session.State.GameplayPreferences with { SharedMagicStock = true });
                }
                EventEngine engine = new(session)
                {
                    AnimationFrameCount = (_, _) => 4,
                    ReadNativeFieldRegistrationResources = () => registration,
                    ReadNativeEmptyAnimationTerminals = models => assets.ReadNativeEmptyAnimationTerminals(field.Id, models),
                    ReadNativeEyes = model => field.Models[model].NativeEyes,
                    ReadNativeDrawPointCatalog = assets.ReadNativeDrawPointCatalog,
                    DrawPointParticleTables = particles,
                    ResolveNativeDrawPointDirection = FieldNativeScriptDirectionProbe.ResolveFacing,
                    ResolveGlobalSoundResource = sample =>
                    {
                        GlobalAudioSampleDescriptor descriptor = GlobalAudioFormat.Read(gameDirectory, sample);
                        return new GlobalSoundResource(sample, GlobalSoundBackendStatus.Muted, new GlobalSoundLoopPolicy(descriptor.PlaybackFlags, descriptor.LoopStartPcmByte, descriptor.LoopEndPcmByte, descriptor.WaveFormat.SampleRate, 16, descriptor.WaveFormat.Channels, 1087));
                    }
                };
                session.RequestField(field.Id, 0);
                engine.LoadField(field.Id, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                // Explicit component visual initialization precedes the declared SETDRAWPOINT anchor reset.
                engine.RestartNativeDrawPointPresentationForMigration();
                session.SetEventMemory(session.EventMemory.Write(211, 2, NativeScalarValue.Exact(0xA580)));
                return (session, engine);
            }
            (GameSession session, EventEngine engine) = Create();
            if (fieldOnlyLeader)
            {
                engine.Step();
                engine.Scene.Party[0] = 5;
                if (missingPersonalReceiver)
                {
                    engine.Scene.Party[1] = 0;
                }
            }
            List<(int MagicId, int Acquired, int Stock)> awards = [];
            engine.DrawPointMagicAcquired += awards.Add;
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
            void RestoreForeignFocus()
            {
                JsonNode state = JsonNode.Parse(engine.Capture().GetRawText())!;
                state["Scene"]!["NativeDialogueFocus"] = 4;
                state["Scene"]!["NativeAaskPreviousFocus"] = 7;
                state["CurrentDialogueChannel"] = null;
                engine.Restore(JsonSerializer.SerializeToElement(state));
            }
            while (engine.Scene.DrawPointInteraction is null && engine.Frame < 8)
            {
                engine.Step();
            }
            byte expectedEligible = equipped ? (byte)(1 << Array.IndexOf(session.CharacterVitals!.CombatParty, (byte)receiver)) : (byte)0;
            Require(engine.Scene.NativeDrawPointParticles!.DrawUnavailable == (full || !equipped), "Ready draw-point availability did not reflect actual magic capacity.");
            if (shared)
            {
                if (fieldOnlyLeader)
                {
                    // The active field identity has no corresponding combat slot after setup.
                    Require(engine.Scene.Party[0] == 5 && !session.CharacterVitals!.CombatParty.Contains((byte)5), "Draw-point mismatch fixture lost its field-only identity.");
                }
                int originalCount = session.CharacterVitals!.GetMagicCount(0, magic);
                Require(originalCount == initialCount, "Shared draw-point setup changed its initial stock.");
                if (full || !equipped)
                {
                    Require(engine.Scene.DrawPointInteraction is { SharedSelection: true, Message: 4 } && Phase() == 3 && engine.CurrentDialogue is { FirstChoice: 1, LastChoice: 2, CancelChoice: 1 } sharedSelection && sharedSelection.Text == catalog.FormatSharedSelection(false) && !sharedSelection.Text.Contains("{Char ", StringComparison.Ordinal), "Unavailable shared draw did not offer only Don't draw/Draw choices.");
                    StepRestored();
                    WaitForNativeWindowReady(engine, 6, engine.Step);
                    Require(engine.Apply(new PlayerAction(PlayerActionKind.Cancel)).Accepted, "Unavailable shared draw selection refused cancellation.");
                    CloseNativeWindow(engine, 6, engine.Step);
                    Require(engine.Scene.DrawPointInteraction is null && engine.CurrentDialogue is null && session.CharacterVitals!.GetMagicCount(0, magic) == initialCount && session.EventRandom!.DrawPoints!.Depletion.All(value => value == 0) && engine.IsUserControlEnabled && session.State.Variables[500] == 77, "Canceled shared draw changed stock/depletion or retained its wait.");
                    Require(awards.Count == 0, "Canceled shared draw published a stock award.");
                    continue;
                }
                Require(engine.Scene.DrawPointInteraction is { RandomStateBefore: 1, RandomStateAfter: 1103527590 } automatic && automatic.Selection == receiver && Phase() == 4 && engine.CurrentDialogue is null, "Shared draw did not select its eligible receiver automatically without dialogue.");
                short sharedQuantity = engine.Scene.DrawPointInteraction!.Quantity;
                Require(sharedQuantity > 2 && engine.Scene.DrawPointInteraction.EligibleCombatSlots == expectedEligible, "Shared draw lost its eligible receiver or sampled quantity.");
                StepRestored();
                Require(Phase() == 5 && engine.NativeDrawPointAcquisitionActive && engine.CurrentDialogue is null, "Shared draw did not launch acquisition without prompting.");
                long sharedLaunched = engine.Frame;
                while (engine.Scene.DrawPointInteraction is not null && engine.Frame - sharedLaunched < 80)
                {
                    Require(engine.CurrentDialogue is null, "Shared draw introduced a discovery, receiver or success dialogue.");
                    if (Phase() is 6 or 7)
                    {
                        StepRestored();
                    }
                    else
                    {
                        engine.Step();
                    }
                }
                int sharedDepletion = (session.EventRandom!.DrawPoints!.Depletion[index / 4] >> (2 * (index & 3))) & 3;
                Require(engine.Scene.DrawPointInteraction is null && engine.CurrentDialogue is null && session.CharacterVitals!.GetMagicCount(0, magic) == Math.Min(100, initialCount + sharedQuantity) && sharedDepletion == ((catalog.PointBytes[index] & 0x40) != 0 ? 2 : 3) && engine.IsUserControlEnabled && session.State.Variables[500] == 77, "Automatic shared draw lost capped stock, depletion or same-callback completion.");
                Require(session.EventRandom.State == 1103527590 && engine.Scene.GlobalSoundRequests.Count(request => request.OriginalEffectId == 67) == 1, "Automatic shared draw repeated RNG or its acquisition sound.");
                int sharedAcquired = Math.Min(sharedQuantity, 100 - initialCount);
                Require(awards.SequenceEqual([(magic, sharedAcquired, initialCount + sharedAcquired)]), "Shared draw did not publish one capped award with the shared pool total.");
                continue;
            }
            Require(engine.Scene.DrawPointInteraction is { Message: 2, RandomStateBefore: 1, RandomStateAfter: 1103527590 } issued && issued.PointIndex == index && issued.EligibleCombatSlots == expectedEligible && issued.Quantity > 2 && Phase() == 1 && engine.CurrentDialogue?.Text == catalog.FormatMessage(2, magic), "Eligible draw point lost its original message, equipped command, point index, sampled quantity or first phase.");
            short quantity = engine.Scene.DrawPointInteraction!.Quantity;
            int basis = unchecked((sbyte)catalog.PointBytes[index]) < 0 ? 20 : 10;
            int nativeQuantity = basis * (198 + 128) / 512 + 1;
            Require(quantity == nativeQuantity + bonus && engine.Scene.DrawPointInteraction.DrawBonus == bonus, "Draw bonus did not preserve the native RNG quantity and issued save-owned bonus.");
            int expectedAcquired = Math.Min(quantity, 100 - initialCount);
            if (bonus == 5)
            {
                session.SetGameplayPreferences(session.State.GameplayPreferences with { Tuning = new GameplayTuning() });
            }
            Require(engine.Scene.GlobalSoundRequests.Count(request => request.OriginalEffectId == 66) == 1 && session.EventRandom!.State == 1103527590, "Eligible draw point omitted native sound66 or drew script RNG more than once.");
            StepRestored();
            WaitForNativeWindowReady(engine, 6, engine.Step);
            Require(engine.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted, "Eligible discovery message refused ordinary acknowledgement.");
            CloseNativeWindow(engine, 6, engine.Step);
            Require(Phase() == 2 && engine.CurrentDialogue is null, "Acknowledged eligible draw skipped its separate selection preparation callback.");
            // Another window may take focus between discovery and selection.
            RestoreForeignFocus();
            StepRestored();
            if (missingPersonalReceiver)
            {
                Require(engine.CurrentDialogue is { LastChoice: 3 } && engine.CurrentDialogue.Text.Split("{Line}")[2].Contains("{Color Disabled}", StringComparison.Ordinal), "The field-only character did not retain its disabled selection row.");
                WaitForNativeWindowReady(engine, 6, engine.Step);
                Require(engine.Apply(new PlayerAction(PlayerActionKind.Choose, 2)).Accepted, "Draw point did not accept the original field-only row index.");
                CloseNativeWindow(engine, 6, engine.Step);
                Require(Phase() == 4, "Draw point lost the selected field-only index.");
                StepRestored();
                Require(Phase() == 6 && engine.Scene.DrawPointInteraction is { CombatSlot: null } && !engine.NativeDrawPointAcquisitionActive, "Field-only selection resolved to a combat owner or started particles.");
                StepRestored();
                Require(Phase() == 1 && engine.Scene.DrawPointInteraction is { Message: 6, Acquired: 0 } && awards.Count == 0 && session.CharacterVitals!.GetMagicCount(0, magic) == initialCount && session.EventRandom!.DrawPoints!.GetDepletion((byte)index) == 0, "Field-only selection failed to refuse without writing stock or depletion.");
                continue;
            }
            Require(Phase() == 3 && engine.CurrentDialogue is { FirstChoice: 1, LastChoice: 2, InitialChoice: 2, CancelChoice: 1 } selection && selection.Text == catalog.FormatSelection([0], session.CharacterVitals!, magic, expectedEligible), "Draw selection lost its original character/cancel rows or actual choice metadata.");
            Require(engine.Scene.NativeDialogueFocus == 6 && engine.Scene.NativeAaskPreviousFocus == 7, "Generated draw choice failed to focus its own window or overwrote AASK focus history.");
            // A retained choice must recover on its next native poll, including after restore.
            RestoreForeignFocus();
            StepRestored();
            Require(engine.Scene.NativeDialogueFocus == 6 && engine.CurrentDialogue?.FirstChoice == 1 && engine.Scene.NativeAaskPreviousFocus == 7, "Draw choice poll failed to recover an unfocused saved selection.");
            if (cancel)
            {
                WaitForNativeWindowReady(engine, 6, engine.Step);
                Require(engine.Apply(new PlayerAction(PlayerActionKind.Cancel)).Accepted, "Original draw selection Cancel refused.");
                CloseNativeWindow(engine, 6, engine.Step);
                Require(engine.Scene.DrawPointInteraction is null && session.CharacterVitals!.Records[0].MagicInventory[0].Count == initialCount && session.EventRandom!.DrawPoints!.Depletion.All(value => value == 0) && session.State.Variables[500] == 77 && engine.IsUserControlEnabled, "Canceled selection awarded/depleted or retained its operand/control wait.");
                Require(awards.Count == 0, "Canceled personal draw published a stock award.");
                continue;
            }
            WaitForNativeWindowReady(engine, 6, engine.Step);
            Require(engine.Apply(new PlayerAction(PlayerActionKind.Choose, 2)).Accepted, "Explicit character draw selection refused.");
            CloseNativeWindow(engine, 6, engine.Step);
            Require(Phase() == 4, "Character choice skipped original phase4 body resolution.");
            StepRestored();
            Require(Phase() == 5 && engine.NativeDrawPointAcquisitionActive && engine.Scene.GlobalSoundRequests.Count(request => request.OriginalEffectId == 67) == 1, "Eligible selection skipped actual native particles or native sound67.");
            long launched = engine.Frame;
            StepRestored();
            while (Phase() == 5 && engine.Frame - launched < 64)
            {
                engine.Step();
            }
            Require(Phase() == 6 && !engine.NativeDrawPointAcquisitionActive && engine.Frame - launched >= 38 && session.CharacterVitals!.Records[0].MagicInventory[0].Count == initialCount, "Acquisition did not wait for all eight native spark records before stock writes.");
            Require(awards.Count == 0, "Personal draw published an award before updating inventory.");
            StepRestored();
            Require(session.EventRandom!.State == 1103527590 && engine.Scene.DrawPointInteraction!.Quantity == quantity, "Draw acquisition or wait resampled script RNG/quantity.");
            if (full)
            {
                Require(Phase() == 1 && engine.Scene.DrawPointInteraction is { Message: 6, Acquired: 0 } && session.MessageVariables!.At(7) == 0 && session.CharacterVitals!.Records[0].MagicInventory[0].Count == 100, "Full native inventory changed stock or failed to return to phase1 after message6.");
                WaitForNativeWindowReady(engine, 6, engine.Step);
                Require(engine.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted, "Failed acquisition message refused acknowledgement.");
                CloseNativeWindow(engine, 6, engine.Step);
                StepRestored();
                Require(Phase() == 3 && engine.Scene.DrawPointInteraction!.Quantity == quantity && session.EventRandom.State == 1103527590, "Failed acquisition retried discovery/RNG instead of original selection.");
                WaitForNativeWindowReady(engine, 6, engine.Step);
                Require(engine.Apply(new PlayerAction(PlayerActionKind.Cancel)).Accepted, "Retried draw selection Cancel refused.");
                CloseNativeWindow(engine, 6, engine.Step);
                Require(engine.Scene.DrawPointInteraction is null && session.EventRandom.DrawPoints!.Depletion.All(value => value == 0), "Failed draw/cancel depleted the original packed point.");
            }
            else
            {
                Require(Phase() == 7 && engine.Scene.DrawPointInteraction is { Message: 5 } acquired && acquired.Acquired == expectedAcquired && session.MessageVariables!.At(7) == expectedAcquired && session.CharacterVitals!.Records[0].MagicInventory[0] == new NativeMagicStock((byte)magic, checked((byte)(initialCount + expectedAcquired))), "Successful native acquisition did not stop at100 and publish the actual accepted count to message variable7.");
                StepRestored();
                Require(engine.CurrentDialogue!.Text.Contains(expectedAcquired.ToString(), StringComparison.Ordinal), "Successful draw text did not expand canonical message variable7.");
                Require(session.EventRandom.DrawPoints!.Depletion.All(value => value == 0), "Successful draw depleted before final message acknowledgement.");
                WaitForNativeWindowReady(engine, 6, engine.Step);
                Require(engine.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted, "Successful acquisition message refused acknowledgement.");
                CloseNativeWindow(engine, 6, engine.Step);
                int depletion = (session.EventRandom.DrawPoints.Depletion[index / 4] >> (2 * (index & 3))) & 3;
                Require(engine.Scene.DrawPointInteraction is null && depletion == ((catalog.PointBytes[index] & 0x40) != 0 ? 2 : 3) && engine.CurrentDialogue is null && session.State.Variables[500] == 77 && engine.IsUserControlEnabled, "Acknowledged successful draw lost native depletion2/3, operand completion or same-callback successor.");
                JsonElement owner = engine.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1);
                Require(owner.GetProperty("Results").GetProperty("0").GetInt32() == 53 && owner.GetProperty("Results").GetProperty("3").GetInt32() == 53, "Successful draw invented result0=1 instead of preserving the issuing result register.");
            }
            Require(full ? awards.Count == 0 : awards.SequenceEqual([(magic, expectedAcquired, initialCount + expectedAcquired)]), "Personal draw published an incorrect or duplicate stock award.");
        }
        Console.WriteLine("Eligible DRAWPOINT: original discovery/selection, explicit Cancel, actual spark completion, full-stock retry, accepted-unit cap/message variable7, depletion2/3, shared automatic acquisition/full-pool cancellation, capacity presentation and independent restore at phases1..7 passed.");
    }
}
