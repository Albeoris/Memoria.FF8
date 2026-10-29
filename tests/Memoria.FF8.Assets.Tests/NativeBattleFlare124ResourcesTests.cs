using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleFlare124ResourcesTests
{
    [Fact]
    public void Both_original_layouts_complete_at88_and_resume_each_live_frame_from_json()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleFlare124Resources resources = game.ReadNativeFlare124Resources();
        Assert.Equal(0x49460u, resources.Sound);
        Assert.Equal(0x2020FFu, resources.Red);
        Assert.Equal(0xDDC, resources.Camera.Data.Length);
        Assert.NotEmpty(resources.Particle.ProjectedFrame571C80(0).Quads);
        Check(resources.Burst, 0xA70, 32);
        Check(resources.Ring, 0xA8, 2);

        static void Check(NativeBattleGfAnimationSource source, int length, int nodes)
        {
            int table = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(4));
            Assert.Equal(nodes, BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(table)));
            Assert.Equal(88, BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(table + 4)));
            NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(new byte[length].ToImmutableArray()).Initialize7016B0(source, length);
            for (int frame = 0; frame < 88; frame++)
            {
                NativeBattleGfAnimationWorkspace restored = NativeBattleSnapshotJson.Deserialize<NativeBattleGfAnimationWorkspace>(NativeBattleSnapshotJson.Serialize(workspace))!;
                NativeBattleGfAnimationAdvance live = workspace.Advance701970(source, 0, (current, _) => current);
                Assert.Equal(87 - frame, live.Remaining);
                Assert.Equal(NativeBattleSnapshotJson.Serialize(live), NativeBattleSnapshotJson.Serialize(restored.Advance701970(source, 0, (current, _) => current)));
                workspace = live.Workspace;
            }
        }
    }
}
