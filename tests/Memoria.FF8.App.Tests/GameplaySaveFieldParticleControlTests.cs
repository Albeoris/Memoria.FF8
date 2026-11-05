using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySaveFieldParticleControlTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Particle_controls_restart_current_visual_pool_without_advancing_gameplay_or_replaying_commands(bool changedResource)
    {
        VerifyParticleRestart(changedResource, false);
    }

    internal static void VerifyParticleRestart(bool changedResource, bool missingSavedPhase)
    {
        string directory = TestResources.GameDirectory;
        string fieldName;
        using (GameAssets original = new(directory))
        {
            fieldName = original.LoadField(165).Name;
        }
        byte[] definition = missingSavedPhase ? new byte[4] : Definition(32);
        using GameAssets sourceAssets = new(directory, overrides: new ScriptOverride(fieldName));
        InstallParticles(sourceAssets, fieldName, definition);
        if (missingSavedPhase)
        {
            Assert.Null(sourceAssets.ReadNativeFieldParticleResources(165));
        }
        else
        {
            Assert.Equal(definition, sourceAssets.ReadNativeFieldParticleResources(165)!.Definition);
        }
        GameplaySaveComponentDocument initial = GameplaySaveSessionLoaderTests.Document(sourceAssets, false);
        GameSession sourceSession = new(GameplaySaveSessionLoader.Restore(initial, directory, sharedAssets: sourceAssets));
        using GameWorld source = new(sourceSession, directory, sharedAssets: sourceAssets);
        source.EnsureField();
        for (int tick = 0; tick < 2; tick++)
        {
            Assert.True(source.Apply(new(PlayerActionKind.Wait)).Accepted);
        }
        if (!missingSavedPhase)
        {
            Assert.NotEmpty(source.Events.FieldParticleSprites);
        }
        GameplaySaveFieldContinuation continuation = source.Events.CaptureGameplaySaveFieldContinuation();
        if (missingSavedPhase)
        {
            continuation = continuation with { Scene = continuation.Scene with { ParticleRandomPhase = null } };
        }
        Assert.Equal((byte)0x80, continuation.Scene.ParticleControls![0]);
        Assert.Equal((byte)1, continuation.Scene.ParticleControls[1]);
        Assert.Equal((byte)0, continuation.Scene.ParticleControls[2]);
        GameState prepared = sourceSession.PrepareSnapshot();
        using (GameWorld exact = new(new GameSession(prepared), directory, sharedAssets: sourceAssets))
        {
            exact.EnsureField();
            Assert.True(JsonElement.DeepEquals(source.Events.Capture(), exact.Events.Capture()));
        }
        GameplaySaveComponentDocument document = GameplaySaveComponentDocument.CaptureProgression(prepared, initial.Resume with { Field = continuation.GetLeaderPosition() });
        document = document with { Progression = document.Progression!.WithFieldContinuation(continuation) };
        using MemoryStream encoded = new();
        GameplaySaveCodec.Write(encoded, document);
        encoded.Position = 0;
        GameplaySaveComponentDocument decoded = GameplaySaveCodec.Read(encoded);
        string before = JsonSerializer.Serialize(decoded);
        byte[] currentDefinition = changedResource ? Definition(48) : definition;
        using GameAssets selected = new(directory, overrides: new ScriptOverride(fieldName));
        InstallParticles(selected, fieldName, currentDefinition);
        GameplaySaveFieldContinuation valid = decoded.Progression!.FieldContinuation!;
        GameplaySaveComponentDocument malformed = decoded with { Progression = decoded.Progression with { FieldContinuation = valid with { Scene = valid.Scene with { ParticleControls = new() { [0] = 0xFF } } } } };
        Assert.Throws<InvalidDataException>(() => GameplaySaveSessionLoader.Restore(malformed, directory, sharedAssets: selected));
        GameState restoredState = GameplaySaveSessionLoader.Restore(decoded, directory, sharedAssets: selected);
        Assert.Equal(before, JsonSerializer.Serialize(decoded));
        Assert.Equal(prepared.CrtRandom, restoredState.CrtRandom);
        Assert.Equal(RandomOwners(prepared), RandomOwners(restoredState));
        Assert.Equal(prepared.Playtime, restoredState.Playtime);
        Assert.Equal(prepared.ExamTimer!.ClockCalls, restoredState.ExamTimer!.ClockCalls);
        GameSession restoredSession = new(restoredState);
        using GameWorld restored = new(restoredSession, directory, sharedAssets: selected);
        restored.EnsureField();
        Assert.Equal(continuation.Scripts.Frame, restored.Events.Frame);
        Assert.Equal(NativeParticleControlOrigin.GameplayContinuation, restored.Events.Scene.NativeParticleSlotOrigin);
        Assert.Empty(restored.Events.Scene.NativeParticleRequests);
        Assert.Empty(restored.Events.FieldParticleSprites);
        NativeFieldParticlesState restarted = restored.Events.Scene.NativeFieldParticles!;
        Assert.Equal(continuation.Scene.ParticleRandomPhase ?? NativeFieldParticlesState.Create(currentDefinition).RandomPhase, restarted.RandomPhase);
        Assert.Equal(restarted.RandomPhase, restored.Events.Scene.NativeFieldParticleRandomPhase);
        Assert.Equal(NativeFieldParticlesState.Create(currentDefinition).Memory, restarted.Memory);
        Assert.Equal((byte)0x80, restarted.Emitters[0]);
        Assert.Equal((byte)1, restarted.Emitters[1]);
        Assert.Equal(0, restored.Events.ReadNativeParticleAttachment(0)!.ObjectId);
        Assert.Null(restored.Events.ReadNativeParticleAttachment(2));
        JsonObject corrupted = JsonNode.Parse(restored.Events.Capture().GetRawText())!.AsObject();
        corrupted["Scene"]!["NativeParticleSlotOrigin"] = "Opcode";
        Assert.Throws<InvalidDataException>(() => restored.Events.Restore(JsonSerializer.SerializeToElement(corrupted)));
        for (int tick = 0; tick < 6; tick++)
        {
            Assert.Equal(source.Apply(new(PlayerActionKind.Wait)), restored.Apply(new(PlayerActionKind.Wait)));
            Assert.Null(restored.Events.LastFault);
            Assert.Equal(sourceSession.State.CrtRandom, restoredSession.State.CrtRandom);
            Assert.Equal(RandomOwners(sourceSession.State), RandomOwners(restoredSession.State));
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(sourceSession.State.ExamTimer), JsonSerializer.SerializeToElement(restoredSession.State.ExamTimer)));
            Assert.Equal(prepared.Economy!.Gil, restoredSession.State.Economy!.Gil);
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(source.Events.CaptureGameplaySaveScripts()), JsonSerializer.SerializeToElement(restored.Events.CaptureGameplaySaveScripts())));
        }
        Assert.Equal(77, restoredSession.EventMemory.Read(600, 4).RequireExact());
        Assert.Equal((byte)0, restored.Events.Scene.NativeParticleSlots[0]);
        Assert.NotEmpty(restored.Events.Scene.NativeParticleRequests);
        restoredSession.PrepareSnapshot();
        Assert.NotNull(selected.LoadField(165));
    }

    private static string RandomOwners(GameState state) => JsonSerializer.Serialize(new { state.CrtRandom, state.EventRandom, state.MenuRandom, state.GlobalRandom, state.RandomEncounters });

    private static byte[] Program()
    {
        (Jsm.Opcode, int)[] initialize = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.ADDGIL, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] ordinary = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PARTICLESET, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PARTICLEON, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PARTICLEOFF, 0), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PARTICLEOFF, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_L, 600), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        return GameplaySaveFieldScriptsTests.Encode([1], [initialize, ordinary]);
    }

    private static byte[] Definition(short width)
    {
        byte[] definition = new byte[NativeFieldParticlesState.DefinitionSize];
        for (int emitter = 0; emitter < 16; emitter++)
        {
            for (int lane = 0; lane < 4; lane++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(definition.AsSpan(5952 + emitter * 254 + 144 + lane * 18), -1);
            }
        }
        definition[14] = 8;
        BinaryPrimitives.WriteInt16LittleEndian(definition.AsSpan(8), width);
        BinaryPrimitives.WriteInt16LittleEndian(definition.AsSpan(346), 4);
        for (int emitter = 0; emitter < 2; emitter++)
        {
            int offset = 5952 + emitter * 254;
            definition[offset + 128] = 1;
            definition[offset + 208] = 10;
            BinaryPrimitives.WriteInt16LittleEndian(definition.AsSpan(offset + 144), 0);
            BinaryPrimitives.WriteInt16LittleEndian(definition.AsSpan(offset + 252), 1);
        }
        return definition;
    }

    private static void InstallParticles(GameAssets assets, string fieldName, byte[] definition)
    {
        byte[] texture = new byte[516];
        BinaryPrimitives.WriteInt16LittleEndian(texture, 256);
        assets.Resources.Prepare(null, encodedResources: [new(new($"field/{fieldName}/{fieldName}.pmd"), "pmd", 1, "fixture", definition), new(new($"field/{fieldName}/{fieldName}.pmp"), "pmp", 1, "fixture", texture)]).Commit();
    }

    private sealed class ScriptOverride(string fieldName) : IAssetOverrideSource
    {
        public bool TryRead(string logicalPath, out byte[] content)
        {
            string name = Path.GetFileName(logicalPath);
            if (name.Equals(fieldName + ".jsm", StringComparison.OrdinalIgnoreCase))
            {
                content = Program();
                return true;
            }
            content = [];
            return false;
        }
    }
}
