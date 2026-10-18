using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffectOwnedModelTests
{
    [Fact]
    public void InitializerWritesOnlyItsDeclaredEmbeddedFieldsAndRequiresExactSymbolicOwners()
    {
        using GameAssets game = Installed();
        NativeBattleSearchModelResources profile = game.ReadNativeSearchModelResources();
        NativeBattleModulePoolTask prior = Task(0xA5);
        NativeBattleEffectOwnedModelStep initialized = NativeBattleEffectOwnedModel.Initialize8DD0F0(prior, 0x2200000, profile.Model, 3, profile.DefaultColor44);
        Assert.Null(initialized.State.DecodedPose);
        Assert.Equal(0x2200000u, initialized.State.TaskAddress);
        Assert.Equal(profile.Model.Address, initialized.State.ResourceAddress);
        Assert.Equal(prior.Flags, initialized.Task.Flags);
        Assert.Equal(prior.Next, initialized.Task.Next);
        Assert.Equal(prior.Callback, initialized.Task.Callback);
        HashSet<int> written = [0x30, 0x90, 0x91];
        foreach ((int offset, int count) in new (int, int)[] { (0xA8, 4), (0x5C, 4), (0x94, 8), (0xB4, 4), (0xCC, 16), (0xAC, 4), (0x58, 4), (0xDE, 2), (0x10C, 4), (0x13E, 2) })
        {
            for (int index = 0; index < count; index++)
            {
                written.Add(offset + index);
            }
        }
        for (int offset = 12; offset < 0x154; offset++)
        {
            if (!written.Contains(offset))
            {
                Assert.Equal((byte)0xA5, initialized.Task.ReadByte(offset));
            }
        }
        Assert.Equal((byte)0xA7, initialized.Task.ReadByte(0x30));
        Assert.Equal(profile.DefaultColor44, initialized.Task.ReadDword(0x5C));
        Assert.Equal(0x00808080u, initialized.Task.ReadDword(0x58));
        Assert.Equal(uint.MaxValue, initialized.Task.ReadDword(0xAC));
        Assert.Equal(0x22000CCu, initialized.Task.ReadDword(0x94));
        Assert.Equal(0x22000DCu, initialized.Task.ReadDword(0xB4));
        Assert.Equal((ushort)3, initialized.Task.ReadWord(0xDE));
        Assert.Equal((ushort)4096, initialized.Task.ReadWord(0x13E));
        for (int section = 0; section < 4; section++)
        {
            Assert.Equal(profile.Model.Address + (uint)profile.Model.Section(section), initialized.Task.ReadDword(0xCC + section * 4));
        }
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectOwnedModel.Validate(initialized.Task, initialized.State with { TaskAddress = 0x2200004 }, profile.Model));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectOwnedModel.Validate(initialized.Task.WriteDword(0xD0, 0), initialized.State, profile.Model));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectOwnedModel.Validate(initialized.Task, initialized.State, profile.Model with { Address = profile.Model.Address + 4 }));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffectOwnedModel.Select8DD190(initialized.Task, initialized.State, profile.Model, 0));
    }

    [Fact]
    public void AllFiveOriginalPosesRetainExactCursorsRestoreAndDistinguishLoopFromOnceCompletion()
    {
        using GameAssets game = Installed();
        NativeBattleSearchModelResources profile = game.ReadNativeSearchModelResources();
        NativeBattleEffectOwnedModelResource resource = profile.Model;
        ImmutableArray<byte> counts = NativeBattlePoseDecoder.ReadFrameCounts(resource.PoseSource.Animations.AsSpan());
        Assert.Equal(new byte[] { 6, 73, 6, 32, 6 }, counts);
        ImmutableArray<ImmutableArray<NativeBattleDecodedPose>> reference = NativeBattlePoseDecoder.DecodeFrames(resource.PoseSource.Animations.AsSpan(), resource.PoseSource.BoneCount);
        NativeBattleEffectOwnedModelStep initialized = NativeBattleEffectOwnedModel.Initialize8DD0F0(Task(0), 0x2200000, resource, 3, profile.DefaultColor44);
        Assert.Throws<NotSupportedException>(() => NativeBattleEffectOwnedModel.Select8DD190(initialized.Task, initialized.State, resource, -1));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffectOwnedModel.Select8DD190(initialized.Task, initialized.State, resource, 5));
        for (short animation = 0; animation < counts.Length; animation++)
        {
            NativeBattleEffectOwnedModelStep selected = NativeBattleEffectOwnedModel.Select8DD190(initialized.Task.WriteWord(0x138, 7).WriteByte(0x9D, 0xA0), initialized.State, resource, animation);
            Assert.Equal((ushort)0, selected.Task.ReadWord(0x136));
            Assert.Equal((ushort)7, selected.Task.ReadWord(0x138));
            Assert.Equal((byte)0xA4, selected.Task.ReadByte(0x9D));
            Assert.Equal(JsonSerializer.Serialize(reference[animation][0]), JsonSerializer.Serialize(selected.State.DecodedPose));
            NativeBattleEffectOwnedModelStep current = selected;
            for (int frame = 2; frame <= counts[animation]; frame++)
            {
                NativeBattleEffectOwnedModelStep restored = JsonSerializer.Deserialize<NativeBattleEffectOwnedModelStep>(JsonSerializer.Serialize(current))!;
                NativeBattleEffectOwnedModelStep next = NativeBattleEffectOwnedModel.AdvanceOnce8DD220(current.Task, current.State, resource);
                NativeBattleEffectOwnedModelStep coldNext = NativeBattleEffectOwnedModel.AdvanceOnce8DD220(restored.Task, restored.State, resource);
                Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(coldNext));
                Assert.False(next.Completed);
                Assert.Equal((byte)frame, next.Task.ReadByte(0xA2));
                Assert.Equal(JsonSerializer.Serialize(reference[animation][frame - 1]), JsonSerializer.Serialize(next.State.DecodedPose));
                current = next;
            }
            NativeBattleEffectOwnedModelStep once = NativeBattleEffectOwnedModel.AdvanceOnce8DD220(current.Task, current.State, resource);
            Assert.True(once.Completed);
            Assert.Equal(counts[animation], once.Task.ReadByte(0xA2));
            Assert.Equal((ushort)0, once.Task.ReadWord(0x138));
            Assert.Equal(JsonSerializer.Serialize(current.State.DecodedPose), JsonSerializer.Serialize(once.State.DecodedPose));
            NativeBattleEffectOwnedModelStep loop = NativeBattleEffectOwnedModel.AdvanceLoop8DD1C0(current.Task.WriteWord(0x136, 0xFFFF).WriteWord(0x138, 0xFFFF), current.State, resource);
            Assert.True(loop.Completed);
            Assert.Equal((ushort)0, loop.Task.ReadWord(0x136));
            Assert.Equal((ushort)0, loop.Task.ReadWord(0x138));
            Assert.Equal((byte)1, loop.Task.ReadByte(0xA2));
            Assert.Equal(selected.Task.ReadWord(0x9E), loop.Task.ReadWord(0x9E));
            Assert.Equal(selected.Task.ReadWord(0xA0), loop.Task.ReadWord(0xA0));
            Assert.Equal(JsonSerializer.Serialize(selected.State.DecodedPose), JsonSerializer.Serialize(loop.State.DecodedPose));
            NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
            Assert.Equal(34, NativeBattlePoseMatrices.EffectOwnedGeometryMatrices(resource.PoseSource, loop.State.DecodedPose!, identity, identity).Length);
        }
    }

    private static NativeBattleModulePoolTask Task(byte fill) => new(1, 9, null, 0x755A80, Enumerable.Repeat(fill, 0x154 - 12).ToImmutableArray());
    private static GameAssets Installed()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        return new(directory);
    }
}
