using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleSandStorm176ResourcesTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Original_resources_drive_both_funnels_lift_restore_and_landing_contact(bool pauseFunnel)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleSandStorm176Resources resources = game.ReadNativeSandStorm176Resources();
        Assert.Same(resources, game.ReadNativeSandStorm176Resources());
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([176])[175];
        NativeBattleEffectInitializerAsset texture = binding.InitializerAsset!;
        NativeBattleActionPacketsState packets = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        (packets, _) = packets.Prepare(new(3, 2, 4, 0, 1, 176, null));
        (packets, _) = packets.AppendContact(new(new(0, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState effect = new(176, 175, binding, new(3, 0, 1, 0, 0, 0), null) { ResourcePointers = [new(0x2374554, 0)] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(175);
        memory = memory with { Arena = memory.Arena!.Write(0, texture.Data.AsSpan()), SandStorm176 = NativeBattleSandStorm176State.FromColdProcess() };
        Assert.Throws<NotSupportedException>(() => NativeBattleSandStorm176Controller.Initialize(memory with { SandStorm176 = null }, effect, resources, new(1)));
        NativeBattleSandStorm176Frame frame = NativeBattleSandStorm176Controller.Initialize(memory, effect, resources, new(1));
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleCameraProjection projection = new(identity with { Z = 12000 }, 216, 160, 108);
        NativeBattleGraphicsModel[] actors = Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 0) { Transform = new(new(0, 0, 0), new(unchecked((short)(index * 400)), 0, 0), 17, 0, 4096), Rotation12 = 100, Rotation14 = 200, Rotation16 = 300, RotationPadding18 = null, GeometryWorldMatrix = identity }).ToArray();
        NativeBattleGraphicsModel before = actors[0];
        NativeBattlePoseSource pose = new(new byte[64].ToImmutableArray(), [1]);
        int contacts = 0;
        int sounds = 0;
        int funnelPackets = 0;
        int spritePackets = 0;
        int writes = 0;
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        NativeBattleSandStorm176Inputs inputs = new(packets, projection, 0, false, index => actors[index], _ => pose, Execute);
        for (int tick = 0; tick < 300 && frame.Effect.ReturnedDescriptor != 0; tick++)
        {
            ordering = NativeBattleLogicalOrdering.Clear();
            frame = NativeBattleSandStorm176Controller.Advance(frame, resources, inputs);
            if (tick == 30)
            {
                frame = JsonSerializer.Deserialize<NativeBattleSandStorm176Frame>(JsonSerializer.Serialize(frame))!;
                if (pauseFunnel)
                {
                    NativeBattleEffectTaskPool active = frame.Effect.Pools[1];
                    int counter = frame.Effect.Pools[0].Slots[0].ReadWord(12);
                    uint random = frame.Random.Seed;
                    NativeBattleSandStorm176Frame held = NativeBattleSandStorm176Controller.Advance(frame, resources, inputs with { DrawOnly = true });
                    Assert.Equal(counter + 1, held.Effect.Pools[0].Slots[0].ReadWord(12));
                    Assert.Equal(active.Slots.Where(task => task.Callback == 0x5D0740).Select(task => task.ReadWord(12)), held.Effect.Pools[1].Slots.Where(task => task.Callback == 0x5D0740).Select(task => task.ReadWord(12)));
                    Assert.NotEqual(random, held.Random.Seed);
                    frame = held;
                }
            }
        }
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.All(frame.Effect.Pools, pool => Assert.Null(pool.Head));
        Assert.Equal(1, contacts);
        Assert.Equal(2, sounds);
        Assert.True(funnelPackets > 0);
        Assert.True(spritePackets > 0);
        Assert.True(writes > 0);
        Assert.Equal(before.Flags0, actors[0].Flags0);
        Assert.Equal(before.Rotation12, actors[0].Rotation12);
        Assert.Equal(before.Rotation14, actors[0].Rotation14);
        Assert.Equal(before.Rotation16, actors[0].Rotation16);
        Assert.Null(actors[0].RotationPadding18);
        Assert.Equal(before.Transform, actors[0].Transform);

        NativeBattleSandStorm176Frame Execute(NativeBattleSandStorm176Frame current, NativeBattleSandStorm176Operation operation)
        {
            switch (operation)
            {
                case NativeBattleSandStorm176Funnel funnel:
                    NativeBattleSandStorm176State state = current.Memory.SandStorm176!;
                    NativeBattleRegionalPacketWrite regional = NativeBattleSandStorm176Packets.WriteFunnel(state.Module, ordering, funnel.Batch);
                    ordering = regional.Ordering;
                    funnelPackets += funnel.Batch.Attempts.Count(attempt => attempt.Primitive is not null);
                    return current with { Memory = current.Memory with { SandStorm176 = state with { Module = regional.Arena } } };
                case NativeBattleSandStorm176Sprite sprite:
                    NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, current.Effect.DrawArenaOffset!.Value, ordering, NativeBattleMeltdownPacketConversion.FromSprite(sprite.Batch));
                    ordering = written.Ordering;
                    spritePackets += sprite.Batch.Primitives.Length;
                    return current with { Memory = written.Memory, Effect = current.Effect with { DrawArenaOffset = written.Cursor } };
                case NativeBattleSandStorm176Actor actor:
                    writes++;
                    actors[actor.Index] = actor.Model;
                    return current;
                case NativeBattleSandStorm176Contact:
                    contacts++;
                    return current;
                case NativeBattleSandStorm176Sound:
                    sounds++;
                    return current;
                default:
                    throw new InvalidOperationException();
            }
        }
    }
}
