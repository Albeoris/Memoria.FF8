using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed partial class NativeBattlePandemona291EntryTests
{
    [Fact]
    public void Rising_parameters_use_signed_division_and_literal_alternate_shapes_without_radial_morph()
    {
        Assert.Equal(-8192, NativeBattlePandemona291Rising.BodyY100(100));
        Assert.Equal(-7851, NativeBattlePandemona291Rising.BodyY100(101));
        Assert.Equal(-3755, NativeBattlePandemona291Rising.BodyY100(113));
        ImmutableArray<NativeBattlePandemona291LayerRequest> layers = NativeBattlePandemona291Rising.Layers100(1900, 113);
        Assert.Equal(new[] { 11776, -30720, -30720 }, layers.Select(layer => layer.HorizontalScale));
        Assert.All(layers, layer =>
        {
            Assert.Equal(4096, layer.Morph);
            Assert.Equal(0, layer.RadialPhase);
            Assert.Equal(0, layer.RadialAmplitude);
            Assert.Equal(0, layer.Fade);
            Assert.Equal(1900, layer.Center);
        });
    }

    [Fact]
    public void Body_bounds_fold_scaled_bone_endpoints_and_preserve_world_padding()
    {
        var fixture = BodyRenderFixture();
        NativeBattleEffectArena arena = fixture.Frame.Memory.Arena!.Write(0x40000, new byte[] { 2 });
        arena = arena.Write(0x40000 + 0x20 + 48 - 14, BitConverter.GetBytes((short)200));
        arena = NativeBattleArenaPose.WriteMatrix(arena, 0x40000 + 0x20 + 48, BodyIdentity with { X = 900, Z = 700 }, false);
        arena = arena.MarkUnknown(0x21A4 + 0x154 + 0x40 + 18, 2);
        NativeBattlePandemona291BodyBounds result = NativeBattlePandemona291BodyDrawing.Prepare6F0AE0(fixture.Frame with { Memory = fixture.Frame.Memory with { Arena = arena } }, fixture.Address, 8192);
        Assert.Equal(new NativeBattleEffectLocalBounds(new(-500, 0, -500), new(1800, 0, 1800)), result.Bounds);
        Assert.Equal((short)1800, BinaryPrimitives.ReadInt16LittleEndian(result.Frame.Memory.Arena!.Read(0x21A4 + 0x154 + 0x3A, 2)));
        Assert.Throws<NotSupportedException>(() => result.Frame.Memory.Arena.Read(0x21A4 + 0x154 + 0x40 + 18, 2));
        Assert.Equal(1800, result.Frame.Memory.ScriptedColorRegisters!.Ir[0]);
        Assert.Equal(1800, result.Frame.Memory.ScriptedColorRegisters.Ir[2]);
        Assert.False(result.Frame.Memory.ScriptedColorRegisters.NormalInputKnown!.Value[6]);
    }

    [Fact]
    public void Body_projection_uses_real_directory_and_restores_local_pose_after_native_packets()
    {
        var fixture = BodyRenderFixture();
        NativeBattlePandemona291BodyBounds bounds = NativeBattlePandemona291BodyDrawing.Prepare6F0AE0(fixture.Frame, fixture.Address, 8192);
        NativeBattlePandemona291Frame composed = NativeBattlePandemona291BodyDrawing.Compose5095B0(bounds, BodyIdentity);
        Assert.Equal(1000, NativeBattleArenaPose.ReadMatrix(composed.Memory.Arena!, 0x40020).Z);
        NativeBattleCameraProjection projection = new(BodyIdentity, 100, 160, 100);
        NativeBattleSummonColors colors = new(0, 0, 1f / 4096f, 4503601774854144d);
        NativeBattlePandemona291Frame independent = NativeBattleTestSnapshots.RestoreJson<NativeBattlePandemona291Frame>(NativeBattleTestSnapshots.Json(composed))!;
        NativeBattlePandemona291BodyDraw drawn = NativeBattlePandemona291BodyDrawing.Render5099D0(composed, fixture.Address, projection, colors);
        Assert.Equal(32, drawn.Packets.AllocationBytes);
        Assert.Single(drawn.Packets.Attempts);
        Assert.Equal(0x20DFAB8u + 0x4103C, drawn.SourceCursor);
        Assert.Equal(24, drawn.Frame.Memory.Arena!.Read(0x31D4, 24).Length);
        Assert.Throws<NotSupportedException>(() => drawn.Frame.Memory.Arena.Read(0x31D4 + 24, 1));
        NativeBattlePandemona291Frame restoredPose = NativeBattlePandemona291BodyDrawing.Restore508C90(NativeBattleTestSnapshots.Independent(drawn.Frame), bounds.SkeletonOffset, bounds.BoneCount);
        Assert.Equal(0, NativeBattleArenaPose.ReadMatrix(restoredPose.Memory.Arena!, 0x40020).Z);
        Assert.Equal(NativeBattleTestSnapshots.Json(drawn), NativeBattleTestSnapshots.Json(NativeBattlePandemona291BodyDrawing.Render5099D0(independent, fixture.Address, projection, colors)));
    }

    [Theory]
    [InlineData(100)]
    [InlineData(107)]
    public void Rising_draws_trails_then_body_before_camera_and_stream_playback(int age)
    {
        var fixture = Fixture();
        NativeBattlePandemona291Frame frame = NativeBattlePandemona291Entry.Initialize6ED250(fixture.Frame, fixture.Resources, fixture.Packets, ReadModel);
        frame = frame with { Memory = NativeBattleEffectIo.Initialize534110(frame.Memory, 0x231D4) };
        NativeBattleModulePoolTask task = new(1, 0, null, 0x6ED900, new byte[0x1024].ToImmutableArray());
        task = task.WriteWord(0xC, unchecked((ushort)age));
        List<string> calls = [];
        NativeBattlePandemona291TimelineServices services = new((_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (current, _, _) =>
        {
            calls.Add("camera");
            return current;
        })
        {
            DrawRising = (current, _, _) =>
            {
                calls.Add("scene");
                return current with { Memory = current.Memory with { Arena = current.Memory.Arena!.Write(0x21A4 + 0x432, BitConverter.GetBytes((ushort)1)) } };
            },
            DrawTrails = (current, _) =>
            {
                calls.Add("trails");
                return current;
            },
            DrawBody = (current, _) =>
            {
                calls.Add("body");
                return current;
            },
            PlayStream = _ => calls.Add("stream")
        };
        NativeBattlePandemona291ChildStep result = NativeBattlePandemona291Timeline.Advance6ED900(frame, 0x20DFAB8u + 0x21A4, task, services);
        Assert.Equal(age == 107 ? new[] { "scene", "trails", "body", "camera", "stream" } : new[] { "scene", "trails", "body", "camera" }, calls);
        Assert.Equal(unchecked((ushort)(age + 1)), result.Task.ReadWord(0xC));
    }

    private static NativeBattleActionMatrix BodyIdentity => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);

    private static (NativeBattlePandemona291Frame Frame, uint Address) BodyRenderFixture()
    {
        var fixture = Fixture();
        NativeBattlePandemona291Frame frame = NativeBattlePandemona291Entry.Initialize6ED250(fixture.Frame, fixture.Resources, fixture.Packets, ReadModel);
        uint address = 0x20DFAB8u + 0x21A4;
        int model = 0x21A4 + 0x154;
        NativeBattleEffectArena arena = frame.Memory.Arena!.Write(model + 0x64, BitConverter.GetBytes(address + 0x41C));
        arena = arena.Write(0x21A4 + 0x41C, BitConverter.GetBytes(0x20DFAB8u + 0x40000)).Write(0x21A4 + 0x420, BitConverter.GetBytes(0x20DFAB8u + 0x41000));
        arena = arena.Write(0x40000, new byte[] { 1 }).Write(0x40002, BitConverter.GetBytes((short)4096));
        arena = arena.Write(0x40010, BitConverter.GetBytes((short)-1)).Write(0x40012, BitConverter.GetBytes((short)100));
        arena = NativeBattleArenaPose.WriteMatrix(arena, 0x40020, BodyIdentity, false);
        arena = NativeBattleArenaPose.WriteMatrix(arena, model + 0x40, BodyIdentity with { Z = 1000 }, false);
        arena = arena.Write(model + 0x28, BitConverter.GetBytes(0x808080u)).Write(model + 0x2C, BitConverter.GetBytes(0x32808080u)).Write(model + 0x7C, BitConverter.GetBytes(1u));
        byte[] geometry = new byte[60];
        BinaryPrimitives.WriteInt32LittleEndian(geometry, 1);
        BinaryPrimitives.WriteInt32LittleEndian(geometry.AsSpan(4), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(geometry.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(geometry.AsSpan(12), 3);
        NativeBattleVector[] vertices = [new(-100, -100, 0), new(100, -100, 0), new(-100, 100, 0)];
        for (int index = 0; index < vertices.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(geometry.AsSpan(14 + index * 6), vertices[index].X);
            BinaryPrimitives.WriteInt16LittleEndian(geometry.AsSpan(16 + index * 6), vertices[index].Y);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(geometry.AsSpan(32), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(geometry.AsSpan(44), 0x8000);
        BinaryPrimitives.WriteUInt16LittleEndian(geometry.AsSpan(46), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(geometry.AsSpan(48), 2);
        arena = arena.Write(0x41000, geometry).MarkUnknown(0x31D4, 48);
        return (frame with { Memory = frame.Memory with { Arena = arena } }, address);
    }
}
