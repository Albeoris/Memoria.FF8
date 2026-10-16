using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeGardenWhiteSeeDAllocationTests
{
    private const string GameDirectory = @"C:\Steam\steamapps\common\FINAL FANTASY VIII";

    [Theory]
    [InlineData(1349, false)]
    [InlineData(1350, true)]
    [InlineData(1549, true)]
    [InlineData(1550, false)]
    public void OriginalClauseOwnsTheShipAndItsNativeDensePosition(ushort story, bool shipPresent)
    {
        using WorldAssets assets = new(GameDirectory);
        using GameAssets game = new(GameDirectory);
        NativeWorldEntryMemory entry = NativeWorldEntryMemory.CreateNewGame().WriteBytes(256, 2, story);
        NativeGardenWorldAllocation allocation = assets.ReadGardenAllocation(new(12, 48, -1), entry, NativeEventMemory.Empty, NativeSavedWorldState.CreateNewGame(), game.ReadNewGameCharacterVitals());
        Assert.Equal(shipPresent ? 22 : 20, allocation.ClauseWords.Length);
        Assert.Equal(shipPresent ? new byte[] { 0, 2, 64, 65, 88, 94 } : [0, 2, 64, 65, 94], allocation.Objects.Select(item => item.ShapeId));
        if (shipPresent)
        {
            Assert.Equal(10, allocation.ClauseIndex);
            Assert.Equal((ushort)1350, allocation.MinimumStory);
            Assert.Equal((ushort)1550, allocation.MaximumStory);
            Assert.Equal((ushort)0x1958, allocation.ClauseWords[19]);
            Assert.Equal(new NativeGardenWorldObject(88, 0, new(-18900, 0, -46850), 0, 3808, 0), allocation.Objects[4]);
            NativeWorldContactShape shape = assets.ReadNativeContactShape(88);
            Assert.Equal(new NativeWorldContactShape(1704, 567, 212, 1), shape);
            Assert.NotNull(assets.ReadScriptedStaticModel(88));
        }
        NativeGardenWorldAllocation restored = JsonSerializer.Deserialize<NativeGardenWorldAllocation>(JsonSerializer.Serialize(allocation))!;
        restored.Validate();
        Assert.Equal(allocation.Objects, restored.Objects);
    }

    [Fact]
    public void TheNativeInstructionCannotBeAdmittedWithoutItsCoordinateOrWithAnotherShape()
    {
        using WorldAssets assets = new(GameDirectory);
        using GameAssets game = new(GameDirectory);
        NativeWorldEntryMemory entry = NativeWorldEntryMemory.CreateNewGame().WriteBytes(256, 2, 1350);
        NativeGardenWorldAllocation allocation = assets.ReadGardenAllocation(new(12, 48, -1), entry, NativeEventMemory.Empty, NativeSavedWorldState.CreateNewGame(), game.ReadNewGameCharacterVitals());
        byte[] wmset = (byte[])typeof(WorldAssets).GetField("wmset", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(assets)!;
        int[] sections = (int[])typeof(WorldAssets).GetField("sections", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(assets)!;
        byte[] program = wmset[sections[9]..sections[10]];
        byte[] coordinates = wmset[sections[10]..sections[11]];
        Assert.Throws<InvalidDataException>(() => NativeGardenWorldAllocationReader.Read(program, coordinates[..(25 * 16)], allocation.Packet, entry, NativeEventMemory.Empty, NativeSavedWorldState.CreateNewGame(), allocation.GuardianForce13Flags, allocation.CactuarLoadPosition, allocation.LoadPose, allocation.CameraYaw, 0, allocation.MapDisplayMode));
        ushort[] words = allocation.ClauseWords.ToArray();
        words[19] = 0x1959;
        Assert.Throws<NotSupportedException>(() => (allocation with { ClauseWords = words.ToImmutableArray() }).Validate());
        Assert.Throws<InvalidDataException>(() => (allocation with { Objects = allocation.Objects.RemoveAt(4) }).Validate());
    }

    [Fact]
    public void ShipRemainsBeforeTheIndependentCactuarAndAuxiliaryAppends()
    {
        using WorldAssets assets = new(GameDirectory);
        using GameAssets game = new(GameDirectory);
        NativeWorldEntryMemory entry = NativeWorldEntryMemory.CreateNewGame().WriteBytes(256, 2, 1350).WriteBytes(264, 1, 5);
        NativeEventMemory memory = NativeEventMemory.Empty.Write(1536, 1, NativeScalarValue.Exact(1));
        NativeGardenWorldAllocation allocation = assets.ReadGardenAllocation(new(12, 48, -1), entry, memory, NativeSavedWorldState.CreateNewGame(), game.ReadNewGameCharacterVitals());
        Assert.Equal(new byte[] { 0, 2, 3, 64, 65, 88, 94, 79, 80, 79, 80 }, allocation.Objects.Select(item => item.ShapeId));
        Assert.Equal(3, allocation.Garden64Index);
        Assert.Equal(4, allocation.Garden65Index);
        Assert.Equal(6, allocation.CactuarIndex);
        Assert.Equal(0, allocation.Objects[5].Flags);
    }
}
