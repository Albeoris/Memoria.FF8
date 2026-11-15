using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect9ResourcesTests
{
    [Fact]
    public void Installed_Doom_camera_retains_every_recipient_selected_track()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        using GameAssets game = new(directory!);
        NativeBattleEffect9Resources resources = game.ReadNativeEffect9Resources();
        ImmutableArray<byte> camera = resources.Camera.Data;
        Assert.Equal(0x244, camera.Length);
        Assert.Equal(new byte[] { 5, 21, 2 }, camera.AsSpan().Slice(8, 3).ToArray());
        NativeBattleCameraBank bank = new(camera.AsSpan().Slice(12).ToArray().ToImmutableArray());
        int?[] offsets = Enumerable.Range(0, 8).Select(selector => bank.TrackOffset((byte)selector)).ToArray();
        Assert.Equal(new int?[] { 0x18, 0x7A, 0xDC, 0x13E, 0x1A0, 0x202, 0x234, 0x236 }, offsets);
        Assert.All(offsets.Skip(6), offset => Assert.Equal(ushort.MaxValue, BinaryPrimitives.ReadUInt16LittleEndian(bank.Data.AsSpan().Slice(offset!.Value))));
        Assert.Null(bank.TrackOffset(0x10));
    }
}
