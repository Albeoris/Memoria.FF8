using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleMoogleCameraResourcesTests
{
    [Fact]
    public void AllTwelveOriginalRowsInitializeCompleteAndContinueAcrossIndependentRestore()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        using GameAssets game = new(directory);
        NativeBattleMoogleCameraResources resources = game.ReadNativeMoogleCameraResources();
        Assert.Equal(12, resources.Sources.Length);
        NativeBattleMoogleCameraState retained = NativeBattleMoogleCamera.Reset732AF0();
        for (int index = 0; index < resources.Sources.Length; index++)
        {
            NativeBattleMoogleCameraSource source = resources.Sources[index];
            Assert.Equal(0x1532C68u + (uint)(index * 0x48), source.Address);
            NativeBattleMoogleCameraStep step = NativeBattleMoogleCamera.Initialize732B10(retained, source, new byte[8].ToImmutableArray(), index == 0 ? 0 : 1);
            int callback = 0;
            while (!step.Complete && callback++ < 4096)
            {
                NativeBattleMoogleCameraState restored = JsonSerializer.Deserialize<NativeBattleMoogleCameraState>(JsonSerializer.Serialize(step.State))!;
                NativeBattleMoogleCameraStep next = NativeBattleMoogleCamera.Advance732E00(step.State);
                Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(NativeBattleMoogleCamera.Advance732E00(restored)));
                step = next;
            }
            Assert.True(step.Complete, "The original camera row did not complete inside the diagnostic callback budget.");
            Assert.False(step.State.EyePaddingKnown);
            retained = step.State;
        }
    }
}
