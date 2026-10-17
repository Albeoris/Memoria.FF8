using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private void RecordNativeWorldBattleDeparture(RandomEncounterRequest request)
    {
        WorldMapScene scene = worldScene ?? throw new NotSupportedException("World combat requires its current original world resource owner.");
        WorldState world = scene.State;
        if (world.NativeFootRuntime is not { EntityType: 0 or 1 or 2 } || world.NativeScriptedRuntime is not null || world.NativeTrainRuntime is not null || request.SourceHash != scene.AllocationProgramSha256)
        {
            throw new NotSupportedException("World combat departure requires the implemented normal foot resource and saved-pose contract.");
        }
        NativeSavedWorldFootReturn receipt = NativeSavedWorldFootReturn.RecordBattleDeparture(world, checked((ushort)session.State.FieldId), WorldEntryVariables.Read(session.State).StoryWord, scene.PlaceId);
        session.RecordNativeWorldBattleDeparture(request, receipt);
    }

    private Action PrepareNativeWorldBattleReturn(BattleSession active, BattleEncounterOccurrence occurrence)
    {
        WorldMapScene previous = worldScene ?? throw new NotSupportedException("World combat return has no original exploration resource owner.");
        GameState departure = session.State;
        NativeSavedWorldState saved = departure.SavedWorld ?? throw new NotSupportedException("World combat return requires its recorded saved-world bytes.");
        NativeSavedWorldFootReturn receipt = saved.FootReturn ?? throw new NotSupportedException("World combat return requires the actual mode3 departure pose.");
        bool menuBattle = Events.PendingMenuBattle is { SourceScene: SceneKind.WorldMap } menu && menu.SourceHash == previous.AllocationProgramSha256 && menu.SourceFrame == receipt.SourceWorldFrame;
        bool randomBattle = departure.RandomEncounters?.Pending is { Scene: SceneKind.WorldMap } request && request.SourceHash == previous.AllocationProgramSha256 && request.Frame == receipt.SourceWorldFrame;
        if (occurrence.SourceScene != SceneKind.WorldMap || !menuBattle && !randomBattle || occurrence.SourceFrame != receipt.SourceWorldFrame || receipt.WorldMode != 3 || active.NativeResult is not (1 or 2 or 4))
        {
            throw new InvalidDataException("World combat return differs from its original request, result or saved foot owner.");
        }
        GameState input = departure with { World = null, Battle = null, PendingBattleEncounter = null, DepartingWorldState = departure.World, RandomEncounters = departure.RandomEncounters is null ? null : departure.RandomEncounters with { Pending = null } };
        GameSession temporary = new(input);
        WorldMapScene candidate = new(temporary, assets.GameDirectory, resources, receipt);
        Action publishMusic;
        try
        {
            if (validateWorldPresentation && !candidate.IsPresentationDeferred)
            {
                _ = candidate.Render(320, 224);
            }
            using WorldAssets musicAssets = new(assets.GameDirectory);
            publishMusic = Events.PrepareWorldMusic(departure.WorldArrival!, musicAssets.ReadMusicResource, musicAssets.ReadMusicFileResource, active.NativeMusicProcess ?? throw new NotSupportedException("World combat return requires its live music return bank."), receipt.EntityType105);
        }
        catch
        {
            candidate.Dispose();
            throw;
        }
        bool published = false;
        return () =>
        {
            if (published || worldScene != previous)
            {
                throw new InvalidOperationException("The prepared world combat reload has already been adopted or replaced.");
            }
            session.CommitPreparedWorldBattleReturn(occurrence, departure.World!, saved, temporary.State, candidate.ValidatePreparedSession);
            candidate.RebindPreparedSession(session);
            worldScene = candidate;
            candidate.PrepareFieldEntry = PrepareNativeWorldFieldEntry;
            candidate.ReadFieldMagicStatRules = (vitals, row) => BattleResources.ReadPlayerNativeStatRules(vitals, row);
            candidate.ResolveNativeAutoJunction = (vitals, row, priority) => assets.ReadNativeTutorialCatalog().AutoJunction(vitals, row, priority);
            ConfigureWorldSalaryEffects(candidate);
            publishMusic();
            session.PublishNativeWorldBattleMusic(Events.Scene.MusicProcess!);
            previous.Dispose();
            published = true;
        };
    }
}
