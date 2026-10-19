using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleDeathResourcesTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Original_Death_runs_reaper_aura_strike_immunity_stage_and_texture_restoration(bool immune)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleDeathResources resources = game.ReadNativeDeathResources();
        NativeBattleGraphicsRules rules = game.ReadNativeBattleGraphicsRules();
        Assert.Same(resources, game.ReadNativeDeathResources());
        Assert.Equal(28, resources.Model.PoseSource.BoneCount);
        NativeBattleRawMesh reaper = Assert.Single(resources.Model.Geometry);
        Assert.Equal(302, reaper.Vertices.Length);
        Assert.Equal(507, reaper.Polygons.Count(polygon => !polygon.Quad));
        Assert.Equal(19, reaper.Polygons.Count(polygon => polygon.Quad));
        Assert.Equal(0x3FCC, resources.Model.Section(3));
        NativeBattleActionPacketsState packets = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        (packets, _) = packets.Prepare(new(0, 2, 4, 0, 8, 14, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, immune ? (byte)4 : (byte)0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState effect = new(14, 13, new(14, 0x8B68C0, 0x8B68A0), new(0, 1, 8, 0, 0, 0), null) { ResourcePointers = [new(0x273AE80, 0)] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(13);
        Assert.Throws<NotSupportedException>(() => NativeBattleDeathController.Initialize(memory with { Death14 = null }, effect, packets, resources, new(1)));
        NativeBattleDeathFrame frame = NativeBattleDeathController.Initialize(memory, effect, packets, resources, new(1));
        NativeBattleCameraProjection projection = new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096), 216, 160, 108);
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        NativeBattlePoseSource pose = new(skeleton.ToImmutableArray(), [1]);
        NativeBattleDecodedPose decoded = new(new(0, 0, 0), [new(new(0, 0, 0), new(4096, 4096, 4096))], false);
        NativeBattleGraphicsModel Model(int index) => new(index, 0) { Transform = new(new(0, 0, 0), new(0, -100, 0), 0, 0, 4096), Rotation14 = 0, GeometryBounds52 = new(new(0, -100, 0), new(0, 0, 0)), SequenceState = new() { Pose = 0, PoseFrame114 = 1, DecodedPose = decoded } };
        List<int> contacts = [];
        List<int> sounds = [];
        List<short> fades = [];
        int allocations = 0;
        int darkDraws = 0;
        int normalDraws = 0;
        int textureRestores = 0;
        int now = 0;
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        NativeBattleDeathInputs inputs = new(packets, projection, 0, rules.SummonColors!, 0x808080, 0x808080, Model, _ => pose, Draw, (current, operation) =>
        {
            switch (operation)
            {
                case NativeBattleDeathContact:
                    contacts.Add(now);
                    return current;
                case NativeBattleDeathSound:
                    sounds.Add(now);
                    return current;
                case NativeBattleDeathStage stage:
                    fades.Add(stage.Fade);
                    return current;
                case NativeBattleDeathTextureLoad texture:
                    textureRestores++;
                    return current with { Effect = NativeBattleDeathController.WriteCompletionByte(current.Effect, texture.Completion, 1) };
                case NativeBattleDeathPackets drawPackets:
                    return Append(current, drawPackets.Batch);
                default:
                    throw new InvalidOperationException();
            }
        });
        for (now = 1; now <= 600 && frame.Effect.ReturnedDescriptor != 0; now++)
        {
            ordering = NativeBattleLogicalOrdering.Clear();
            frame = NativeBattleDeathController.Advance(frame, resources, inputs);
            if (now == 1)
            {
                NativeBattleModulePoolTask root = frame.Effect.Pools[0].Slots[0];
                Assert.Equal((ushort)1, root.ReadWord(0x24));
                Assert.Equal((byte)1, root.ReadByte(0x29));
            }
            if (now is 14 or 60)
            {
                string json = JsonSerializer.Serialize(frame);
                frame = JsonSerializer.Deserialize<NativeBattleDeathFrame>(json)!;
                Assert.Equal(json, JsonSerializer.Serialize(frame));
            }
        }
        Assert.Equal(new[] { NativeBattlePoseDecoder.ReadFrameCounts(resources.Model.PoseSource.Animations.AsSpan())[5] + 12 }, contacts);
        Assert.Equal(2, sounds.Count);
        Assert.Equal((short)0, fades[^1]);
        Assert.Contains((short)1536, fades);
        Assert.True(darkDraws > 0);
        Assert.True(normalDraws > 0);
        Assert.True(allocations > 0);
        Assert.Equal(1, textureRestores);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.All(frame.Effect.Pools, pool => Assert.Null(pool.Head));
        Assert.Equal(32, frame.Memory.EffectLight1D99AB0!.Value.Length);
        Assert.Equal(!immune, frame.Effect.Pools[6].Slots.Any(task => task.Callback == 0x8BDBD0));

        NativeBattleDeathModelStep Draw(NativeBattleDeathFrame current, NativeBattleModulePoolTask task, NativeBattleEffectOwnedModelState model)
        {
            NativeBattleDeathDraw drawn;
            if ((task.ReadByte(0x26) & 8) != 0)
            {
                darkDraws++;
                drawn = NativeBattleDeathDrawing.Dark(current, task, model, resources, projection, rules.ShadowSource!, NativeBattlePrimitiveCapabilities.PrimaryRgba8());
            }
            else
            {
                normalDraws++;
                drawn = NativeBattleDeathDrawing.Normal(current, task, model, resources, projection, rules.ShadowSource!, NativeBattlePrimitiveCapabilities.PrimaryRgba8(), rules.SummonColors!);
            }
            return new(Append(drawn.Frame, drawn.Packets), drawn.Task);
        }
        NativeBattleDeathFrame Append(NativeBattleDeathFrame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattleDeathState state = current.Memory.Death14!;
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, state.Cursor!.Value, ordering, batch);
            allocations += batch.AllocationBytes;
            ordering = written.Ordering;
            return current with { Memory = written.Memory with { Death14 = state with { Cursor = written.Cursor } } };
        }
    }
}

