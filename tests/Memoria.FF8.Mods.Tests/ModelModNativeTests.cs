using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;
using System.Security.Cryptography;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Mods.Tests;

public sealed class ModelModNativeTests
{
    [Fact]
    public void EditedClipIsDecodedByNativeGameplayPoseSourceWithoutChangingOtherClips()
    {
        BattleModel original = Model();
        EditableModel editable = ModelExchange.FromBattle(original);
        EditableAnimation changed = editable.Animations[1] with { Channels = editable.Animations[1].Channels.Select(channel => channel.Path == "translation" ? channel with { Values = [new(2, 0, 0, 0), new(3, 0, 0, 0)] } : channel).ToArray() };
        ModelModInstallPlan plan = ModelModInstaller.Plan([new("battle/test.dat", editable, false, [new("embedded", 1, changed)])]);
        ModelMods mods = Prepare(plan);
        BattleModel result = mods.Apply("battle/test.dat", original);
        Assert.Equal(original.NativePoseSource!.Sample(0, 1, 0).Translation, result.NativePoseSource!.Sample(0, 1, 0).Translation);
        Assert.Equal(original.NativePoseSource.Sample(0, 1, 0).Bones.ToArray(), result.NativePoseSource.Sample(0, 1, 0).Bones.ToArray());
        Assert.Equal(-200, result.NativePoseSource.Sample(1, 1, 0).Translation.X);
        Assert.Equal(-300, result.NativePoseSource.Sample(1, 2, 0).Translation.X);
        Assert.Equal(new byte[] { 1, 2 }, NativeBattlePoseDecoder.ReadFrameCounts(result.NativePoseSource.Animations.AsSpan()));
    }

    [Theory]
    [InlineData("finisher-165", 0xD780C8u)]
    [InlineData("victory-0", null)]
    public void StagedNativeResolutionUsesOnlyExplicitlyBoundBankAndPreservesOtherOwner(string bank, uint? directory)
    {
        BattleModel original = Model();
        EditableModel editable = ModelExchange.FromBattle(original);
        EditableAnimation changed = editable.Animations[1] with { Channels = editable.Animations[1].Channels.Select(channel => channel.Path == "translation" ? channel with { Values = [new(2, 0, 0, 0), new(3, 0, 0, 0)] } : channel).ToArray() };
        ModelMods mods = Prepare(ModelModInstaller.Plan([new("battle/test.dat", editable, false, [new(bank, 1, changed)])]));
        BattleModel modified = mods.ApplyBattleBank("battle/test.dat", bank, original);
        NativeBattlePoseSource source = original.NativePoseSource!;
        NativeBattleGraphicsResource resource = new([0, 0])
        {
            PoseSource = source,
            AnimationFrames = [1, 2],
            AnimationBankOverrides = [new(bank, directory, false, Convert.ToHexString(SHA256.HashData(source.Animations.AsSpan())), modified.NativePoseSource!.Animations)]
        };
        NativeBattleGraphicsModel model = new(0, 3) { ActiveStagedResourceRow100 = 100, ActiveAnimationDirectory = directory, ActiveAnimationBank = directory is null ? bank : null };
        NativeBattleGraphicsResource resolved = NativeBattleVictoryResources.Resolve(model, resource, ReadPointer, Read);
        Assert.Equal(-200, resolved.PoseSource!.Sample(1, 1, 0).Translation.X);
        NativeBattleGraphicsResource unrelated = NativeBattleVictoryResources.Resolve(model with { ActiveAnimationDirectory = 123, ActiveAnimationBank = null }, resource, ReadPointer, Read);
        Assert.Equal(0, unrelated.PoseSource!.Sample(1, 1, 0).Translation.X);
        string json = System.Text.Json.JsonSerializer.Serialize(resource);
        Assert.Single(System.Text.Json.JsonSerializer.Deserialize<NativeBattleGraphicsResource>(json)!.AnimationBankOverrides);

        NativeBattleResourcePointer? ReadPointer(uint address) => address switch
        {
            100 => new(null, 1000),
            108 => new(null, 2000, source.Animations.Length),
            112 => new(null, 3000, 2),
            _ => null
        };
        byte[] Read(NativeBattleResourcePointer pointer, int length) => pointer.StagingAddress switch
        {
            1000 => source.Skeleton.AsSpan().Slice(0, length).ToArray(),
            2000 => source.Animations.AsSpan().Slice(0, length).ToArray(),
            3000 => [0, 0],
            _ => throw new InvalidDataException()
        };
    }

    [Fact]
    public void PreviewRejectsImpossibleNativeTranslationsAndLeavesUncheckedTracks()
    {
        EditableModel original = ModelExchange.FromBattle(Model());
        EditableModel edited = original with { Bones = [original.Bones[0], new("bone_1", 0, Vector3.Zero, Quaternion.Identity, Vector3.One) { NativeIndex = 1 }] };
        Assert.Throws<NotSupportedException>(() => ModelMods.Preview(original, new("battle/test.dat", edited, false, [])));
        Assert.Same(original.Animations[0], ModelMods.Preview(original, new("battle/test.dat", original, false, [])).Animations[0]);
    }

    private static ModelMods Prepare(ModelModInstallPlan plan)
    {
        ModFile[] files = plan.Files.Select(output => new ModFile("sample", ModFormat.Memoria, output.RelativePath, 1, () => output.Content)).ToArray();
        Assert.True(ModelMods.TryPrepare(new(1, [], files), out ModelMods mods, out var diagnostics), string.Join("; ", diagnostics.Select(item => item.Message)));
        return mods;
    }

    private static BattleModel Model()
    {
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        byte[] section = [2, 0, 0, 0, 12, 0, 0, 0, 16, 0, 0, 0, 1, 0, 0, 0, 2, 0, 0, 0, 0, 0];
        BattlePose pose = new(Vector3.Zero, [Vector3.Zero], [Vector3.One]);
        return new("test.dat", [new(-1, 0)], [], [], [new([pose]), new([pose, pose])], 1) { NativePoseSource = new(skeleton.ToImmutableArray(), section.ToImmutableArray()) };
    }
}
