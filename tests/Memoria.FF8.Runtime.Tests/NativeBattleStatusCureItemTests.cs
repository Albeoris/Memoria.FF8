using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStatusCureItemTests
{
    [Theory]
    [InlineData((ushort)292)]
    [InlineData((ushort)293)]
    [InlineData((ushort)294)]
    [InlineData((ushort)295)]
    [InlineData((ushort)296)]
    public void Root_starts_child_on_counter_one_and_contacts_only_after_actual_ACT_completion(ushort effect)
    {
        NativeBattleStatusCureItemProfile profile = NativeBattleStatusCureItemProfile.ForEffect(effect);
        byte[] layout = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(layout.AsSpan(4), 8);
        BinaryPrimitives.WriteInt32LittleEndian(layout.AsSpan(12), 2);
        NativeBattleStatusCureItemResources resources = new(effect, new(profile.SourceAddress, layout.ToImmutableArray()), 300000u + effect);
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(2, 2, 11, 0, (ushort)1, effect, null));
        (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState selected = new(effect, effect - 1, new(effect, profile.Entry, profile.Initializer), new(0, 1, 8, 0, 0, 0), null) { ResourcePointers = [new(profile.TextureGlobal, 0)] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(effect - 1);
        byte[] tim = new byte[32];
        BinaryPrimitives.WriteInt32LittleEndian(tim.AsSpan(8), 12);
        BinaryPrimitives.WriteInt32LittleEndian(tim.AsSpan(20), 12);
        memory = memory.LoadIntoArena(tim);
        memory = memory with { Arena = memory.Arena!.Write(256 + 18, new byte[] { 0xEF, 0xBE }) };
        NativeBattleStatusCureItemFrame frame = NativeBattleStatusCureItemController.Initialize(memory, selected, resources, 256);
        Assert.Equal(1, frame.Memory.Textures!.Count);
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 30000, 4096);
        NativeBattleStatusCureItemInputs inputs = new(packets, view, _ => new(10, 20, 30), _ => -47, (current, _, _) => current);
        NativeBattleStatusCureItemStep first = NativeBattleStatusCureItemController.Advance(frame, resources, inputs);
        Assert.Empty(first.Sounds);
        Assert.Empty(first.Contacts);
        Assert.Single(first.Frame.Effect.Pools);
        Assert.Equal(256 + profile.SecondaryBufferOffset, first.Frame.Effect.DrawArenaOffset);
        NativeBattleStatusCureItemStep started = NativeBattleStatusCureItemController.Advance(first.Frame, resources, inputs);
        Assert.Equal(resources.Sound, Assert.Single(started.Sounds).Sound);
        Assert.Empty(started.Contacts);
        Assert.Equal(256 + profile.PrimaryBufferOffset, started.Frame.Effect.DrawArenaOffset);
        NativeBattleModulePoolTask child = started.Frame.Effect.Pools[1].Slots[0];
        Assert.Equal(profile.SourceAddress, child.ReadDword(20));
        Assert.Equal(1u, child.ReadDword(24));
        Assert.Equal(effect == 293 ? unchecked((ushort)0xBEEF) : unchecked((ushort)-47), child.ReadWord(18));
        Assert.Equal(BitConverter.GetBytes(child.Flags), started.Frame.Memory.Arena!.Read(256, 2));
        NativeBattleStatusCureItemFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleStatusCureItemFrame>(NativeBattleTestSnapshots.Json(started.Frame))!;
        NativeBattleStatusCureItemStep complete = NativeBattleStatusCureItemController.Advance(restored, resources, inputs);
        Assert.Equal(new[] { 0 }, complete.Contacts);
        Assert.Empty(complete.Sounds);
        Assert.Equal(0u, complete.Frame.Effect.ReturnedDescriptor);
        Assert.Empty(NativeBattleStatusCureItemController.Advance(complete.Frame, resources, inputs).Contacts);
        if (effect == 295)
        {
            Assert.NotNull(restored.Execution.Billboard);
        }
    }

    [Theory]
    [InlineData((short)-5, (short)-2, (short)-2)]
    [InlineData((short)-1, (short)0, (short)0)]
    [InlineData((short)1, (short)0, (short)0)]
    [InlineData((short)5, (short)2, (short)2)]
    [InlineData((short)11, (short)5, (short)6)]
    public void Height_adjustments_preserve_native_signed_truncation(short height, short half, short holyWater)
    {
        NativeBattleVector position = new(10, 100, 30);
        Assert.Equal((short)(100 + half), NativeBattleStatusCureItemGeometry.Anchor(292, position, 100, height).Y);
        Assert.Equal((short)(100 + half), NativeBattleStatusCureItemGeometry.Anchor(294, position, 100, height).Y);
        Assert.Equal((short)(100 + holyWater), NativeBattleStatusCureItemGeometry.Anchor(296, position, 100, height).Y);
        Assert.Equal(position, NativeBattleStatusCureItemGeometry.Anchor(295, position, 100, height));
        Assert.Throws<NotSupportedException>(() => NativeBattleStatusCureItemGeometry.Anchor(293, position, 100, height));
        Assert.Equal(new NativeBattleVector(40, 50, 60), NativeBattleStatusCureItemGeometry.Anchor(293, position, 100, height, new(40, 50, 60)));
    }

    [Theory]
    [InlineData((ushort)292, true)]
    [InlineData((ushort)293, false)]
    [InlineData((ushort)294, true)]
    [InlineData((ushort)295, true)]
    [InlineData((ushort)296, false)]
    public void Header_8000_skips_view_rotation_only_in_its_original_wrappers(ushort effect, bool skipsView)
    {
        NativeBattleGfAnimationNode node = Node(0x8000, new(100, 0, 0));
        NativeBattleActionMatrix view = NativeBattleGfMatrices.Euler701310(new(0, 1024, 0)) with { X = 10, Y = 20, Z = 30 };
        NativeBattleActionMatrix actual = NativeBattleStatusCureItemGeometry.Compose(effect, node, new(32760, 0, 0), view);
        Assert.Equal(skipsView ? new short[] { 4096, 0, 0, 0, 4096, 0, 0, 0, 4096 } : view.Rotation.ToArray(), actual.Rotation.ToArray());
        (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(view, new(unchecked((short)(32760 + 100)), 0, 0));
        Assert.Equal(unchecked(x + 10), actual.X);
        Assert.Equal(unchecked(y + 20), actual.Y);
        Assert.Equal(unchecked(z + 30), actual.Z);
    }

    internal static NativeBattleGfAnimationNode Node(uint header, NativeBattleVector offset)
    {
        byte[] data = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), header);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(8), offset.X);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(10), offset.Y);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(12), offset.Z);
        for (int index = 0; index < 3; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(24 + index * 2), 4096);
        }
        return new(0, 0, data.ToImmutableArray());
    }
}
