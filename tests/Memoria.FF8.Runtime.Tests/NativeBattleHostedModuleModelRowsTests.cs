using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedModuleModelRowsTests
{
    [Theory]
    [InlineData(7, (byte)0, (ushort)5, (ushort)6)]
    [InlineData(24, (byte)1, (ushort)7, (ushort)8)]
    [InlineData(7, (byte)2, (ushort)22, (ushort)23)]
    [InlineData(24, (byte)4, (ushort)19, (ushort)20)]
    [InlineData(6, (byte)4, (ushort)19, (ushort)20)]
    public void CastingAndBoneSpriteCallbacksUseTheBoundModel(int row, byte selector, ushort firstSound, ushort secondSound)
    {
        BattleModelRoster? roster = row == 6 ? null : Roster();
        NativeBattleModuleState state = NativeBattleModuleState.Clear().AppendModelEffect(row, selector, null, roster);
        Assert.Equal(NativeBattleGraphicsModelAddress.Encode(row), Pointer(state, 20));
        NativeBattleModuleAnchor anchor = new((short)row, 200, 300, 0);
        var inputs = new NativeBattleModuleInputs(0)
        {
            PresentationRoster = roster,
            CrtRandom = new(7),
            Resources = [Sprite(), SpectralSprite()],
            ReadModelAnchor = actual =>
            {
                Assert.Equal(row, actual);
                return anchor;
            },
            ReadModelTransform = actual =>
            {
                Assert.Equal(row, actual);
                return new(default, default, 0, 123, 4096);
            }
        };

        NativeBattleModuleStep step = state.Advance(inputs);
        NativeBattleModuleDraw[] sounds = step.Draws.Where(draw => draw.Kind == NativeBattleModuleDrawKind.Sound).ToArray();
        Assert.Equal(new[] { firstSound, secondSound }, sounds.Select(draw => draw.Sound));
        Assert.All(sounds, draw => Assert.Equal(anchor, draw.Anchor));
        NativeBattleModuleState restored = JsonSerializer.Deserialize<NativeBattleModuleState>(JsonSerializer.Serialize(state))!;
        Assert.Equal(JsonSerializer.Serialize(step), JsonSerializer.Serialize(restored.Advance(inputs)));
    }

    [Fact]
    public void PlacementVariantsSendTheirActualRowsAndOriginalOperandsToModelServices()
    {
        BattleModelRoster roster = Roster();
        NativeBattleModuleResource resource = Sprite();
        var local = new NativeBattleVector(-1, 2, -3);
        NativeBattleModuleState state = NativeBattleModuleState.Clear()
            .AppendModelResource(7, resource, 5, roster)
            .AppendPositionResource(24, local, resource, 5, roster)
            .AppendBoneParticle(7, 240, short.MinValue, resource, 5, roster);
        var inputs = new NativeBattleModuleInputs(0)
        {
            PresentationRoster = roster,
            Resources = [resource],
            ReadModelAnchor = row =>
            {
                Assert.Equal(7, row);
                return new(10, 11, 12, 0);
            },
            TransformModelPosition = (row, point) =>
            {
                Assert.Equal(24, row);
                Assert.Equal(local, point);
                return new(20, 21, 22);
            },
            ReadModelBone = (row, bone, fraction) =>
            {
                Assert.Equal(7, row);
                Assert.Equal((byte)240, bone);
                Assert.Equal((short)0, fraction);
                return new(30, 31, 32);
            }
        };

        NativeBattleModuleStep step = state.Advance(inputs);
        Assert.Equal(new NativeBattleModuleAnchor?[] { new(10, 11, 12, 0), new(20, 21, 22, 0), new(30, 0, 32, 0) }, step.Draws.Select(draw => draw.Anchor));
        Assert.Equal((byte)3, step.State.PublishedCount);
    }

    [Theory]
    [InlineData(0x5705A0u)]
    [InlineData(0x5707A0u)]
    public void PlacementChildDecodesItsRetainedOwnerAndUsesNativeRandom(uint callback)
    {
        BattleModelRoster roster = Roster();
        NativeBattleModuleResource resource = new(5, 0xC70000, 0x570CC0, callback, [0, 0, 0, 1]);
        NativeBattleModuleState state = NativeBattleModuleState.Clear().AppendModelResource(24, resource, 0x7F0005, roster);
        var anchor = new NativeBattleModuleAnchor(-1, 222, -333, -444);
        NativeCrtRandomState initial = new(1);
        var inputs = new NativeBattleModuleInputs(0)
        {
            PresentationRoster = roster,
            Resources = [resource],
            CrtRandom = initial,
            ReadModelAnchor = row =>
            {
                Assert.Equal(24, row);
                return anchor;
            },
            ReadModelRotation14 = row =>
            {
                Assert.Equal(24, row);
                return -1234;
            }
        };

        NativeBattleModuleStep step = state.Advance(inputs);
        NativeCrtRandomState expected = initial.Draw(out _);
        if (callback == 0x5707A0)
        {
            expected = expected.Draw(out _).Draw(out _);
        }
        Assert.Equal(expected, step.CrtRandomAfter);
        NativeBattleModuleDraw draw = Assert.Single(step.Draws, draw => draw.Kind is NativeBattleModuleDrawKind.Defensive or NativeBattleModuleDrawKind.Impact570860);
        Assert.Equal(anchor, draw.Anchor);
        Assert.Equal((short)-1234, draw.Rotation);
    }

    [Fact]
    public void ProducerAndRestoredTaskRejectRowsOutsideTheirActualOwnerBeforeModelReads()
    {
        NativeBattleModuleState empty = NativeBattleModuleState.Clear();
        BattleModelRoster roster = Roster();
        NativeBattleModuleResource resource = Sprite();
        Assert.Throws<InvalidDataException>(() => empty.AppendModelEffect(7, 4));
        Assert.Throws<InvalidDataException>(() => empty.AppendModelEffect(8, 4, null, roster));
        Assert.Throws<InvalidDataException>(() => empty.AppendPositionResource(25, default, resource, 5, roster));
        Assert.Throws<InvalidDataException>(() => empty.AppendBoneParticle(8, 240, 0, resource, 5, roster));

        NativeBattleModuleState state = empty.AppendModelEffect(24, 4, null, roster);
        NativeBattleModuleState restored = JsonSerializer.Deserialize<NativeBattleModuleState>(JsonSerializer.Serialize(state))!;
        var inputs = new NativeBattleModuleInputs(0)
        {
            ReadModelAnchor = _ => throw new InvalidOperationException("An unbound model reached its reader.")
        };
        Assert.Throws<InvalidDataException>(() => restored.Advance(inputs));
        Assert.Throws<InvalidDataException>(() => restored.Advance(inputs with { PresentationRoster = Roster(8) }));
        Assert.Throws<InvalidDataException>(() => restored.Advance(inputs with { PresentationRoster = Roster(bindLast: false) }));
    }

    private static uint Pointer(NativeBattleModuleState state, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(state.Slots[state.Head!.Value].Payload.AsSpan().Slice(offset));
    }

    private static BattleModelRoster Roster(int capacity = 25, bool bindLast = true)
    {
        return new(Enumerable.Range(0, capacity).Select(row => row switch
        {
            7 => new BattleModelOwner("party", true),
            24 when bindLast => new BattleModelOwner("enemy", false),
            _ => null
        }).ToImmutableArray());
    }

    private static NativeBattleModuleResource Sprite()
    {
        byte[] bytes = new byte[72];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 10);
        for (int phase = 0; phase < 10; phase++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + phase * 2), (ushort)(32 + phase * 4));
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(30), ushort.MaxValue);
        return new(5, 0xC70000, 0x570CC0, 0x570BC0, [.. bytes]);
    }

    private static NativeBattleModuleResource SpectralSprite()
    {
        byte[] bytes = new byte[298];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 11);
        for (int phase = 0; phase < 11; phase++)
        {
            int offset = 34 + phase * 24;
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + phase * 2), (ushort)offset);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), 1);
            bytes[offset + 4] = 8;
            bytes[offset + 5] = 128;
            bytes[offset + 6] = 8;
            bytes[offset + 7] = 0x2E;
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(32), ushort.MaxValue);
        return new(6, 0xC79344, 0x570CC0, 0x570BC0, [.. bytes]);
    }
}
