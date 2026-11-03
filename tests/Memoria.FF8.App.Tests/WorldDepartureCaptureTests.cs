using System.Buffers.Binary;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class WorldDepartureCaptureTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WorldMapJumpPublishesItsCompletedVmAfterDirectAndPlayerCallbacks(bool direct)
    {
        string directory = TestResources.GameDirectory;
        GameSession session = new(GameSession.Load(TestResources.FixturePath("WorldDepartureCaptureTests.Source")));
        using GameWorld host = new(session, directory, validateWorldPresentation: false);
        host.EnsureField();
        byte music = host.Events.Scene.WorldMusicSelection!.Music;
        int[] party = host.Events.Scene.Party.ToArray();
        // A module-only fixture isolates the issuing callback from installed actor scripts.
        host.Events.LoadField(session.State.FieldId, WorldJumpProgram(music), [], new FieldTransition(session.State.FieldId, 0, 0, 0, 0), nativeRuntimeProfile: host.Events.NativeRuntimeProfile);
        host.Events.Step();
        Assert.Null(host.Events.LastFault);
        Assert.Equal(SceneKind.Field, session.State.Scene);
        Assert.Equal(1, host.Events.Scene.NativeFieldLoadVersion);
        session.SetEventState(null);
        session.SetDepartingFieldState(null);

        if (direct)
        {
            host.Events.Step();
        }
        else
        {
            Assert.True(host.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        }

        Assert.Null(host.Events.LastFault);
        Assert.Equal(SceneKind.WorldMap, session.State.Scene);
        Assert.Equal(new WorldTransition(61, 32, -1), session.State.WorldArrival);
        Assert.NotNull(host.WorldScene);
        Assert.NotNull(host.Events.Scene.WorldMusicLoad);
        Assert.Single(host.Events.Scene.WorldJumpWaits);
        JsonElement completed = host.Events.Capture();
        Assert.False(host.EnsureField());
        Assert.Null(session.State.DepartingFieldState);
        Assert.True(JsonElement.DeepEquals(completed, Assert.IsType<JsonElement>(session.State.EventState)));
        Assert.Equal(party, session.State.EventState.Value.GetProperty("Scene").GetProperty("Party").EnumerateArray().Select(value => value.GetInt32()));
    }

    [Fact]
    public void PreparedDeparturePublishesCompletedFieldVmBeforeWorldContinuationAndColdRestore()
    {
        string directory = TestResources.GameDirectory;
        string source = TestResources.FixturePath("WorldDepartureCaptureTests.Source");
        byte[] original = File.ReadAllBytes(source);
        GameSession session = new(GameSession.Load(source));
        using GameWorld host = new(session, directory, validateWorldPresentation: false);
        host.EnsureField();
        long beforeFrame = host.Events.Frame;
        host.Events.Step();
        Assert.Null(host.Events.LastFault);
        Assert.Equal(beforeFrame + 1, host.Events.Frame);
        Assert.Equal(SceneKind.Field, session.State.Scene);
        int[] party = host.Events.Scene.Party.ToArray();
        Assert.Equal(new[] { 0, 4, 3 }, party);

        NativeWorldControlState controls = session.WorldControls!;
        NativeWorldControlState loader = controls.WithWorldEntryMenuFlag();
        NativeWorldTransitionRequest request = new(1, host.Events.FieldId, host.Events.Frame, NativeWorldTransitionOrigin.Script, new WorldTransition(61, 32, -1), controls, loader, loader);
        Action commit = host.Events.PrepareWorldTransition!(request)!;
        commit();
        Assert.NotNull(host.WorldScene);
        Assert.NotNull(host.Events.Scene.WorldMusicLoad);
        // The host must capture after the issuing callback finishes, including its last VM writes.
        host.Events.Scene.Names[0] = "Departure";
        JsonElement completed = host.Events.Capture();
        Assert.False(host.EnsureField());
        Assert.Null(session.State.DepartingFieldState);
        JsonElement retained = Assert.IsType<JsonElement>(session.State.EventState);
        Assert.True(JsonElement.DeepEquals(completed, retained));
        Assert.Equal(party, retained.GetProperty("Scene").GetProperty("Party").EnumerateArray().Select(value => value.GetInt32()));

        // Further world checks must not replace the retained VM with later field-only mutations.
        host.Events.Scene.Names[0] = "Later";
        Assert.False(host.EnsureField());
        Assert.True(JsonElement.DeepEquals(retained, Assert.IsType<JsonElement>(session.State.EventState)));
        host.Events.Scene.Names[0] = "Departure";

        string saved = Path.Combine(Path.GetTempPath(), "ff8-world-departure-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(saved);
            GameSession restoredSession = new(GameSession.Load(saved));
            using GameWorld restored = new(restoredSession, directory, validateWorldPresentation: false);
            restored.EnsureField();
            Assert.True(JsonElement.DeepEquals(completed, restored.Events.Capture()));
            Assert.Equal(party, restored.Events.Scene.Party);
            PlayerAction next = new(PlayerActionKind.Wait);
            Assert.Equal(host.Apply(next), restored.Apply(next));
            Assert.Equal(JsonSerializer.Serialize(session.State.World), JsonSerializer.Serialize(restoredSession.State.World));
            Assert.Equal(JsonSerializer.Serialize(session.State.LimitProgress), JsonSerializer.Serialize(restoredSession.State.LimitProgress));
        }
        finally
        {
            File.Delete(saved);
        }
        Assert.Equal(original, File.ReadAllBytes(source));
    }

    private static byte[] WorldJumpProgram(byte music)
    {
        (Jsm.Opcode Opcode, int Parameter)[] operations =
        [
            (Jsm.Opcode.LBL, 0),
            (Jsm.Opcode.PSHN_L, music),
            (Jsm.Opcode.POPM_B, 268),
            (Jsm.Opcode.RET, 0),
            (Jsm.Opcode.LBL, 1),
            (Jsm.Opcode.PSHN_L, 61),
            (Jsm.Opcode.PSHN_L, 32),
            (Jsm.Opcode.PSHN_L, -1),
            (Jsm.Opcode.WORLDMAPJUMP, 0),
            (Jsm.Opcode.RET, 0)
        ];
        const int operationsOffset = 16;
        byte[] bytes = new byte[operationsOffset + operations.Length * 4];
        bytes[2] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), operationsOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), checked((ushort)operations.Length));
        for (int index = 0; index < operations.Length; index++)
        {
            (Jsm.Opcode opcode, int parameter) = operations[index];
            int encoded = parameter != 0 || opcode == Jsm.Opcode.PSHN_L ? (int)opcode << 24 | parameter & 0xFFFFFF : (int)opcode;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(operationsOffset + index * 4), encoded);
        }
        return bytes;
    }
}
