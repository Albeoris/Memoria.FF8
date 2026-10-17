using System.Text.Json;
using System.Text.Json.Nodes;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeBattleFieldReturnContract()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? throw new InvalidOperationException("Set MEMORIA_FF8_GAME_DIR to the installed game directory.");
        NativeBattleAbandonmentContract();
        NativeRandomFieldReturnContract(directory);
        NativeBattleFieldRegistrationContract(directory);
        NativeBattleMusicEntryResources resources = new NativeMusicCatalog(directory).ReadBattleEntryResources();
        GameSession session = CreateNativeMusicSession();
        byte[] content = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.BATTLEMODE, 0), (Jsm.Opcode.PSHN_L, 94), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.BATTLE, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_W, 800), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
        EventEngine engine = new(session) { UseInteractiveBattle = (_, _) => true, ValidateInteractiveBattle = (_, _) => null };
        engine.LoadField(74, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        while (engine.PendingInteractiveBattle is null && engine.Frame < 4)
        {
            engine.Step();
        }
        NativeBattleCompletion battle = engine.PendingInteractiveBattle ?? throw new InvalidOperationException("Return protocol fixture did not suspend BATTLE.");
        string before = engine.Capture().GetRawText();
        try
        {
            engine.RequireNativeBattleFieldReload(4);
            throw new InvalidOperationException("Paused VM was treated as actual kind3 resource completion.");
        }
        catch (NotSupportedException exception) when (exception.Message.Contains("actual471F70", StringComparison.Ordinal))
        {
        }
        Require(engine.Capture().GetRawText() == before && !session.State.Variables.ContainsKey(800), "Missing actual kind3 producer changed the suspended VM.");
        PreparedNativeBattleMusicEntry entry = engine.PrepareNativeBattleMusicEntry(94, 0, resources);
        engine.CommitNativeBattleMusicEntry(entry, candidate => session.SetGlobalSoundProcess(candidate.SoundProcess));
        JsonNode declared = JsonNode.Parse(engine.Capture().GetRawText())!;
        // Controlled post-loader protocol component only: no claim that the unimplemented live kind3 loader executed.
        declared["Scene"]!["NativeBattleFieldReload"] = JsonSerializer.SerializeToNode(new NativeBattleFieldReloadCompletion(1, NativeFieldRuntimeProfile.FF8EnExecutableHash, 74, declared["ProgramHash"]!.GetValue<string>(), battle.Frame, 4));
        declared["Scene"]!["MusicChannel108"] = JsonSerializer.SerializeToNode(new NativeMusicChannelState(-1, 7));
        engine.Restore(JsonSerializer.SerializeToElement(declared));
        int output = 0;
        engine.AudioCommandExecuted += command =>
        {
            if (command is NativeBattleMusicReturnReceipt)
            {
                output++;
            }
        };
        EventEngine.PreparedNativeBattleFieldReturn prepared = engine.PrepareNativeBattleFieldReturn(entry.Native.MusicProcess, 0, 4, resources);
        engine.CommitNativeBattleFieldReturn(prepared);
        Require(output == 1 && engine.Scene.MusicChannel108 is { ChannelId: 0, MusicId: 7 } && engine.Scene.NativeBattleFieldReload is null && ReferenceEquals(engine.Scene.MusicProcess, session.State.NativeBattleMemory!.MusicProcess!.Buffers) && !session.State.Variables.ContainsKey(800), "Prepared return lost atomic root/Scene ownership,199, or ran VM before publication.");
        JsonElement paused = engine.Capture();
        GameSession independent = CloneMusicSession(session.State);
        EventEngine clone = new(independent) { UseInteractiveBattle = (_, _) => true, ValidateInteractiveBattle = (_, _) => null };
        clone.LoadField(74, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        clone.Restore(paused);
        Require(JsonElement.DeepEquals(paused, clone.Capture()), "Committed field-return protocol did not preserve its exact independent snapshot.");
        engine.ReturnFromInteractiveBattle(0, nativeResult: 4);
        clone.ReturnFromInteractiveBattle(0, nativeResult: 4);
        Require(session.State.Variables[800] == 77 && JsonElement.DeepEquals(engine.Capture(), clone.Capture()) && output == 1, "VM did not resume after committed return or restore replayed output.");

        GameSession resettingSession = CreateNativeMusicSession();
        EventEngine resetting = new(resettingSession) { UseInteractiveBattle = (_, _) => true, ValidateInteractiveBattle = (_, _) => null };
        resetting.LoadField(74, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        while (resetting.PendingInteractiveBattle is null && resetting.Frame < 4)
        {
            resetting.Step();
        }
        JsonElement beforeReset = resetting.Capture();
        // Explicit synthetic ONE input for this reset-only protocol. Actual installed members are checked in Assets.
        byte[] emptyOne = new byte[4];
        NativeFieldRegistrationResources resetSource = new(74, "controlled-kind3-reset", NativeFieldRuntimeProfile.FF8EnExecutableHash, beforeReset.GetProperty("ProgramHash").GetString()!, new("controlled/chara.one", Convert.ToHexString(SHA256.HashData(emptyOne)), emptyOne.ToImmutableArray()), [], []);
        NativeBattleFieldReloadState reset = resetting.ExecuteNativeBattleFieldCacheReset(resetSource, 4);
        Require(reset.Stage == NativeBattleFieldReloadStage.ModelCachesReset && resetting.Scene.NativeBattleFieldReload is null && resetting.Scene.ModelParts is { Resources.Count: 0, Instances.Length: 0, Draws.Count: 0, CacheReset: not null }, "Actual5310F0 did not clear registrations or fabricated completed kind3 reload.");
        JsonElement resetSaved = resetting.Capture();
        Require(JsonElement.DeepEquals(beforeReset.GetProperty("Fibers"), resetSaved.GetProperty("Fibers")) && JsonElement.DeepEquals(beforeReset.GetProperty("Actors"), resetSaved.GetProperty("Actors")) && resetting.Frame == beforeReset.GetProperty("Frame").GetInt64(), "Cache-reset stage reset paused PCs, stacks, actors or animation phase, or invented a callback.");
        GameSession resetIndependent = CloneMusicSession(resettingSession.State);
        EventEngine resetClone = new(resetIndependent) { UseInteractiveBattle = (_, _) => true, ValidateInteractiveBattle = (_, _) => null };
        resetClone.LoadField(74, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        resetClone.Restore(resetSaved);
        Require(JsonElement.DeepEquals(resetSaved, resetClone.Capture()), "Actual pending5310F0 stage did not survive an independent full-root restore.");
        foreach (EventEngine pending in new[] { resetting, resetClone })
        {
            try
            {
                pending.ExecuteNativeBattleFieldCacheReset(resetSource, 4);
                throw new InvalidOperationException("Repeated reset replaced the unsupported registration with another cold cache.");
            }
            catch (NotSupportedException exception) when (exception.Message.Contains("532A40", StringComparison.Ordinal))
            {
            }
            Require(JsonElement.DeepEquals(resetSaved, pending.Capture()), "Unsupported registration advanced or changed the exact pending state.");
        }
        JsonNode malformed = JsonNode.Parse(resetSaved.GetRawText())!;
        malformed["Scene"]!["ModelParts"]!.AsObject().Remove("CacheReset");
        try
        {
            resetClone.Restore(JsonSerializer.SerializeToElement(malformed));
            throw new InvalidOperationException("Pending5310F0 accepted unknown reset memory.");
        }
        catch (InvalidDataException)
        {
        }
        Require(JsonElement.DeepEquals(resetSaved, resetClone.Capture()), "Rejected missing reset owner changed the VM.");
    }

    private static void NativeRandomFieldReturnContract(string directory)
    {
        using GameAssets assets = new(directory);
        FieldAssets field = assets.LoadField(74);
        NativeBattleMusicEntryResources music = new NativeMusicCatalog(directory).ReadBattleEntryResources();
        byte[] content = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 16), (Jsm.Opcode.BATTLEMODE, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
        string hash = Convert.ToHexString(SHA256.HashData(content));
        NativeFieldRegistrationResources source = field.NativeRegistration! with { ProgramHash = hash, Loader = field.NativeRegistration!.Loader! with { Program = new("controlled/random-return.jsm", hash, content.ToImmutableArray()) } };
        bool pending = false;
        EventEngine Create(GameSession owner)
        {
            EventEngine engine = new(owner) { HasPendingRandomBattle = () => pending, AnimationFrameCount = field.GetAnimationFrameCount, ReadNativeModelParts = model => field.Models[model].NativeParts, ReadNativeEyes = model => field.Models[model].NativeEyes };
            engine.LoadField(74, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        GameSession session = CreateNativeMusicSession();
        EventEngine live = Create(session);
        for (int frame = 0; frame < 8 && live.Scene.NativeBattleFlagWrites?.LastOrDefault()?.ActiveFlags != 16; frame++)
        {
            live.Step();
        }
        pending = true;
        RandomEncounterRequest request = new(SceneKind.Field, 74, live.Frame, 94, hash);
        session.SetRandomEncounters(new(true, NativeRandomEncounterState.FromColdProcess(), request));
        Require(live.BeginNativeRandomFieldBattle(request) == 16 && live.PendingHostedBattle is null, "Random entry changed retained flags or fabricated a BATTLE owner.");
        PreparedNativeBattleMusicEntry entry = live.PrepareNativeBattleMusicEntry(94, 16, music);
        live.CommitNativeBattleMusicEntry(entry, native => session.SetGlobalSoundProcess(native.SoundProcess));
        live.AdvanceRandomBattleCallbacks(1);
        JsonElement suspended = live.Capture();
        GameSession independent = CloneMusicSession(session.State);
        EventEngine clone = Create(independent);
        clone.Restore(suspended);
        Require(JsonElement.DeepEquals(suspended, clone.Capture()), "Random owner/callback count did not survive independent Events restoration.");
        foreach ((EventEngine engine, GameSession owner) in new[] { (live, session), (clone, independent) })
        {
            NativeBattleFieldResourceReloadResult reload = engine.ExecuteNativeBattleFieldResourceReload(source, 4, owner.State.NativeBattleMemory!, owner.State.CrtRandom);
            engine.ValidateNativeBattleFieldRegistrationMemory(reload.Memory);
            EventEngine.PreparedNativeBattleFieldReturn prepared = engine.PrepareNativeBattleFieldReturn(entry.Native.MusicProcess, 16, 4, music, reload.Memory, reload.CrtRandomBefore, reload.CrtRandomAfter);
            Require(prepared.Owners.Music.Receipt.KeptMusic, "Random field return lost the retained keep-music flag.");
            engine.ReturnFromNativeRandomFieldBattle(1, 4, () => engine.CommitNativeBattleFieldReturn(prepared));
            owner.SetRandomEncounters(owner.State.RandomEncounters! with { Pending = null });
            Require(engine.Scene.NativeRandomFieldBattle is { Result: 4, Callbacks: 1 } && engine.Scene.NativeBattleCompletions is not { Count: > 0 } && JsonElement.DeepEquals(suspended.GetProperty("Fibers"), engine.Capture().GetProperty("Fibers")) && engine.Frame == suspended.GetProperty("Frame").GetInt64(), "Random return changed suspended fibers/frame or fabricated a scripted completion.");
            string returned = engine.Capture().GetRawText();
            try
            {
                engine.ReturnFromNativeRandomFieldBattle(1, 4, () => throw new InvalidOperationException("Duplicate outcome callback ran."));
                throw new InvalidOperationException("Random return accepted duplicate completion.");
            }
            catch (InvalidOperationException exception) when (exception.Message == "No random encounter owns the suspended field.")
            {
            }
            Require(returned == engine.Capture().GetRawText(), "Duplicate random return refusal changed the field.");
        }
        pending = false;
        Require(JsonElement.DeepEquals(live.Capture(), clone.Capture()) && JsonSerializer.Serialize(session.State.NativeBattleMemory) == JsonSerializer.Serialize(independent.State.NativeBattleMemory), "Random field atomic return diverged across independent roots.");
    }

    private static void NativeBattleFieldRegistrationContract(string directory)
    {
        using GameAssets assets = new(directory);
        FieldAssets field = assets.LoadField(74);
        // A controlled retained BATTLE program uses original model/PCB/placement inputs. It does not replay an original field route.
        byte[] content = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.BATTLEMODE, 0), (Jsm.Opcode.PSHN_L, 94), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.BATTLE, 0), (Jsm.Opcode.POPM_W, 800), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
        string hash = Convert.ToHexString(SHA256.HashData(content));
        NativeFieldRegistrationResources source = field.NativeRegistration! with
        {
            ProgramHash = hash,
            Loader = field.NativeRegistration!.Loader! with { Program = new("controlled/retained.jsm", hash, content.ToImmutableArray()) }
        };
        foreach (byte result in new byte[] { 2, 4 })
        {
            GameSession session = CreateNativeMusicSession();
            EventEngine Create(GameSession owner)
            {
                EventEngine engine = new(owner)
                {
                    UseInteractiveBattle = (_, _) => true,
                    ValidateInteractiveBattle = (_, _) => null,
                    AnimationFrameCount = field.GetAnimationFrameCount,
                    ReadNativeModelParts = model => field.Models[model].NativeParts,
                    ReadNativeEyes = model => field.Models[model].NativeEyes
                };
                engine.LoadField(74, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                return engine;
            }
            EventEngine live = Create(session);
            for (int frame = 0; frame < 8 && live.PendingInteractiveBattle is null; frame++)
            {
                live.Step();
            }
            Require(live.PendingInteractiveBattle is not null, "Registration fixture never suspended its actual BATTLE fiber.");
            JsonElement suspended = live.Capture();
            EventEngine restored = Create(CloneMusicSession(session.State));
            restored.Restore(suspended);
            NativeBattleRetainedMemory memory = assets.ReadColdProcessNativeBattleMemory();
            NativeBattleFieldResourceReloadResult first = live.ExecuteNativeBattleFieldResourceReload(source, result, memory);
            NativeBattleFieldResourceReloadResult second = restored.ExecuteNativeBattleFieldResourceReload(source, result, JsonSerializer.Deserialize<NativeBattleRetainedMemory>(JsonSerializer.Serialize(memory))!);
            Require(JsonSerializer.Serialize(first) == JsonSerializer.Serialize(second) && JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Actual kind3 registration diverged across independent suspended roots.");
            live.ValidateNativeBattleFieldRegistrationMemory(first.Memory);
            Require(live.RequireNativeBattleFieldReload(result) == live.Scene.NativeBattleFieldReload && live.Scene.NativeFieldRegistration is not null && live.Scene.NativeBattleFieldReloadState is null, "Executed registration did not own its physical completion.");
            Require(JsonElement.DeepEquals(suspended.GetProperty("Fibers"), live.Capture().GetProperty("Fibers")) && live.Frame == suspended.GetProperty("Frame").GetInt64() && !session.State.Variables.ContainsKey(800), "Kind3 reset retained PCs, stacks, waits or advanced the suspended script.");
            string completed = live.Capture().GetRawText();
            try
            {
                live.ExecuteNativeBattleFieldResourceReload(source, result, memory);
                throw new InvalidOperationException("Completed registration accepted stale pre-reload physical memory.");
            }
            catch (NotSupportedException)
            {
            }
            Require(completed == live.Capture().GetRawText(), "Repeated registration refusal mutated a completed Scene.");
        }
    }
}
