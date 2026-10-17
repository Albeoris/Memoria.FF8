using System.Security.Cryptography;
using System.Text.Json;
using System.Collections.Immutable;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeMenuBattleContract(string directory)
    {
        using GameAssets assets = new(directory);
        FieldAssets field = assets.LoadField(74);
        NativeBattleMusicEntryResources music = new NativeMusicCatalog(directory).ReadBattleEntryResources();
        byte[] content = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 16), (Jsm.Opcode.BATTLEMODE, 0), (Jsm.Opcode.MENUNORMAL, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
        string hash = Convert.ToHexString(SHA256.HashData(content));
        NativeFieldRegistrationResources source = field.NativeRegistration! with { ProgramHash = hash, Loader = field.NativeRegistration!.Loader! with { Program = new("controlled/menu-return.jsm", hash, content.ToImmutableArray()) } };
        EventEngine Create(GameSession session)
        {
            EventEngine engine = new(session) { AnimationFrameCount = field.GetAnimationFrameCount, ReadNativeModelParts = model => field.Models[model].NativeParts, ReadNativeEyes = model => field.Models[model].NativeEyes };
            engine.LoadField(74, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        foreach (byte result in new byte[] { 2, 4 })
        {
            GameSession session = CreateNativeMusicSession();
            session.InitializeNewGameShotAvailability(assets.ReadNewGameShotAvailability());
            EventEngine engine = Create(session);
            while (engine.PartyMenu is null && engine.Frame < 8)
            {
                engine.Step();
            }
            engine.Apply(new PlayerAction(PlayerActionKind.Wait));
            engine.Apply(new PlayerAction(PlayerActionKind.Wait));
            engine.BeginNativeMenuBattle(168, 811, SceneKind.Field, engine.Frame, hash, 16);
            JsonElement fibers = engine.Capture().GetProperty("Fibers").Clone();
            Require(engine.PartyMenu is null && engine.PendingMenuBattle is { Formation: 811, Flags: 16, Callbacks: 0 } && engine.PendingInteractiveBattle is null && engine.Scene.NativeRandomFieldBattle is null, "Menu entry fabricated a script or random encounter.");
            PreparedNativeBattleMusicEntry entry = engine.PrepareNativeMenuBattleMusicEntry(811, 16, music);
            engine.CommitNativeBattleMusicEntry(entry, native => session.SetGlobalSoundProcess(native.SoundProcess));
            engine.AdvanceMenuBattleCallbacks(2);
            JsonElement pending = engine.Capture();
            EventEngine clone = Create(CloneMusicSession(session.State));
            clone.Restore(pending);
            Require(JsonElement.DeepEquals(pending, clone.Capture()), "Menu battle lost its exact standalone owner or callback clock.");
            NativeBattleFieldResourceReloadResult reload = engine.ExecuteNativeBattleFieldResourceReload(source, result, session.State.NativeBattleMemory!, session.State.CrtRandom);
            EventEngine.PreparedNativeBattleFieldReturn prepared = engine.PrepareNativeBattleFieldReturn(entry.Native.MusicProcess, 16, result, music, reload.Memory, reload.CrtRandomBefore, reload.CrtRandomAfter);
            engine.ReturnFromNativeMenuBattle(2, result, () => engine.CommitNativeBattleFieldReturn(prepared));
            Require(engine.PartyMenu is null && engine.PendingMenuBattle is null && engine.Scene.NativeBattleResult == result && engine.Scene.NativeBattleFlagWrites!.Last().Writer == NativeBattleFlagWriter.MenuFieldBattleReturn && JsonElement.DeepEquals(fibers, engine.Capture().GetProperty("Fibers")), "Menu return lost result/flag ownership or resumed a fabricated BATTLE fiber.");
            try
            {
                engine.ReturnFromNativeMenuBattle(2, result, () => throw new InvalidOperationException("duplicate publication"));
                throw new InvalidOperationException("Duplicate menu return was accepted.");
            }
            catch (InvalidOperationException exception) when (exception.Message == "No native menu battle owns physical scene return.")
            {
            }
        }
    }
}
