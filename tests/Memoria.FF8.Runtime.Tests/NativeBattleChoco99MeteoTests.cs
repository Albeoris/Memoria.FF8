using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleChoco99MeteoTests
{
    private static NativeBattleActionMatrix View => NativeBattleCuragaMatrices.Identity8DD770() with { Z = 12000 };
    private static NativeBattleCameraProjection Projection => new(View, 216, 160, 108);

    [Fact]
    public void Meteo_hides16_callbacks_then_draws_all15_frames_and_appends_one_actual_small_GF_child()
    {
        NativeBattleModulePoolTask task = Task(0x720500).WriteWord(0x54, 0xFF00);
        int appends = 0;
        List<ushort> frames = [];
        for (int index = 0; index <= 31; index++)
        {
            NativeBattleModulePoolTask restored = JsonSerializer.Deserialize<NativeBattleModulePoolTask>(JsonSerializer.Serialize(task))!;
            NativeBattleChoco97SpriteStep step = Advance(task, true);
            Assert.Equal(JsonSerializer.Serialize(step), JsonSerializer.Serialize(Advance(restored, false)));
            if (step.Packets is not null)
            {
                frames.Add(step.Task.ReadWord(0x50));
            }
            Assert.Equal((ushort)0xFF00, step.Task.ReadWord(0x54));
            Assert.Equal((ushort)(index + 1), step.Task.ReadWord(0x24));
            Assert.False(step.ReadyToComplete);
            task = step.Task;
        }
        Assert.Equal(1, appends);
        Assert.Equal(Enumerable.Range(0, 15).Select(value => (ushort)value).ToArray(), frames.ToArray());
        Assert.Equal((byte)3, task.ReadByte(0x29));
        Assert.Equal((byte)1, task.ReadByte(0x28));
        Assert.True(Advance(task.WriteByte(0x28, 0), true).ReadyToComplete);

        NativeBattleChoco97SpriteStep Advance(NativeBattleModulePoolTask current, bool count) => NativeBattleChoco99Meteo.Advance720500(current, Resources(), 0x19F0F40, (parent, operation) =>
        {
            Assert.Equal(new NativeBattleChoco97AppendProgram(0x720610, 0x19F0F40, 0x2C, 4, 0), operation);
            if (count)
            {
                appends++;
            }
            return parent.WriteByte(0x28, 1);
        }, View, Projection, 0);
    }

    [Fact]
    public void Emitter_appends_two_children_and_consumes6_CRT_even_on_terminal_mode8_then_phase0_overwrites_only_Y_and_resource()
    {
        NativeBattleEffectHierarchy hierarchy = new([NativeBattleEffectTaskPool.Initialize(NativeBattleChoco99Particles.AuxiliaryDescriptor, 0x2566000, 0xB0, 3)]);
        NativeBattleEffectHierarchyAppend emitter = hierarchy.Append8DC540(0, 0x720790, 0xB0);
        hierarchy = emitter.Hierarchy.Write(emitter.Address, emitter.Hierarchy.Read(emitter.Address).WriteDword(0xC, 0x1D99A78).WriteByte(0x2A, 0xFF).WriteByte(0x2B, 2).WriteWord(0x22, 0x1234));
        int selects = 0;
        NativeBattleChoco99MeteoEmitterStep step = NativeBattleChoco99Meteo.Advance720790(hierarchy, emitter.Address, Resources(), threshold => threshold == 8, new(1), (pointer, header, target) =>
        {
            Assert.Equal(0x1D99A78u, pointer);
            Assert.Equal((sbyte)-1, header);
            Assert.Equal((sbyte)2, target);
            selects++;
            return new(0, 3);
        });
        Assert.Equal(2, selects);
        Assert.Equal((byte)2, step.Task.ReadByte(0x28));
        Assert.Equal((byte)1, step.Task.ReadByte(0x29));
        Assert.False(step.ReadyToComplete);
        NativeCrtRandomState expected = new(1);
        for (int draw = 0; draw < 6; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, step.Random);
        NativeBattleModulePoolTask[] children = step.Hierarchy.Pools[0].Slots.Where(slot => slot.Callback == 0x7208D0).ToArray();
        Assert.Equal(new uint[] { 0x152A894, 0x152A7D0 }, children.Select(child => child.ReadDword(0x4C)).ToArray());
        foreach (NativeBattleModulePoolTask child in children)
        {
            NativeBattleChoco97SpriteStep initialized = NativeBattleChoco99Meteo.Advance7208D0(child, Resources(), _ => throw new InvalidOperationException(), View, Projection, 0);
            Assert.Equal(child.ReadWord(0x1C), initialized.Task.ReadWord(0x1C));
            Assert.Equal(child.ReadWord(0x20), initialized.Task.ReadWord(0x20));
            Assert.Equal((ushort)0x1234, initialized.Task.ReadWord(0x22));
            Assert.Equal(unchecked((ushort)-8192), initialized.Task.ReadWord(0x1E));
            Assert.Equal(0x152A70Cu, initialized.Task.ReadDword(0x4C));
        }
        NativeBattleChoco99MeteoEmitterStep held = NativeBattleChoco99Meteo.Advance720790(step.Hierarchy, emitter.Address, Resources(), _ => throw new InvalidOperationException(), step.Random, (_, _, _) => throw new InvalidOperationException());
        Assert.Equal(step.Random, held.Random);
        Assert.Equal((byte)2, held.Task.ReadByte(0x28));
    }

    [Fact]
    public void Secondary_terminal_frame_short_circuits_mode_query_and_hides_while_early_mode_also_sets_both_flags()
    {
        NativeBattleModulePoolTask task = Task(0x7208D0).WriteByte(0x29, 1).WriteDword(0x4C, 0x152A70C).WriteWord(0x50, 6).WriteWord(0x52, 6);
        NativeBattleChoco97SpriteStep terminal = NativeBattleChoco99Meteo.Advance7208D0(task, Resources(), _ => throw new InvalidOperationException(), View, Projection, 0);
        Assert.True(terminal.ReadyToComplete);
        Assert.Null(terminal.Packets);
        Assert.Equal((byte)5, terminal.Task.ReadByte(0x26));
        NativeBattleChoco97SpriteStep early = NativeBattleChoco99Meteo.Advance7208D0(task.WriteWord(0x50, 0), Resources(), _ => true, View, Projection, 0);
        Assert.True(early.ReadyToComplete);
        Assert.Null(early.Packets);
        Assert.Equal((ushort)1, early.Task.ReadWord(0x50));
    }

    [Fact]
    public void CameraY_delay_transition_does_not_execute_program_then_writes_only_actual_values_and_increments_sentinel_index()
    {
        NativeBattleModulePoolTask task = new NativeBattleModulePoolTask(1, 0, null, 0x71F880, new byte[0x40 - 12].ToImmutableArray()).WriteDword(0x34, 0x152AEF0).WriteWord(0x38, 8);
        for (int index = 0; index < 8; index++)
        {
            NativeBattleChoco99CameraYStep delayed = NativeBattleChoco99Meteo.Advance71F880(task, Resources().CameraY);
            Assert.Null(delayed.Y);
            Assert.False(delayed.ReadyToComplete);
            task = delayed.Task;
        }
        Assert.Equal((byte)1, task.ReadByte(0x29));
        List<short> values = [];
        for (int index = 0; index < 9; index++)
        {
            NativeBattleModulePoolTask restored = JsonSerializer.Deserialize<NativeBattleModulePoolTask>(JsonSerializer.Serialize(task))!;
            NativeBattleChoco99CameraYStep step = NativeBattleChoco99Meteo.Advance71F880(task, Resources().CameraY);
            Assert.Equal(JsonSerializer.Serialize(step), JsonSerializer.Serialize(NativeBattleChoco99Meteo.Advance71F880(restored, Resources().CameraY)));
            if (step.Y is short y)
            {
                values.Add(y);
            }
            task = step.Task;
        }
        Assert.Equal(Resources().CameraY.Values.Take(8).ToArray(), values.ToArray());
        Assert.Equal((byte)2, task.ReadByte(0x29));
        Assert.Equal((ushort)9, task.ReadWord(0x38));
        Assert.Null(NativeBattleChoco99Meteo.Advance71F880(task, Resources().CameraY).Y);
    }

    [Fact]
    public void Overlay_phase0_pause_gates_delay_and_transition_has_no_draw_then_shared_program_preserves_fixed_packets_and_FF_phase()
    {
        NativeBattleEffectHierarchy hierarchy = new([NativeBattleEffectTaskPool.Initialize(0x256C470, 0x256C480, 0x40, 1)]);
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(0, 0x8DDC30, 0x40);
        hierarchy = appended.Hierarchy.Write(appended.Address, appended.Hierarchy.Read(appended.Address).WriteDword(0x34, 0x152AF04).WriteWord(0x38, 1));
        NativeBattleFullscreenOverlayFrame frame = new(NativeBattleEffectMemory.FromColdProcess(), hierarchy.Pools, NativeBattleLogicalOrdering.Clear());
        NativeBattleFullscreenOverlayStep paused = NativeBattleFullscreenOverlay.Advance8DDC30(frame, appended.Address, 1, Resources().Overlay);
        Assert.Equal((ushort)1, paused.Frame.Pools[0].Slots[0].ReadWord(0x38));
        Assert.Equal((ushort)0, paused.Frame.Pools[0].Slots[0].ReadWord(0x3C));
        Assert.Equal((ushort)1, paused.Frame.Pools[0].Slots[0].ReadWord(0x24));
        Assert.Empty(paused.Frame.Ordering.Packets);
        frame = NativeBattleFullscreenOverlay.Advance8DDC30(paused.Frame, appended.Address, 0, Resources().Overlay).Frame;
        Assert.Equal((byte)1, frame.Pools[0].Slots[0].ReadByte(0x29));
        Assert.Empty(frame.Ordering.Packets);
        for (int index = 0; index < 3; index++)
        {
            frame = frame with { Ordering = NativeBattleLogicalOrdering.Clear() };
            NativeBattleFullscreenOverlayFrame restored = JsonSerializer.Deserialize<NativeBattleFullscreenOverlayFrame>(JsonSerializer.Serialize(frame))!;
            NativeBattleFullscreenOverlayStep drawn = NativeBattleFullscreenOverlay.Advance8DDC30(frame, appended.Address, 0, Resources().Overlay);
            Assert.Equal(JsonSerializer.Serialize(drawn), JsonSerializer.Serialize(NativeBattleFullscreenOverlay.Advance8DDC30(restored, appended.Address, 0, Resources().Overlay)));
            Assert.Equal(5, drawn.Frame.Ordering.ProxyCursor);
            Assert.Equal(0x2A000000u | Resources().Overlay.Commands[index], BinaryPrimitives.ReadUInt32LittleEndian(drawn.Frame.Memory.FullscreenOverlay!.Read(0, 24).AsSpan(4)));
            frame = drawn.Frame;
        }
        NativeBattleFullscreenOverlayStep terminal = NativeBattleFullscreenOverlay.Advance8DDC30(frame, appended.Address, 0, Resources().Overlay);
        Assert.Equal(2, terminal.Result);
        Assert.Equal((byte)1, terminal.Frame.Pools[0].Slots[0].ReadByte(0x29));
    }

    private static NativeBattleModulePoolTask Task(uint callback) => new(1, 0, null, callback, new byte[0xB0 - 12].ToImmutableArray());
    private static NativeBattleChoco99MeteoResources Resources() => new(Sprite(0x152A460, 0x2AC, 15), [Sprite(0x152A70C, 0xC4, 7), Sprite(0x152A7D0, 0xC4, 7), Sprite(0x152A894, 0xC4, 7)], new(0x152AEF0, [88, -80, 72, -64, 56, -48, 56, -48, 127]), new(0x152AF04, [0x00C0C0C0, 0x00808080, 0x00404040, 0xFF000000]));
    private static NativeBattleChoco97SpriteResources Sprite(uint address, int length, int frames)
    {
        byte[] bytes = new byte[length];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), (ushort)frames);
        int start = 12 + frames * 2;
        for (int frame = 0; frame < frames; frame++)
        {
            int cursor = start + frame * 24;
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + frame * 2), (ushort)cursor);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor), 1);
            bytes[cursor + 4] = 16;
            bytes[cursor + 5] = 128;
            bytes[cursor + 6] = 8;
            bytes[cursor + 7] = 0x2C;
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + frames * 2), 0xFFFF);
        return new(address, bytes.ToImmutableArray());
    }
}
