using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleChoco99ParticleResourcesTests
{
    [Fact]
    public void Original_both_projected_resources_retain_twelve_actual_frames_and_JSON()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        using GameAssets game = new(directory);
        NativeBattleChoco99ParticleResources resources = game.ReadNativeChoco99ParticleResources();
        Assert.Same(resources, game.ReadNativeChoco99ParticleResources());
        resources.Validate();
        Assert.Equal(0x152A958u, resources.Trail.Address);
        Assert.Equal(0x152A31Cu, resources.Radial.Address);
        foreach (NativeBattleChoco97SpriteResources sprite in new[] { resources.Trail, resources.Radial })
        {
            Assert.Equal(0x144, sprite.Bytes.Length);
            Assert.Equal((ushort)12, BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8)));
            NativeBattleModuleResource module = new(0, sprite.Address, 0, 0, sprite.Bytes);
            for (int frame = 0; frame < 12; frame++)
            {
                Assert.NotNull(module.ProjectedFrame571C80(frame));
            }
        }
        NativeBattleChoco99ParticleResources restored = JsonSerializer.Deserialize<NativeBattleChoco99ParticleResources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Original_four_point_emissions_draw_all_frames_then_release_with_actual_next_callback_restore(bool radial)
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        using GameAssets game = new(directory);
        NativeBattleChoco99ParticleResources resources = game.ReadNativeChoco99ParticleResources();
        NativeBattleEffectHierarchy hierarchy = new([NativeBattleEffectTaskPool.Initialize(0x256B9D8, 0x256B000, 0x548, 1), NativeBattleEffectTaskPool.Initialize(NativeBattleChoco99Particles.AuxiliaryDescriptor, 0x2566000, 0xB0, 1)]);
        NativeBattleEffectHierarchyAppend parent = hierarchy.Append8DC540(0, 0x71FFB0, 0x548);
        NativeBattleModulePoolTask parentTask = parent.Hierarchy.Read(parent.Address).WriteDword(0xC, 0x1D99A78).WriteByte(0x2A, 0xFE).WriteByte(0x2B, 3).WriteByte(0x2C, 7).WriteByte(0x2D, 7).WriteWord(0x1E, 100).WriteWord(0x20, 1000).WriteWord(0x542, 200).WriteWord(0x544, 800);
        hierarchy = parent.Hierarchy.Write(parent.Address, parentTask);
        NativeBattleChoco99ParticleEmission emission = radial ? NativeBattleChoco99Particles.EmitRadial720D70(hierarchy, parent.Address, 10, new(1), SelectActors) : NativeBattleChoco99Particles.EmitTrail7209B0(hierarchy, parent.Address, 8, new(1), SelectActors);
        Assert.Equal((byte)1, emission.Hierarchy.Read(parent.Address).ReadByte(0x28));
        NativeBattleModulePoolTask task = emission.Hierarchy.Read(emission.Address);
        Assert.Equal((byte)1, task.ReadByte(0x2C));
        Assert.Equal((byte)6, task.ReadByte(0x2D));
        NativeBattleActionMatrix view = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 4000 };
        NativeBattleCameraProjection projection = new(view, 216, 160, 108);
        List<ushort> frames = [];
        for (int callback = 0; callback < 13; callback++)
        {
            NativeBattleModulePoolTask restored = JsonSerializer.Deserialize<NativeBattleModulePoolTask>(JsonSerializer.Serialize(task))!;
            NativeBattleChoco99ParticleStep next = Advance(task);
            Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(Advance(restored)));
            Assert.Equal(callback == 12 ? 0 : 4, next.Packets.Length);
            if (callback < 12)
            {
                frames.Add(next.Task.ReadWord(0x50));
                Assert.All(next.Packets, packet => Assert.NotEmpty(packet.Primitives));
            }
            Assert.Equal(callback == 12, next.ReadyToComplete);
            task = next.Task;
        }
        Assert.Equal(Enumerable.Range(0, 12).Select(value => (ushort)value).ToArray(), frames.ToArray());
        Assert.Equal((byte)5, task.ReadByte(0x26));
        Assert.Equal((byte)2, task.ReadByte(0x29));
        Assert.Equal(task.ReadDword(0x84), task.ReadDword(0x1C));
        Assert.Equal(task.ReadDword(0x88), task.ReadDword(0x20));
        NativeBattleEffectHierarchy completed = emission.Hierarchy.Write(emission.Address, task);
        NativeBattleEffectHierarchyStep released = completed.Advance(1, (current, address) => new(current.Complete8DC530(address), 1));
        Assert.Equal((byte)0, released.Hierarchy.Read(parent.Address).ReadByte(0x28));

        NativeBattleChoco99ParticleStep Advance(NativeBattleModulePoolTask current) => NativeBattleChoco99Particles.Advance(current, resources, _ => false, view, projection, 512);
    }
    private static NativeBattleEffectTaskActors SelectActors(uint parameters, sbyte header, sbyte target)
    {
        Assert.Equal(0x1D99A78u, parameters);
        Assert.Equal((sbyte)-2, header);
        Assert.Equal((sbyte)3, target);
        return new(1, 6);
    }

}
