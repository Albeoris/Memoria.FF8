using System.Buffers.Binary;
using System.Text.Json;
using System.Security.Cryptography;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeBattleCameraTests
{
    public static void Run()
    {
        MotionCallbacks();
        OutputSelection();
        Check(Ff7NativeBattleCamera.SelectTrack(1, 3) == 1 && Ff7NativeBattleCamera.SelectTrack(3, 2) == 3 && Ff7NativeBattleCamera.SelectTrack(0, 1) == 1 && Ff7NativeBattleCamera.SelectTrack(2, 255) == 2, "Native camera track selection must use counter low bits and preserve either special value three.");
        Ff7BattleAttack attack = Ff7BattleAttack.Read(new byte[Ff7BattleAttack.RecordSize]) with { SingleTargetCamera = 12, MultipleTargetCamera = 36 };
        Check(Ff7NativeBattleCamera.SelectAction(attack, ushort.MaxValue, false, false) == new Ff7BattleActionCameras(12, 36), "Ordinary camera selection lost independent attack cameras.");
        Check(Ff7NativeBattleCamera.SelectAction(attack, 210, false, false) == new Ff7BattleActionCameras(210, 210), "Enemy camera override must replace both cameras.");
        Check(Ff7NativeBattleCamera.SelectAction(attack, 210, true, false) == new Ff7BattleActionCameras(12, 12), "Limit single camera must take precedence over the enemy override.");
        Check(Ff7NativeBattleCamera.SelectAction(attack, 210, true, true) == new Ff7BattleActionCameras(36, 36), "Limit multiple-target camera must replace both cameras.");
        Check(Ff7NativeBattleCamera.SelectAction(attack with { SingleTargetCamera = ushort.MaxValue }, 210, true, false) == new Ff7BattleActionCameras(ushort.MaxValue, 36), "The native camera sentinel must retain the original pair after Limit selection.");

        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP installed battle camera resources: MEMORIA_FF7_GAME_DIR is unset.");
            return;
        }

        Ff7Installation installation = Ff7Installation.Discover(root);
        Ff7BattleCameraMath math = Ff7PcExecutableData.LoadBattleCameraMath(installation);
        Check(math.DegreesPerAngleUnit == 0.087890625f && BitConverter.DoubleToInt64Bits(math.RadiansPerDegree) == 0x3F91DF46A2529E84 && math.Sine(1024) == 4096 && math.Cosine(2048) == -4096 && math.Angle(0, 100) == 0 && math.Angle(100, 0) == 1024, "Original camera angle conversion constants changed their native consumers.");
        Ff7BattleCameraPoint joint = new(100, 200, 300);
        Ff7BattleCameraPoint modelOffset = new(300, -829, -1234);
        Ff7BattleCameraModelParameters cloud = new(384, 306, 384, 300);
        Check(Ff7NativeBattleCameraGeometry.Position(joint, modelOffset, cloud, 0x1000, false, 0, math) == new Ff7BattleCameraPoint(550, -791, -1551), "Native camera offsets lost their unsigned per-axis scale or arithmetic shift.");
        Check(Ff7NativeBattleCameraGeometry.Position(joint, modelOffset, cloud, 0xFFF, false, 0, math) == new Ff7BattleCameraPoint(400, -629, -934), "The native scale-control threshold must halve each axis with a minimum of 100 hex.");
        Check(Ff7NativeBattleCameraGeometry.Position(joint, new(0, 0, 1024), new(256, 256, 256, 0), 0x1000, true, 1024, math) == new Ff7BattleCameraPoint(1124, 200, 300), "Native inverted camera offsets must rotate around the selected native actor facing.");
        Check(Ff7NativeBattleCameraGeometry.Focus(joint, new(10, 20, 30), 500, 0, true) == new Ff7BattleCameraPoint(110, 720, 330), "Immediate focus must retain its signed model height adjustment.");
        Check(Ff7NativeBattleCameraGeometry.Focus(joint, new(10, 20, 30), 500, 1, true) == new Ff7BattleCameraPoint(110, 220, 330), "Focus motion mode one with positive model height must skip the extra height.");
        Check(Ff7NativeBattleCameraGeometry.Focus(joint, new(10, 20, 30), 500, 0, false) == new Ff7BattleCameraPoint(110, 220, 330), "A focus follow callback must retain its saved Y after the overwritten height computation.");
        IReadOnlyList<Ff7BattleCameraEntryScript> entryScripts = Ff7PcExecutableData.LoadBattleCameraEntryScripts(installation);
        Check(entryScripts.Count == 103 && entryScripts.All(script => !script.Position.IsEmpty && !script.Focus.IsEmpty), "Original battle entry camera pointer tables must retain all 103 pairs.");
        Check(entryScripts[0].Position.Span.SequenceEqual(new byte[] { 0xF9, 0xE8, 3, 0, 0x83, 0xE8, 3, 0xE2, 60, 0xF5, 60, 0xF4, 0xFF, 0, 0, 0 }), "Original entry camera zero must retain its signed Y point, 60-callback motion and wait.");
        Check(entryScripts[88].Position.Length == 224 && entryScripts[88].Focus.Length == 208, "Long original entry scripts must not be truncated to ordinary 16-byte rows.");
        EntryCallbacks(entryScripts, math);
        using LgpArchive actors = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        foreach ((string name, Ff7BattleCameraModelParameters parameters) in new[]
        {
            ("rtab", new Ff7BattleCameraModelParameters(384, 306, 384, 300)),
            ("sbab", new Ff7BattleCameraModelParameters(384, 306, 384, 300)),
            ("ruab", new Ff7BattleCameraModelParameters(384, 306, 384, 300)),
            ("aeab", new Ff7BattleCameraModelParameters(512, 370, 512, 500)),
            ("arab", new Ff7BattleCameraModelParameters(370, 306, 370, 300)),
            ("atab", new Ff7BattleCameraModelParameters(370, 306, 370, 200)),
            ("beab", new Ff7BattleCameraModelParameters(370, 306, 370, 200))
        })
        {
            Check(Ff7BattleCameraModelParameters.Read(actors.ReadEntry(actors.Find(name).Single())) == parameters, "Native camera offsets must retain distinct original actor axis scales and focus heights.");
        }
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        foreach ((int item, ushort camera) in new (int, ushort)[] { (0, 42), (3, 44), (7, 45), (8, 42), (20, ushort.MaxValue) })
        {
            Check(Ff7NativeBattleCamera.SelectItem(kernel[4].Data.Span.Slice(item * 28, 28)) == new Ff7BattleActionCameras(camera, camera), "Original item camera must use only its one WORD at +8, including FFFF.");
        }
        Ff7ResourceRole[] roles = [Ff7ResourceRole.BattleCamera0, Ff7ResourceRole.BattleCamera1, Ff7ResourceRole.BattleCamera2];
        int[] counts = [394, 375, 374];
        for (int bank = 0; bank < roles.Length; bank++)
        {
            byte[] bytes = File.ReadAllBytes(installation.GetRequired(roles[bank]).Path);
            Ff7BattleCameraArchive archive = Ff7BattleCameraArchive.Read(bytes);
            Check(archive.CameraCount == counts[bank], "Original camera count must use DWORD entries and three variants.");
            for (int camera = 0; camera < archive.CameraCount; camera++)
            {
                foreach (Ff7BattleCameraComponent component in Enum.GetValues<Ff7BattleCameraComponent>())
                {
                    for (int track = 0; track < 3; track++)
                    {
                        ReadOnlyMemory<byte> script = archive.Script(camera, track, component);
                        int table = checked((int)(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan((int)component * 4)) - 0x801A0000));
                        int offset = checked((int)(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(table + (camera * 3 + track) * 4)) - 0x801A0000));
                        Check(!script.IsEmpty && script.Span.SequenceEqual(bytes.AsSpan(offset, script.Length)), "A camera track changed its relocated pointer or script bytes.");
                    }
                }
            }

            for (int track = 0; track < 3; track++)
            {
                foreach (Ff7BattleCameraComponent component in Enum.GetValues<Ff7BattleCameraComponent>())
                {
                    Check(!archive.SpecialScript(track, component).IsEmpty, "The special tracks after the main tables must remain readable.");
                }
            }

            byte[] corrupt = (byte[])bytes.Clone();
            BinaryPrimitives.WriteUInt32LittleEndian(corrupt, 0x801A0000);
            Throws<InvalidDataException>(() => Ff7BattleCameraArchive.Read(corrupt));
            int focus = checked((int)(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)) - 0x801A0000));
            BinaryPrimitives.WriteUInt32LittleEndian(corrupt.AsSpan(0), BinaryPrimitives.ReadUInt32LittleEndian(bytes));
            BinaryPrimitives.WriteUInt32LittleEndian(corrupt.AsSpan(focus), 0x801A0000 + (uint)focus);
            Throws<InvalidDataException>(() => Ff7BattleCameraArchive.Read(corrupt));
            Throws<ArgumentOutOfRangeException>(() => archive.Script(archive.CameraCount, 0, Ff7BattleCameraComponent.Position));
            Throws<ArgumentOutOfRangeException>(() => archive.Script(0, 3, Ff7BattleCameraComponent.Position));
        }

        Ff7BattleCameraArchive normal = Ff7BattleCameraResources.Load(installation, 0);
        Ff7BattleCameraScriptResources resources = Ff7BattleCameraResources.LoadScripts(installation, 0);
        Check(resources.Script(-1, 0, 0, Ff7BattleCameraComponent.Position).Span.SequenceEqual(new byte[] { 0xFF }) && resources.Script(-2, 0, 1, Ff7BattleCameraComponent.Focus).Span.SequenceEqual(entryScripts[0].Focus.Span) && resources.Script(-3, 0, 2, Ff7BattleCameraComponent.Position).Span.SequenceEqual(normal.SpecialScript(2, Ff7BattleCameraComponent.Position).Span), "Native negative camera IDs must resolve idle, executable entry and archive special owners independently.");
        Throws<InvalidDataException>(() => resources.Script(-2, 103, 0, Ff7BattleCameraComponent.Position));
        OpeningCameraCallbacks(installation, actors, math);
        OpeningEntryCallbacks(installation, math);
        Check(normal.Script(6, 0, Ff7BattleCameraComponent.Position).Span[..8].SequenceEqual(new byte[] { 0xDC, 0xF0, 0, 0x2C, 1, 0xC3, 0xFC, 0x2E }), "Original camera 6 position operands changed.");
        Check(normal.Script(50, 1, Ff7BattleCameraComponent.Position).Span.SequenceEqual(normal.Script(50, 2, Ff7BattleCameraComponent.Position).Span), "Aliased camera track pointers changed their shared bytes.");
        Check(Ff7BattleCameraResources.Load(installation, 1).CameraCount == 394 && Ff7BattleCameraResources.Load(installation, 2).CameraCount == 375 && Enumerable.Range(3, 5).All(layout => Ff7BattleCameraResources.Load(installation, (byte)layout).CameraCount == 374) && Ff7BattleCameraResources.Load(installation, 8).CameraCount == 394, "Native formation layout must select its original camera bank.");
        Throws<NotSupportedException>(() => Ff7BattleCameraResources.Load(installation, 9));
    }

    private static void MotionCallbacks()
    {
        Ff7BattleCameraPoint point = new(5, 100, -100);
        Ff7BattleCameraMotion? motion = new(point, new(-8, -27, 101), 5);
        Ff7BattleCameraPoint[] expected = [new(2, 74, -60), new(-1, 48, -20), new(-4, 22, 20), new(-7, -4, 60), new(-8, -27, 101)];
        for (int callback = 0; callback < expected.Length; callback++)
        {
            Ff7BattleCameraMotion cold = JsonSerializer.Deserialize<Ff7BattleCameraMotion>(JsonSerializer.Serialize(motion))!;
            Ff7BattleCameraMotionUpdate update = Ff7NativeBattleCameraMotion.Advance(motion!, point, true, _ => throw new InvalidOperationException("Linear camera motion consumed cosine."));
            Check(update == Ff7NativeBattleCameraMotion.Advance(cold, point, true, _ => throw new InvalidOperationException("Restored linear camera consumed cosine.")), "Cold camera motion changed the next callback.");
            Check(update.Point == expected[callback], "Native linear camera motion changed signed speed, per-step rounding or final snap.");
            motion = update.Motion;
            point = update.Point;
        }
        Check(motion is null, "Native camera motion must retire on its final callback.");

        point = new(1000, 2000, -3000);
        motion = new(point, new(-1000, -2000, 3000), 4);
        expected = [new(1000, 2000, -3000), new(708, 1415, -2122), new(0, 0, 0), new(-1000, -2000, 3000)];
        int Cosine(int angle) => angle switch
        {
            0x800 => -4096,
            0xA00 => -2896,
            0xC00 => 0,
            _ => throw new InvalidOperationException("Camera easing sampled an unexpected native angle.")
        };
        for (int callback = 0; callback < expected.Length; callback++)
        {
            Ff7BattleCameraMotion cold = JsonSerializer.Deserialize<Ff7BattleCameraMotion>(JsonSerializer.Serialize(motion))!;
            Ff7BattleCameraMotionUpdate update = Ff7NativeBattleCameraMotion.Advance(motion!, point, false, Cosine);
            Check(update == Ff7NativeBattleCameraMotion.Advance(cold, point, false, Cosine), "Cold easing changed the next native point or retained duration.");
            Check(update.Point == expected[callback], "Native camera easing changed its initial hold, signed truncation or final snap.");
            motion = update.Motion;
            point = update.Point;
        }
        Check(motion is null, "Native easing must retire on its final callback.");
        Ff7BattleCameraMotion one = new(new(1, 2, 3), new(4, 5, 6), 1);
        Check(Ff7NativeBattleCameraMotion.Advance(one, one.Start, false, _ => throw new InvalidOperationException("A one-callback camera sampled cosine.")).Point == one.Destination, "A one-callback motion must immediately snap to its destination.");
    }

    private static void OutputSelection()
    {
        SceneCameraPlacement idle = new(new(1000, 1001, 1002), new(1003, 1004, 1005));
        SceneCameraPlacement displayed = new(new(101, -101, 202), new(-202, 303, -303));
        SceneCameraPlacement selected = new(new(20, 21, 22), new(24, 25, 26));
        SceneCameraPlacement smooth = new(new(550, 450, 602), new(400, 653, 351));
        Ff7BattleCameraOutputInputs input = new(1, 0, 0, 1, 0);
        (short Camera, Ff7BattleCameraOutputInputs Input, bool Returning, SceneCameraPlacement Expected, bool ExpectedReturning, ushort Options, sbyte Active)[] cases =
        [
            (6, input, false, selected, true, 0, 0),
            (6, input with { MenuPhase = 0 }, true, smooth, true, 0, 0),
            (6, input with { MenuPhase = 0 }, false, displayed, false, 0, 0),
            (6, input with { Options = 256 }, false, idle, false, 256, 0),
            (59, input with { Options = 256, PrimaryQueueState = 1 }, true, smooth, true, 256, 0),
            (-1, input with { Options = 256 }, false, idle, false, 256, 0),
            (-2, input with { Options = 256 }, false, selected, true, 256, 0),
            (6, input with { BattlePhase = 100 }, false, selected, true, 0, 0),
            (6, input with { BattlePhase = 100, Options = 256 }, false, idle, false, 256, 0),
            (6, input with { MenuPhase = 0 }, false, selected, true, 0, 1),
            (6, input with { PrimaryQueueState = 1 }, true, smooth, true, 0, 0),
            (6, input with { Location = 78, Options = 768, PrimaryQueueState = 1 }, false, selected, true, 0, 0)
        ];
        foreach (var test in cases)
        {
            Ff7BattleCameraScriptState state = Ff7NativeBattleCameraScripts.Create(test.Camera, 0, idle) with { ReturningToIdle = test.Returning, SelectedTrack = 2, ActiveAction = test.Active };
            for (int track = 0; track < 4; track++)
            {
                state.Tracks[track] = state.Tracks[track] with { Point = new((short)(track * 10), (short)(track * 10 + 1), (short)(track * 10 + 2)) };
                state.Tracks[track + 4] = state.Tracks[track + 4] with { Point = new((short)(track * 10 + 4), (short)(track * 10 + 5), (short)(track * 10 + 6)) };
            }
            Ff7BattleCameraOutput output = Ff7NativeBattleCameraOutput.Select(state, test.Input, [idle, idle, idle], displayed);
            Check(output.Camera == test.Expected && output.State.ReturningToIdle == test.ExpectedReturning && output.Options == test.Options, "Native camera output selection changed its idle blend, active track, fixed option or special arena branch.");
            Ff7BattleCameraScriptState cold = JsonSerializer.Deserialize<Ff7BattleCameraScriptState>(JsonSerializer.Serialize(state))!;
            Check(JsonSerializer.Serialize(output) == JsonSerializer.Serialize(Ff7NativeBattleCameraOutput.Select(cold, test.Input, [idle, idle, idle], displayed)), "Cold camera output changed its native return-to-idle owner.");
        }
    }

    private static void EntryCallbacks(IReadOnlyList<Ff7BattleCameraEntryScript> entryScripts, Ff7BattleCameraMath math)
    {
        SceneCameraPlacement idle = new(new(1000, 2000, 3000), new(-1000, -2000, -3000));
        SceneCameraPlacement[] placements = [idle, idle, idle];
        Ff7BattleCameraScriptState state = Ff7NativeBattleCameraScripts.Create(-2, 0, idle);
        ReadOnlyMemory<byte> Script(short camera, byte entry, int track, Ff7BattleCameraComponent component)
        {
            Check(camera == -2 && entry == 0 && (uint)track < 3, "Entry camera script owner changed its resource identity.");
            return component == Ff7BattleCameraComponent.Position ? entryScripts[entry].Position : entryScripts[entry].Focus;
        }
        int Cosine(int angle) => math.Cosine(angle);
        Dictionary<int, (Ff7BattleCameraPoint Position, Ff7BattleCameraPoint Focus, ushort Pc, ushort Wait)> expected = new()
        {
            [0] = (new(1000, -32000, 1000), new(-1000, -1000, -1000), 11, 59),
            [1] = (new(1000, -31976, 1001), new(-1000, -1000, -1001), 11, 58),
            [2] = (new(1000, -31905, 1005), new(-1000, -1002, -1005), 11, 57),
            [3] = (new(1000, -31789, 1012), new(-1000, -1006, -1012), 11, 56),
            [4] = (new(1000, -31631, 1021), new(-1000, -1010, -1021), 11, 55),
            [58] = (new(1000, 1904, 2994), new(-1000, -1997, -2994), 11, 1),
            [59] = (new(1000, 2000, 3000), new(-1000, -2000, -3000), 11, 0),
            [60] = (new(1000, 2000, 3000), new(-1000, -2000, -3000), 12, 0)
        };
        for (int callback = 0; callback < 62; callback++)
        {
            Ff7NativeBattleCameraScripts.Validate(state, Script);
            Ff7BattleCameraScriptState cold = JsonSerializer.Deserialize<Ff7BattleCameraScriptState>(JsonSerializer.Serialize(state))!;
            Check(ReferenceEquals(state, Ff7NativeBattleCameraScripts.Advance(state, false, false, Script, placements, idle, Cosine).State), "A non-live camera callback advanced scripts or motion.");
            Check(ReferenceEquals(state, Ff7NativeBattleCameraScripts.Advance(state, true, true, Script, placements, idle, Cosine).State), "The native camera suspension owner failed to freeze scripts and motion.");
            Ff7BattleCameraScriptState update = Ff7NativeBattleCameraScripts.Advance(state, true, false, Script, placements, idle, Cosine).State;
            Check(JsonSerializer.Serialize(update) == JsonSerializer.Serialize(Ff7NativeBattleCameraScripts.Advance(cold, true, false, Script, placements, idle, Cosine).State), "Cold entry camera changed its next original callback.");
            if (expected.TryGetValue(callback, out var sample))
            {
                for (int track = 0; track < 3; track++)
                {
                    Check(update.Tracks[track].Point == sample.Position && update.Tracks[track + 4].Point == sample.Focus && update.Tracks[track].Pc == sample.Pc && update.Tracks[track].Wait == sample.Wait && update.Tracks[track + 4].Pc == sample.Pc && update.Tracks[track + 4].Wait == sample.Wait, "Original entry camera changed position, focus, per-track WORD PC or wait timing.");
                }
            }
            state = update;
        }
        Check(state.Callbacks.All(callback => callback is null), "Entry camera retained a completed motion callback.");
        Check(ReferenceEquals(state, Ff7NativeBattleCameraScripts.Start(state, -4)), "Native camera sentinel -4 must preserve all existing owners.");
        Ff7BattleCameraScriptState restarted = Ff7NativeBattleCameraScripts.Start(state, 6);
        Check(restarted.Tracks.All(track => track.Pc == 255) && restarted.Callbacks.All(callback => callback is null) && restarted.Tracks[0].Point == state.Tracks[0].Point, "A new camera must invalidate eight PCs and clear the callback pool while retaining coordinates.");
        Ff7BattleCameraScriptState counter = Ff7NativeBattleCameraScripts.Create(-2, 0, idle, 255);
        Check(Ff7NativeBattleCameraScripts.AdvanceOuterCallback(counter).PresentationCounter == 0, "The native outer presentation counter must wrap independently of the camera live gate.");
        Ff7BattleCameraScriptState copy = Ff7NativeBattleCameraScripts.Copy(state);
        Check(!ReferenceEquals(copy.Tracks, state.Tracks) && !ReferenceEquals(copy.Callbacks, state.Callbacks), "Camera snapshots must not alias their mutable owner arrays.");
        Throws<InvalidDataException>(() => Ff7NativeBattleCameraScripts.Validate(state with { Tracks = [] }, Script));
        Throws<InvalidDataException>(() => Ff7NativeBattleCameraScripts.Validate(state with { SelectedTrack = -1 }, Script));
        Throws<InvalidDataException>(() => Ff7NativeBattleCameraScripts.Validate(state with { ActiveAction = 2 }, Script));
        copy.Tracks[0] = copy.Tracks[0] with { Pc = 254 };
        Throws<InvalidDataException>(() => Ff7NativeBattleCameraScripts.Validate(copy, Script));
    }

    private static void OpeningCameraCallbacks(Ff7Installation installation, LgpArchive models, Ff7BattleCameraMath math)
    {
        Dictionary<int, (int Frames, string Hash)> original = new()
        {
            [0] = (79, "A7E254AE6EB486DA00EBF3094389B981B52A6EF5C2BF07A48B21BE24C82DEE5E"),
            [6] = (74, "7C442E6FB12E28579C55BB2F7C89BCFA4212946CEF2987D5855F9DE53D470FF4"),
            [8] = (59, "C3D7B1F506DF46373CBD19E4D61907A36D5E7FEB916718427F7B1B68AF454EBB"),
            [42] = (63, "D954AFDF8433243546707D00D5D3559F8483F62D802E280A0E02EA5B40903EA1"),
            [50] = (75, "C3834FFCEDD263079A8EE1A40E5E2BA978C137216303476681FA16C0FE853EEF"),
            [62] = (85, "CB0A89004D9D50F403A97225468E002DE2969000BC625F6F805396F9C0C86AC4"),
            [70] = (74, "CF7F6CD2AB1AA96149692008CBDB4D3CDB8BB441D03DF0B1892CCD8C63D1FE57"),
            [210] = (51, "7848D22536E172D1AE0F1062E67779C11BFE0D705BCE6CF7E2757B5C5084DE37"),
            [272] = (51, "FBB8FF023B959C8D6441F1B5761911DF755919F9A9E7560C655302E0FE7AABC8"),
            [350] = (54, "768A19F467BC5E132A7AE8F51A306D5329ED7BB049FF5FA18E292CDB9AD77F87")
        };
        int[] cameras = [0, 6, 7, 8, 36, 40, 42, 44, 45, 50, 62, 70, 210, 211, 212, 213, 214, 215, 216, 217, 218, 219, 240, 250, 261, 267, 268, 272, 275, 350];
        SceneCameraPlacement idle = new(new(2500, -1500, 4000), new(0, -300, 0));
        SceneCameraPlacement[] placements = [idle, idle, idle];
        BattleActionBank bank = BattleActionBank.Read(models.ReadEntry(models.Find("rtab").Single()));
        Ff7BattleCameraModelParameters parameters = Ff7BattleCameraModelParameters.Read(models.ReadEntry(models.Find("rtab").Single()));
        Ff7BattleCameraActors input = new(0, 4, (_, selector) => bank.JointSelectors.Span[selector], (actor, bone) => new((short)(actor * 100 + bone), (short)(-bone * 10), (short)(actor * -200)), _ => new(parameters, 0x1000, 0, 2048, 0), () => new(0, 0, 0), math);
        foreach (byte layout in new byte[] { 0, 2, 3 })
        {
            Ff7BattleCameraArchive archive = Ff7BattleCameraResources.Load(installation, layout);
            foreach (int camera in cameras)
            {
                Ff7BattleCameraScriptState state = Ff7NativeBattleCameraScripts.Create((short)camera, 0, idle);
                int target = 4;
                ReadOnlyMemory<byte> Script(short id, byte entry, int track, Ff7BattleCameraComponent component) => archive.Script(id, track, component);
                bool complete = false;
                int callbacks = 0;
                using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                byte[] packet = new byte[63];
                for (int callback = 0; callback < 3000; callback++)
                {
                    Ff7BattleCameraActors actors = input with { Target = target };
                    Ff7BattleCameraScriptUpdate update = Ff7NativeBattleCameraScripts.Advance(state, true, false, Script, placements, idle, math.Cosine, actors);
                    if (callback is 0 or 1 or 12)
                    {
                        Ff7BattleCameraScriptState cold = JsonSerializer.Deserialize<Ff7BattleCameraScriptState>(JsonSerializer.Serialize(state))!;
                        Check(JsonSerializer.Serialize(update) == JsonSerializer.Serialize(Ff7NativeBattleCameraScripts.Advance(cold, true, false, Script, placements, idle, math.Cosine, actors)), "Cold opening camera changed a relative/follow/spiral callback or shared target write.");
                    }
                    state = update.State;
                    target = update.TargetWrite ?? target;
                    int cursor = 0;
                    foreach (int track in new[] { 0, 1, 2, 4, 5, 6 })
                    {
                        Ff7BattleCameraTrack row = state.Tracks[track];
                        BinaryPrimitives.WriteInt16LittleEndian(packet.AsSpan(cursor), row.Point.X);
                        BinaryPrimitives.WriteInt16LittleEndian(packet.AsSpan(cursor + 2), row.Point.Y);
                        BinaryPrimitives.WriteInt16LittleEndian(packet.AsSpan(cursor + 4), row.Point.Z);
                        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(cursor + 6), row.Pc);
                        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(cursor + 8), row.Wait);
                        cursor += 10;
                    }
                    int activeMask = 0;
                    for (int slot = 0; slot < state.Callbacks.Length; slot++)
                    {
                        if (state.Callbacks[slot] is not null)
                        {
                            activeMask |= 1 << slot;
                        }
                    }
                    BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(60), (ushort)activeMask);
                    packet[62] = (byte)target;
                    hash.AppendData(packet);
                    callbacks++;
                    complete = state.Callbacks.All(entry => entry is null) && Enumerable.Range(0, 3).All(track => archive.Script(camera, track, Ff7BattleCameraComponent.Position).Span[state.Tracks[track].Pc] == 0xFF && archive.Script(camera, track, Ff7BattleCameraComponent.Focus).Span[state.Tracks[track + 4].Pc] == 0xFF);
                    if (complete)
                    {
                        break;
                    }
                }
                Check(complete, $"Original camera {camera} in layout {layout} did not retire its supported script callbacks.");
                if (layout == 0 && original.TryGetValue(camera, out var expected))
                {
                    Check(callbacks == expected.Frames && Convert.ToHexString(hash.GetHashAndReset()) == expected.Hash, $"Camera {camera} changed its original per-callback point, PC, wait, physical callback-slot occupancy or target trajectory.");
                }
            }
        }
        Check(Ff7NativeBattleCamera.InitialTarget([]) is null && Ff7NativeBattleCamera.InitialTarget([6]) == 6 && Ff7NativeBattleCamera.InitialTarget([4, 6]) == 15 && Ff7NativeBattleCamera.InitialTarget([4, 4]) == 15, "Native initial presentation target must follow actual target-row count, preserving an empty owner.");
    }

    private static void OpeningEntryCallbacks(Ff7Installation installation, Ff7BattleCameraMath math)
    {
        IReadOnlyList<SceneFormation> formations = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path));
        int[] opening = [.. Enumerable.Range(300, 25), 328, 329, 330, .. Enumerable.Range(332, 28), 364, 367, 392, 393, 394, 395];
        Dictionary<byte, Ff7BattleCameraScriptResources> resources = [];
        Ff7BattleCameraActors actors = new(0, 4, (_, selector) => selector, (actor, bone) => new((short)(actor * 100 + bone), (short)(-bone * 10), (short)(actor * -200)), _ => new(new(256, 256, 256, 0), 0x1000, 0, 2048, 0), () => new(0, 0, 0), math);
        foreach (int id in opening)
        {
            SceneFormation formation = formations[id];
            SceneBattleData battle = SceneBattleData.Read(formation);
            if (!resources.TryGetValue(formation.Layout, out Ff7BattleCameraScriptResources? source))
            {
                source = Ff7BattleCameraResources.LoadScripts(installation, formation.Layout);
                resources.Add(formation.Layout, source);
            }
            foreach (short camera in new short[] { -2, -3 })
            {
                Ff7BattleCameraScriptState state = Ff7NativeBattleCameraScripts.Create(camera, formation.InitialCamera, battle.Cameras[0]);
                bool complete = false;
                for (int callback = 0; callback < 3000; callback++)
                {
                    Ff7NativeBattleCameraScripts.Validate(state, source.Script);
                    Ff7BattleCameraScriptUpdate update = Ff7NativeBattleCameraScripts.Advance(state, true, false, source.Script, battle.Cameras, battle.Cameras[0], source.Math.Cosine, actors);
                    if (callback == 1)
                    {
                        Ff7BattleCameraScriptState cold = JsonSerializer.Deserialize<Ff7BattleCameraScriptState>(JsonSerializer.Serialize(state))!;
                        Check(JsonSerializer.Serialize(update) == JsonSerializer.Serialize(Ff7NativeBattleCameraScripts.Advance(cold, true, false, source.Script, battle.Cameras, battle.Cameras[0], source.Math.Cosine, actors)), "Original formation entry or terminal camera changed after JSON restoration.");
                    }
                    state = update.State;
                    complete = state.Callbacks.All(entry => entry is null) && Enumerable.Range(0, 3).All(track => source.Script(camera, formation.InitialCamera, track, Ff7BattleCameraComponent.Position).Span[state.Tracks[track].Pc] == 0xFF && source.Script(camera, formation.InitialCamera, track, Ff7BattleCameraComponent.Focus).Span[state.Tracks[track + 4].Pc] == 0xFF);
                    if (complete)
                    {
                        break;
                    }
                }
                Check(complete, $"Original formation {id} camera {camera}/{formation.InitialCamera} did not retire its supported callbacks.");
            }
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}
