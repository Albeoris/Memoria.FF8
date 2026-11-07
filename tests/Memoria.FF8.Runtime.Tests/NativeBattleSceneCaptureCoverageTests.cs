using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSceneCaptureCoverageTests
{
    public static IEnumerable<object[]> Domains()
    {
        for (int domain = 0; domain < 3; domain++)
        {
            foreach (ushort flags in new ushort[] { 0, 1 })
            {
                foreach (int height in new[] { 216, 232 })
                {
                    yield return [domain, flags, height];
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Domains))]
    public void Full_source_coverage_maps_capture_local_bounds_without_changing_native_words(int domain, ushort flags, int height)
    {
        NativeBattleSceneCapture capture = Capture(domain, height);
        NativeBattleLogicalCommand command = Black(flags) with { ProjectedCoordinates = [new(-999, -999), new(9999, -999), new(-999, 9999), new(9999, 9999)] };
        byte[] original = Packet(command);
        byte[] mapped = original.ToArray();

        capture.MapSourcePacket(command, mapped);

        int scale = (flags & 1) != 0 ? 1 : 8;
        for (int corner = 0; corner < 4; corner++)
        {
            Assert.Equal((short)((corner & 1) == 0 ? 0 : 320 * scale), BinaryPrimitives.ReadInt16LittleEndian(mapped.AsSpan(8 + corner * 4)));
            Assert.Equal((short)(corner < 2 ? 0 : height * scale), BinaryPrimitives.ReadInt16LittleEndian(mapped.AsSpan(10 + corner * 4)));
        }
        Assert.Equal(original.AsSpan(0, 8).ToArray(), mapped.AsSpan(0, 8).ToArray());
        Assert.Equal(original, Packet(command));
    }

    [Theory]
    [MemberData(nameof(Domains))]
    public void Software_full_clear_overwrites_stale_capture_borders_and_preserves_surrounding_vram(int domain, ushort flags, int height)
    {
        NativeBattleSceneCapture capture = Capture(domain, height);
        const int x = 576;
        const int y = 256;
        const int guardWidth = 322;
        byte[] stale = Enumerable.Repeat((byte)0x5A, 2 * guardWidth * (height + 2)).ToArray();
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(x - 1, y - 1, guardWidth, checked((short)(height + 2)), stale);
        NativeBattleSoftwarePrimitiveState state = NativeBattleSoftwarePrimitiveState.FromInitializedRenderer().Initialize500400();
        NativeSoftwareDrawingEnvironmentState environment = NativeSoftwareDrawingEnvironment.Recalculate45B6F0(new((short)x, (short)y, (uint)(x + 319), (uint)(y + height - 1), (short)x, (short)y, 0, 0, 0, null));
        uint[] handlers = new uint[256];
        handlers[0x28] = 0x4668F0;
        NativeBattleSoftwarePrimitiveExecution execution = new(state with { Environment = environment }, textures, new(handlers.ToImmutableArray()), true, NativeBattlePcTextureWindowCache.Reset4647A0(null));
        NativeBattleLogicalCommand command = Black(flags) with { SceneCapture = capture };
        byte[] native = Packet(command);
        Assert.Equal(3u, execution.LiveMode);

        execution.ExecuteSoftware(command);
        execution.EndBatch();

        for (int row = -1; row <= height; row++)
        {
            for (int column = -1; column <= 320; column++)
            {
                ushort expected = row < 0 || row == height || column < 0 || column == 320 ? (ushort)0x5A5A : (ushort)0;
                Assert.Equal(expected, execution.Textures.ReadVramWord(x + column, y + row));
            }
        }
        Assert.Equal(native, Packet(command));
    }

    [Theory]
    [InlineData(NativeBattleSceneCaptureKind.Source)]
    [InlineData(NativeBattleSceneCaptureKind.SourceEnvironment)]
    public void Ordinary_source_geometry_retains_world_domain_mapping(NativeBattleSceneCaptureKind kind)
    {
        NativeBattleSceneCapture capture = Capture(1, 232) with { Kind = kind };
        int left = checked((int)Math.Round(capture.Source.Left));
        int right = checked((int)Math.Round(capture.Source.Right));
        int top = checked((int)Math.Round(capture.Source.Top + 64));
        int bottom = checked((int)Math.Round(capture.Source.Bottom + 64));
        NativeBattleLogicalCommand command = Black(1) with
        {
            ScreenCoverage = null,
            ProjectedCoordinates = [new(left, top), new(right, top), new(left, bottom), new(right, bottom)]
        };
        byte[] packet = Packet(command);

        capture.MapSourcePacket(command, packet);

        Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(packet.AsSpan(8)));
        Assert.Equal((short)8, BinaryPrimitives.ReadInt16LittleEndian(packet.AsSpan(10)));
        Assert.Equal((short)320, BinaryPrimitives.ReadInt16LittleEndian(packet.AsSpan(12)));
        Assert.Equal((short)224, BinaryPrimitives.ReadInt16LittleEndian(packet.AsSpan(18)));
    }

    [Fact]
    public void Screen_sample_coverage_does_not_rewrite_its_packet()
    {
        NativeBattleSceneCapture capture = Capture(1, 232) with { Kind = NativeBattleSceneCaptureKind.ScreenSample };
        NativeBattleLogicalCommand command = Black(1);
        byte[] packet = Packet(command);

        capture.MapSourcePacket(command, packet);

        Assert.Equal(Packet(command), packet);
    }

    [Fact]
    public void Full_coverage_rejects_a_complete_textured_quad_without_changing_coordinates_or_material_words()
    {
        NativeBattleSceneCapture capture = Capture(1, 232);
        NativeBattleLogicalCommand command = new(new("fixture", 0, 0), 1, 0x2C, [0x2C808080, 0, 0x12340000, 320, 0x013B00FF, 248u << 16, 0x0000FF00, 320 | 248u << 16, 0x0000FFFF], 1, [], null) { ScreenCoverage = NativeBattleScreenCoverage.Full };
        byte[] original = Packet(command);
        byte[] packet = original.ToArray();

        Assert.Throws<InvalidDataException>(() => capture.MapSourcePacket(command, packet));

        Assert.Equal(original, packet);
        Assert.Equal(original, Packet(command));
    }

    [Fact]
    public void Full_coverage_rejects_a_triangle_or_truncated_quad_before_writing()
    {
        NativeBattleSceneCapture capture = Capture(0, 216);
        NativeBattleLogicalCommand triangle = new(new("fixture", 0, 0), 1, 0x20, [0x20000000, 0, 320, 216u << 16], 1, [], null) { ScreenCoverage = NativeBattleScreenCoverage.Full };
        byte[] triangleBytes = Packet(triangle);
        byte[] truncated = Packet(Black(1))[..^1];
        byte[] original = truncated.ToArray();

        Assert.Throws<InvalidDataException>(() => capture.MapSourcePacket(triangle, triangleBytes));
        Assert.Throws<InvalidDataException>(() => capture.MapSourcePacket(Black(1), truncated));
        Assert.Equal(Packet(triangle), triangleBytes);
        Assert.Equal(original, truncated);
    }

    private static NativeBattleSceneCapture Capture(int domain, int height)
    {
        BattleSceneVisibleArea source = domain switch
        {
            0 => BattleSceneVisibleArea.Native,
            1 => BattleSceneVisibleArea.FromViewport(1920, 1080, 240, 0, 1440, 1080),
            2 => BattleSceneVisibleArea.FromViewport(2560, 1080, 560, 0, 1440, 1080),
            _ => throw new ArgumentOutOfRangeException(nameof(domain))
        };
        int padding = (height - 216) / 2;
        return new(source, NativeBattleSceneCaptureKind.Source) { X = 576, Y = 256, Height = height, PaddingTop = padding, PaddingBottom = padding, ProjectionOffsetY = padding };
    }

    private static NativeBattleLogicalCommand Black(ushort flags)
    {
        uint scale = (flags & 1) != 0 ? 1u : 8u;
        uint right = 320 * scale;
        uint bottom = 248 * scale << 16;
        return new(new("fixture", 0, 0), 1, 0x28, [0x28000000, 0, right, bottom, right | bottom], flags, [], null) { ScreenCoverage = NativeBattleScreenCoverage.Full };
    }

    private static byte[] Packet(NativeBattleLogicalCommand command)
    {
        byte[] packet = new byte[(command.Words.Length + 1) * 4];
        for (int index = 0; index < command.Words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan((index + 1) * 4), command.Words[index]);
        }
        return packet;
    }
}
