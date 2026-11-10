using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class WorldRuntimeTests
{
    public static void Run()
    {
        NativeModuleAudio();
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP installed world execution: MEMORIA_FF7_GAME_DIR is not configured.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        byte[] persistent = new byte[1280];
        persistent[0] = 152;
        persistent[29] = 255;
        persistent[31] = 255;
        using Ff7WorldRuntime original = new(installation, persistent, 1, new byte[] { 0, 1, 2 });
        List<FieldAudioCommand> audio = [];
        original.AudioRequested = audio.Add;
        Check(original.Process.Tick == 0 && original.Process.Entities.Length == 0, "Construction must not silently execute the destination.");
        original.Advance();
        Check(audio.Count == 7 && audio[0].Operation == 0xF1 && audio[5] == new FieldAudioCommand(0xC0, 127) && audio[6] == new FieldAudioCommand(0x18, 13, 4) && original.Process.Audio is { Initialized: true, Song: 1, Publications: 7 }, "Native world entry must reset global sound/tempo and select its original theme exactly once.");
        Ff7WorldEntity cloud = original.Process.Entities.Single(value => value.Model == 0);
        Check(cloud.X == 185636 && cloud.Z == 123325 && cloud.Direction == 0 && original.ControlledModel == 0, "Original entry must author the outside-Midgar placement through world EV.");
        Check(original.Process.Vm.Models.Length == 8 && original.Process.Lights.Length == 13 && !original.MovementEnabled, "Original system/onload must create native model/light owners and retain tutorial input lock.");
        Check(original.Process.Vm.Tick == 2 && original.Process.Tick == 1 && original.ReadPersistentByte(31) == 252, "Native initialization must run its separate scheduler pass and clear the original world menu lock bits.");
        for (int i = 0; i < 28; i++)
        {
            original.Advance();
        }
        Check(original.Process.Tick == 29 && original.Process.Dialogue is { Text: 53, Completed: false } && original.Process.Window is { X: 40, Y: 8, Width: 219, Height: 73 }, "Original model onload must reach its tutorial at callback29 with native minimum-eight window placement.");
        Ff7WorldProcess saved = JsonSerializer.Deserialize<Ff7WorldProcess>(JsonSerializer.Serialize(original.Process))!;
        using Ff7WorldRuntime restored = new(installation, original.Machine.CopyPersistent(), 1, new byte[] { 0, 1, 2 });
        List<FieldAudioCommand> restoredAudio = [];
        restored.AudioRequested = restoredAudio.Add;
        restored.Restore(saved);
        Check(restoredAudio.Count == 0, "Cold world restore must not replay module audio initialization or restart music.");
        Check(JsonSerializer.Serialize(original.Process) == JsonSerializer.Serialize(restored.Process), "Cold world restore must preserve native and presentation owners without executing scripts.");
        bool confirmed = false;
        for (int i = 0; i < 600 && !original.MovementEnabled; i++)
        {
            if (original.Process.Dialogue is { Completed: false, AwaitingInput: true } dialogue)
            {
                original.CompleteDialogue(dialogue.RequestId, dialogue.Selected);
                restored.CompleteDialogue(dialogue.RequestId, dialogue.Selected);
                confirmed = true;
            }
            original.Advance();
            restored.Advance();
            Check(JsonSerializer.Serialize(original.Process) == JsonSerializer.Serialize(restored.Process), "Cold world continuation must preserve native script/window timing and input.");
        }
        Check(confirmed && original.MovementEnabled && original.Process.Dialogue is { Completed: true }, "Only native tutorial completion may release world controls.");
        Check(original.CanSave && original.Process.Entities.Single(value => value.Model == original.ControlledModel).TerrainResolved, "The original terrain query and completed tutorial must establish the native world menu/save permission.");
        Ff7WorldProcess completed = original.Process;
        restored.Restore(completed);
        Check(restored.CanSave, "Native save menu must be eligible only after its control, terrain, main-state, fade and original mask checks.");
        Ff7WorldEntity beforeMove = original.Process.Entities.Single(value => value.Model == original.ControlledModel);
        original.SetMovement(1, 0, true);
        restored.SetMovement(1, 0, true);
        original.Advance();
        restored.Advance();
        Ff7WorldEntity afterCardinal = original.Process.Entities.Single(value => value.Model == original.ControlledModel);
        Check(afterCardinal.X == beforeMove.X + 60 && afterCardinal.Z == beforeMove.Z, "Physical cardinal input must traverse the original terrain using its native60-unit movement.");
        Check(JsonSerializer.Serialize(original.Process) == JsonSerializer.Serialize(restored.Process), "Physical cardinal movement must preserve cold native terrain/collision continuation.");
        original.SetMovement(1, 1, true);
        restored.SetMovement(1, 1, true);
        original.Advance();
        restored.Advance();
        Ff7WorldEntity afterDiagonal = original.Process.Entities.Single(value => value.Model == original.ControlledModel);
        Check(afterDiagonal.X == afterCardinal.X + 45 && afterDiagonal.Z == afterCardinal.Z + 45, "Native diagonal input must preserve its authored45/45 displacement.");
        Check(JsonSerializer.Serialize(original.Process) == JsonSerializer.Serialize(restored.Process), "Physical diagonal movement must preserve cold native terrain/collision continuation.");
        Ff7WorldProcess ready = restored.Process;
        restored.Restore(ready with { Entities = ready.Entities.Select(value => value with { Terrain = 14 }).ToArray() });
        Check(!restored.CanSave, "The native world menu cannot open on bridge terrain.");
        restored.Restore(saved with { Camera = null });
        Check(restored.Process.Camera == new Ff7WorldCameraState(), "Absent presentation-only camera metadata must recover its native reset default.");
        bool rejected = false;
        try
        {
            restored.Restore(saved with { Entities = saved.Entities[..^1] });
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Check(rejected, "World restore must reject differing entity/script ownership.");
        string beforeInvalid = JsonSerializer.Serialize(restored.Process);
        try
        {
            restored.Restore(saved with { Dialogue = saved.Dialogue! with { Text = int.MaxValue } });
        }
        catch (InvalidDataException)
        {
        }
        Check(JsonSerializer.Serialize(restored.Process) == beforeInvalid, "Invalid cold dialogue state must not partially replace the existing world.");
        Ff7Dialogue current = restored.Process.Dialogue!;
        restored.UpdateDialogue(current with { Presentation = current.Presentation! with { ConfirmPending = true } });
        Check(restored.Process.Dialogue!.Presentation!.ConfirmPending && restored.Process.Vm.ProgramIdentity == saved.Vm.ProgramIdentity, "Shared pager updates must preserve the world script owner and saved request identity.");
        string beforeBadUpdate = JsonSerializer.Serialize(restored.Process);
        try
        {
            restored.UpdateDialogue(current with { RequestId = current.RequestId + 1 });
            throw new InvalidOperationException("World dialogue accepted a different request owner.");
        }
        catch (InvalidDataException)
        {
        }
        Check(JsonSerializer.Serialize(restored.Process) == beforeBadUpdate, "A corrupt shared pager update must not mutate the recorded native dialogue.");
        Console.WriteLine("PASS installed original world system/onload/on_tick, source placement, tutorial, cold continuation and corruption");
    }

    private static void NativeModuleAudio()
    {
        Ff7FieldResources field = new("module-audio-fixture", null!, null!, null!, [], [], null!, null!, null!);
        Ff7FieldProcess process = new() { NativeMusicTrack = 13, MusicTicks = 91, MusicVolume = 47, MusicVolumeFade = new(0, -30) };
        Ff7FieldHost host = new(field, (_, _) => 1, process);
        List<FieldAudioCommand> publications = [];
        host.AudioRequested = publications.Add;
        host.SubmitNativeAudio(new(0x18, 13, 4));
        Check(host.Process.NativeMusicTrack == 13 && host.Process.MusicTicks == 91 && host.Process.MusicVolume == 127 && host.Process.MusicVolumeFade is null, "The original short same-track transition must preserve playback position and complete its volume change synchronously.");
        host.SubmitNativeAudio(new(0x18, 71, 4));
        Check(host.Process.NativeMusicTrack == 71 && host.Process.MusicTicks == 0 && host.Process.Tick == process.Tick, "A native module song change must start the selected global track once without advancing field actors or scripts.");
        host.AdvanceNativeMusicClock();
        Check(host.Process.MusicTicks == 1 && host.Process.Tick == process.Tick, "The shared module music clock must not advance frozen field simulation.");
        Ff7FieldHost cold = new(field, (_, _) => 1, JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        cold.SubmitNativeAudio(new(0x14, 71, 4));
        Check(cold.Process.MusicTicks == 1, "Native immediate same-track publication must preserve cold playback continuation.");
        string before = JsonSerializer.Serialize(cold.Capture());
        try
        {
            cold.SubmitNativeAudio(new(0x18, 13, 10));
            throw new InvalidOperationException("An unproved pending music transition was accepted.");
        }
        catch (NotSupportedException)
        {
        }
        Check(JsonSerializer.Serialize(cold.Capture()) == before, "An unsupported long music transition must fail before changing global audio.");
        cold.SubmitNativeAudio(new(0xF1));
        Check(cold.Process.SoundChannelStoppedByFade.All(value => value) && cold.Process.SoundChannelLoopIds.All(value => value == 0), "Native module sound stop must retire all existing resident playback owners.");
        Check(publications.Count == 2, "Each native module audio command must publish once through the existing FFVII audio owner.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
