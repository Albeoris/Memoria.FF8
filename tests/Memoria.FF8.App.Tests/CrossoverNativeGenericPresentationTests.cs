using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverNativeGenericPresentationTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData((ushort)1, false, (byte)2)]
    [InlineData((ushort)2, true, (byte)2)]
    [InlineData((ushort)21, true, (byte)2)]
    [InlineData((ushort)22, true, (byte)2)]
    [InlineData((ushort)40, true, (byte)2)]
    [InlineData((ushort)16, true, (byte)4)]
    public void Shared_native_dispatcher_presents_original_spell_with_one_contact(ushort ability, bool self, byte command)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets battle = new(TestResources.GameDirectory);
        NativeBattleGraphicsResource resource = battle.ReadNativeGraphicsResource(new() { IsPlayer = true, ModelId = 0, WeaponId = 0 });
        NativeBattleGraphicsModel model = ReadSource(resource);
        BattleNativeAction action = battle.NativeActions.Single(action => action.Command == command && action.ActionId == ability);
        CrossoverNativeMagicPresentation presentation = new(game, resource, battle);
        CrossoverNativeMagicPresentationState state = presentation.Begin(model, self ? 0 : 4, NativeCrtRandomState.FromColdProcess(), new(self ? (byte)0 : (byte)3, 4, 0, 0, 0, 83, 0), action, new(0));
        NativeBattleCameraProjection projection = Camera();
        bool admitted = presentation.TryPreflight(state, Anchor, Transform, projection, null, out string? reason);
        Assert.True(admitted, $"{action.Name}: {reason}");
        Assert.Null(state.Effect);
        Assert.Equal(0, state.PublishedContacts);
        int contacts = 0;
        int sounds = 0;
        int visualCallbacks = 0;
        int peakStageDepth = 0;
        for (int callback = 0; callback < 4096 && !state.Complete; callback++)
        {
            CrossoverNativeMagicPresentationStep step = presentation.Advance(state, Anchor, Transform, projection);
            state = step.State;
            peakStageDepth = Math.Max(peakStageDepth, state.StageDepth);
            contacts += step.Contact ? 1 : 0;
            sounds += step.Sounds.Length + step.EffectSounds.Length;
            visualCallbacks += !state.Draws.IsEmpty || state.Ordering is { Proxies.Count: > 0 } ? 1 : 0;
        }
        Assert.True(state.Complete);
        Assert.Equal(1, contacts);
        Assert.Equal(1, state.PublishedContacts);
        Assert.NotNull(state.Effect);
        Assert.Equal(action.MagicId, state.Effect.Graphics.OriginalEffect!.Binding.Effect);
        Assert.True(sounds > 0);
        Assert.True(visualCallbacks > 0);
        if (command == 4 && ability == 16)
        {
            Assert.Equal(1024, peakStageDepth);
        }
        output.WriteLine($"{action.Name}: original effect {action.MagicId}; {visualCallbacks} visual callbacks; {sounds} sound requests; {contacts} contact.");
    }

    [Fact]
    public void Native_geometry_requirement_is_rejected_before_host_mutation()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets battle = new(TestResources.GameDirectory);
        NativeBattleGraphicsResource resource = battle.ReadNativeGraphicsResource(new() { IsPlayer = true, ModelId = 0, WeaponId = 0 });
        BattleNativeAction action = battle.NativeActions.Single(action => action.Command == 2 && action.ActionId == 21);
        CrossoverNativeMagicPresentation presentation = new(game, resource, battle);
        CrossoverNativeMagicPresentationState state = presentation.Begin(ReadSource(resource), 0, NativeCrtRandomState.FromColdProcess(), new(3, 4, 0, 1, 0, 83, 0), action, new(0));
        bool admitted = presentation.TryPreflight(state, Anchor, Transform, Camera(), null, out string? reason);
        Assert.False(admitted);
        Assert.False(string.IsNullOrWhiteSpace(reason));
        Assert.Contains("host", reason, StringComparison.OrdinalIgnoreCase);
        Assert.Null(state.Effect);
        Assert.Equal(0, state.PublishedContacts);
        output.WriteLine(reason);
    }

    private static NativeBattleGraphicsModel ReadSource(NativeBattleGraphicsResource resource)
    {
        NativeGlobalRandomState random = new(0);
        NativeBattleGraphicsModel source = NativeBattleGraphicsModels.CompleteLoad(new(0, 0)
        {
            Transform = new(new(0, 0, 2200), new(0, 0, 2200), 0, 0, 4096),
            LinkedTransform = new(default, default),
            SequenceState = new() { Locals = NativeBattleSequenceState.ColdProcessLocals() }
        }, 0, 0, ref random);
        source = NativeBattleSequenceMachine.Advance(NativeBattleGraphicsModels.SelectSequence(source, 1), resource, new(0, 0)).Model;
        return source with { Flags2 = (ushort)(source.Flags2!.Value & ~4), GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(source, resource.PoseSource!) };
    }

    private static NativeBattleModuleAnchor Anchor() => new(0, -600, -2200, 500);
    private static NativeBattleModelTransform Transform() => new(new(0, 0, -2200), new(0, 0, -2200), 0, 0, 1024);
    private static NativeBattleCameraProjection Camera() => new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 7000, 4096), 216, 160, 108);
}
