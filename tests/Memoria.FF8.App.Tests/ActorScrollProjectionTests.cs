using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ActorScrollProjectionTests
{
    [Fact]
    public void SharedProjectionUsesTheSelectedRetainedActorAndAProvedNonzeroCenter()
    {
        // Synthetic camera metadata isolates the native projection contract, not a played checkpoint.
        byte[] scriptIdentity = [5, 6];
        FieldCamera camera = new(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(90, -10, 3896), Vector2.Zero, 640);
        FieldInformation information = new(0, 200, Enumerable.Repeat(new FieldBounds(-512, 512, 512, -512), 8).ToArray(), [new FieldBounds(0, 224, 320, 0)], [], []);
        FieldModel model = new("synthetic", [], [], [], [], []);
        FieldAssets field = new(600, "synthetic-actor-camera", scriptIdentity, [], [], camera, new FieldWalkmesh([]), [model]) { Information = information };
        NativeFieldInitializationProof initialization = new(1, NativeFieldRuntimeProfile.FF8EnExecutableHash, field.Id, Convert.ToHexString(SHA256.HashData(scriptIdentity)), 20, NativeFieldInitializationStage.Completed);
        SceneEventState scene = JsonSerializer.Deserialize<SceneEventState>(JsonSerializer.Serialize(new { NativeFieldInitialization = initialization, NativeFieldLoadVersion = 1, NativeActorLayoutVersion = 1, NativeActorObjectIds = new[] { 0, 1 } }))!;
        ActorState leader = JsonSerializer.Deserialize<ActorState>("{\"ObjectId\":0,\"Label\":0,\"ModelId\":0,\"X\":-45,\"Y\":-170,\"Z\":0}")!;
        ActorState target = JsonSerializer.Deserialize<ActorState>("{\"ObjectId\":1,\"Label\":1,\"ModelId\":0,\"X\":-80,\"Y\":110,\"Z\":0}")!;
        Assert.Equal(((short)1, (short)15), ActorBackgroundScrollCertificate.Resolve(field, scene, [leader, target], 1));
        Assert.Equal(((short)7, (short)-28), ActorBackgroundScrollCertificate.Resolve(field, scene, [leader, target], 0));
        Assert.Null(ActorBackgroundScrollCertificate.Resolve(field, scene, [leader, target], 255));
        Assert.Null(ActorBackgroundScrollCertificate.Resolve(field with { Information = null }, scene, [leader, target], 1));
        FieldAssets otherFieldId = field with { Id = 601 };
        Assert.Null(ActorBackgroundScrollCertificate.Resolve(otherFieldId, scene, [leader, target], 1));
        FieldNativeCameraResult endpoint = NormalCameraCertificate.ProjectActorCenter(field, scene, target);
        Assert.Equal(new FieldNativeCameraCenter(1, 15), endpoint.Center);
        Assert.True(endpoint.Supported);
        SceneEventState following = JsonSerializer.Deserialize<SceneEventState>(JsonSerializer.Serialize(new { NativeFieldInitialization = initialization, NativeFieldLoadVersion = 1, NativeActorLayoutVersion = 1, NativeActorObjectIds = new[] { 0, 1 }, ActorBackgroundScroll = new NativeActorBackgroundScroll(1, 1, 1, 1, 1, 1, 15, null, null) }))!;
        Assert.True(ActorBackgroundScrollCertificate.CanRestore(field, following, [leader, target]));
        Assert.Equal(new Vector2(-1, -15), ActorBackgroundScrollCertificate.Prepare(field, following)!.ActorOffset);
        SceneEventState pending = JsonSerializer.Deserialize<SceneEventState>(JsonSerializer.Serialize(new { NativeFieldInitialization = initialization, NativeFieldLoadVersion = 1, NativeActorLayoutVersion = 1, NativeActorObjectIds = new[] { 0, 1 }, ActorBackgroundScroll = new NativeActorBackgroundScroll(1, 1, 1, 1, 0, null, null, null, null) }))!;
        Assert.Throws<NotSupportedException>(() => ActorBackgroundScrollCertificate.Prepare(field, pending));
    }
}
