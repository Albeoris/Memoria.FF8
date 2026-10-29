using System.Buffers.Binary;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleResonance49Controller;
using static Memoria.FF8.Runtime.Tests.NativeBattleResonance49ProgrammeTests;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleResonance49Smooth60Tests
{
    [Fact]
    public void Screen_captures_both_native_channels_with_fixed_cell_identity_and_full_coordinate_sampling_scale()
    {
        const uint address = 0x26467E8;
        var frame = InitializeScreen824300(Frame(4), address, Resources(), Inputs());
        frame = frame with { State = frame.State.WriteWord(0x26466B0, 4096) };
        List<NativeBattleResonance49DeliverScreenPair> pairs = [];
        var inputs = Inputs((current, operation) =>
        {
            pairs.Add(Assert.IsType<NativeBattleResonance49DeliverScreenPair>(operation));
            return current;
        });
        var first = DrawScreen8244E0(frame, address, 5, Resources(), inputs);
        Assert.Equal(140, pairs.Count);
        NativeBattlePilotVisualIdentity[] identities = new NativeBattlePilotVisualIdentity[140];
        for (int index = 0; index < pairs.Count; index++)
        {
            var pair = pairs[index];
            var geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(pair.Quad.Primitive!.PilotGeometry);
            identities[index] = geometry.Identity;
            int channel = index < 70 ? 1 : 0;
            Assert.Equal(channel * 70 + index % 70, geometry.Identity.Primitive);
            Assert.Equal((byte)4, geometry.CornerCount);
            Assert.True(geometry.ScreenSpace);
            Assert.Equal(8, geometry.ScreenScale);
            Assert.Null(pair.Mode.Primitive!.PilotGeometry);
            Assert.Equal(channel == 1 ? 0x2Fu : 0x2Du, pair.Quad.Primitive.Words[1] >> 24);
            for (int corner = 0; corner < 4; corner++)
            {
                int offset = 8 + corner * 8;
                short x = BinaryPrimitives.ReadInt16LittleEndian(pair.Quad.Data.AsSpan().Slice(offset));
                short y = BinaryPrimitives.ReadInt16LittleEndian(pair.Quad.Data.AsSpan().Slice(offset + 2));
                Assert.Equal(x, geometry.Corner(corner).X);
                Assert.Equal(y, geometry.Corner(corner).Y);
                Assert.Equal(x, geometry.Corner(corner).X * geometry.ScreenScale * 0.125f);
                Assert.Equal(y, geometry.Corner(corner).Y * geometry.ScreenScale * 0.125f);
            }
            Assert.Equal(40, pair.Quad.PacketLength);
            Assert.Equal(12, pair.Mode.PacketLength);
        }
        Assert.Equal(140, identities.Distinct().Count());
        Assert.Single(identities.Select(identity => (identity.Bank, identity.Slot, identity.Generation)).Distinct());
        Assert.Equal(NativeBattleTestSnapshots.Json(frame.Memory.GeometryRegisters), NativeBattleTestSnapshots.Json(first.Memory.GeometryRegisters));
        var expectedRandom = frame.Random;
        for (int index = 0; index < 6; index++)
        {
            expectedRandom = expectedRandom.Draw(out _);
        }
        Assert.Equal(expectedRandom, first.Random);
        pairs.Clear();
        _ = DrawScreen8244E0(first, address, 5, Resources(), inputs);
        Assert.Equal(identities, pairs.Select(pair => pair.Quad.Primitive!.PilotGeometry!.Identity));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Programme_operation_uses_its_original_task_and_selected_geometry_without_serializing_metadata(bool second)
    {
        const uint address = 0x2646BF0;
        var frame = Frame(3, 20);
        frame = Write(frame, address, Read(frame, address).WriteDword(0x74, second ? 0x17B656Cu : 0x17B329Cu).WriteDword(0x78, second ? 712u : 724u));
        NativeBattlePilotVisualIdentity owner = NativeBattlePilotVisualLifetimes.Identity(49, Read(frame, address));
        List<NativeBattleResonance49DrawMesh104> draws = [];
        var result = InitializeProgramme(frame, address, Resources(), Inputs((current, operation) =>
        {
            draws.Add(Assert.IsType<NativeBattleResonance49DrawMesh104>(operation));
            return current;
        }));
        var draw = Assert.Single(draws);
        NativeBattlePilotVisualIdentity identity = Assert.IsType<NativeBattlePilotVisualIdentity>(draw.PilotIdentity);
        Assert.Equal(owner.Bank, identity.Bank);
        Assert.Equal(owner.Slot, identity.Slot);
        Assert.Equal(owner.Generation, identity.Generation);
        Assert.Equal((long)draw.SourceAddress, identity.ResourceGeneration);
        Assert.DoesNotContain("PilotIdentity", JsonSerializer.Serialize(draw));
        Assert.Equal(frame.Random, result.Random);
    }
}
