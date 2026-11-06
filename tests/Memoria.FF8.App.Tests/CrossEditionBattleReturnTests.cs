using System.Security.Cryptography;
using System.Collections.Immutable;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossEditionBattleReturnTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Native_defeat_branch_preserves_its_real_caller_and_selects_the_next_allocation(bool resume)
    {
        string directory = TestResources.GameDirectory;
        string? targetDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_CROSS_EDITION_GAME_DIR");
        Assert.SkipUnless(targetDirectory is not null && Directory.Exists(targetDirectory), "Configure MEMORIA_FF8_CROSS_EDITION_GAME_DIR for the other installed edition.");
        using GameAssets original = new(directory);
        FieldAssets installed = original.LoadField(165);
        byte[] script = GameplaySaveFieldScriptsTests.Encode([1],
        [
            [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.FADENONE, 0), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, resume ? 512 : 0), (Jsm.Opcode.BATTLEMODE, 0), (Jsm.Opcode.PSHN_L, 94), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.BATTLE, 0), (Jsm.Opcode.PSHN_L, 13), (Jsm.Opcode.ADDGIL, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]
        ]);
        using GameAssets sourceAssets = new(directory);
        SelectScript(sourceAssets, installed.Name, script);
        NativeBattleExecutionStack execution = new(0x240100, 0x569C3D, NativeFieldRuntimeProfile.FF8EnExecutableHash, new string('C', 64), 1, 2);
        NativeBattleHeroCrtStackContract crt = new(NativeBattleHeroCrtStackContract.Win32X86Profile, execution.CaptureSha256, execution.CapturedThreadId);
        NativeBattleEffectMemory environment = NativeBattleEffectMemory.FromColdProcess() with { ExecutionStack = execution, HeroCrtStack = crt };
        GameSession sourceSession = new(GameplaySaveSessionLoader.Restore(GameplaySaveSessionLoaderTests.Document(sourceAssets, false), directory, sharedAssets: sourceAssets, executionEnvironment: environment));
        sourceSession.SetFullBattlePlaythrough(true);
        using GameWorld source = new(sourceSession, directory, sharedAssets: sourceAssets);
        source.EnsureField();
        for (int callback = 0; callback < 4 && !source.HasPendingBattle; callback++)
        {
            Assert.True(source.Apply(new(PlayerActionKind.Wait)).Accepted);
        }
        Assert.Null(source.Events.LastFault);
        Assert.Equal(resume, source.Events.ReadNativeDefeatReturnBranch4706B0().ResumePreviousScene);
        source.ApplyBattleInput(new(BattleInputKind.StartEncounter, NativeSeed: 0));
        for (int callback = 0; callback < 600 && !source.Battle!.NativeEntryReady; callback++)
        {
            source.ApplyBattleInput(new(BattleInputKind.Tick));
        }
        Assert.True(source.Battle!.NativeEntryReady);
        GameState pending = sourceSession.PrepareSnapshot();
        BattleSnapshot entry = pending.Battle!;
        BattleSnapshot defeated = entry with
        {
            Combatants = entry.Combatants.Select(actor => entry.Definition.Combatants.Any(definition => definition.Id == actor.Id && definition.IsPlayer) ? actor with { CurrentHp = 0, NativeStatus = actor.NativeStatus! with { Saved = (ushort)(actor.NativeStatus.Saved | 1) } } : actor).ToArray(),
            NativePartyPresentation = entry.NativePartyPresentation! with { Rows = entry.NativePartyPresentation.Rows.Select(row => row is null ? null : row with { HitPoints = 0, DisplayedHitPointsQ12 = 0 }).ToImmutableArray() }
        };
        pending = pending with { Battle = defeated };
        GameSession physicalSession = new(pending);
        using GameWorld physicalSource = new(physicalSession, directory, sharedAssets: sourceAssets);
        physicalSource.EnsureField();
        using GameAssets targetOriginal = new(targetDirectory!);
        using GameAssets targetAssets = new(targetDirectory!);
        SelectScript(targetAssets, targetOriginal.LoadField(165).Name, script);
        Assert.Equal(script, targetAssets.LoadField(165).Jsm);
        GameSession targetSession = new(pending);
        using GameWorld target = new(targetSession, targetDirectory!, sharedAssets: targetAssets);
        target.EnsureField();
        GameState live = targetSession.State;
        Assert.Throws<InvalidOperationException>(() => targetSession.ReconstructCompletedBattleRuntime(live.PendingBattleEncounter!, targetAssets.ReadColdProcessNativeBattleMemory(), targetAssets.ReadNewGameCharacterVitals(), targetAssets.ReadNewGameShotAvailability()));
        Assert.Same(live, targetSession.State);
        for (int callback = 0; callback < 600 && (physicalSource.Battle is not null || target.Battle is not null); callback++)
        {
            if (physicalSource.Battle is not null)
            {
                physicalSource.ApplyBattleInput(new(BattleInputKind.Tick));
            }
            if (target.Battle is not null)
            {
                target.ApplyBattleInput(new(BattleInputKind.Tick));
            }
        }
        Assert.True(physicalSource.Battle is null, $"Source defeat remains: outcome={physicalSource.Battle?.Outcome}, result={physicalSource.Battle?.NativeResult}, frame={physicalSource.Battle?.Frame}, callbacks={physicalSource.Battle?.NativeHostCallbacks}, entry={physicalSource.Battle?.NativeEntryReady}, fade={physicalSource.Battle?.NativeGraphics?.NativeOutcome?.FadeDurationB0C}, nativeResult={physicalSource.Battle?.NativeGraphics?.NativeOutcome?.ResultByte6E7}.");
        Assert.True(target.Battle is null, $"Target defeat remains: outcome={target.Battle?.Outcome}, result={target.Battle?.NativeResult}, frame={target.Battle?.Frame}, callbacks={target.Battle?.NativeHostCallbacks}, entry={target.Battle?.NativeEntryReady}, fade={target.Battle?.NativeGraphics?.NativeOutcome?.FadeDurationB0C}, nativeResult={target.Battle?.NativeGraphics?.NativeOutcome?.ResultByte6E7}.");
        Assert.Null(target.Events.LastFault);
        Assert.Equal(resume ? 165 : 75, targetSession.State.FieldId);
        GameState physical = physicalSession.PrepareSnapshot();
        GameState projected = targetSession.PrepareSnapshot();
        Assert.Equal(execution, physical.NativeBattleMemory!.EffectMemory!.ExecutionStack);
        Assert.Equal(crt, physical.NativeBattleMemory.EffectMemory.HeroCrtStack);
        Assert.Equal(execution, projected.NativeBattleMemory!.EffectMemory!.ExecutionStack);
        Assert.Equal(crt, projected.NativeBattleMemory.EffectMemory.HeroCrtStack);
        Assert.Equal(physical.GlobalSoundProcess!.MusicPlayback!.BattleFirstLoadB8B7D4, projected.GlobalSoundProcess!.MusicPlayback!.BattleFirstLoadB8B7D4);
        Assert.True(Same(physical.ExamTimer, projected.ExamTimer));
        Assert.Equal(physical.CrtRandom, projected.CrtRandom);
        Assert.True(Same(physical.Economy, projected.Economy));
        Assert.True(Same(GameplaySaveCharacters.Capture(physical.CharacterVitals!), GameplaySaveCharacters.Capture(projected.CharacterVitals!)));
        Assert.Equal(physical.AppliedBattleResultIds, projected.AppliedBattleResultIds);
        Assert.Equal(physical.ShotAvailability?.Flags770, projected.ShotAvailability?.Flags770);
        Assert.Equal(targetAssets.NativeExecutableHash, target.Events.NativeRuntimeProfile?.ExecutableHash);
        Assert.Equal(targetAssets.NativeExecutableHash, projected.NativeBattleMemory?.ModelCpuResources?.Cpu.EffectiveLayout.ExecutableHash);
        Assert.Equal(targetAssets.ReadNewGameCharacterVitals().KernelSha256, projected.CharacterVitals?.KernelSha256);
        Assert.Equal(targetAssets.NativeExecutableHash, target.CreateEncounterBattle(94).NativeGraphics?.Rules.ExecutableSha256);
        if (resume)
        {
            Assert.Equal(pending.Economy!.Gil + 13, projected.Economy!.Gil);
        }
        else
        {
            Assert.Equal(pending.Economy!.Gil, projected.Economy!.Gil);
            Assert.Single(projected.AbandonedNativeBattles!);
        }
        GameSession coldSession = new(projected);
        using GameWorld cold = new(coldSession, targetDirectory!, sharedAssets: targetAssets);
        cold.EnsureField();
        Assert.Equal(target.Events.Frame, cold.Events.Frame);
        Assert.Equal(projected.AppliedBattleResultIds, coldSession.State.AppliedBattleResultIds);
    }

    [Fact]
    public void Installed_menu_battle_returns_to_selected_allocation_without_consuming_another_item()
    {
        string sourceDirectory = TestResources.GameDirectory;
        string? targetDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_CROSS_EDITION_GAME_DIR");
        Assert.SkipUnless(targetDirectory is not null && Directory.Exists(targetDirectory), "Configure MEMORIA_FF8_CROSS_EDITION_GAME_DIR for the other installed edition.");
        string path = TestResources.FixturePath("menu-battle-field-entry");
        byte[] hash = Hash(path);
        using GameAssets sourceAssets = new(sourceDirectory);
        GameSession sourceSession = new(PlayerStateLoader.Load(path, sourceDirectory, sharedAssets: sourceAssets));
        using GameWorld source = new(sourceSession, sourceDirectory, sharedAssets: sourceAssets);
        source.EnsureField();
        Assert.True(source.Apply(new(PlayerActionKind.UseFieldItem, 168, Quantity: 0)).Accepted);
        Assert.NotNull(source.Events.PendingMenuBattle);
        source.ApplyBattleInput(new(BattleInputKind.StartEncounter, NativeSeed: 0));
        GameState pending = sourceSession.PrepareSnapshot();
        Assert.NotNull(pending.Battle);
        using GameAssets targetAssets = new(targetDirectory!);
        GameSession targetSession = new(pending);
        using GameWorld target = new(targetSession, targetDirectory!, sharedAssets: targetAssets);
        target.EnsureField();
        BattleEncounterOccurrence occurrence = targetSession.State.PendingBattleEncounter!;
        source.ApplyBattleInput(new(BattleInputKind.InstantVictory));
        target.ApplyBattleInput(new(BattleInputKind.InstantVictory));
        Assert.True(source.Battle!.CanReturnToField);
        Assert.True(target.Battle!.CanReturnToField);
        source.ApplyBattleInput(new(BattleInputKind.Return));
        try
        {
            target.ApplyBattleInput(new(BattleInputKind.Return));
        }
        catch (InvalidDataException error)
        {
            NativeSavedWorldFootReturn? foot = pending.SavedWorld?.FootReturn;
            NativeSavedWorldScriptedReturn? scripted = pending.SavedWorld?.ScriptedReturn;
            throw new InvalidDataException($"Menu return: dormant foot mode={foot?.WorldMode}, display={foot?.MapDisplayMode110}, channels={foot?.Channel106}/{foot?.Channel107}, yaw={foot?.CameraYaw96}; car={pending.SavedWorld?.CarReturn is not null}, garden={pending.SavedWorld?.GardenReturn is not null}, ship={pending.SavedWorld?.RagnarokReturn is not null}; scripted display={scripted?.MapDisplayMode110}, channels={scripted?.Channel106}/{scripted?.Channel107}, yaw={scripted?.CameraYaw96}, mode={scripted?.Packet.Mode}.", error);
        }
        Assert.Null(target.Battle);
        Assert.Null(target.Events.PendingMenuBattle);
        Assert.Null(targetSession.State.PendingBattleEncounter);
        Assert.Equal(source.Events.Frame, target.Events.Frame);
        GameState physical = sourceSession.PrepareSnapshot();
        GameState projected = targetSession.PrepareSnapshot();
        Assert.True(Same(physical.ExamTimer, projected.ExamTimer));
        Assert.Equal(physical.CrtRandom, projected.CrtRandom);
        Assert.True(Same(physical.Economy, projected.Economy));
        Assert.True(Same(GameplaySaveCharacters.Capture(physical.CharacterVitals!), GameplaySaveCharacters.Capture(projected.CharacterVitals!)));
        Assert.Equal(physical.AppliedBattleResultIds, projected.AppliedBattleResultIds);
        Assert.Contains(occurrence.OccurrenceId, projected.AppliedBattleResultIds);
        Assert.Equal(targetAssets.NativeExecutableHash, target.Events.NativeRuntimeProfile?.ExecutableHash);
        Assert.Equal(targetAssets.NativeExecutableHash, projected.NativeBattleMemory?.ModelCpuResources?.Cpu.EffectiveLayout.ExecutableHash);
        Assert.Equal(targetAssets.ReadNewGameCharacterVitals().KernelSha256, projected.CharacterVitals?.KernelSha256);
        Assert.Equal(targetAssets.NativeExecutableHash, target.CreateEncounterBattle(occurrence.Formation).NativeGraphics?.Rules.ExecutableSha256);
        GameSession coldSession = new(projected);
        using GameWorld cold = new(coldSession, targetDirectory!, sharedAssets: targetAssets);
        cold.EnsureField();
        Assert.Equal(target.Events.Frame, cold.Events.Frame);
        Assert.Throws<InvalidOperationException>(() => target.ApplyBattleInput(new(BattleInputKind.Return)));
        Assert.True(Same(projected.Economy, targetSession.State.Economy));
        Assert.Equal(hash, Hash(path));
    }

    private static bool Same<T>(T expected, T actual) => JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expected), JsonSerializer.SerializeToElement(actual));

    private static byte[] Hash(string path)
    {
        using FileStream source = File.OpenRead(path);
        return SHA256.HashData(source);
    }

    private static void SelectScript(GameAssets assets, string fieldName, byte[] script)
    {
        assets.Resources.Prepare(null, encodedResources: [new EncodedResource(new ResourceKey("field/" + fieldName + "/" + fieldName + ".jsm"), "jsm", 1, "test", script)]).Commit();
    }
}
