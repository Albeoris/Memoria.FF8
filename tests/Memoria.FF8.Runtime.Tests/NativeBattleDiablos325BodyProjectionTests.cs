using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDiablos325BodyProjectionTests
{
    [Fact]
    public void Height_fade_uses_world_short_Y_before_camera_and_low_word_comparison_after_shift()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        NativeBattleCameraProjection projection = new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 8192, 4096, 4096), 288, 160, 108);
        NativeBattleVector[] points = [new(0, -8192, 0), new(0, -8196, 0), new(0, -7936, 0)];
        NativeBattleEffectArena result = NativeBattleDiablos325BodyProjection.Project657B50(arena, 0x100, points.Length, index => points[index], projection, -8192, 2, 128);
        Assert.Equal((byte)64, result.Read(0x107, 1)[0]);
        Assert.Equal((byte)63, result.Read(0x10F, 1)[0]);
        Assert.Equal((byte)128, result.Read(0x117, 1)[0]);
        Assert.Equal((ushort)4096, BinaryPrimitives.ReadUInt16LittleEndian(result.Read(0x104, 2)));
        Assert.Equal((byte)0, result.Read(0x106, 1)[0]);
    }

    [Fact]
    public void Nonpositive_depth_preserves_first_pass_world_XY_and_untouched_clip_shade_unknownness()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(0x106, 2);
        var preserved = NativeBattleStorageSnapshots.Capture(arena);
        string original = NativeBattleTestSnapshots.Json(preserved);
        NativeBattleCameraProjection projection = new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, -500, 4096), 288, 160, 108);
        NativeBattleEffectArena result = NativeBattleDiablos325BodyProjection.Project657B50(arena, 0x100, 1, _ => new(123, -456, 0), projection, -8192, 2, 128);
        Assert.Equal((short)123, BinaryPrimitives.ReadInt16LittleEndian(result.Read(0x100, 2)));
        Assert.Equal((short)-456, BinaryPrimitives.ReadInt16LittleEndian(result.Read(0x102, 2)));
        Assert.Equal(new byte[] { 0, 0 }, result.Read(0x104, 2));
        Assert.Throws<NotSupportedException>(() => result.Read(0x106, 1));
        Assert.Throws<NotSupportedException>(() => result.Read(0x107, 1));
        Assert.Equal(original, NativeBattleTestSnapshots.Json(preserved));
    }
}
