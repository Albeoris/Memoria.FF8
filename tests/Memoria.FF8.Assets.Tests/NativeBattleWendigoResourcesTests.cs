using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleWendigoResourcesTests
{
    [Theory]
    [InlineData(41)]
    [InlineData(42)]
    public void Original_directories_keep_absolute_relocation_and_fresh_module_owners_round_trip(int selected)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        ushort effectId = (ushort)selected;
        bool shoot = selected == 42;
        NativeBattleWendigoResources resources = shoot ? game.ReadNativeShoot42Resources() : game.ReadNativeDribble41Resources();
        Assert.Same(resources, shoot ? game.ReadNativeShoot42Resources() : game.ReadNativeDribble41Resources());
        int directory = checked((int)(resources.ActorDirectory - resources.ActorBankAddress));
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(resources.ActorBank.AsSpan(directory, 4)));
        uint channels = shoot ? 0x267B024u : 0x2698AB8u;
        int channelBytes = shoot ? 0x834 : 0x16A8;
        Assert.Equal(channelBytes, resources.ConstantChannels.Length);
        Assert.Equal(resources.ConstantChannels.ToArray(), resources.ReadConstantChannel(channels, channelBytes));
        Assert.Throws<NotSupportedException>(() => resources.ReadConstantChannel(channels - 1, 1));
        Assert.Throws<NotSupportedException>(() => resources.ReadConstantChannel(channels + (uint)channelBytes, 1));
        Assert.Throws<NotSupportedException>(() => (resources with { ConstantChannels = [] }).ReadConstantChannel(channels, 2));
        NativeBattleActionPacketsState packets = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        (packets, _) = packets.Prepare(new(0, 2, 4, 0, 8, effectId, null));
        (packets, _) = packets.AppendContact(new(new(1, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState effect = new(effectId, selected - 1, new(effectId, shoot ? 0x83ECC0u : 0x8465C0u, shoot ? 0x83ECA0u : 0x8465A0u), new(0, 1, 8, 0, 0, 0), null) { ResourcePointers = [new(NativeBattleWendigoController.ModuleBase(shoot), 0)] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(selected - 1);
        Assert.Throws<NotSupportedException>(() => NativeBattleWendigoController.Initialize(shoot ? memory with { Shoot42 = null } : memory with { Dribble41 = null }, effect, packets, resources, new(1)));
        NativeBattleWendigoFrame frame = NativeBattleWendigoController.Initialize(memory, effect, packets, resources, new(1));
        NativeBattleWendigoState state = (shoot ? frame.Memory.Shoot42 : frame.Memory.Dribble41)!;
        Assert.False(state.PendingColdSources);
        Assert.Equal(NativeBattleSnapshotJson.Serialize(resources.ActorBank), NativeBattleSnapshotJson.Serialize(state.ActorBank));
        Assert.Equal(shoot ? 5 : 4, frame.Effect.Pools.Length);
        Assert.Equal(shoot ? (ushort)40 : (ushort)20, (ushort)frame.Effect.Pools[3].Slots.Length);
        string json = NativeBattleSnapshotJson.Serialize(frame);
        Assert.Equal(json, NativeBattleSnapshotJson.Serialize(NativeBattleSnapshotJson.Deserialize<NativeBattleWendigoFrame>(json)));
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        NativeBattlePoseSource pose = new(skeleton.ToImmutableArray(), [1]);
        NativeBattleDecodedPose decoded = new(new(0, 0, 0), [new(new(0, 0, 0), new(4096, 4096, 4096))], false);
        NativeBattleGraphicsModel Model(int index) => new(index, 0) { Transform = new(new(0, 0, 0), new(0, -100, 0), 0, 0, 4096), Rotation14 = 0, GeometryBounds52 = new(new(0, -100, 0), new(0, 0, 0)), SequenceState = new() { Pose = 0, PoseFrame114 = 1, DecodedPose = decoded } };
        NativeBattleCameraProjection projection = new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096), 216, 160, 108);
        NativeBattleWendigoInputs inputs = new(packets, projection, 0, game.ReadNativeBattleGraphicsRules().SummonColors!, Model, _ => pose, (current, operation) => throw new InvalidDataException($"Unexpected operation before the ball boundary: {operation.GetType().Name}."));
        for (int callback = 0; callback < 8; callback++)
        {
            frame = NativeBattleWendigoController.Advance(frame, resources, inputs);
            if (callback == 4)
            {
                frame = NativeBattleSnapshotJson.Deserialize<NativeBattleWendigoFrame>(NativeBattleSnapshotJson.Serialize(frame))!;
            }
        }
        NativeBattleEffectTaskPool roots = frame.Effect.Pools[0];
        NativeBattleModulePoolTask root = roots.Slots[roots.Head!.Value];
        Assert.Equal((ushort)8, root.ReadWord(0x24));
        Assert.Equal((ushort)8, root.ReadWord(0x5C));
        NativeBattleEffectTaskPool emitters = frame.Effect.Pools[1];
        Assert.Equal((ushort)5, emitters.Slots[emitters.Head!.Value].ReadWord(0x24));
        Assert.NotNull(frame.Effect.Pools[2].Head);
        Assert.NotNull(frame.Effect.Pools[3].Head);
        if (shoot)
        {
            NativeBattleGfAnimationWorkspace ball = new(new byte[0x84].ToImmutableArray());
            ball = ball.Initialize7016B0(resources.BallPrim!, 0x54);
            Assert.Equal(resources.BallPrim!.Address, ball.ResourceAddress);
            NativeBattleGfAnimationWorkspace kick = new(new byte[0x84].ToImmutableArray());
            kick = kick.Initialize7016B0(resources.KickPrim!, 0x84);
            Assert.Equal(resources.KickPrim!.Address, kick.ResourceAddress);
        }
    }

    [Fact]
    public void Original_shoot_pass_durations_are_nonzero_until_the_terminal_record_and_all_flipbooks_are_decodable()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleWendigoResources resources = game.ReadNativeShoot42Resources();
        Assert.Equal(70, resources.PassScript.Length);
        for (int index = 0; index < 6; index++)
        {
            Assert.True(BinaryPrimitives.ReadInt16LittleEndian(resources.PassScript.AsSpan(index * 10 + 2, 2)) > 0);
        }
        Assert.Equal((short)127, BinaryPrimitives.ReadInt16LittleEndian(resources.PassScript.AsSpan(60, 2)));
        foreach (NativeBattleModuleResource sprite in new[] { resources.Dust, resources.SmallBurst!, resources.LargeBurst! })
        {
            sprite.Validate();
            int count = BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan(8, 2));
            Assert.True(sprite.SpriteFrame(count - 1).NextOffset < 0);
        }
        Assert.Equal(57 * 6, game.ReadNativeDribble41Resources().Radii.Length);
        Assert.Equal(77 * 14, resources.KickScript.Length);
    }
}
