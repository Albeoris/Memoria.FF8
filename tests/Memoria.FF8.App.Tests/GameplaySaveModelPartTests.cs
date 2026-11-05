using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySaveModelPartTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Active_and_settled_door_offsets_resume_against_current_model_template(bool settled, bool changedTemplate)
    {
        CheckResume(settled, changedTemplate, false);
    }

    [Fact]
    public void Legacy_settled_absolute_endpoint_keeps_its_original_meaning_with_a_changed_template()
    {
        CheckResume(true, true, true);
    }

    private static void CheckResume(bool settled, bool changedTemplate, bool legacy)
    {
        string directory = TestResources.GameDirectory;
        string fieldName;
        (int X, int Y, int Z, int Triangle) placement;
        NativeModelPartsDefinition definition;
        using (GameAssets original = new(directory))
        {
            FieldAssets field = original.LoadField(919);
            fieldName = field.Name;
            definition = field.Models[13].NativeParts!;
            placement = Placement(field);
        }
        byte[] program = Program(placement);
        byte[] originalProgram = program.ToArray();
        using GameAssets assets = new(directory, overrides: new ScriptOverride(fieldName, program));
        GameplaySaveComponentDocument initial = GameplaySaveSessionLoaderTests.Document(assets, false);
        initial = initial with { Resume = initial.Resume with { FieldId = 919, PreviousFieldId = 918, Field = new(placement.X, placement.Y, placement.Triangle, 128) }, Progression = initial.Progression! with { SceneFacts = initial.Progression.SceneFacts! with { FieldId = 919 }, FieldLocation = null } };
        GameSession sourceSession = new(GameplaySaveSessionLoader.Restore(initial, directory, sharedAssets: assets));
        using GameWorld source = new(sourceSession, directory, sharedAssets: assets);
        source.EnsureField();
        for (int tick = 0; tick < 34 && (!source.Events.Scene.ModelParts!.Resources[13].Translations.TryGetValue(1, out NativeModelPartTranslation? state) || (settled ? state.Mode != 4 : state.Counter < 8)); tick++)
        {
            Assert.True(source.Apply(new(PlayerActionKind.Wait)).Accepted);
        }
        NativeModelPartTranslation saved = source.Events.Scene.ModelParts!.Resources[13].Translations[1];
        Assert.Equal(settled ? (short)4 : (short)1, saved.Mode);
        Assert.Equal(73, source.Events.EventMemory.Read(610, 1).Values[0]);
        Assert.Equal(0, source.Events.EventMemory.Read(611, 1).Values[0]);
        AssertExactRoundTrip(source);
        GameplaySaveFieldContinuation continuation = source.Events.CaptureGameplaySaveFieldContinuation();
        Assert.Empty(continuation.Actors.ModelParts);
        Assert.NotNull(continuation.Actors.ModelPartOffsets);
        Assert.Equal(new[] { 13 }, continuation.Actors.ModelPartOffsets.Keys);
        Assert.Equal(new[] { 1 }, continuation.Actors.ModelPartOffsets[13].Keys);
        Assert.Equal(!settled, continuation.Actors.ModelPartOffsets[13][1].Transition is not null);
        string offsets = JsonSerializer.Serialize(continuation.Actors.ModelPartOffsets);
        Assert.DoesNotContain("SourceHash", offsets, StringComparison.Ordinal);
        Assert.DoesNotContain("SkinBindings", offsets, StringComparison.Ordinal);
        Assert.DoesNotContain("SampleState", offsets, StringComparison.Ordinal);
        if (settled)
        {
            Assert.DoesNotContain("Counter", offsets, StringComparison.Ordinal);
            Assert.DoesNotContain("Duration", offsets, StringComparison.Ordinal);
            Assert.DoesNotContain("StartX", offsets, StringComparison.Ordinal);
        }
        if (legacy)
        {
            NativeModelPartResourceState resource = new(source.Events.Scene.ModelParts.Resources[13].Translations.ToDictionary(pair => pair.Key, pair => pair.Value));
            continuation = continuation with { Actors = continuation.Actors with { ModelParts = new() { [13] = resource }, ModelPartOffsets = null } };
        }
        GameState prepared = sourceSession.PrepareSnapshot();
        GameplaySaveComponentDocument document = GameplaySaveComponentDocument.CaptureProgression(prepared, initial.Resume with { Field = continuation.GetLeaderPosition() });
        document = document with { Progression = document.Progression!.WithFieldContinuation(continuation) };
        using MemoryStream encoded = new();
        GameplaySaveCodec.Write(encoded, document);
        encoded.Position = 0;
        GameplaySaveComponentDocument decoded = GameplaySaveCodec.Read(encoded);
        string before = JsonSerializer.Serialize(decoded);
        GameState restoredState;
        if (changedTemplate)
        {
            GameSession initializer = GameSession.CreateColdProcess(assets.ReadColdProcessNativeBattleMemory());
            initializer.StartNewGame();
            using (GameWorld initialized = new(initializer, directory, sharedAssets: assets))
            {
            }
            restoredState = GameplaySaveStateRestoration.Restore(decoded, initializer.State) with { FieldLoadContext = NativeFieldLoadContext.FromGameplayContinuation(919) };
        }
        else
        {
            restoredState = GameplaySaveSessionLoader.Restore(decoded, directory, sharedAssets: assets);
        }
        GameSession restoredSession = new(restoredState);
        using GameWorld restored = new(restoredSession, directory, sharedAssets: assets, gameplaySaveFacts: changedTemplate ? decoded.Progression!.SceneFacts : null);
        restored.EnsureField();
        NativeModelPartsDefinition selected = definition;
        if (changedTemplate)
        {
            short[] words = definition.Parts[1].Words.ToArray();
            words[12] = unchecked((short)(words[12] + 11));
            words[13] = unchecked((short)(words[13] - 7));
            words[14] = unchecked((short)(words[14] + 23));
            selected = definition with { Parts = definition.Parts.Select((part, index) => index == 1 ? new NativeModelPartDefinition(words) : part).ToArray() };
            Func<int, NativeModelPartsDefinition?> provider = restored.Events.ReadNativeModelParts!;
            restored.Events.ReadNativeModelParts = model => model == 13 ? selected : provider(model);
            restored.Events.RestoreGameplaySaveFieldContinuation(decoded.Progression!.FieldContinuation!, decoded.Progression.SceneFacts!);
        }
        if (!settled || legacy)
        {
            Assert.Equal(saved.Counter, restored.Events.Scene.ModelParts!.Resources[13].Translations[1].Counter);
        }
        Assert.Equal(prepared.CrtRandom, restoredSession.State.CrtRandom);
        Assert.Equal(prepared.Playtime, restoredSession.State.Playtime);
        Assert.Equal(prepared.ExamTimer!.ClockCalls, restoredSession.State.ExamTimer!.ClockCalls);
        Assert.Equal(source.Events.Frame, restored.Events.Frame);
        Assert.Equal(prepared.Economy!.Gil, restoredSession.State.Economy!.Gil);
        AssertExactRoundTrip(restored);
        AssertLogicalState(source, restored, definition, selected, legacy);
        for (int tick = 0; tick < 70; tick++)
        {
            Assert.Equal(source.Apply(new(PlayerActionKind.Wait)), restored.Apply(new(PlayerActionKind.Wait)));
            AssertLogicalState(source, restored, definition, selected, legacy);
            Assert.Equal(sourceSession.State.CrtRandom, restoredSession.State.CrtRandom);
            Assert.Equal(sourceSession.State.Playtime, restoredSession.State.Playtime);
            Assert.Equal(sourceSession.State.ExamTimer!.ClockCalls, restoredSession.State.ExamTimer!.ClockCalls);
        }
        NativeModelPartTranslation completed = restored.Events.Scene.ModelParts!.Resources[13].Translations[1];
        Assert.Equal((short)4, completed.Mode);
        Assert.Equal(legacy ? definition.Parts[1].TargetX : selected.Parts[1].TargetX, completed.TargetX);
        Assert.Equal(legacy ? definition.Parts[1].TargetY : selected.Parts[1].TargetY, completed.TargetY);
        Assert.Equal(legacy ? definition.Parts[1].TargetZ : selected.Parts[1].TargetZ, completed.TargetZ);
        Assert.Equal(74, restored.Events.EventMemory.Read(611, 1).Values[0]);
        Assert.Equal(prepared.Economy.Gil + 13, restoredSession.State.Economy!.Gil);
        Assert.Equal(before, JsonSerializer.Serialize(decoded));
        Assert.Equal(originalProgram, program);
        Assert.Equal(definition.Parts[1].Words, assets.LoadField(919).Models[13].NativeParts!.Parts[1].Words);
    }

    private static void AssertLogicalState(GameWorld expected, GameWorld actual, NativeModelPartsDefinition original, NativeModelPartsDefinition selected, bool absolute)
    {
        NativeModelPartTranslation first = expected.Events.Scene.ModelParts!.Resources[13].Translations[1];
        NativeModelPartTranslation second = actual.Events.Scene.ModelParts!.Resources[13].Translations[1];
        Assert.Equal(first.Mode, second.Mode);
        NativeModelPartDefinition baseline = absolute ? original.Parts[1] : selected.Parts[1];
        Assert.Equal((unchecked((short)(first.TargetX - original.Parts[1].TargetX)), unchecked((short)(first.TargetY - original.Parts[1].TargetY)), unchecked((short)(first.TargetZ - original.Parts[1].TargetZ))), (unchecked((short)(second.TargetX - baseline.TargetX)), unchecked((short)(second.TargetY - baseline.TargetY)), unchecked((short)(second.TargetZ - baseline.TargetZ))));
        if (first.Mode == 1)
        {
            Assert.Equal((first.Counter, first.Duration), (second.Counter, second.Duration));
            Assert.Equal((unchecked((short)(first.StartX - original.Parts[1].TargetX)), unchecked((short)(first.StartY - original.Parts[1].TargetY)), unchecked((short)(first.StartZ - original.Parts[1].TargetZ))), (unchecked((short)(second.StartX - baseline.TargetX)), unchecked((short)(second.StartY - baseline.TargetY)), unchecked((short)(second.StartZ - baseline.TargetZ))));
        }
        else if (absolute)
        {
            Assert.Equal(first, second);
        }
        for (int objectId = 0; objectId < 2; objectId++)
        {
            ActorState source = expected.Events.Actors[objectId];
            ActorState target = actual.Events.Actors[objectId];
            Assert.Equal((source.X, source.Y, source.Z, source.WalkmeshTriangle, source.Direction), (target.X, target.Y, target.Z, target.WalkmeshTriangle, target.Direction));
        }
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expected.Events.CaptureGameplaySaveScripts()), JsonSerializer.SerializeToElement(actual.Events.CaptureGameplaySaveScripts())));
    }

    private static void AssertExactRoundTrip(GameWorld world)
    {
        JsonElement snapshot = world.Events.Capture();
        world.Events.Restore(snapshot);
        Assert.True(JsonElement.DeepEquals(snapshot, world.Events.Capture()));
    }

    private static (int X, int Y, int Z, int Triangle) Placement(FieldAssets field)
    {
        foreach (var triangle in field.Walkmesh.Triangles)
        {
            int x = (int)MathF.Round((triangle.A.X + triangle.B.X + triangle.C.X) / 3f);
            int y = (int)MathF.Round((triangle.A.Y + triangle.B.Y + triangle.C.Y) / 3f);
            int z = (int)MathF.Round((triangle.A.Z + triangle.B.Z + triangle.C.Z) / 3f);
            if (FieldPartyTraceGeometry.Resolve(field.Walkmesh, x, y, z) is int resolved)
            {
                return (x, y, z, resolved);
            }
        }
        throw new InvalidDataException("The configured field has no integral walkmesh centroid for the model-part fixture.");
    }

    private static byte[] Program((int X, int Y, int Z, int Triangle) placement)
    {
        List<(Jsm.Opcode, int)[]> scripts = [];
        for (int objectId = 0; objectId < 2; objectId++)
        {
            List<(Jsm.Opcode, int)> init = [(Jsm.Opcode.LBL, objectId * 16), (Jsm.Opcode.SETMODEL, objectId == 0 ? 0 : 13)];
            if (objectId == 0)
            {
                init.AddRange([(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.ADDGIL, 0)]);
            }
            init.AddRange([(Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PUSHOFF, 0), (Jsm.Opcode.TALKOFF, 0), (Jsm.Opcode.FOOTSTEPOFF, 0), (Jsm.Opcode.PSHN_L, placement.X), (Jsm.Opcode.PSHN_L, placement.Y), (Jsm.Opcode.PSHN_L, placement.Z), (Jsm.Opcode.SET3, placement.Triangle), (Jsm.Opcode.RET, 8)]);
            scripts.Add(init.ToArray());
            List<(Jsm.Opcode, int)> main = [(Jsm.Opcode.LBL, objectId * 16 + 1)];
            if (objectId == 1)
            {
                main.AddRange([(Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.MODELDOOROPEN, 0), (Jsm.Opcode.PSHN_L, 73), (Jsm.Opcode.POPM_B, 610), (Jsm.Opcode.PSHN_L, 35), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.MODELDOORCLOSE, 0), (Jsm.Opcode.PSHN_L, 74), (Jsm.Opcode.POPM_B, 611), (Jsm.Opcode.PSHN_L, 13), (Jsm.Opcode.ADDGIL, 0)]);
            }
            main.AddRange([(Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
            scripts.Add(main.ToArray());
        }
        return GameplaySaveFieldScriptsTests.Encode([1, (16 << 7) | 1], scripts);
    }

    private sealed class ScriptOverride(string fieldName, byte[] script) : IAssetOverrideSource
    {
        public bool TryRead(string logicalPath, out byte[] content)
        {
            if (Path.GetFileName(logicalPath).Equals(fieldName + ".jsm", StringComparison.OrdinalIgnoreCase))
            {
                content = script.ToArray();
                return true;
            }
            content = [];
            return false;
        }
    }
}
