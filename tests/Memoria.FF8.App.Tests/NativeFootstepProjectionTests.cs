using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeFootstepProjectionTests
{
    [Theory]
    [InlineData(-2048, 9600, 0, 27)]
    [InlineData(2048, 9600, 255, 27)]
    [InlineData(0, 0, 128, 119)]
    [InlineData(0, -100, 128, 119)]
    [InlineData(0, 100000, 128, 17)]
    [InlineData(-1, 1024, 127, 117)]
    public void NativeProjectionUsesEighthPixelsUnsignedSzQuarterDepthAndSignedPanDivision(int x, int z, uint pan, uint volume)
    {
        Assert.Equal((pan, volume), NativeFootstepSoundAdapter.Spatialize(new NativeFootstepOrigin(x, z), 512));
    }

    [Fact]
    public void NativePrecision53HalfTiesAndDwordWrappingAreDeterministic()
    {
        Assert.Equal((129u, 119u), NativeFootstepSoundAdapter.Spatialize(new NativeFootstepOrigin(1, 512), 224));
        Assert.Equal((127u, 119u), NativeFootstepSoundAdapter.Spatialize(new NativeFootstepOrigin(-1, 512), 224));
        Assert.Equal((129u, 119u), NativeFootstepSoundAdapter.Spatialize(new NativeFootstepOrigin(1, 512), 288));
        Assert.Equal((127u, 119u), NativeFootstepSoundAdapter.Spatialize(new NativeFootstepOrigin(-1, 512), 288));
        Assert.Equal((126u, 119u), NativeFootstepSoundAdapter.Spatialize(new NativeFootstepOrigin(int.MaxValue, 512), 512));
        Assert.Equal((128u, 119u), NativeFootstepSoundAdapter.Spatialize(new NativeFootstepOrigin(0x20000000, 512), 512));
    }

    [Fact]
    public void SoundSelectionKeepsOriginalDefaultSpecialCustomAndLadderChannels()
    {
        FieldAssets field = InstalledField();
        SceneEventState scene = new();
        ActorState actor = Actor();
        FieldSoundEffectTable table = FieldSoundEffectTable.Read("test.sfx", [0x85, 0x5B, 3, 0]);
        NativeFootstepSound Resolve(ActorState target, int mode, Func<FieldSoundEffectTable>? effects = null)
        {
            return NativeFootstepSoundAdapter.Resolve(field.GetCamera(scene.CameraIndex), target, mode, effects ?? (() => throw new InvalidOperationException("Default/ladder sounds must not read the field SFX table.")));
        }
        Assert.Equal((3u, 0x400000u), Identity(Resolve(actor, 0)));
        Assert.Equal((2u, 0x400000u), Identity(Resolve(actor, 1)));
        ActorState special = Actor((nameof(ActorState.NativeFootstepSpecial), true));
        Assert.Equal((54u, 0x400000u), Identity(Resolve(special, 0)));
        Assert.Equal((53u, 0x400000u), Identity(Resolve(special, 1)));
        NativeFootstepPairState custom = actor.NativeFootstepPair! with { First392 = 0, Second393 = 0 };
        ActorState customActor = Actor((nameof(ActorState.NativeFootstepPair), custom));
        Assert.Equal((220037u, 0x400000u), Identity(Resolve(customActor, 0, () => table)));
        Assert.Equal((220037u, 0x800000u), Identity(Resolve(customActor, 1, () => table)));
        FieldFixedPosition zero = new(0, 0, 0);
        NativeLadderState ladder = new(1, true, 1, 0, 0, true, 0, 0, 0, 0, zero, zero, zero, zero, 0);
        ActorState climbing = Actor((nameof(ActorState.NativeFootstepPair), custom), (nameof(ActorState.NativeLadder), ladder));
        Assert.Equal((64u, 0x400000u), Identity(Resolve(climbing, 0)));
        Assert.Equal((63u, 0x400000u), Identity(Resolve(climbing, 1)));
        Assert.Throws<NotSupportedException>(() => Resolve(Actor((nameof(ActorState.NativeFootstepPair), custom with { First392 = 128 })), 0, () => table));
        Assert.Throws<NotSupportedException>(() => Resolve(Actor((nameof(ActorState.NativeFootstepPair), custom with { First392 = 1 })), 0, () => table));
    }

    [Fact]
    public void OriginCaptureUsesSar12AndDisplayWordsWithoutAnimationRootOrDrawingPan()
    {
        FieldAssets field = InstalledField();
        FieldCamera camera = new(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(0, 0, 9600), new Vector2(900, -700), 512);
        field = field with { Camera = camera, Cameras = [camera] };
        ActorState actor = Actor((nameof(ActorState.NativeDisplayOffset), NativeDisplayOffsetState.AllocatedZero with { X = 2 }), (nameof(ActorState.RootTranslationZ), (short)3000));
        NativeFootstepModelOrigin origin = NativeFootstepSoundAdapter.Capture(field.GetCamera(0), 0, new NativeFootstepProjectionQuery(actor, new FieldFixedPosition(-1, 0, 0), 0, 7));
        Assert.Equal(new NativeFootstepOrigin(1, 9600), origin.Translation);
        Assert.Equal(7, origin.Frame);
        Assert.Null(origin.UnsupportedReason);
    }

    [Fact]
    public void MovieCameraUsesTheSameOriginTransformAndItsOwnFocalWithoutDrawingPan()
    {
        FieldCamera camera = new(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(128, 0, 1024), new Vector2(900, -700), 300);
        ActorState actor = Actor((nameof(ActorState.NativeDisplayOffset), NativeDisplayOffsetState.AllocatedZero));
        NativeFootstepModelOrigin origin = NativeFootstepSoundAdapter.Capture(camera, 0, new NativeFootstepProjectionQuery(actor, new FieldFixedPosition(-4096, 0, 0), 0, 7));
        Assert.Equal(new NativeFootstepOrigin(127, 1024), origin.Translation);
        ActorState drawn = Actor((nameof(ActorState.NativeFootstepModelOrigin), origin));
        NativeFootstepSound previous = NativeFootstepSoundAdapter.Resolve(camera, drawn, 1, () => throw new InvalidOperationException());
        NativeFootstepSound current = NativeFootstepSoundAdapter.Resolve(camera with { FocalLength = 600 }, drawn, 1, () => throw new InvalidOperationException());
        Assert.Equal(NativeFootstepSoundAdapter.Spatialize(origin.Translation!, 300), (previous.Pan, previous.Volume));
        Assert.Equal(NativeFootstepSoundAdapter.Spatialize(origin.Translation!, 600), (current.Pan, current.Volume));
        Assert.NotEqual(previous.Pan, current.Pan);
        Assert.Equal(previous.EffectId, current.EffectId);
    }

    [Fact]
    public void MissingAndUnsupportedProjectionRetainIdentityWhileMalformedReceiptsRefuse()
    {
        FieldAssets field = InstalledField();
        NativeFootstepSound Resolve(ActorState actor)
        {
            return NativeFootstepSoundAdapter.Resolve(field.GetCamera(0), actor, 0, () => throw new InvalidOperationException());
        }
        NativeFootstepSound unknown = Resolve(Actor((nameof(ActorState.NativeFootstepModelOrigin), null)));
        Assert.Equal((3u, 0x400000u), Identity(unknown));
        Assert.Null(unknown.Spatialization);
        Assert.NotNull(unknown.UnsupportedProjectionReason);
        Assert.Throws<NotSupportedException>(() => FieldAudioPresentation.ResolveFootstepPlayback(unknown));
        NativeFootstepModelOrigin origin = Actor().NativeFootstepModelOrigin!;
        Assert.Throws<NotSupportedException>(() => Resolve(Actor((nameof(ActorState.NativeFootstepModelOrigin), origin with { Version = 2 }))));
        Assert.Throws<NotSupportedException>(() => Resolve(Actor((nameof(ActorState.NativeFootstepModelOrigin), origin with { Frame = -1 }))));
        NativeFootstepSound unsupported = Resolve(Actor((nameof(ActorState.NativeFootstepModelOrigin), origin with { Translation = null, UnsupportedReason = "Unproved movie camera origin." })));
        Assert.Null(unsupported.Spatialization);
        Assert.Equal("Unproved movie camera origin.", unsupported.UnsupportedProjectionReason);
        Assert.Throws<NotSupportedException>(() => FieldAudioPresentation.ResolveFootstepPlayback(unsupported));
    }

    private static (uint Effect, uint Flags) Identity(NativeFootstepSound sound)
    {
        return (sound.EffectId, sound.Flags);
    }

    private static ActorState Actor(params (string Name, object? Value)[] changes)
    {
        JsonObject node = JsonSerializer.SerializeToNode(new ActorState(0, 0))!.AsObject();
        node[nameof(ActorState.NativeFootstepSpecial)] = false;
        node[nameof(ActorState.NativeFootstepPair)] = JsonSerializer.SerializeToNode(new NativeFootstepPairState(1, 255, 255, new NativeFootstepPairSource(1, "Allocation", 74, "test", "test", 0, -1, -1, 0)));
        node[nameof(ActorState.NativeFootstepModelOrigin)] = JsonSerializer.SerializeToNode(new NativeFootstepModelOrigin(1, 1, 0, 0, new NativeFootstepOrigin(0, 9600), null));
        foreach ((string name, object? value) in changes)
        {
            node[name] = JsonSerializer.SerializeToNode(value);
        }
        return node.Deserialize<ActorState>()!;
    }

    private static FieldAssets InstalledField()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original field corpus is required.");
        return new GameAssets(directory).LoadField(74);
    }
}
