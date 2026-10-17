using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRestorationNodesTests
{
    [Theory]
    [InlineData((ushort)24, 40, 100)]
    [InlineData((ushort)27, 50, 140)]
    public void Allocation_uses_original_capacities_clear_link_kinds_and_exact_pointer_fields(ushort effect, int emitterCount, int geometryCount)
    {
        var fixture = Fixture(effect);
        NativeBattleRestorationFrame frame = fixture.Frame;
        int emitter = frame.State.Emitters!.Value;
        int geometry = frame.State.Geometry!.Value;
        Assert.Equal(emitterCount * 108, geometry - emitter);
        Assert.Equal(geometryCount * 672, frame.State.AuxiliaryCursor - geometry);
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.MarkUnknown(emitter + 12, 16) } };
        NativeBattleRestorationNodeAllocation first = NativeBattleRestorationNodes.AllocateEmitter(frame, fixture.Task, 0x12345678, -2, 3);
        Assert.Equal(emitter, first.ArenaOffset);
        Assert.All(first.Frame.Memory.Arena!.Read(emitter + 12, 16), value => Assert.Equal((byte)0, value));
        Assert.Equal(0x12345678u, Dword(first.Frame, emitter + 0x5C));
        NativeBattleRestorationNodeAllocation second = NativeBattleRestorationNodes.AllocateGeometry(first.Frame, fixture.Task, emitter, 8);
        Assert.Equal(geometry, second.ArenaOffset);
        Assert.Equal(0x20DFAB8u + (uint)emitter, Dword(second.Frame, geometry + 0x1BC));
        Assert.Equal(0x20DFAB8u + (uint)geometry, Dword(second.Frame, emitter + 4));
        Assert.Equal(0x20DFAB8u + (uint)emitter, Dword(second.Frame, geometry));
        Assert.Equal((byte)3, second.Frame.Memory.Arena!.Read(geometry + 0x1D9, 1)[0]);
        Assert.Equal((ushort)1, new NativeBattleEffectHierarchy(second.Frame.Effect.Pools).Read(fixture.Task).ReadWord(0x34 + 0x14));
        Assert.Equal((ushort)1, new NativeBattleEffectHierarchy(second.Frame.Effect.Pools).Read(fixture.Task).ReadWord(0x34 + 0x16));
        NativeBattleRestorationFrame clone = JsonSerializer.Deserialize<NativeBattleRestorationFrame>(JsonSerializer.Serialize(second.Frame))!;
        Assert.Equal(JsonSerializer.Serialize(NativeBattleRestorationNodes.AllocateEmitter(second.Frame, fixture.Task, 0, 1, 0)), JsonSerializer.Serialize(NativeBattleRestorationNodes.AllocateEmitter(clone, fixture.Task, 0, 1, 0)));
    }

    [Theory]
    [InlineData((ushort)24, 40, 100)]
    [InlineData((ushort)27, 50, 140)]
    public void Exhausted_scan_keeps_native_count_minus_one_wrap_and_final_increment(ushort effect, int emitterCount, int geometryCount)
    {
        var fixture = Fixture(effect);
        int emitter = fixture.Frame.State.Emitters!.Value;
        NativeBattleRestorationFrame emitterFull = FillActive(fixture.Frame, emitter, emitterCount, 108, 0x69);
        NativeBattleRestorationNodeAllocation exhaustedEmitter = NativeBattleRestorationNodes.AllocateEmitter(emitterFull, fixture.Task, 0, 0, 0);
        Assert.Null(exhaustedEmitter.ArenaOffset);
        Assert.Equal((ushort)2, exhaustedEmitter.Frame.State.EmitterScanCursor);
        NativeBattleRestorationFrame live = NativeBattleRestorationNodes.AllocateEmitter(fixture.Frame, fixture.Task, 0, 0, 0).Frame;
        live = FillActive(live, live.State.Geometry!.Value, geometryCount, 672, 0x1D7);
        NativeBattleRestorationNodeAllocation exhaustedGeometry = NativeBattleRestorationNodes.AllocateGeometry(live, fixture.Task, emitter, 0);
        Assert.Null(exhaustedGeometry.ArenaOffset);
        Assert.Equal((ushort)2, exhaustedGeometry.Frame.State.GeometryScanCursor);
        Assert.Equal((ushort)0, new NativeBattleEffectHierarchy(exhaustedGeometry.Frame.Effect.Pools).Read(fixture.Task).ReadWord(0x34 + 0x16));
    }

    [Theory]
    [InlineData((ushort)24, 40, 100)]
    [InlineData((ushort)27, 50, 140)]
    public void Observed_last_physical_cursor_can_allocate_that_slot_before_native_wrap(ushort effect, int emitterCount, int geometryCount)
    {
        var fixture = Fixture(effect);
        NativeBattleRestorationFrame frame = fixture.Frame with { State = fixture.Frame.State with { EmitterScanCursor = (ushort)(emitterCount - 1), GeometryScanCursor = (ushort)(geometryCount - 1) } };
        NativeBattleRestorationNodeAllocation emitter = NativeBattleRestorationNodes.AllocateEmitter(frame, fixture.Task, 0, 0, 0);
        Assert.Equal(frame.State.Emitters + (emitterCount - 1) * 108, emitter.ArenaOffset);
        Assert.Equal((ushort)0, emitter.Frame.State.EmitterScanCursor);
        NativeBattleRestorationNodeAllocation geometry = NativeBattleRestorationNodes.AllocateGeometry(emitter.Frame, fixture.Task, emitter.ArenaOffset!.Value, 0);
        Assert.Equal(frame.State.Geometry + (geometryCount - 1) * 672, geometry.ArenaOffset);
        Assert.Equal((ushort)0, geometry.Frame.State.GeometryScanCursor);
    }

    [Theory]
    [InlineData((ushort)24)]
    [InlineData((ushort)27)]
    public void Walker_skips_unknown_signed_kind_but_keeps_following_its_original_next_link(ushort effect)
    {
        var fixture = Fixture(effect);
        NativeBattleRestorationNodeAllocation emitter = NativeBattleRestorationNodes.AllocateEmitter(fixture.Frame, fixture.Task, 0, 0, 0);
        NativeBattleRestorationNodeAllocation geometry = NativeBattleRestorationNodes.AllocateGeometry(emitter.Frame, fixture.Task, emitter.ArenaOffset!.Value, 0);
        NativeBattleRestorationFrame frame = geometry.Frame;
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(emitter.ArenaOffset.Value + 8, [255, 255]) } };
        List<short> kinds = [];
        NativeBattleRestorationNodes.AdvanceList(frame, fixture.Task, (current, node, kind) =>
        {
            kinds.Add(kind);
            Assert.Equal(geometry.ArenaOffset, node);
            return current;
        });
        Assert.Equal(new short[] { 1 }, kinds);
    }

    [Theory]
    [InlineData((ushort)24)]
    [InlineData((ushort)27)]
    public void Walker_reads_next_after_callback_and_visits_appended_node_after_current_is_unlinked(ushort effect)
    {
        var fixture = Fixture(effect);
        NativeBattleRestorationNodeAllocation emitter = NativeBattleRestorationNodes.AllocateEmitter(fixture.Frame, fixture.Task, 0, 0, 0);
        List<short> kinds = [];
        int? geometry = null;
        NativeBattleRestorationFrame frame = NativeBattleRestorationNodes.AdvanceList(emitter.Frame, fixture.Task, (current, node, kind) =>
        {
            kinds.Add(kind);
            if (kind == 0)
            {
                NativeBattleRestorationNodeAllocation appended = NativeBattleRestorationNodes.AllocateGeometry(current, fixture.Task, node, 0);
                geometry = appended.ArenaOffset;
                return NativeBattleRestorationNodes.Unlink(appended.Frame, fixture.Task, node);
            }
            Assert.Equal(geometry, node);
            return current;
        });
        Assert.Equal(new short[] { 0, 1 }, kinds);
        Assert.Equal(0x20DFAB8u + (uint)geometry!.Value, Dword(frame, emitter.ArenaOffset!.Value + 4));
        Assert.Equal(0x20DFAB8u + (uint)geometry.Value, new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(fixture.Task).ReadDword(0x34 + 0x2C));
    }

    [Theory]
    [InlineData((ushort)24)]
    [InlineData((ushort)27)]
    public void Unknown_pool_or_scan_is_not_seeded_from_another_spell(ushort effect)
    {
        var fixture = Fixture(effect);
        Assert.Throws<NotSupportedException>(() => NativeBattleRestorationNodes.AllocateEmitter(fixture.Frame with { State = fixture.Frame.State with { EmitterScanCursor = null } }, fixture.Task, 0, 0, 0));
        Assert.Throws<NotSupportedException>(() => NativeBattleRestorationNodes.AllocateEmitter(fixture.Frame with { State = fixture.Frame.State with { Emitters = null } }, fixture.Task, 0, 0, 0));
    }

    internal static (NativeBattleRestorationFrame Frame, uint Task) Fixture(ushort effect)
    {
        var fixture = NativeBattleRestorationControllerTests.Fixture(effect);
        NativeBattleRestorationFrame frame = fixture.Frame;
        NativeBattleRestorationResources resources = new(effect, new("skipped-original-registration", []), effect == 24 ? 0x49442u : 0x49445u);
        for (int callback = 0; callback < 4; callback++)
        {
            frame = NativeBattleRestorationController.Advance(frame, fixture.Inputs, resources, (current, _) => new(current, 0)).Frame;
        }
        return (frame, NativeBattleRestorationProfile.ForEffect(effect).Banks[2]);
    }

    private static uint Dword(NativeBattleRestorationFrame frame, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Memory.Arena!.Read(offset, 4));
    private static NativeBattleRestorationFrame FillActive(NativeBattleRestorationFrame frame, int start, int count, int stride, int flag)
    {
        byte[] bytes = frame.Memory.Arena!.Read(start, count * stride);
        for (int index = 0; index < count; index++)
        {
            bytes[index * stride + flag] = 1;
        }
        return frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena.Write(start, bytes) } };
    }
}
