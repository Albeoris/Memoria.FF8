using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleBio22ActorPacketTests
{
    [Fact]
    public void Original_party_body_uses_current_decoded_bones_plain_materials_and_retained_work_JSON_next()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(new() { Id = "player", IsPlayer = true, ModelId = 0, WeaponId = 0 });
        NativeBattlePoseSource source = resource.PoseSource!;
        NativeBattleDecodedPose pose = NativeBattlePoseDecoder.DecodeFirst509440(source.Animations.AsSpan(), source.BoneCount, 0).Pose;
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattlePoseMatrices.ComposeCurrentPose(source, pose, identity);
        NativeBattleModelTextureLoad texture = NativeBattleModelTextures.Prepare507400(resource.BodyTexturePack!, 0xFFFC);
        NativeBattleEffectArena work = NativeBattleEffectArena.Clear().MarkUnknown(0x10000, 0x10000);
        int packets = 0;
        foreach (NativeBattleRawMesh mesh in resource.Geometry!.Value)
        {
            NativeBattleBio22ActorDraw current = Draw(mesh, work);
            packets += current.Packets.Attempts.Length;
            Assert.All(current.Packets.Attempts, attempt => Assert.Contains(attempt.Data.Length, new[] { 32, 40 }));
            Assert.All(current.Packets.Attempts, attempt => Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[30]));
            if (mesh.Vertices.Length > 0)
            {
                Assert.Throws<NotSupportedException>(() => current.Work.Read(0x10000, 4));
                Assert.Throws<NotSupportedException>(() => current.Work.Read(0x1000A, 6));
            }
            NativeBattleEffectArena restored = NativeBattleSnapshotJson.Deserialize<NativeBattleEffectArena>(NativeBattleSnapshotJson.Serialize(current.Work))!;
            Assert.Equal(NativeBattleSnapshotJson.Serialize(Draw(mesh, current.Work)), NativeBattleSnapshotJson.Serialize(Draw(mesh, restored)));
            work = current.Work;
        }
        Assert.True(packets > 0);

        NativeBattleBio22ActorDraw Draw(NativeBattleRawMesh mesh, NativeBattleEffectArena owner) => NativeBattleBio22ActorPackets.Emit89D780(mesh, bones, identity with { Z = 4096 }, new(null, 256, 160, 120), 0x8090A0, 1024, 4096, 0x155, 0x100, owner, 0x10000, texture.Binding);
    }
}
