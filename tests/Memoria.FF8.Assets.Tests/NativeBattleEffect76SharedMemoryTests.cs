using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect76Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect76SharedMemoryTests
{
    private sealed class ExecutableCopies : INativeBattleTelemetrySink
    {
        public long Bytes { get; private set; }

        public void Record(int siteId, NativeBattleTrafficKind kind, long payloadBytes)
        {
            if (siteId == 1043 && kind == NativeBattleTrafficKind.CpuCopyPayload)
            {
                Bytes = checked(Bytes + payloadBytes);
            }
        }
    }

    [Fact]
    public void Shared_owner_preserves_all_original_resources_without_another_executable_snapshot()
    {
        using GameAssets assets = Open();
        _ = assets.ReadNativeCureResources();
        ExecutableCopies productionCopies = new();
        NativeBattleEffect76Resources inputs;
        using (NativeBattleTelemetry.BeginScope(productionCopies))
        {
            inputs = assets.ReadNativeEffect76Resources();
        }
        Assert.Equal(0, productionCopies.Bytes);
        Assert.Same(inputs, assets.ReadNativeEffect76Resources());
        byte[] executable = File.ReadAllBytes(Path.Combine(assets.GameDirectory, "FF8_EN.exe"));
        NativeExecutableMemory memory = new(executable);
        ExecutableCopies sharedCopies = new();
        NativeBattleEffect76Resources shared;
        using (NativeBattleTelemetry.BeginScope(sharedCopies))
        {
            shared = NativeBattleEffect76ResourcesReader.Read(memory, inputs.MainTim, inputs.StageFile38);
        }
        ExecutableCopies isolatedCopies = new();
        NativeBattleEffect76Resources isolated;
        using (NativeBattleTelemetry.BeginScope(isolatedCopies))
        {
            isolated = NativeBattleEffect76ResourcesReader.Read(executable, inputs.MainTim, inputs.StageFile38);
        }
        Assert.Equal(JsonSerializer.Serialize(isolated), JsonSerializer.Serialize(shared));
        Assert.Equal(0, sharedCopies.Bytes);
        Assert.Equal(executable.LongLength, isolatedCopies.Bytes);
        Assert.Same(inputs.MainTim, shared.MainTim);
        Assert.Same(inputs.StageFile38, shared.StageFile38);
    }

    [Fact]
    public void Resource_result_and_authenticated_owner_survive_caller_and_read_buffer_mutation()
    {
        using GameAssets assets = Open();
        NativeBattleEffect76Resources inputs = assets.ReadNativeEffect76Resources();
        byte[] executable = File.ReadAllBytes(Path.Combine(assets.GameDirectory, "FF8_EN.exe"));
        NativeExecutableMemory memory = new(executable);
        NativeBattleEffect76Resources first = NativeBattleEffect76ResourcesReader.Read(memory, inputs.MainTim, inputs.StageFile38);
        string expected = JsonSerializer.Serialize(first);
        executable[0] ^= 1;
        byte[] borrowedRead = memory.Read(CameraAddress, CameraLength);
        borrowedRead[0] ^= 1;
        byte[] exportedRead = first.ReadStatic(CameraAddress, CameraLength);
        exportedRead[0] ^= 1;
        NativeBattleEffect76Resources repeated = NativeBattleEffect76ResourcesReader.Read(memory, inputs.MainTim, inputs.StageFile38);
        Assert.Equal(expected, JsonSerializer.Serialize(first));
        Assert.Equal(expected, JsonSerializer.Serialize(repeated));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect76ResourcesReader.Read(executable, inputs.MainTim, inputs.StageFile38));
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed original resources for shared native owner validation.");
        return new(directory!);
    }
}
