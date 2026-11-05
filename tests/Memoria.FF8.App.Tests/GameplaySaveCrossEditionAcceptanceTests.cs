using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySaveCrossEditionAcceptanceTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Semantic_field_and_world_slots_cold_load_and_save_back_across_installed_editions(bool sourceRemastered, bool worldMap)
    {
        string sourceDirectory = DirectoryFor(sourceRemastered);
        string targetDirectory = DirectoryFor(!sourceRemastered);
        string folder = Path.Combine(Path.GetTempPath(), "Memoria.FF8-cross-edition-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(folder);
            using GameAssets sourceAssets = new(sourceDirectory);
            using GameAssets targetAssets = new(targetDirectory);
            Assert.Equal(sourceRemastered ? GameDataEdition.Remastered : GameDataEdition.Classic, sourceAssets.Resources.Source.Edition);
            Assert.Equal(sourceRemastered ? GameDataEdition.Classic : GameDataEdition.Remastered, targetAssets.Resources.Source.Edition);
            GameSession sourceSession = new(CreateProgress(sourceAssets, sourceDirectory, worldMap));
            using GameWorld source = new(sourceSession, sourceDirectory, sharedAssets: sourceAssets);
            source.EnsureField();
            if (!worldMap)
            {
                Assert.True(source.Apply(new(PlayerActionKind.Wait, Frames: 1)).Accepted);
            }
            GameState expected = sourceSession.PrepareSnapshot();
            string sourcePath = Path.Combine(folder, "source.sav");
            source.WriteGameplaySave(sourcePath);
            byte[] originalFile = File.ReadAllBytes(sourcePath);
            using (FileStream stream = File.OpenRead(sourcePath))
            {
                GameplaySaveComponentDocument document = GameplaySaveCodec.Read(stream);
                string encoded = JsonSerializer.Serialize(document);
                Assert.DoesNotContain("NativeBattleMemory", encoded);
                Assert.DoesNotContain("ModelCpuResources", encoded);
                Assert.DoesNotContain("TextureSourceDescriptor", encoded);
                Assert.Equal(worldMap ? GameplaySaveResumeKind.World : GameplaySaveResumeKind.Field, document.Resume.Kind);
            }
            GameSession targetSession = new(PlayerStateLoader.Load(sourcePath, targetDirectory, sharedAssets: targetAssets));
            AssertRepresentableProgress(expected, targetSession.State);
            AssertFreshDefinitions(targetSession.State, targetAssets);
            AssertSemanticProgress(expected, targetSession.State);
            Assert.NotSame(expected.NativeBattleMemory, targetSession.State.NativeBattleMemory);
            using GameWorld target = new(targetSession, targetDirectory, sharedAssets: targetAssets);
            target.EnsureField();
            Assert.NotEmpty(targetAssets.LoadField(expected.FieldId).Models);
            Assert.NotSame(sourceAssets.LoadField(expected.FieldId).Models, targetAssets.LoadField(expected.FieldId).Models);
            string returnedPath = Path.Combine(folder, "returned.sav");
            target.WriteGameplaySave(returnedPath);
            GameState returned = PlayerStateLoader.Load(returnedPath, sourceDirectory, sharedAssets: sourceAssets);
            AssertRepresentableProgress(expected, returned);
            AssertSemanticProgress(expected, returned);
            AssertFreshDefinitions(returned, sourceAssets);
            Assert.Equal(originalFile, File.ReadAllBytes(sourcePath));
            GameSession resumedSession = new(returned);
            using GameWorld resumed = new(resumedSession, sourceDirectory, sharedAssets: sourceAssets);
            resumed.EnsureField();
            PlayerActionResult continuation = resumed.Apply(new(PlayerActionKind.Wait, Frames: 1));
            Assert.True(continuation.Accepted, continuation.Detail);
            Assert.Equal(1, continuation.FramesAdvanced);
            Assert.Null(resumed.Events.LastFault);
            AssertRepresentableProgress(expected, resumedSession.State);
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Native_PC_export_import_and_export_back_keep_representable_progress_with_both_profiles(bool sourceRemastered, bool worldMap)
    {
        string sourceDirectory = DirectoryFor(sourceRemastered);
        string targetDirectory = DirectoryFor(!sourceRemastered);
        using GameAssets sourceAssets = new(sourceDirectory);
        using GameAssets targetAssets = new(targetDirectory);
        GameSession sourceSession = new(CreateProgress(sourceAssets, sourceDirectory, worldMap));
        using GameWorld source = new(sourceSession, sourceDirectory, sharedAssets: sourceAssets);
        source.EnsureField();
        if (!worldMap)
        {
            Assert.True(source.Apply(new(PlayerActionKind.Wait, Frames: 1)).Accepted);
        }
        GameState expected = sourceSession.PrepareSnapshot();
        byte[] image = NativePcSaveConverter.Export(expected, sourceAssets.ReadNewGameCharacterVitals(), originalPlaceId: 0);
        AssertNativeEnvelope(image);
        byte[] original = image.ToArray();
        GameState imported = NativePcSaveConverter.Import(image, targetAssets);
        AssertRepresentableProgress(expected, imported);
        AssertFreshDefinitions(imported, targetAssets);
        Assert.Equal(expected.Scene, imported.Scene);
        Assert.Equal(expected.FieldId, imported.FieldId);
        byte[] returnedImage = NativePcSaveConverter.Export(imported, targetAssets.ReadNewGameCharacterVitals(), originalPlaceId: 0);
        AssertNativeEnvelope(returnedImage);
        GameState returned = NativePcSaveConverter.Import(returnedImage, sourceAssets);
        AssertRepresentableProgress(expected, returned);
        AssertFreshDefinitions(returned, sourceAssets);
        Assert.Equal(original, image);
    }

    private static GameState CreateProgress(GameAssets assets, string directory, bool worldMap)
    {
        GameplaySaveComponentDocument initial = GameplaySaveSessionLoaderTests.Document(assets, worldMap);
        GameplaySaveGuardianForce[] guardians = initial.Characters.GuardianForces.ToArray();
        guardians[0] = guardians[0] with { Flags = (byte)(guardians[0].Flags | 1), Experience = 76543, HitPoints = 123, KillCount = 9, DeathCount = 2 };
        initial = initial with { Characters = initial.Characters with { GuardianForces = guardians } };
        GameState state = GameplaySaveSessionLoader.Restore(initial, directory, sharedAssets: assets);
        NativeCharacterVitals characters = state.CharacterVitals!;
        NativeGuardianForceAbility ability = characters.GuardianForceAbilities(0).First(entry => !entry.Learned && entry.RequiredPoints > 3);
        characters = characters.SelectGuardianForceLearning(0, ability.Ability).AwardGuardianForceAbilityPoints(0, checked((ushort)ability.RequiredPoints));
        Assert.True(characters.GuardianForces[0].HasLearnedAbility(ability.Ability));
        NativeGuardianForceAbility pending = characters.GuardianForceAbilities(0).First(entry => !entry.Learned && entry.RequiredPoints > 3);
        characters = characters.SelectGuardianForceLearning(0, pending.Ability).AwardGuardianForceAbilityPoints(0, 3);
        return state with { CharacterVitals = characters, KnownMagic = state.KnownMagic!.Add(7).Add(33) };
    }

    private static void AssertRepresentableProgress(GameState expected, GameState actual)
    {
        Assert.Equal(expected.Scene, actual.Scene);
        Assert.Equal(expected.FieldId, actual.FieldId);
        Assert.Equal(expected.Economy!.Gil, actual.Economy!.Gil);
        Assert.Equal(expected.Economy.PastGil, actual.Economy.PastGil);
        Assert.Equal(expected.Economy.Items, actual.Economy.Items);
        Assert.Equal(expected.KnownMagic!.Words, actual.KnownMagic!.Words);
        Assert.Equal(expected.CharacterVitals!.CombatParty, actual.CharacterVitals!.CombatParty);
        for (int row = 0; row < expected.CharacterVitals.Records.Length; row++)
        {
            NativeCharacterVital character = expected.CharacterVitals.Records[row];
            NativeCharacterVital restored = actual.CharacterVitals.Records[row];
            Assert.Equal(character.CharacterId, restored.CharacterId);
            Assert.Equal(character.Experience, restored.Experience);
            Assert.Equal(character.HitPoints.Minimum, restored.HitPoints.Minimum);
            Assert.Equal(character.Status, restored.Status);
            Assert.Equal(character.Weapon, restored.Weapon);
            Assert.Equal(character.StatBonuses, restored.StatBonuses);
            Assert.Equal(character.MagicInventory, restored.MagicInventory);
        }
        for (int id = 0; id < expected.CharacterVitals.GuardianForces.Length; id++)
        {
            NativeGuardianForceVital guardian = expected.CharacterVitals.GuardianForces[id];
            NativeGuardianForceVital restored = actual.CharacterVitals.GuardianForces[id];
            Assert.Equal(guardian.Experience, restored.Experience);
            Assert.Equal(guardian.Flags, restored.Flags);
            Assert.Equal(guardian.HitPoints, restored.HitPoints);
            if (guardian.KillCount is { } kills)
            {
                Assert.Equal(kills, restored.KillCount);
            }
            if (guardian.DeathCount is { } deaths)
            {
                Assert.Equal(deaths, restored.DeathCount);
            }
            Assert.Equal(guardian.Learning, restored.Learning);
            var savedAbilities = expected.CharacterVitals.GuardianForceAbilities(id).Select(entry => (entry.Ability, entry.Learned, entry.Progress)).OrderBy(entry => entry.Ability).ToArray();
            var loadedAbilities = actual.CharacterVitals.GuardianForceAbilities(id).Select(entry => (entry.Ability, entry.Learned, entry.Progress)).OrderBy(entry => entry.Ability).ToArray();
            Assert.Equal(savedAbilities, loadedAbilities);
        }
    }

    private static void AssertSemanticProgress(GameState expected, GameState actual)
    {
        Assert.Equal(expected.EventMemory.Read(500, 4).RequireExact(), actual.EventMemory.Read(500, 4).RequireExact());
        Assert.Equal(expected.LimitProgress!.AngeloFlags, actual.LimitProgress!.AngeloFlags);
        Assert.Equal(expected.LimitProgress.TrainingCounters, actual.LimitProgress.TrainingCounters);
        Assert.Equal(expected.AppliedBattleResultIds, actual.AppliedBattleResultIds);
        Assert.Equal(expected.BattleStatistics, actual.BattleStatistics);
        Assert.Equal(expected.PendingOriginalAchievementActions, actual.PendingOriginalAchievementActions);
        Assert.Equal(expected.Playtime, actual.Playtime);
        Assert.Equal(JsonSerializer.Serialize(expected.ExamTimer), JsonSerializer.Serialize(actual.ExamTimer));
    }

    private static void AssertFreshDefinitions(GameState actual, GameAssets currentAssets)
    {
        NativeCharacterVitals currentDefinitions = currentAssets.ReadNewGameCharacterVitals();
        Assert.Equal(currentDefinitions.InitOutSha256, actual.CharacterVitals!.InitOutSha256);
        Assert.Equal(currentDefinitions.KernelSha256, actual.CharacterVitals.KernelSha256);
        Assert.NotSame(currentDefinitions, actual.CharacterVitals);
    }

    private static void AssertNativeEnvelope(byte[] image)
    {
        Assert.Equal(image.Length - 4, checked((int)BinaryPrimitives.ReadUInt32LittleEndian(image)));
        NativePcSaveData decoded = NativePcSaveCodec.Decode(image);
        Assert.Equal(NativePcSaveData.BlockSize, decoded.Block.Length);
        ushort checksum = NativePcSaveCodec.Checksum(decoded.Block.AsSpan(NativePcSaveData.MainOffset, 4944));
        Assert.Equal(checksum, BinaryPrimitives.ReadUInt16LittleEndian(decoded.Block.AsSpan(384)));
        Assert.Equal(checksum, BinaryPrimitives.ReadUInt16LittleEndian(decoded.Block.AsSpan(5408)));
        Assert.Equal(decoded.Block, NativePcSaveCodec.Decode(NativePcSaveCodec.Encode(decoded)).Block);
    }

    private static string DirectoryFor(bool remastered)
    {
        if (!remastered)
        {
            return TestResources.GameDirectory;
        }
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_DIR");
        Assert.False(string.IsNullOrWhiteSpace(directory), "Set MEMORIA_FF8_REMASTERED_DIR to the installed Remastered resources for cross-edition acceptance.");
        Assert.True(Directory.Exists(directory), "The selected Remastered resource directory is unavailable.");
        return Path.GetFullPath(directory!);
    }
}
