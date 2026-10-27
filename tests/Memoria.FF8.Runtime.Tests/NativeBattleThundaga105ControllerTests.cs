using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleThundaga105ControllerTests
{
    [Fact]
    public void Moving_angles_reach_trigonometry_as_full_native_DWORDs()
    {
        int angle = NativeBattleThundaga105Controller.MovingAngle(32634, 1);
        Assert.Equal(33146, angle);
        Assert.Equal(36218, NativeBattleThundaga105Controller.MovingAngle(32634, 7));
        Assert.Equal(3426, NativeBattleCameraMath.Cosine(angle));
        Assert.Equal(3427, NativeBattleCameraMath.Cosine(unchecked((short)angle)));
    }

    [Fact]
    public void Physical_scheduler_runs_same_pass_children_preserves_failed_stationary_rng_and_waits_for_actual_completion()
    {
        var fixture = Fixture();
        NativeBattleThundaga105Frame frame = fixture.Frame;
        List<NativeBattleThundaga105Operation> operations = [];
        Dictionary<int, uint> colors = new() { [3] = 0xAB123456 };
        NativeBattleThundaga105Inputs inputs = Inputs(fixture.Packets, fixture.Resources, (current, operation) =>
        {
            operations.Add(operation);
            if (operation is NativeBattleThundaga105ActorColor color)
            {
                colors[color.Actor] = color.Color40;
            }
            return current;
        }, actor => colors[actor]);
        frame = NativeBattleThundaga105Controller.Initialize6D3DE0(frame, inputs, fixture.Resources);
        int primaryDraws = 0;
        int movingDraws = 0;
        int stationaryDraws = 0;
        int loadCallback = 0;
        uint completion = 0;
        for (int callback = 1; callback <= 120; callback++)
        {
            NativeBattleThundaga105Frame before = NativeBattleStorageSnapshots.Capture(frame);
            int operationStart = operations.Count;
            frame = NativeBattleThundaga105Controller.Advance6D3E50(frame, inputs, fixture.Resources);
            Assert.Equal((callback & 1) != 0 ? 0x20C1C : 0x8C1C, frame.Effect.DrawArenaOffset);
            if (callback == 1)
            {
                Assert.Single(frame.Effect.Pools);
            }
            if (callback == 2)
            {
                Assert.Equal(new ushort[] { 0x1C, 0x884, 0x3C, 0x2C }, frame.Effect.Pools.Select(pool => pool.Stride));
                Assert.Equal(new[] { 1, 3, 14, 32 }, frame.Effect.Pools.Select(pool => pool.Slots.Length));
                NativeBattleModulePoolTask primary = frame.Effect.Pools[1].Slots[0];
                Assert.Equal(0x1D97494u, primary.ReadDword(0x10));
                Assert.Equal(0xAB123456u, primary.ReadDword(0x14));
                Assert.Equal(2883u, primary.ReadDword(0x18));
                Assert.Equal((ushort)1, primary.ReadWord(0xC));
            }
            if (frame.Effect.Pools.Length == 4)
            {
                NativeBattleModulePoolTask primary = frame.Effect.Pools[1].Slots[0];
                if (primary.ReadWord(0xC) != before.Effect.Pools.ElementAtOrDefault(1)?.Slots[0].ReadWord(0xC))
                {
                    primaryDraws++;
                }
            }
            foreach (NativeBattleThundaga105Operation operation in operations.Skip(operationStart))
            {
                if (operation is NativeBattleThundaga105Sprite sprite)
                {
                    if (sprite.Task.Callback == 0x6D6100)
                    {
                        movingDraws++;
                    }
                    else
                    {
                        stationaryDraws++;
                    }
                }
                if (operation is NativeBattleThundaga105LoadCommon load)
                {
                    Assert.Equal(0, loadCallback);
                    Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x8C1C, load.DestinationAddress);
                    loadCallback = callback;
                    completion = load.CompletionWordAddress;
                }
            }
            if (loadCallback != 0 && callback == loadCallback + 2)
            {
                Assert.NotEqual(0u, frame.Effect.ReturnedDescriptor);
                frame = frame with { Effect = NativeBattleThundaga105Controller.WriteCompletionWord(frame.Effect, completion, 1) };
            }
            if (frame.Effect.ReturnedDescriptor == 0)
            {
                Assert.Equal(loadCallback + 3, callback);
                break;
            }
        }
        Assert.Equal(64, primaryDraws);
        Assert.Equal(32 * 12, movingDraws);
        Assert.Equal(5 * 15, stationaryDraws);
        Assert.Single(operations, operation => operation is NativeBattleThundaga105Capture);
        Assert.Single(operations, operation => operation is NativeBattleThundaga105Sound);
        Assert.Single(operations, operation => operation is NativeBattleThundaga105Contact);
        Assert.Equal(3, operations.Count(operation => operation is NativeBattleThundaga105CommonFlash));
        Assert.Equal(0xAB123456u, colors[3]);
        NativeCrtRandomState expected = new(1);
        for (int draw = 0; draw < 191; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, frame.Random);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.All(frame.Effect.Pools, pool => Assert.Null(pool.Head));
    }

    [Fact]
    public void Cold_json_continuation_preserves_live_fragment_rng_and_independent_retained_sources()
    {
        var fixture = Fixture(flags: 2);
        NativeBattleThundaga105Inputs inputs = Inputs(fixture.Packets, fixture.Resources);
        NativeBattleThundaga105Frame frame = NativeBattleThundaga105Controller.Initialize6D3DE0(fixture.Frame, inputs, fixture.Resources);
        for (int callback = 1; callback <= 90 && frame.Effect.ReturnedDescriptor != 0; callback++)
        {
            if (callback is 2 or 31 or 47 or 66)
            {
                NativeBattleThundaga105Frame cold = NativeBattleTestSnapshots.RestoreJson<NativeBattleThundaga105Frame>(NativeBattleTestSnapshots.Json(frame))!;
                Assert.Equal(NativeBattleTestSnapshots.Json(NativeBattleThundaga105Controller.Advance6D3E50(NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(frame)), inputs, fixture.Resources)), NativeBattleTestSnapshots.Json(NativeBattleThundaga105Controller.Advance6D3E50(cold, inputs, fixture.Resources)));
            }
            frame = NativeBattleThundaga105Controller.Advance6D3E50(frame, inputs, fixture.Resources);
        }
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.False(frame.Memory.Thundaga105!.ColdSourcesUnmaterialized);
        Assert.Equal(fixture.Resources.FragmentPairs[0].MutableMesh.Bytes.ToArray(), fixture.Frame.Memory.Thundaga105!.Materialize(fixture.Resources).MutableFragments[0].Bytes.ToArray());
    }

    [Fact]
    public void Missing_warm_source_and_wrong_completion_owners_fail_instead_of_reloading_or_releasing()
    {
        var fixture = Fixture();
        NativeBattleThundaga105Inputs inputs = Inputs(fixture.Packets, fixture.Resources);
        Assert.Throws<NotSupportedException>(() => NativeBattleThundaga105Controller.Initialize6D3DE0(fixture.Frame with { Memory = fixture.Frame.Memory with { Thundaga105 = null } }, inputs, fixture.Resources));
        Assert.Throws<NotSupportedException>(() => new NativeBattleThundaga105State(false, [], null).Materialize(fixture.Resources));
        NativeBattleThundaga105Frame frame = NativeBattleThundaga105Controller.Initialize6D3DE0(fixture.Frame, inputs, fixture.Resources);
        frame = NativeBattleThundaga105Controller.Advance6D3E50(frame, inputs, fixture.Resources);
        frame = NativeBattleThundaga105Controller.Advance6D3E50(frame, inputs, fixture.Resources);
        Assert.Throws<InvalidDataException>(() => NativeBattleThundaga105Controller.WriteCompletionWord(frame.Effect, frame.Effect.Pools[3].BankAddress + 0xE, 1));
    }

    private static NativeBattleThundaga105Inputs Inputs(NativeBattleActionPacketsState packets, NativeBattleThundaga105Resources resources, Func<NativeBattleThundaga105Frame, NativeBattleThundaga105Operation, NativeBattleThundaga105Frame>? execute = null, Func<int, uint>? color = null) => new(packets, _ => new byte[8].ToImmutableArray(), _ => new(0, -500, 0, 0), color ?? (_ => 0xAB123456), _ => 4096, (frame, task, _, _) =>
    {
        NativeBattleThundaga105ProgramDraw draw = NativeBattleThundaga105ProgramDrawing.Advance6D43B0(frame.Memory, task, resources.ProgramSource, resources.NodeSelector.Data, new byte[8].ToImmutableArray(), View, View, new(View, 216, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270);
        return new(frame with { Memory = draw.Memory }, draw.Task);
    }, execute ?? ((frame, _) => frame));

    private static NativeBattleActionMatrix View => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 18000, 4096);

    private static (NativeBattleThundaga105Frame Frame, NativeBattleActionPacketsState Packets, NativeBattleThundaga105Resources Resources) Fixture(byte flags = 0)
    {
        NativeBattleActionPacketsState packets = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        (packets, _) = packets.Prepare(new(0, 2, 4, 0, 8, 105, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        byte[] source = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(4), 8);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(12), 64);
        ImmutableArray<NativeBattleThundagaFragmentPair>.Builder pairs = ImmutableArray.CreateBuilder<NativeBattleThundagaFragmentPair>();
        for (int index = 0; index < 14; index++)
        {
            byte[] mesh = new byte[84];
            BinaryPrimitives.WriteInt32LittleEndian(mesh, 32);
            BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(4), 3);
            for (int vertex = 0; vertex < 3; vertex++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(8 + vertex * 8), (short)(vertex * 100 - 100));
                BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(10 + vertex * 8), -100);
            }
            BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(40), 1);
            BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(44), 0x24808080);
            BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(50), 2);
            BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(52), 4);
            uint address = (uint)(0x10000 + index * 0x100);
            pairs.Add(new(new(1, address, mesh.ToImmutableArray()), new(address + 84, mesh[..32].ToImmutableArray())));
        }
        NativeBattleThundaga105Resources resources = new(new("animation", []), new(0x13009C0, source.ToImmutableArray()), new(0, 0x131136C, 0, 0, new byte[12].ToImmutableArray()), new(0, 0x13115DC, 0, 0, new byte[12].ToImmutableArray()), new("sound", new byte[4].ToImmutableArray()), new("selector", new byte[32].ToImmutableArray()), pairs.ToImmutable());
        NativeBattleOriginalEffectState effect = new(105, 104, new(105, 0x6D3DB0, 0x6D3DC0), new(0, flags, 8, 0, 0, 0), null);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), Thundaga105 = NativeBattleThundaga105State.FromColdProcess() };
        return (new(memory, effect, new(1)), packets, resources);
    }
}
