using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeTrainDoorModelPartsTests
{
    private const string GameDirectory = @"C:\Steam\steamapps\common\FINAL FANTASY VIII";

    [Fact]
    public void OriginalTimberDoorRetainsSeparatePartAndSkinTables()
    {
        using GameAssets assets = new(GameDirectory);
        FieldAssets field = assets.LoadField(919);
        FieldModel model = field.Models[13];
        NativeModelPartsDefinition definition = Assert.IsType<NativeModelPartsDefinition>(model.NativeParts);

        Assert.Equal("titown4", field.Name);
        Assert.Equal("o038???", model.Name);
        Assert.Equal(2, model.Bones.Count);
        Assert.Equal(1004, model.Vertices.Count);
        Assert.Equal(811, model.Faces.Count);
        Assert.Single(model.Animations);
        Assert.Single(model.Animations[0].Frames);
        Assert.Equal("A07F93363187838920BAB4B834F74338D95600F9B9DDB355F24464BDDA1ADFC6", definition.SourceHash);
        Assert.Equal(447556, definition.SourceOffset);
        short[][] originalRows =
        {
            [0, 1, 0, 0, 0, 0, 0, 0, 0, 194, 0, 613, 0, 0, 0, 0],
            [1, 1, 0, 0, 0, 0, 0, 0, 194, 0, 613, 2, 0, 0, 0, 0],
            [2, 1, 0, 0, 0, 0, 0, 0, 194, 0, 615, 2, 0, 0, 0, 0]
        };
        Assert.Equal(3, definition.Parts.Count);
        for (int part = 0; part < originalRows.Length; part++)
        {
            Assert.Equal(originalRows[part], definition.Parts[part].Words);
        }
        Assert.Equal(new NativeModelSkinBinding[]
        {
            new(0, 988, 1, 0),
            new(988, 8, 1, 0),
            new(996, 8, 1, 0)
        }, definition.SkinBindings);

        // The two door leaves share bone1; their part bindings distinguish them.
        Assert.Equal(1, Assert.Single(model.Vertices.Select(vertex => vertex.Bone).Distinct()));
        Assert.All(model.Vertices.Take(988), vertex => Assert.Equal(0, vertex.SkinBinding));
        Assert.All(model.Vertices.Skip(988).Take(8), vertex => Assert.Equal(1, vertex.SkinBinding));
        Assert.All(model.Vertices.Skip(996), vertex => Assert.Equal(2, vertex.SkinBinding));
        Assert.Equal(988, definition.SkinBindings[definition.Parts[1].FirstSkinBinding].FirstVertex);
        Assert.Equal(996, definition.SkinBindings[definition.Parts[2].FirstSkinBinding].FirstVertex);
    }

    [Fact]
    public void FreshConstructorClearsOnlyFlagsAndDoesNotAliasOriginalWords()
    {
        using GameAssets assets = new(GameDirectory);
        NativeModelPartsDefinition original = assets.LoadField(919).Models[13].NativeParts!;
        short[] words = original.Parts[1].Words.ToArray();
        words[2] = -7;
        words[4] = -300;
        words[5] = 23;
        words[6] = 456;
        words[7] = 0x1234;
        words[12] = 32760;
        words[13] = -234;
        words[14] = 789;
        words[15] = -30;
        NativeModelPartsDefinition definition = original with { Parts = [original.Parts[0], new(words), original.Parts[2]] };
        NativeModelPartResourceState state = NativeModelPartResourceState.Create(definition);

        Assert.Equal((short)0x1234, words[7]);
        Assert.NotSame(words, state.Parts[1]);
        for (int word = 0; word < 16; word++)
        {
            Assert.Equal(word == 7 ? (short)0 : words[word], state.Parts[1][word]);
        }
        state.Validate(definition);
        Assert.Empty(state.Step(definition).SkinTranslations);
        NativeModelPartStep wrappedTarget = state.StartTranslation(1, 32, 0, 207, 30).Step(definition);
        Assert.Equal((short)32760, wrappedTarget.State.Parts[1][4]);
        Assert.Equal((short)-32744, wrappedTarget.State.Parts[1][12]);
        Assert.Equal(new Vector3(30577, -234, 795), wrappedTarget.SkinTranslations[1]);
    }

    [Theory]
    [InlineData(1, 1, 6)]
    [InlineData(2, 2, 13)]
    [InlineData(15, 16, 103)]
    [InlineData(29, 30, 200)]
    [InlineData(30, 32, 207)]
    public void ActualDoorLeafUsesNativeIntegerSamplesAndPurePose(int callback, int x, int z)
    {
        using GameAssets assets = new(GameDirectory);
        FieldModel model = assets.LoadField(919).Models[13];
        NativeModelPartsDefinition definition = model.NativeParts!;
        NativeModelPartResourceState state = NativeModelPartResourceState.Create(definition).StartTranslation(1, 32, 0, 207, 30);
        NativeModelPartStep sample = state.Step(definition);
        for (int index = 1; index < callback; index++)
        {
            sample = sample.State.Step(definition);
        }
        Assert.Equal(new Vector3(x, 0, z), Assert.Single(sample.SkinTranslations).Value);
        Assert.Equal(1, Assert.Single(sample.SkinTranslations).Key);
        Assert.Equal((short)(callback + 1), sample.State.Parts[1][2]);
        Assert.Equal(callback == 30 ? (short)4 : (short)1, sample.State.Parts[1][7]);
        string before = JsonSerializer.Serialize(sample.State);
        Vector3[] baseline = model.Pose(0, 0);
        Vector3[] translated = model.Pose(0, 0, skinTranslations: sample.SkinTranslations);
        Vector3 delta = translated[988] - baseline[988];
        Assert.Equal(MathF.Sqrt(x * x + z * z), delta.Length(), 3);
        for (int vertex = 0; vertex < baseline.Length; vertex++)
        {
            if (vertex is >= 988 and < 996)
            {
                Assert.True(Vector3.Distance(delta, translated[vertex] - baseline[vertex]) < 0.001f);
            }
            else
            {
                Assert.Equal(baseline[vertex], translated[vertex]);
            }
        }
        Assert.Equal(translated, model.Pose(0, 0, skinTranslations: sample.SkinTranslations));
        Assert.Equal(before, JsonSerializer.Serialize(sample.State));
        NativeModelPartResourceState restored = JsonSerializer.Deserialize<NativeModelPartResourceState>(before)!;
        Assert.Equal(JsonSerializer.Serialize(sample.State.Step(definition)), JsonSerializer.Serialize(restored.Step(definition)));
        if (callback == 30)
        {
            NativeModelPartStep retained = sample.State.Step(definition);
            Assert.Equal(new Vector3(32, 0, 207), retained.SkinTranslations[1]);
            Assert.Equal((short)31, retained.State.Parts[1][2]);
            NativeModelPartStep reversed = retained.State.StartTranslation(1, -32, 0, -207, 30).Step(definition);
            Assert.Equal(new Vector3(31, 0, 201), reversed.SkinTranslations[1]);
        }
    }

    [Fact]
    public void PerSkinOrderSignedDurationAndCounterWrapFollowNativeWords()
    {
        using GameAssets assets = new(GameDirectory);
        NativeModelPartsDefinition original = assets.LoadField(919).Models[13].NativeParts!;
        short[] words = original.Parts[0].Words.ToArray();
        words[1] = 2;
        NativeModelPartsDefinition twoSkins = original with { Parts = [new(words)] };
        NativeModelPartStep ordered = NativeModelPartResourceState.Create(twoSkins).StartTranslation(0, 32, 0, 207, 30).Step(twoSkins);
        Assert.Equal(new Vector3(1, 0, 6), ordered.SkinTranslations[0]);
        Assert.Equal(new Vector3(2, 0, 13), ordered.SkinTranslations[1]);
        Assert.Equal((short)3, ordered.State.Parts[0][2]);

        NativeModelPartStep negative = NativeModelPartResourceState.Create(original).StartTranslation(1, -32, 0, -207, -1).Step(original);
        Assert.Equal(new Vector3(32, 0, 207), negative.SkinTranslations[1]);
        Assert.Equal((short)4, negative.State.Parts[1][7]);
        Assert.Equal(new Vector3(-32, 0, -207), negative.State.Step(original).SkinTranslations[1]);

        NativeModelPartResourceState state = NativeModelPartResourceState.Create(original).StartTranslation(1, 32, 0, 207, short.MaxValue);
        for (int callback = 1; callback < short.MaxValue; callback++)
        {
            state = state.Step(original).State;
        }
        NativeModelPartStep wrapped = state.Step(original);
        Assert.Equal(new Vector3(32, 0, 207), wrapped.SkinTranslations[1]);
        Assert.Equal(short.MinValue, wrapped.State.Parts[1][2]);
        Assert.Equal((short)1, wrapped.State.Parts[1][7]);
        Assert.Equal(new Vector3(-32, 0, -207), wrapped.State.Step(original).SkinTranslations[1]);
        Assert.Throws<NotSupportedException>(() => state.StartTranslation(0, 1, 2, 3, 0));
    }
}
