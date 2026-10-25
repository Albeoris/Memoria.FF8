using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect257CameraControlTests
{
    [Theory]
    [InlineData(-1, 0, -1)]
    [InlineData(0, 0, 0)]
    [InlineData(0, 1, 1)]
    [InlineData(1, 65535, 1)]
    public void Original_camera_control_preserves_signed_skip_random_track_and_Json_continuation(int selector, int random, int expectedChannel)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select original Effect257 resources.");
        using GameAssets game = new(directory!);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([257])[256];
        ImmutableArray<byte> file = NativeBattleEffectInitializerPolicy.Assets(binding)[0].Data;
        int cameraOffset = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan().Slice(0x20, 4));
        Assert.Equal(0xE8C, cameraOffset);
        NativeBattleEffectHeapAllocation source = new("SelectedCamera", file.AsSpan().Slice(cameraOffset).ToArray().ToImmutableArray());
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess().Register5099A0(source);
        NativeBattleEffectAnimationRegisters control = Assert.IsType<NativeBattleEffectAnimationRegisters>(textures.Animation);
        Assert.Equal(8, control.StreamOffset);
        Assert.Equal(0x20, control.ModelTableOffset);
        NativeBattleCameraBank bank = new(control.Source.Data.AsSpan().Slice(control.ModelTableOffset).ToArray().ToImmutableArray());
        ImmutableArray<byte> program = control.Source.Data.AsSpan().Slice(control.StreamOffset).ToArray().ToImmutableArray();
        Assert.Equal(0x18, bank.TrackOffset(0));
        Assert.Equal(0x128, bank.TrackOffset(1));

        NativeBattleCameraPose initialPose = new(new(100, 200, -1000), new(0, 0, 0), 512, 0);
        NativeBattleCameraState initial = NativeBattleCameraState.Initialize() with { CurrentPose = initialPose, CapturedPose = initialPose, FocalLength = 512, BlendProgress = 0 };
        initial = initial with { Variables = initial.Variables.SetItem(0, checked((short)selector)) };
        NativeBattleCameraState registered = initial.RegisterControlResource(control);
        int randomReads = 0;
        NativeBattleCameraInputs inputs = new()
        {
            GlobalRandom = () =>
            {
                randomReads++;
                return checked((ushort)random);
            },
            ReadExternal = _ => throw new InvalidOperationException("The selected camera control has no external variable consumer."),
            ScheduleBlend = _ => throw new InvalidOperationException("The selected camera control has no blend producer.")
        };
        NativeBattleCameraState selected = registered.AdvanceControl(bank, program, inputs);
        Assert.Equal(expectedChannel < 0 ? 0 : 1, randomReads);
        NativeBattleCameraInputs restoredInputs = inputs with { GlobalRandom = () => checked((ushort)random) };
        NativeBattleCameraState restoredRegistration = RoundTrip(registered);
        Assert.Equal(JsonSerializer.Serialize(selected), JsonSerializer.Serialize(restoredRegistration.AdvanceControl(bank, program, restoredInputs)));
        Assert.Null(selected.ControlCursor);
        Assert.Equal(checked((short)selector), selected.Variables[0]);
        if (expectedChannel < 0)
        {
            Assert.Empty(selected.Order);
            Assert.Equal(initialPose, selected.CurrentPose);
        }
        else
        {
            Assert.Equal(checked((short)expectedChannel), selected.Variables[1]);
            int slot = Assert.Single(selected.Order);
            NativeBattleCameraTask task = Assert.IsType<NativeBattleCameraTask>(selected.Tasks[slot]);
            Assert.Equal(checked((byte)expectedChannel), task.Channel);
            Assert.Equal(bank.TrackOffset(task.Channel), task.ProgramOffset);
        }

        HashSet<byte> transformedReferences = [];
        NativeBattleCameraPoseInputs poses = new()
        {
            TransformReference = (reference, point) =>
            {
                transformedReferences.Add(reference);
                NativeBattleCameraPoint origin = reference switch
                {
                    0x90 => new(100, 50, -3000),
                    0xB0 => new(-200, 20, 400),
                    0xF2 => new(0, 0, -1000),
                    _ => throw new NotSupportedException("The selected camera component requires its explicit synthetic reference owner.")
                };
                return new(unchecked((short)(point.X + origin.X)), unchecked((short)(point.Y + origin.Y)), unchecked((short)(point.Z + origin.Z)));
            }
        };
        NativeBattleCameraState current = selected;
        bool changedPose = false;
        int callbacks = 0;
        do
        {
            NativeBattleCameraState restored = RoundTrip(current);
            NativeBattleCameraState directNext = Next(current);
            Assert.Equal(JsonSerializer.Serialize(directNext), JsonSerializer.Serialize(Next(restored)));
            current = directNext;
            changedPose |= current.CurrentPose is { } pose && (pose.Eye != initialPose.Eye || pose.Target != initialPose.Target);
            callbacks++;
        }
        while (!current.Order.IsEmpty && callbacks < 512);
        Assert.True(callbacks < 512, "The selected camera tracks did not reach their original terminal record.");
        Assert.Empty(current.Order);
        Assert.Null(current.ControlCursor);
        Assert.Equal(expectedChannel < 0 ? 0 : 1, randomReads);
        Assert.Equal(expectedChannel >= 0, changedPose);
        if (expectedChannel < 0)
        {
            Assert.Empty(transformedReferences);
        }
        else
        {
            Assert.Contains((byte)0x90, transformedReferences);
            Assert.Contains((byte)0xB0, transformedReferences);
            Assert.Contains((byte)0xF2, transformedReferences);
        }

        NativeBattleCameraState Next(NativeBattleCameraState camera)
        {
            return camera.AdvanceControl(bank, program, inputs).Tick(0, poses);
        }
    }

    private static NativeBattleCameraState RoundTrip(NativeBattleCameraState camera)
    {
        return JsonSerializer.Deserialize<NativeBattleCameraState>(JsonSerializer.Serialize(camera))!;
    }
}
