using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect3Tests
{
    [Theory]
    [InlineData(false, 6, 31)]
    [InlineData(true, 17, 42)]
    public void OriginalHeaderControllerUsesItsFiveCallbackCycleAndSameModelFifteenFrameGate(bool sameModel, int secondSpawn, int duration)
    {
        var fixture = Fixture([4, sameModel ? (byte)4 : (byte)5]);
        NativeBattleOriginalEffectState effect = fixture.Effect;
        List<int> flashes = [];
        List<int> sounds = [];
        List<int> contacts = [];
        List<int> applied = [];
        NativeBattleEffect3Inputs inputs = fixture.Inputs with { ApplyContact = applied.Add };
        int callback = 0;
        while (effect.ReturnedDescriptor != 0 && callback < 100)
        {
            NativeBattleEffect3Step step = NativeBattleEffect3.Step(effect, inputs);
            foreach (NativeBattleModuleDraw draw in step.Draws.Where(draw => draw.Kind == NativeBattleModuleDrawKind.Flash))
            {
                Assert.Equal((byte)128, draw.Intensity);
                Assert.Equal((byte)0x2A, draw.NativePrimitiveCommand);
                Assert.Equal(8, draw.NativeOrderingBucket);
                Assert.Equal(new NativeBattleScreenPoint[] { new(0, 0), new(320, 0), new(0, 216), new(320, 216) }, draw.NativeScreenVertices.ToArray());
                flashes.Add(callback);
            }
            foreach (NativeBattleEffect3Sound sound in step.Sounds)
            {
                Assert.Equal(0x493E6u, sound.Sound);
                Assert.Equal(0x101u, sound.Flags);
                sounds.Add(callback);
            }
            contacts.AddRange(step.Contacts);
            Assert.Equal(callback % 2 == 0 ? 0x8000 : 0, step.Effect.DrawArenaOffset);
            effect = JsonSerializer.Deserialize<NativeBattleOriginalEffectState>(JsonSerializer.Serialize(step.Effect))!;
            effect.Validate();
            callback++;
        }
        Assert.Equal(duration, callback);
        Assert.Equal(new[] { 1, secondSpawn }, flashes);
        Assert.Equal(new[] { 2, secondSpawn + 1 }, sounds);
        Assert.Equal(new[] { 0, 1 }, contacts);
        Assert.Equal(contacts, applied);
        Assert.All(effect.Pools, pool => Assert.All(pool.Slots, task => Assert.Equal(0, task.Flags)));
    }

    [Fact]
    public void EverySpriteLayerKeepsItsActualPhaseWindowsDepthAndGroundCameraTranslation()
    {
        var fixture = Fixture([4]);
        NativeBattleOriginalEffectState effect = fixture.Effect;
        Dictionary<uint, List<ushort>> phases = [];
        List<int> contacts = [];
        int callbacks = 0;
        while (effect.ReturnedDescriptor != 0 && callbacks < 100)
        {
            NativeBattleEffect3Step step = NativeBattleEffect3.Step(effect, fixture.Inputs);
            foreach (NativeBattleModuleDraw draw in step.Draws.Where(draw => draw.Kind == NativeBattleModuleDrawKind.Sprite))
            {
                uint address = draw.Resource!.Address;
                if (!phases.TryGetValue(address, out List<ushort>? selected))
                {
                    selected = [];
                    phases.Add(address, selected);
                }
                selected.Add(draw.Phase);
                Assert.Equal((ushort)0, draw.SpriteFlags);
                Assert.Equal(4096, draw.Scale);
                if (address == 0x1523DE0)
                {
                    Assert.Equal(500, draw.SpriteCameraZOffset);
                    Assert.Equal((short)35, draw.Anchor!.Y);
                    Assert.Equal(0, draw.DepthOffset);
                    Assert.Equal(NativeBattlePoseMatrices.RotationWords(new(90, 0, 0)).ToArray(), draw.SpriteWorldRotation.ToArray());
                }
                else
                {
                    Assert.Equal(-2048, draw.DepthOffset);
                    Assert.Empty(draw.SpriteWorldRotation);
                    Assert.Equal(0, draw.SpriteCameraZOffset);
                    Assert.Equal(address == 0x1523204 ? (short)-400 : (short)-600, draw.Anchor!.Y);
                }
            }
            contacts.AddRange(step.Contacts);
            effect = step.Effect;
            callbacks++;
        }
        Assert.Equal(26, callbacks);
        Assert.Equal(new[] { 0 }, contacts);
        foreach ((uint address, int count) in new (uint, int)[] { (0x1522FB0, 7), (0x1523204, 7), (0x1523458, 9), (0x1523578, 9), (0x1523684, 9), (0x15237A4, 9), (0x1523928, 19), (0x1523DE0, 13) })
        {
            Assert.Equal(Enumerable.Range(0, count).Select(value => (ushort)value), phases[address]);
        }
        NativeBattleEffect3Step completed = NativeBattleEffect3.Step(effect, fixture.Inputs);
        Assert.Same(effect, completed.Effect);
        Assert.Empty(completed.Draws);
        Assert.Empty(completed.Contacts);
    }

    private static (NativeBattleOriginalEffectState Effect, NativeBattleEffect3Inputs Inputs) Fixture(byte[] targets)
    {
        NativeBattleActionPacketsState packets = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        foreach (byte target in targets)
        {
            (packets, _) = packets.Prepare(new(3, 2, 11, -1, 2, 3, null));
            (packets, _) = packets.AppendContact(new(new(target, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
            (packets, _) = packets.Publish(1);
        }
        packets = packets.WritePlayerRepeatMode(0, checked((byte)targets.Length));
        ImmutableArray<NativeBattleModuleResource> resources = new uint[] { 0x1522FB0, 0x1523204, 0x1523458, 0x1523578, 0x1523684, 0x15237A4, 0x1523928, 0x1523DE0 }.Select(Sprite).ToImmutableArray();
        NativeBattleEffectBinding binding = new(3, 0x700C10, 0x575610) { InitializerIsRet = true, Effect3Resources = new(new("texture", []), new("animation", []), resources, 0x493E6, 0x808080) };
        NativeBattleOriginalEffectState effect = NativeBattleEffect3.InitializePools(new(3, 2, binding, new(3, 0, 0x30, 0, 0, 0), null));
        return (effect, new(packets, model => new((short)(model * 100), -600, 200), _ => new(new(0, 0, 0), new(0, 0, 0), 0, 35, 4096)));
    }

    private static NativeBattleModuleResource Sprite(uint address)
    {
        byte[] bytes = new byte[64];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 24);
        for (int phase = 0; phase <= 24; phase++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + 2 * phase), 60);
        }
        return new(0, address, 0x570CC0, 0x570BC0, bytes.ToImmutableArray());
    }
}
