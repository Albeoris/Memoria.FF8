using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStatus120Smooth60Tests
{
    [Fact]
    public void Both_workspaces_capture_real_target_generation_without_changing_single_slot_three_contact_schedule()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(3, 8, 14, -1, 19, 120, null));
        for (byte actor = 0; actor < 3; actor++)
        {
            (packets, _) = packets.AppendContact(new(new(actor, 0, 0, 0, 4, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        }
        (packets, _) = packets.Publish(3);
        Assert.Equal((byte)3, packets.Headers[0].ReadByte(16));
        Assert.Equal(0, packets.Headers[0].FirstContactIndex);
        packets = packets.WritePlayerRepeatMode(0, 1);
        NativeBattleStatusMagicResources resources = new(120, new("animation", []), [Source(0x1277640), Source(0x1279C68)], 1);
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(resources.RootDescriptor, resources.RootBank, 20, 1);
        (root, _) = root.Append(resources.RootCallback);
        NativeBattleOriginalEffectState effect = new(120, 119, new(120, resources.Entry, 1), new(3, 0, 7, 0, 0, 0), root.DescriptorAddress) { Pools = [root] };
        NativeBattleStatusMagicFrame frame = new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, effect);
        NativeBattleGraphicsModel[] models = Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 1) { Transform = new(new(0, 0, 0), new((short)(index * 100), -100, 3000), 0, 0, 4096), Rotation14 = 0 }).ToArray();
        List<NativeBattlePilotVisualGeometry> geometries = [];
        List<(int Tick, int Contact)> contacts = [];
        NativeBattleStatusMagicInputs inputs = new(packets, new(NativeBattleCuragaMatrices.Identity8DD770(), 216, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270, index => models[index], _ => new([], []), (_, _) => throw new InvalidOperationException("120 does not write122 flags."), (current, operation) =>
        {
            if (operation is NativeBattleStatusMagicPackets draw)
            {
                foreach (NativeBattleGfGeometryAttempt attempt in draw.Batch.Attempts)
                {
                    if (attempt.Primitive is { } primitive)
                    {
                        geometries.Add(Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry));
                    }
                }
            }
            return current;
        });
        NativeBattlePilotVisualIdentity[] firstPair = [];
        for (int tick = 1; tick <= 51; tick++)
        {
            geometries.Clear();
            NativeBattleStatusMagicStep step = NativeBattleStatusMagicController.Advance(frame, resources, inputs);
            frame = step.Frame;
            contacts.AddRange(step.Contacts.Select(contact => (tick, contact)));
            if (frame.Effect.Pools.Length > 1)
            {
                Assert.Single(frame.Effect.Pools[1].Slots);
            }
            if (tick is 16 or 17)
            {
                Assert.Equal(2, geometries.Count);
                Assert.All(geometries, geometry => Assert.Equal((ushort)120, geometry.Identity.Effect));
                Assert.Equal(2, geometries.Select(geometry => geometry.Identity).Distinct().Count());
                Assert.Single(geometries.Select(geometry => (geometry.Identity.Bank, geometry.Identity.Slot, geometry.Identity.Generation)).Distinct());
                if (tick == 16)
                {
                    firstPair = geometries.Select(geometry => geometry.Identity).ToArray();
                }
                else
                {
                    Assert.Equal(firstPair, geometries.Select(geometry => geometry.Identity));
                }
            }
        }
        Assert.Equal(new[] { (44, 0), (44, 1), (44, 2) }, contacts);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
    }

    private static NativeBattleGfAnimationSource Source(uint address)
    {
        byte[] bytes = new byte[288];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 256);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 32);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(32), 76);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(36), 4);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(40), 1);
        short[] xy = [-100, -100, 100, -100, -100, 100, 100, 100];
        for (int vertex = 0; vertex < 4; vertex++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(44 + vertex * 8), xy[vertex * 2]);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(46 + vertex * 8), xy[vertex * 2 + 1]);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(120), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(124), 0x2C808080);
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(128 + corner * 2), (ushort)(corner * 2));
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(256), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(260), 60);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(264), 16);
        return new(address, bytes.ToImmutableArray());
    }
}
