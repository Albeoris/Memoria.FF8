using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStatusMagicControllerTests
{
    [Theory]
    [InlineData(122, 1, 62, 52)]
    [InlineData(122, 3, 82, 52)]
    [InlineData(120, 1, 51, 44)]
    [InlineData(120, 3, 51, 44)]
    public void Distinct_controllers_keep_native_contact_end_action_capacity_and_JSON_continuation(ushort effect, int actions, int end, int firstContact)
    {
        (NativeBattleStatusMagicFrame frame, NativeBattleStatusMagicResources resources, NativeBattleActionPacketsState packets) = Fixture(effect, actions);
        NativeBattleGraphicsModel[] models = Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 1) { Transform = new(new(0, 0, 0), new((short)(index * 100), -100, 0), 0, 0, 4096), Rotation14 = 1024 }).ToArray();
        List<int> contactTicks = [];
        List<int> contactIndices = [];
        List<int> soundTicks = [];
        List<int> fadeTicks = [];
        NativeBattleStatusMagicInputs inputs = new(packets, new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096), 216, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270, index => models[index], _ => new([], []), (index, flags) => models[index] = models[index] with { Flags0 = flags }, (current, _) => current);
        for (int tick = 1; tick <= end; tick++)
        {
            int now = tick;
            NativeBattleStatusMagicInputs observed = inputs with { Execute = (current, operation) =>
            {
                if (operation is NativeBattleStatusMagicFade)
                {
                    fadeTicks.Add(now);
                }
                return current;
            } };
            NativeBattleStatusMagicStep step = NativeBattleStatusMagicController.Advance(frame, resources, observed);
            frame = step.Frame;
            foreach (int contact in step.Contacts)
            {
                contactTicks.Add(tick);
                contactIndices.Add(contact);
            }
            soundTicks.AddRange(step.Sounds.Select(_ => tick));
            if (tick == 25)
            {
                frame = JsonSerializer.Deserialize<NativeBattleStatusMagicFrame>(JsonSerializer.Serialize(frame))!;
            }
            if (tick < end)
            {
                Assert.NotEqual(0u, frame.Effect.ReturnedDescriptor);
            }
        }
        int played = effect == 120 ? 1 : actions;
        Assert.Equal(Enumerable.Range(0, played).Select(index => firstContact + index * 10), contactTicks);
        Assert.Equal(Enumerable.Range(0, played), contactIndices);
        Assert.Equal(Enumerable.Range(0, played).Select(index => 2 + index * 10), soundTicks);
        Assert.Equal(effect == 120 ? new[] { 6 } : Array.Empty<int>(), fadeTicks);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.All(frame.Effect.Pools, pool => Assert.Null(pool.Head));
        Assert.Equal((ushort)1, models[3].Flags0);
        Assert.Same(frame, NativeBattleStatusMagicController.Advance(frame, resources, inputs).Frame);
    }

    [Fact]
    public void Screen_axis_offset_retains_full_MAC_while_world_offset_wraps_WORD_and_native_rotation_variants_differ()
    {
        NativeBattleActionMatrix camera = new([0, -4096, 0, 4096, 0, 0, 0, 0, 4096], 123, 456, 4096, 4096);
        NativeBattleVector anchor = new(30000, 0, 0);
        NativeBattleActionMatrix billboard = NativeBattleStatusMagicDrawing.Matrix(122, Node(0x200, new(10000, 0, 0)), anchor, camera);
        NativeBattleActionMatrix world = NativeBattleStatusMagicDrawing.Matrix(122, Node(0, new(10000, 0, 0)), anchor, camera);
        Assert.Equal(10123, billboard.X);
        Assert.Equal(30456, billboard.Y);
        Assert.Equal(123, world.X);
        Assert.Equal(-25080, world.Y);
        NativeBattleGfAnimationNode alternate = Node(0x600, new(0, 0, 0), new(300, 500, 700));
        Assert.Equal(NativeBattleGfMatrices.Euler701430(alternate.Vector16).Rotation, NativeBattleStatusMagicDrawing.Matrix(120, alternate, new(0, 0, 0), camera).Rotation);
        Assert.NotEqual(NativeBattleStatusMagicDrawing.Matrix(120, alternate, new(0, 0, 0), camera).Rotation.ToArray(), NativeBattleStatusMagicDrawing.Matrix(122, alternate, new(0, 0, 0), camera).Rotation.ToArray());
    }

    [Fact]
    public void Model_pointer_and_size_depth_use_exact_native_widths()
    {
        Assert.Equal(6, NativeBattleStatusMagicController.ModelIndex(0x1D972C0 + 6 * 156));
        Assert.Throws<InvalidDataException>(() => NativeBattleStatusMagicController.ModelIndex(0x1D972C1));
        Assert.Throws<InvalidDataException>(() => NativeBattleStatusMagicController.ModelIndex(0x1D972C0 + 7 * 156));
        NativeBattleGraphicsModel model = new(3, 1) { Transform = new(new(0, 0, 0), new(0, 0, 0), 0, 0, -4096) };
        Assert.Equal(64, NativeBattleStatusMagicDrawing.SizeDepth(model));
    }

    private static NativeBattleGfAnimationNode Node(uint header, NativeBattleVector offset, NativeBattleVector? rotation = null)
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), header);
        PutVector(8, offset);
        PutVector(16, rotation ?? new(0, 0, 0));
        PutVector(24, new(4096, 4096, 4096));
        return new(0, 0, bytes.ToImmutableArray());

        void PutVector(int start, NativeBattleVector vector)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(start), vector.X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(start + 2), vector.Y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(start + 4), vector.Z);
        }
    }

    private static (NativeBattleStatusMagicFrame Frame, NativeBattleStatusMagicResources Resources, NativeBattleActionPacketsState Packets) Fixture(ushort effect, int actions)
    {
        NativeBattleActionPacketsState packets = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        for (int action = 0; action < actions; action++)
        {
            (packets, _) = packets.Prepare(new(0, 2, 4, 0, 8, effect, null));
            (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
            (packets, _) = packets.Publish(1);
        }
        packets = packets.WritePlayerRepeatMode(0, checked((byte)actions));
        byte[] source = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(4), 8);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(12), 60);
        NativeBattleStatusMagicResources resources = new(effect, new("animation", []), effect == 120 ? [new(0x1277640, source.ToImmutableArray()), new(0x1279C68, source.ToImmutableArray())] : [new(0x126E4C0, source.ToImmutableArray())], 1);
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(resources.RootDescriptor, resources.RootBank, 20, 1);
        (root, _) = root.Append(resources.RootCallback);
        NativeBattleOriginalEffectState original = new(effect, effect - 1, new(effect, resources.Entry, 1), new(0, 0, 8, 0, 0, 0), root.DescriptorAddress) { Pools = [root] };
        return (new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, original), resources, packets);
    }
}
