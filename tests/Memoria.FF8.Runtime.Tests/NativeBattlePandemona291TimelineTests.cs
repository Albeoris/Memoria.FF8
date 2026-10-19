using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed partial class NativeBattlePandemona291EntryTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(17, false)]
    public void First_surface_initializes_current_workspace_then_spawns_moves_and_draws_before_camera(int pause, bool active)
    {
        var fixture = Fixture();
        byte[] programme = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(programme.AsSpan(4), 8);
        NativeBattlePandemona291Resources resources = fixture.Resources with { SurfaceProgram = new(0x13AFF48, programme.ToImmutableArray()) };
        NativeBattlePandemona291Frame frame = NativeBattlePandemona291Entry.Initialize6ED250(fixture.Frame, resources, fixture.Packets, ReadModel);
        frame = frame with { Effect = frame.Effect with { Binding = frame.Effect.Binding with { Pandemona291Resources = resources } }, Memory = NativeBattleEffectIo.Initialize534110(frame.Memory, 0x231D4) };
        frame = frame with { Memory = frame.Memory with { Pandemona291 = frame.Memory.Pandemona291! with { Pause2556258 = (uint)pause }, Arena = frame.Memory.Arena!.Write(0x21A4 + 0x102C, BitConverter.GetBytes(0x20DFAB8u + 0x21A4 + 0x42C)).MarkUnknown(0x21D2, 2) } };
        NativeBattleModulePoolTask task = new(1, 0, null, 0x6ED900, new byte[0x1024].ToImmutableArray());
        task = task.WriteWord(0xC, 10);
        List<string> calls = [];
        NativeBattlePandemona291TimelineServices services = new((_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (current, _, age) =>
        {
            Assert.Equal(10, age);
            calls.Add("camera");
            return current;
        })
        {
            DrawTrails = (current, _) =>
            {
                Assert.Equal((ushort)19, BinaryPrimitives.ReadUInt16LittleEndian(current.Memory.Arena!.Read(0x21A4 + 0x432, 2)));
                calls.Add("draw");
                return current;
            }
        };
        NativeBattlePandemona291ChildStep result = NativeBattlePandemona291Timeline.Advance6ED900(frame, 0x20DFAB8u + 0x21A4, task, services);
        Assert.Equal(active ? new[] { "draw", "camera" } : new[] { "camera" }, calls);
        Assert.Equal((ushort)11, result.Task.ReadWord(0xC));
        Assert.Equal(0x13AFF48u, BinaryPrimitives.ReadUInt32LittleEndian(result.Frame.Memory.Arena!.Read(0x21A4 + 0x1F0, 4)));
        Assert.Throws<NotSupportedException>(() => result.Frame.Memory.Arena.Read(0x21D2, 2));
        if (active)
        {
            Assert.NotEqual(frame.Random, result.Frame.Random);
        }
        else
        {
            Assert.Equal(frame.Random, result.Frame.Random);
        }
    }

    [Fact]
    public void OpeningTimelineQueuesFourDescriptorsOnceAndAdvancesWhileControllerIsBusy()
    {
        var fixture = Fixture();
        NativeBattlePandemona291Resources resources = fixture.Resources with { OpeningSound = 0x1234 };
        NativeBattlePandemona291Frame frame = NativeBattlePandemona291Entry.Initialize6ED250(fixture.Frame, resources, fixture.Packets, ReadModel);
        frame = frame with { Effect = frame.Effect with { Binding = frame.Effect.Binding with { Pandemona291Resources = resources } }, Memory = NativeBattleEffectIo.Initialize534110(frame.Memory, 0x231D4) };
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.MarkUnknown(0x21D2, 2) } };
        NativeBattleModulePoolTask task = new(1, 0, null, 0x6ED900, new byte[0x1024].ToImmutableArray());
        List<string> calls = [];
        NativeBattlePandemona291TimelineServices services = new((current, request) =>
        {
            Assert.Equal((short)719, request.FileId);
            Assert.Equal(0x20DFAB8u + 0x2321C, request.DestinationAddress);
            calls.Add("file");
            return current;
        }, (current, sound) =>
        {
            Assert.Equal(0x1234u, sound);
            calls.Add("sound");
            return current;
        }, (current, center, age) =>
        {
            Assert.Equal(3999, center);
            calls.Add("camera" + age);
            return current;
        });
        NativeCrtRandomState random = frame.Random;
        NativeBattlePandemona291ChildStep first = NativeBattlePandemona291Timeline.Advance6ED900(frame, 0x20DFAB8u + 0x21A4, task, services);
        NativeBattlePandemona291ChildStep second = NativeBattlePandemona291Timeline.Advance6ED900(first.Frame, 0x20DFAB8u + 0x21A4, first.Task, services);
        Assert.Equal(new[] { "file", "sound", "camera0", "camera1" }, calls);
        Assert.Equal((ushort)2, second.Task.ReadWord(0xC));
        Assert.Equal(350u, second.Frame.Memory.Pandemona291!.Flash25562F0);
        Assert.Equal(17u, second.Frame.Memory.Pandemona291.Pause2556258);
        Assert.Equal(random, second.Frame.Random);
        NativeBattleEffectArena arena = second.Frame.Memory.Arena!;
        Assert.Equal((ushort)2, BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(0x21A4 + 0xC, 2)));
        Assert.Equal(new byte[] { 1, 0, 1, 4, 0 }, arena.Read(0x231D4, 5));
        short[] types = [2, 1, 3, 5];
        for (int index = 0; index < 4; index++)
        {
            Assert.Equal(types[index], BinaryPrimitives.ReadInt16LittleEndian(arena.Read(0x231D4 + 8 + 8 * index, 2)));
            Assert.Equal((short)(719 + index), BinaryPrimitives.ReadInt16LittleEndian(arena.Read(0x231D4 + 10 + 8 * index, 2)));
        }
        Assert.Throws<NotSupportedException>(() => arena.Read(0x21D2, 2));
        NativeBattlePandemona291Frame restored = JsonSerializer.Deserialize<NativeBattlePandemona291Frame>(JsonSerializer.Serialize(second.Frame))!;
        Assert.Equal(JsonSerializer.Serialize(second.Frame), JsonSerializer.Serialize(restored));
        Assert.Throws<NotSupportedException>(() => NativeBattlePandemona291Timeline.Advance6ED900(second.Frame, 0x20DFAB8u + 0x21A4, second.Task.WriteWord(0xC, 18), services));
    }

    [Fact]
    public void OpeningCameraUsesLiteralIterativeOrbitAndRestoresOnlyTheEyeYWord()
    {
        NativeBattleCameraState camera = NativeBattleCameraState.Initialize() with { EyePaddingF6 = 9, TargetPaddingFE = 7, Mask = 3 };
        NativeBattleCameraState first = NativeBattlePandemona291Timeline.AdvanceOpeningCamera(camera, 4000, 0);
        Assert.Equal(new NativeBattleCameraPoint(0, -1200, 3940), first.CurrentPose!.Eye);
        Assert.Equal(new NativeBattleCameraPoint(0, -900, 2000), first.CurrentPose.Target);
        Assert.Equal((short)240, first.FocalLength);
        Assert.Equal((short)0, first.Roll);
        Assert.Equal((ushort)3, first.Mask);
        Assert.Equal((short)9, first.EyePaddingF6);
        Assert.Equal((short)7, first.TargetPaddingFE);
        NativeBattleCameraState second = NativeBattlePandemona291Timeline.AdvanceOpeningCamera(first, 4000, 1);
        Assert.Equal(new NativeBattleCameraPoint(0, -1200, 4087), second.CurrentPose!.Eye);
        NativeBattleCameraState independent = JsonSerializer.Deserialize<NativeBattleCameraState>(JsonSerializer.Serialize(first))!;
        Assert.Equal(JsonSerializer.Serialize(second), JsonSerializer.Serialize(NativeBattlePandemona291Timeline.AdvanceOpeningCamera(independent, 4000, 1)));
    }
}
