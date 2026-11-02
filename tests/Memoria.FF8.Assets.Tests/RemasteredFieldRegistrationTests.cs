using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class RemasteredFieldRegistrationTests
{
    [Theory]
    [InlineData(false, NativeFieldRegistrationEntry.FieldEntry)]
    [InlineData(false, NativeFieldRegistrationEntry.BattleReturn)]
    [InlineData(true, NativeFieldRegistrationEntry.FieldEntry)]
    [InlineData(true, NativeFieldRegistrationEntry.BattleReturn)]
    public void Classroom_retained_models_register_and_continue_from_their_edition_allocation(bool remastered, NativeFieldRegistrationEntry entry)
    {
        string variable = remastered ? "MEMORIA_FF8_REMASTERED_DIR" : "MEMORIA_FF8_GAME_DIR";
        string? directory = Environment.GetEnvironmentVariable(variable);
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory), $"Set {variable} to run the optional installed registration check.");
        using GameAssets game = new(directory!);
        FieldAssets field = game.LoadField(232);
        NativeFieldRegistrationResources source = Assert.IsType<NativeFieldRegistrationResources>(field.NativeRegistration);
        Assert.Equal(remastered ? NativeFieldRuntimeProfile.RemasteredEnImageHash : NativeFieldRuntimeProfile.FF8EnExecutableHash, source.ExecutableHash);
        int groupCount = field.Jsm[0] + field.Jsm[1] + field.Jsm[2] + field.Jsm[3];
        ushort[] groups = Enumerable.Range(0, groupCount).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(field.Jsm.AsSpan(8 + index * 2, 2))).OrderBy(group => group >> 7).ToArray();
        (int Object, int Model)[] bindings = [(1, 0), (9, 8), (11, 10), (14, 13), (15, 14), (16, 15)];
        NativeFieldRetainedActor[] actors = bindings.Select(binding => new NativeFieldRetainedActor(binding.Object, binding.Model, groups[binding.Object] >> 7, (groups[binding.Object] >> 7) + (groups[binding.Object] & 127) + 1)).ToArray();
        NativeBattleRetainedMemory cold = NativeFieldEyeProcess.EnsureOwnedMemory(NativeBattleRetainedMemory.FromColdProcess(source.ExecutableHash));
        byte[] cpuMarker = [0x12, 0x34, 0x56, 0x78];
        NativeBattleModelCpuResources cpu = cold.ModelCpuResources!;
        cold = cold with { ModelCpuResources = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(0, cpu.Cpu.Banks[0].Write(64, cpuMarker)) } } };
        uint programTail = 0x019AB018 + (uint)source.Loader!.Program.Data.Length + 16;
        if (remastered)
        {
            cold.FileStaging!.Write(0x0196B058, cpuMarker);
            cold.FileStaging.Write(programTail, cpuMarker);
        }
        NativeFieldRegistrationStep registered = NativeFieldRegistrationMachine.Register(entry, source, actors, cold);
        registered.State.Validate(registered.Memory);
        Assert.Equal(NativeFieldRegistrationLayout.ForImage(source.ExecutableHash, entry), registered.State.Layout);
        Assert.Equal(bindings.Select(binding => binding.Object), registered.State.Instances.Select(instance => instance.ObjectId));
        Assert.Equal(bindings.Select(binding => binding.Model), registered.State.Resources.Select(resource => resource.ModelId));
        Assert.InRange(registered.State.PreModelAddress, registered.State.MemoryLayout.ArenaAddress, registered.State.MemoryLayout.Limit);
        Assert.InRange(registered.State.EndAddress, registered.State.PreModelAddress, registered.State.MemoryLayout.Limit);
        Assert.InRange((ulong)registered.State.RelocatedSourceAddress + (uint)registered.State.RelocatedSourceLength, (ulong)registered.State.PreModelAddress, registered.State.MemoryLayout.Limit);
        Assert.NotEmpty(registered.Uploads);
        if (remastered)
        {
            Assert.Equal(source.Loader!.Program.Data.ToArray(), registered.Memory.FileStaging!.Read(0x019AB018, source.Loader.Program.Data.Length));
            Assert.Equal(cpuMarker, registered.Memory.ModelCpuResources!.Cpu.Banks[0].Read(64, cpuMarker.Length));
            Assert.Equal(cpuMarker, registered.Memory.FileStaging!.Read(0x0196B058, cpuMarker.Length));
            Assert.Equal(cpuMarker, registered.Memory.FileStaging.Read(programTail, cpuMarker.Length));
        }
        foreach (NativeFieldRegisteredResource resource in registered.State.Resources)
        {
            Assert.Equal(field.Models[resource.ModelId].NativeParts!.SourceHash, resource.SourceHash);
            Assert.Equal(field.Models[resource.ModelId].NativeParts!.SourceOffset, resource.SourceOffset);
        }
        FieldAssets projected = game.BindNativeRegisteredTextures(field, registered.State, registered.Memory.EffectMemory!.Textures!, registered.Memory.FieldTextureAnimations);
        foreach ((int _, int model) in bindings)
        {
            Assert.Equal(field.Models[model].Textures.Count, projected.Models[model].Textures.Count);
            Assert.NotSame(field.Models[model].Textures[0], projected.Models[model].Textures[0]);
        }
        NativeFieldRetainedPresentation[] presentation = bindings.Select(binding => new NativeFieldRetainedPresentation(binding.Object, 0, 0, false, 0, true, 17, 0, 128, 128, 128, false, false, binding.Object == 1 ? 0x80000u : 0u) { EyeBlinkParameter611 = 128 }).ToArray();
        NativeBattleFieldResourceReloadResult presented = NativeFieldRegistrationMachine.RestoreRetainedPresentation(registered.State, registered.Memory, presentation, new NativeCrtRandomState(12345));
        NativeFieldEyeProcess captured = new NativeFieldEyeProcess(1, presented.Memory, presented.CrtRandomAfter).CaptureSnapshot();
        string snapshot = JsonSerializer.Serialize(captured);
        NativeFieldEyeProcess restored = JsonSerializer.Deserialize<NativeFieldEyeProcess>(snapshot)!.RestoreOwned();
        NativeFieldRegistrationState restoredRegistration = JsonSerializer.Deserialize<NativeFieldRegistrationState>(JsonSerializer.Serialize(registered.State))!;
        restored.Validate(restoredRegistration);
        NativeBattleFieldResourceReloadResult first = NativeFieldRegistrationMachine.AdvanceEyes530810(registered.State, captured.RestoreOwned().Memory, captured.CrtRandom);
        NativeBattleFieldResourceReloadResult second = NativeFieldRegistrationMachine.AdvanceEyes530810(restoredRegistration, restored.Memory, restored.CrtRandom);
        NativeFieldEyeProcess firstSnapshot = new NativeFieldEyeProcess(1, first.Memory, first.CrtRandomAfter).CaptureSnapshot();
        NativeFieldEyeProcess secondSnapshot = new NativeFieldEyeProcess(1, second.Memory, second.CrtRandomAfter).CaptureSnapshot();
        Assert.Equal(JsonSerializer.Serialize(firstSnapshot), JsonSerializer.Serialize(secondSnapshot));
        Assert.Equal(snapshot, JsonSerializer.Serialize(captured));
        firstSnapshot.Validate(registered.State);
    }
}
