using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRestorationVariantTests
{
    [Theory]
    [InlineData((ushort)57, -0x8FE20, -0xF2E80, 0x17A3DD0u, 0x159E3C0u, 0x6534)]
    [InlineData((ushort)67, -0xAE550, -0xFFEA0, 0x178F3F4u, 0x1595758u, 0x5C0C)]
    public void Proved_profiles_relocate_all_callbacks_and_physical_pools_without_changing_native_scalars(ushort effect, int codeDelta, int globalDelta, uint executable, uint animation, int length)
    {
        NativeBattleRestorationProfile basis = NativeBattleRestorationProfile.Esuna;
        NativeBattleRestorationProfile profile = NativeBattleRestorationProfile.ForEffect(effect);
        Assert.Equal(executable, profile.ExecutableAddress);
        Assert.Equal(animation, profile.AnimationAddress);
        Assert.Equal(animation + 0xB28, profile.AssetNameAddress);
        Assert.Equal(length, NativeBattleRestorationExecutableState.Length(effect));
        Assert.Equal(new[] { basis.Entry, basis.Initializer, basis.MainCallback, basis.TargetCallback, basis.GenericCallback, basis.FadeInCallback, basis.FadeOutCallback }.Select(value => checked((uint)((long)value + codeDelta))), new[] { profile.Entry, profile.Initializer, profile.MainCallback, profile.TargetCallback, profile.GenericCallback, profile.FadeInCallback, profile.FadeOutCallback });
        Assert.Equal(checked((uint)((long)basis.InitializerGlobal + globalDelta)), profile.InitializerGlobal);
        Assert.Equal(basis.Descriptors.Select(value => checked((uint)((long)value + globalDelta))), profile.Descriptors);
        Assert.Equal(basis.Banks.Select(value => checked((uint)((long)value + globalDelta))), profile.Banks);
        Assert.Equal((basis.DrawBankBytes, basis.EmitterBytes, basis.GeometryBytes, basis.TargetReleaseCounter, basis.RootWait), (profile.DrawBankBytes, profile.EmitterBytes, profile.GeometryBytes, profile.TargetReleaseCounter, profile.RootWait));
        Assert.True(profile.UsesEsunaMesh);
        Assert.False(NativeBattleRestorationProfile.FullLife.UsesEsunaMesh);
    }

    [Fact]
    public void New_executable_owners_remain_unknown_without_authenticated_input_and_preserve_existing_JSON_names()
    {
        NativeBattleEffectMemory generic = NativeBattleEffectMemory.FromColdProcess();
        Assert.Null(generic.Effect57Executable);
        Assert.Null(generic.RemedyExecutable);
        string oldJson = JsonSerializer.Serialize(generic);
        Assert.DoesNotContain("Effect57Executable", oldJson);
        Assert.DoesNotContain("RemedyExecutable", oldJson);
        NativeBattleEffectMemory old = JsonSerializer.Deserialize<NativeBattleEffectMemory>(oldJson)!;
        Assert.Null(old.BeginLoad(56).GetRestorationExecutable(57));
        Assert.Null(old.BeginLoad(66).GetRestorationExecutable(67));
        Assert.Equal(generic.EsunaExecutable, old.EsunaExecutable);
        Assert.Equal(generic.FullLifeExecutable, old.FullLifeExecutable);

        NativeBattleRestorationExecutableState first = new(57, 1, null);
        NativeBattleRestorationExecutableState second = new(67, 2, null);
        NativeBattleEffectMemory declared = generic.WithRestorationExecutable(first).WithRestorationExecutable(second);
        Assert.Same(first, declared.GetRestorationExecutable(57));
        Assert.Same(second, declared.GetRestorationExecutable(67));
        Assert.Same(generic.EsunaExecutable, declared.EsunaExecutable);
        Assert.Same(generic.FullLifeExecutable, declared.FullLifeExecutable);
        NativeBattleEffectMemory clone = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(declared))!;
        Assert.Equal(first, clone.Effect57Executable);
        Assert.Equal(second, clone.RemedyExecutable);
        Assert.Throws<InvalidDataException>(() => (declared with { RemedyExecutable = first }).Validate());
        Assert.Throws<InvalidDataException>(() => (declared with { Effect57Executable = second }).Validate());
    }

    [Theory]
    [InlineData((ushort)24)]
    [InlineData((ushort)57)]
    [InlineData((ushort)67)]
    public void Esuna_style_mesh_preparation_accepts_only_proved_generic_callbacks_and_preserves_uv_word_wrapping(ushort effect)
    {
        var fixture = NativeBattleRestorationControllerTests.Fixture(effect);
        NativeBattleRestorationProfile profile = NativeBattleRestorationProfile.ForEffect(effect);
        NativeBattleModulePoolTask task = fixture.Frame.Effect.Pools[2].Slots[0] with { Callback = profile.GenericCallback };
        task = task.WriteWord(0x34 + 0x2A, 255);
        NativeBattleEsunaMeshPreparation prepared = NativeBattleRestorationMeshPackets.Prepare88E090(task, 1, 0, 0, null);
        Assert.Equal((ushort)1, prepared.Task.ReadWord(0x34 + 0x2A));
        Assert.Equal((ushort)1, prepared.Context.VOffset);
        Assert.Equal(new NativeBattleTextureWindow(0, 0, 128, 128), prepared.Context.WindowAfter);
        Assert.Throws<InvalidDataException>(() => NativeBattleRestorationMeshPackets.Prepare88E090(task with { Callback = NativeBattleRestorationProfile.FullLife.GenericCallback }, 1, 0, 0, null));
    }
}
