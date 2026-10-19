using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleIfritCallbackTests
{
    private const uint Manager = 0x2797690;

    [Fact]
    public void Callback_restores_the_current_scratch_focal_after_script_or_draw_changes()
    {
        short scratchFocal = 320;
        short? published = null;
        NativeBattleScriptedCallbackServices services = Services() with
        {
            DrawSlots = (current, _) =>
            {
                scratchFocal = -123;
                return current;
            },
            ReadCurrentFocal = _ => scratchFocal,
            PublishFocal = (current, value) =>
            {
                published = value;
                return current;
            }
        };
        _ = NativeBattleIfritCallback.RunB25DF0(Create(), Input(0), services);
        Assert.Equal((short)-123, published);
    }

    [Fact]
    public void Callback_orders_pre_physics_post_and_selected_lit_draw_before_focal()
    {
        NativeBattleWaterExecutionFrame frame = Create();
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0, 0x80001000);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 4, 0x1000);
        List<string> calls = [];
        NativeBattleScriptedCallbackServices services = Services() with
        {
            ReadInstruction = (_, _) => 9,
            ExecuteInstruction = (current, _) =>
            {
                calls.Add(unchecked((int)WaterSchedulerFixture.ReadCoreDword(current, 0x2797450)) < 0 ? "pre" : "post");
                return WaterSchedulerFixture.CoreDword(current, 0x2797450, 0).WriteTemporaryWord(0x13E, 3);
            },
            UpdateSlot = (current, _) =>
            {
                calls.Add("physics");
                return current;
            },
            DrawSlots = (current, _) =>
            {
                calls.Add("lit-draw");
                return current;
            },
            PublishFocal = (current, _) =>
            {
                calls.Add("focal");
                return current;
            }
        };
        NativeBattleWaterCallbackResult result = NativeBattleIfritCallback.RunB25DF0(frame, Input(0), services);
        Assert.Equal(new[] { "pre", "physics", "post", "lit-draw", "focal" }, calls);
        Assert.Equal(frame.Random.Draw(out _), result.Frame.Random);
        Assert.Equal(0u, result.TaskResult);
    }

    [Fact]
    public void Lit_traversal_prepares_signed_nonzero_light_before_alternate_cursor_swap_and_reads_live_order()
    {
        NativeBattleWaterExecutionFrame frame = Create();
        frame = frame with { Core = frame.Core.Write(0x2797554, [0, 1, 0xFF]), Arena = frame.Arena.Write(0x20E1, [0x83]).Write(0x204C, [1]) };
        frame = WaterSchedulerFixture.CoreDword(frame, Manager + 0x7C, 0x300000);
        frame = WaterSchedulerFixture.CoreDword(frame, Manager + 0xD8, 0x400000);
        List<string> calls = [];
        NativeBattleWaterExecutionFrame result = NativeBattleIfritDraw.RunB2ABE0(frame, (current, light) =>
        {
            calls.Add("light");
            Assert.Equal(unchecked((sbyte)0x83), light);
            Assert.Equal(0x300000u, WaterSchedulerFixture.ReadCoreDword(current, Manager + 0x7C));
            Assert.Equal((byte)0, current.ReadTemporaryByte(0x142));
            Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x2000, WaterSchedulerFixture.ReadCoreDword(current, 0x27973E8));
            return current;
        }, (current, _) =>
        {
            calls.Add("draw");
            Assert.Equal(0x400000u, WaterSchedulerFixture.ReadCoreDword(current, Manager + 0x7C));
            current = WaterSchedulerFixture.CoreDword(current, Manager + 0x7C, 0x400004);
            return current with { Core = current.Core.Write(0x2797555, [0xFF]) };
        });
        Assert.Equal(new[] { "light", "draw" }, calls);
        Assert.Equal(0x300000u, WaterSchedulerFixture.ReadCoreDword(result, Manager + 0x7C));
        Assert.Equal(0x400004u, WaterSchedulerFixture.ReadCoreDword(result, Manager + 0xD8));
    }

    [Fact]
    public void Zero_light_does_not_invoke_lighting_and_unknown_light_service_fails_at_reached_slot()
    {
        NativeBattleWaterExecutionFrame frame = Create();
        frame = frame with { Core = frame.Core.Write(0x2797554, [0, 0xFF]) };
        int draws = 0;
        NativeBattleIfritDraw.RunB2ABE0(frame, (_, _) => throw new InvalidOperationException(), (current, _) =>
        {
            draws++;
            return current;
        });
        Assert.Equal(1, draws);
        frame = frame with { Arena = frame.Arena.Write(0x20E1, [1]) };
        Assert.Throws<NotSupportedException>(() => NativeBattleIfritDraw.RunB2ABE0(frame, (_, _) => throw new NotSupportedException("Selected lighting kernel unavailable."), (current, _) => current));
    }

    [Fact]
    public void Independent_restore_preserves_defined_next_callback_and_latest_external_owners()
    {
        NativeBattleIfrit201Frame live = ControllerFrame();
        NativeBattleIfrit201Frame restored = JsonSerializer.Deserialize<NativeBattleIfrit201Frame>(JsonSerializer.Serialize(live))!;
        NativeBattleEffectMemory latest = live.Memory with { ScriptedGraphics = live.Memory.ScriptedGraphics! with { Dqa = -7 } };
        NativeBattleScriptedCallbackServices services = Services() with
        {
            DrawSlots = (current, _) =>
            {
                Assert.Equal((byte)9, current.ReadTemporaryByte(0x145));
                Assert.Throws<NotSupportedException>(() => current.ReadTemporaryByte(0x120));
                return current.WriteTemporaryByte(0x145, 6);
            }
        };
        NativeBattleIfrit201Frame first = NativeBattleIfrit201Controller.AdvanceB25DF0(live, Input(1), services, 0x1D9A000, () => latest);
        NativeBattleIfrit201Frame next = NativeBattleIfrit201Controller.AdvanceB25DF0(restored, Input(1), services, 0x1D9A000, () => restored.Memory with { ScriptedGraphics = restored.Memory.ScriptedGraphics! with { Dqa = -7 } });
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(next));
        Assert.Equal((byte)6, first.Memory.Ifrit201!.Filter145);
        Assert.Equal((short)-7, first.Memory.ScriptedGraphics!.Dqa);
        Assert.Equal(Input(1).ViewMatrix.SetItem(18, 0).SetItem(19, 0), first.Memory.ScriptedGraphics.ModuleViewBackup);
    }

    [Fact]
    public void Completion_uses_live_command_and_released_task_never_reenters()
    {
        NativeBattleIfrit201Frame frame = ControllerFrame();
        NativeBattleScriptedCallbackServices services = Services() with { DrawSlots = (current, _) => WaterSchedulerFixture.CoreWord(current, 0x2797628 + 0xA, 0) };
        NativeBattleIfrit201Frame completed = NativeBattleIfrit201Controller.AdvanceB25DF0(frame, Input(1), services, 0x1D9A000, () => frame.Memory);
        Assert.Equal(0u, completed.Effect.ReturnedDescriptor);
        Assert.Null(completed.Effect.Pools[0].Head);
        NativeBattleIfrit201Frame again = NativeBattleIfrit201Controller.AdvanceB25DF0(completed, Input(1), Services(), 0x1D9A000, () => throw new InvalidOperationException());
        Assert.Equal(JsonSerializer.Serialize(completed), JsonSerializer.Serialize(again));
    }

    [Fact]
    public void Missing_retained_filter_and_replaced_callback_fail_without_reconstruction()
    {
        NativeBattleIfrit201Frame frame = ControllerFrame();
        NativeBattleIfrit201Frame missing = frame with { Memory = frame.Memory with { Ifrit201 = null } };
        Assert.Throws<NotSupportedException>(() => NativeBattleIfrit201Controller.AdvanceB25DF0(missing, Input(1), Services(), 0x1D9A000, () => missing.Memory));
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[0];
        pool = pool with { Slots = pool.Slots.SetItem(0, pool.Slots[0] with { Callback = 1 }) };
        NativeBattleIfrit201Frame replaced = frame with { Effect = frame.Effect with { Pools = [pool] } };
        Assert.Throws<NotSupportedException>(() => NativeBattleIfrit201Controller.AdvanceB25DF0(replaced, Input(1), Services(), 0x1D9A000, () => replaced.Memory));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeBattleIfrit201State>("{}"));
    }

    private static NativeBattleIfrit201Frame ControllerFrame()
    {
        NativeBattleWaterExecutionFrame execution = Create();
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { ScriptedEffects = execution.Core, Arena = execution.Arena, Ifrit201 = new(9), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess(), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() with { PacketBank = "off_B6D070" } };
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2796E18, 0x2796E28, 0x10, 1);
        (pool, _) = pool.Append(0xB25DF0);
        NativeBattleOriginalEffectState effect = new(201, 200, new(201, 0xB25780, 0xB25750), new(0, 0, 0x14, 0, 0, 0), null) { Pools = [pool], ReturnedDescriptor = pool.DescriptorAddress };
        return new(memory, effect, execution.Random);
    }

    private static NativeBattleWaterExecutionFrame Create()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.CoreWord(frame, 0x2797628 + 0xA, 0x8000);
        frame = WaterSchedulerFixture.CoreWord(frame, 0x2797068 + 0x38, 8);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2798A6C, NativeBattleEffectNodeMemory.ArenaAddress + 0x1FA8);
        return WaterSchedulerFixture.CoreDword(frame, Manager + 0xD4, 0x400000);
    }

    private static NativeBattleScriptedCallbackInput Input(ushort flags)
    {
        byte[] owners = new byte[20];
        uint[] addresses = [Manager, 0x803FF000, 0x2797068, 0x27975D8, 0x2797628];
        for (int index = 0; index < addresses.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(owners.AsSpan(index * 4), addresses[index]);
        }
        NativeBattleScriptedInitializationDirectories directories = new(new("initialization", new byte[28].ToImmutableArray()), new("owners", owners.ToImmutableArray()));
        return new(directories, 0x2000, flags, Enumerable.Range(0, 32).Select(value => (byte)value).ToImmutableArray(), 0x600000, 0, 216);
    }

    private static NativeBattleScriptedCallbackServices Services() => new((_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (current, _) => current, (current, _) => current, (_, _, _) => throw new InvalidOperationException(), (current, _) => current);
}
