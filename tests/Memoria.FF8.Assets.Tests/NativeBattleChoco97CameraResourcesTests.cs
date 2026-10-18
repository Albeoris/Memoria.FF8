using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleChoco97CameraResourcesTests
{
    [Fact]
    public void FourOriginalSourcesUseExplicitIndependentAnchorsAndRetainExactColdNextStates()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        using GameAssets game = new(directory);
        NativeBattleChoco97CameraResources resources = game.ReadNativeChoco97CameraResources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeChoco97CameraResources());
        NativeBattleChoco97CameraResources restoredResources = JsonSerializer.Deserialize<NativeBattleChoco97CameraResources>(JsonSerializer.Serialize(resources))!;
        restoredResources.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restoredResources));
        ImmutableArray<byte> actor = Anchor(170, -240, 310, 419);
        ImmutableArray<byte> target = Anchor(-520, 630, -740, -851);
        NativeBattleMoogleCameraState untouchedOtherOwner = NativeBattleMoogleCamera.Reset732AF0();
        string otherOwner = JsonSerializer.Serialize(untouchedOtherOwner);
        NativeBattleMoogleCameraState retained = NativeBattleMoogleCamera.Reset732AF0();
        for (int index = 0; index < 4; index++)
        {
            NativeBattleMoogleCameraSource source = resources.Sources[index];
            Assert.Equal(0x152BA38u + (uint)(index * 0x48), source.Address);
            ImmutableArray<byte> anchor = index == 3 ? target : actor;
            NativeBattleMoogleCameraStep step = NativeBattleMoogleCamera.Initialize732B10(retained, source, anchor, 0);
            Assert.Equal(source.Address, BinaryPrimitives.ReadUInt32LittleEndian(step.State.Data.AsSpan().Slice(0xAC)));
            Assert.Equal(anchor.ToArray(), step.State.Data.AsSpan().Slice(0xA4, 8).ToArray());
            Assert.False(step.State.EyePaddingKnown);
            int callbacks = 0;
            while (!step.Complete && callbacks++ < 4096)
            {
                NativeBattleMoogleCameraState restored = JsonSerializer.Deserialize<NativeBattleMoogleCameraState>(JsonSerializer.Serialize(step.State))!;
                NativeBattleMoogleCameraStep expected = NativeBattleMoogleCamera.Advance732E00(step.State);
                Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(NativeBattleMoogleCamera.Advance732E00(restored)));
                step = expected;
            }
            Assert.True(step.Complete, "The camera source exceeded the diagnostic callback budget.");
            Assert.Equal(otherOwner, JsonSerializer.Serialize(untouchedOtherOwner));
            retained = step.State;
        }
        Assert.Throws<InvalidDataException>(() => (resources with { Sources = resources.Sources.RemoveAt(3) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Sources = resources.Sources.SetItem(0, resources.Sources[0] with { Address = 0x1532C68 }) }).Validate());
        byte[] changed = resources.Sources[0].Data.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(changed, 1);
        Assert.Throws<NotSupportedException>(() => (resources with { Sources = resources.Sources.SetItem(0, resources.Sources[0] with { Data = changed.ToImmutableArray() }) }).Validate());
    }

    private static ImmutableArray<byte> Anchor(short x, short y, short z, short fourth)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, x);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), y);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), z);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(6), fourth);
        return bytes.ToImmutableArray();
    }
}
