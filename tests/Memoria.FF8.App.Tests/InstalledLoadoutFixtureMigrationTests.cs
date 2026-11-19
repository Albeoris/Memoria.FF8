using System.Security.Cryptography;
using System.Text.Json;
using System.Reflection;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

/// <summary>Explicit one-fixture migration; output is selected externally and the source is never changed.</summary>
public sealed class InstalledLoadoutFixtureMigrationTests
{
    [Fact]
    public void Reconstruct_selected_process_owners_without_replaying_saved_field_scripts()
    {
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_LOADOUT_MIGRATION_OUTPUT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(output), "Select a distinct output path for the one requested standalone fixture migration.");
        Assert.False(File.Exists(output), "Preserve an existing migrated copy; select a new output path.");
        string directory = TestResources.GameDirectory;
        string source = TestResources.FixturePath("weapon-limit-field-source");
        Assert.NotEqual(Path.GetFullPath(source), Path.GetFullPath(output!));
        byte[] originalHash = SHA256.HashData(File.ReadAllBytes(source));
        using GameResourceHost selected = new(directory, watch: false);
        using GameAssets game = new(directory, resources: selected.Resources, modelOverrides: selected);
        GameSession original = new(GameSession.Load(source));
        using GameWorld extraction = new(original, directory, resources: selected.Resources, sharedAssets: game, modelOverrides: selected, readGameplayPolicy: () => selected.GameplayPolicy, validateWorldPresentation: false, legacyGameplayProjection: true);
        extraction.EnsureField();
        Assert.Equal(SceneKind.Field, original.State.Scene);
        Assert.Null(original.State.Battle);
        GameplaySaveComponentDocument before = extraction.CaptureLegacyGameplayDocument();
        Assert.NotNull(before.Progression!.FieldContinuation);
        GameState migrated = GameplaySaveSessionLoader.Restore(before, directory, game, selected.Resources, selected, () => selected.GameplayPolicy);
        GameSession candidate = new(migrated);
        using GameWorld resumed = new(candidate, directory, resources: selected.Resources, sharedAssets: game, modelOverrides: selected, readGameplayPolicy: () => selected.GameplayPolicy, validateWorldPresentation: false);
        resumed.EnsureField();
        GameplaySaveComponentDocument after = resumed.CaptureGameplaySave();
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(before.Progression.FieldContinuation.Scripts), JsonSerializer.SerializeToElement(after.Progression!.FieldContinuation!.Scripts)), "No field script may run during process allocation.");
        NativeCrtRandomState? allocatedRandom = candidate.State.CrtRandom;
        if (candidate.State.CrtRandom != before.Progression.Clocks.CrtRandom)
        {
            NativeFieldEyeProcess eyes = Assert.IsType<NativeFieldEyeProcess>(resumed.Events.Scene.NativeEyeProcess);
            typeof(EventEngine).GetMethod("PublishNativeFieldEyeProcess", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(resumed.Events, [eyes with { CrtRandom = before.Progression.Clocks.CrtRandom }]);
            after = resumed.CaptureGameplaySave();
        }
        if (before.Progression.World?.Return is { Kind: GameplaySaveWorldReturnKind.Foot } savedReturn && candidate.State.SavedWorld?.FootReturn is { } allocatedReturn && allocatedReturn.StoryWord != savedReturn.StoryWord)
        {
            candidate.SetSavedWorldState(candidate.State.SavedWorld! with { FootReturn = allocatedReturn with { StoryWord = savedReturn.StoryWord } });
            after = resumed.CaptureGameplaySave();
        }
        AssertDocumentEqual(before, after);
        Assert.Equal(original.State.EventRandom, candidate.State.EventRandom);
        Assert.Equal(original.State.CrtRandom, candidate.State.CrtRandom);
        Assert.Equal(original.State.GlobalRandom, candidate.State.GlobalRandom);
        Assert.Equal(extraction.Events.Frame, resumed.Events.Frame);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(before.Progression.FieldContinuation.Scripts), JsonSerializer.SerializeToElement(after.Progression!.FieldContinuation!.Scripts)));
        string parent = Path.GetDirectoryName(Path.GetFullPath(output!))!;
        Directory.CreateDirectory(parent);
        candidate.Save(output!, compact: true);
        GameSession reloaded = new(PlayerStateLoader.Load(output!, directory, sharedAssets: game, resources: selected.Resources, modelOverrides: selected));
        using GameWorld reopened = new(reloaded, directory, resources: selected.Resources, sharedAssets: game, modelOverrides: selected, readGameplayPolicy: () => selected.GameplayPolicy, validateWorldPresentation: false);
        reopened.EnsureField();
        AssertDocumentEqual(after, reopened.CaptureGameplaySave());

        long continuationFrame = extraction.Events.Frame;
        PlayerActionResult expected = extraction.Apply(new(PlayerActionKind.Wait));
        PlayerActionResult actual = reopened.Apply(new(PlayerActionKind.Wait));
        Assert.True(expected.Accepted);
        Assert.True(actual.Accepted);
        Assert.Equal(expected, actual);
        Assert.Equal(continuationFrame + 1, extraction.Events.Frame);
        Assert.Equal(continuationFrame + 1, reopened.Events.Frame);
        Assert.Null(extraction.Events.LastFault);
        Assert.Null(reopened.Events.LastFault);
        AssertDocumentEqual(extraction.CaptureLegacyGameplayDocument(), reopened.CaptureGameplaySave());
        Assert.Equal(originalHash, SHA256.HashData(File.ReadAllBytes(source)));
        string receipt = JsonSerializer.Serialize(new
        {
            SourcePath = Path.GetFullPath(source),
            MigratedPath = Path.GetFullPath(output!),
            Field = after.Resume.FieldId,
            Frame = after.Progression!.FieldContinuation!.Scripts.Frame,
            ContinuationCallbacks = 1,
            AllocatedCrtSeed = allocatedRandom?.Seed,
            RestoredCrtSeed = before.Progression.Clocks.CrtRandom?.Seed,
            SourceSha256 = Convert.ToHexString(originalHash),
            MigratedSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(output!))),
            MigratedBytes = new FileInfo(output!).Length,
            DurableSha256 = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(after))),
            Changed = "Reconstructed process-local execution, graphics, effect and stack owners; missing battle-counter aliases recovered from saved canonical counters; absent bars/shake use documented defaults; legacy automatic eye mode zero becomes 0x80000. Saved RNG, historical world-return story word, progress and script PCs preserved."
        });
        File.WriteAllText(output! + ".migration.json", receipt);
        Console.WriteLine(receipt);
    }

    private static void AssertDocumentEqual(GameplaySaveComponentDocument expected, GameplaySaveComponentDocument actual)
    {
        JsonElement first = JsonSerializer.SerializeToElement(Normalize(expected));
        JsonElement second = JsonSerializer.SerializeToElement(Normalize(actual));
        Assert.True(JsonElement.DeepEquals(first, second), "Canonical durable documents differ after process reconstruction: " + string.Join(", ", ChangedPaths(first, second, "$")));
    }

    private static GameplaySaveComponentDocument Normalize(GameplaySaveComponentDocument document)
    {
        GameplaySaveProgression progress = document.Progression!;
        NativeEventMemory memory = progress.Story.RestoreEventMemory();
        if (progress.Transactions.BattleStatistics is { } statistics)
        {
            MissingAlias(20, 4, statistics.Victories);
            MissingAlias(24, 2, statistics.Defeats);
            MissingAlias(26, 2, statistics.Escapes);
        }
        uint total = 0;
        for (int row = 0; row < 8; row++)
        {
            NativeCharacterVital character = document.Characters.Records[row];
            MissingAlias(28 + 2 * row, 2, character.KillCount);
            total += character.KillCount;
            if (character.DeathCount is ushort deaths)
            {
                MissingAlias(44 + 2 * row, 2, deaths);
            }
        }
        MissingAlias(68, 4, total);
        GameplaySaveEventCell[] bytes = memory.Cells.SelectMany(cell => Enumerable.Range(cell.Key, cell.Value.Width)).Distinct().Order().Select(address => new GameplaySaveEventCell(address, 1, memory.Read(address, 1).RequireExact())).Where(cell => cell.Value != 0).ToArray();
        GameplaySaveFieldContinuation? continuation = progress.FieldContinuation;
        if (continuation is not null)
        {
            continuation = continuation with
            {
                Scene = continuation.Scene with { Presentation = continuation.Scene.Presentation ?? GameplaySaveFieldPresentation.CreateDefaults() },
                Actors = continuation.Actors with { Actors = continuation.Actors.Actors.Select(actor => actor.Appearance.EyeMode == 0 ? actor with { Appearance = actor.Appearance with { EyeMode = 0x80000 } } : actor).ToArray() }
            };
        }
        return document with { SavedLocalAt = null, Progression = progress with { Story = progress.Story with { Cells = bytes }, FieldContinuation = continuation } };

        void MissingAlias(int address, int width, uint value)
        {
            for (int offset = 0; offset < width; offset++)
            {
                if (!memory.HasRepresentedExactBytes(address + offset, 1))
                {
                    memory = memory.Write(address + offset, 1, NativeScalarValue.Exact((byte)(value >> (8 * offset))));
                }
            }
        }
    }

    private static IEnumerable<string> ChangedPaths(JsonElement expected, JsonElement actual, string path)
    {
        if (JsonElement.DeepEquals(expected, actual))
        {
            yield break;
        }
        if (expected.ValueKind == JsonValueKind.Object && actual.ValueKind == JsonValueKind.Object)
        {
            foreach (string property in expected.EnumerateObject().Select(item => item.Name).Union(actual.EnumerateObject().Select(item => item.Name)).Take(128))
            {
                if (!expected.TryGetProperty(property, out JsonElement left) || !actual.TryGetProperty(property, out JsonElement right))
                {
                    yield return path + "." + property;
                    continue;
                }
                foreach (string changed in ChangedPaths(left, right, path + "." + property).Take(path is "$" or "$.Progression" or "$.Progression.FieldContinuation" ? int.MaxValue : 24))
                {
                    yield return changed;
                }
            }
            yield break;
        }
        if (expected.ValueKind == JsonValueKind.Array && actual.ValueKind == JsonValueKind.Array)
        {
            if (expected.GetArrayLength() != actual.GetArrayLength())
            {
                yield return path + ".Length=" + expected.GetArrayLength() + " -> " + actual.GetArrayLength();
            }
            for (int index = 0; index < Math.Min(expected.GetArrayLength(), actual.GetArrayLength()); index++)
            {
                foreach (string changed in ChangedPaths(expected[index], actual[index], path + "[" + index + "]").Take(16))
                {
                    yield return changed;
                }
            }
            yield break;
        }
        string before = expected.GetRawText();
        string after = actual.GetRawText();
        yield return path + "=" + (before.Length <= 96 ? before : before[..96]) + " -> " + (after.Length <= 96 ? after : after[..96]);
    }
}
