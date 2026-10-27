using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWater222ControllerTests
{
    private const uint Manager = 0x2797690;
    private const uint TemporaryAddress = 0x1A77238;

    [Fact]
    public void Initialization_creates_actual_single_pool_and_publishes_only_the_initialized_filter()
    {
        var fixture = Fixture();
        NativeBattleWater222Frame initialized = NativeBattleWater222Controller.InitializeA9B200(fixture.Frame, fixture.Resources, fixture.Inputs);
        NativeBattleEffectTaskPool pool = Assert.Single(initialized.Effect.Pools);
        Assert.Equal(0x2796BA8u, initialized.Effect.ReturnedDescriptor);
        Assert.Equal(0x2796BB8u, pool.BankAddress);
        Assert.Equal((ushort)0x10, pool.Stride);
        Assert.Equal(0, pool.Head);
        Assert.Equal(0xA9B870u, Assert.Single(pool.Slots).Callback);
        Assert.Equal((byte)0, initialized.Memory.Water222!.Filter145);
        Assert.Equal(fixture.Frame.Random, initialized.Random);
        using JsonDocument document = JsonDocument.Parse(NativeBattleTestSnapshots.Json(initialized.Memory.Water222));
        Assert.Equal("Filter145", Assert.Single(document.RootElement.EnumerateObject()).Name);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeBattleWater222State>("{}"));
    }

    [Fact]
    public void Transient_factory_does_not_seed_any_unwritten_byte_or_serialize_a_callback_frame()
    {
        NativeBattleWaterTemporaryFrame temporary = NativeBattleWater222Controller.CreateTemporaryFrame(TemporaryAddress);
        Assert.Equal(TemporaryAddress, temporary.Address);
        Assert.Equal(0x180, temporary.Length);
        Assert.All(Enumerable.Range(0, temporary.Length), offset => Assert.False(temporary.IsKnown(offset, 1)));
        Assert.Throws<InvalidDataException>(() => NativeBattleWater222Controller.CreateTemporaryFrame(0));
        Assert.Throws<InvalidDataException>(() => NativeBattleWater222Controller.CreateTemporaryFrame(uint.MaxValue));
    }

    [Fact]
    public void Callback_merges_into_latest_external_memory_and_publishes_current_filter_view_and_common_cursor()
    {
        var fixture = Initialized();
        NativeBattleWater222Frame frame = fixture.Frame;
        NativeBattleEffectMemory latest = frame.Memory;
        int draws = 0;
        NativeBattleWaterCallbackServices services = Services() with
        {
            DrawSlot = (execution, kind) =>
            {
                draws++;
                Assert.Equal((byte)6, kind);
                Assert.Equal((byte)9, execution.ReadTemporaryByte(0x145));
                Assert.Throws<NotSupportedException>(() => execution.ReadTemporaryByte(0x120));
                latest = latest with
                {
                    Textures = latest.Textures!.Upload45BD30(1, 2, 1, 1, [0x34, 0x12]),
                    ScriptedGraphics = latest.ScriptedGraphics! with { Dqa = -7, Dqb = 99, Depths = latest.ScriptedGraphics!.Depths.Write(4, [1, 2, 3, 4]) }
                };
                execution = WaterSchedulerFixture.CoreDword(execution, Manager + 0x7C, 4);
                return execution.WriteTemporaryByte(0x145, 6);
            }
        };
        NativeBattleWater222Frame result = NativeBattleWater222Controller.AdvanceA9B870(frame, Input(fixture.Resources), services, TemporaryAddress, () => latest);
        Assert.Equal(1, draws);
        Assert.Equal((byte)6, result.Memory.Water222!.Filter145);
        Assert.Equal((short)-7, result.Memory.ScriptedGraphics!.Dqa);
        Assert.Equal(99, result.Memory.ScriptedGraphics.Dqb);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, result.Memory.ScriptedGraphics.Depths.Read(4, 4));
        Assert.Same(latest.Textures, result.Memory.Textures);
        Assert.Equal(4, result.Memory.PrimitiveProcess!.ByteCursor);
        Assert.Equal(TemporaryAddress, Dword(result.Memory.ScriptedEffects!, 0x2797624));
        byte[] expectedView = Input(fixture.Resources).ViewMatrix.ToArray();
        expectedView[18] = 0;
        expectedView[19] = 0;
        Assert.Equal(expectedView, result.Memory.ScriptedGraphics.ModuleViewBackup.ToArray());
        Assert.Equal(frame.Random.Draw(out _), result.Random);
        Assert.Equal(0, result.Effect.Pools[0].Head);
    }

    [Fact]
    public void Real_callback_completion_releases_once_and_empty_pool_does_not_run_services_or_random_again()
    {
        var fixture = Initialized();
        NativeBattleWater222Frame frame = fixture.Frame;
        NativeBattleWaterCallbackServices services = Services() with { DrawSlot = (execution, kind) => WaterSchedulerFixture.CoreWord(execution, 0x2797628 + 0xA, 0) };
        NativeBattleWater222Frame completed = NativeBattleWater222Controller.AdvanceA9B870(frame, Input(fixture.Resources), services, TemporaryAddress, () => frame.Memory);
        Assert.Equal(0u, completed.Effect.ReturnedDescriptor);
        Assert.Null(completed.Effect.Pools[0].Head);
        Assert.Null(completed.Effect.Pools[0].Tail);
        Assert.Equal((ushort)0, completed.Effect.Pools[0].Slots[0].Flags);
        NativeBattleWater222Frame again = NativeBattleWater222Controller.AdvanceA9B870(completed, Input(fixture.Resources), Services(), TemporaryAddress, UnexpectedMemory);
        Assert.Equal(completed.Random, again.Random);
        Assert.Equal(NativeBattleTestSnapshots.Json(completed), NativeBattleTestSnapshots.Json(again));
    }

    [Fact]
    public void Missing_warm_filter_or_actual_graphics_owner_is_not_reconstructed()
    {
        var fixture = Initialized();
        NativeBattleWater222Frame missing = fixture.Frame with { Memory = fixture.Frame.Memory with { Water222 = null } };
        NativeBattleWater222Frame imported = NativeBattleTestSnapshots.RestoreJson<NativeBattleWater222Frame>(NativeBattleTestSnapshots.Json(missing));
        Assert.Null(imported.Memory.Water222);
        Assert.Throws<NotSupportedException>(() => NativeBattleWater222Controller.AdvanceA9B870(imported, Input(fixture.Resources), Services(), TemporaryAddress, () => imported.Memory));
        NativeBattleWater222Frame graphicsMissing = fixture.Frame with { Memory = fixture.Frame.Memory with { ScriptedGraphics = null } };
        Assert.Throws<NotSupportedException>(() => NativeBattleWater222Controller.AdvanceA9B870(graphicsMissing, Input(fixture.Resources), Services(), TemporaryAddress, () => graphicsMissing.Memory));
    }

    [Fact]
    public void Independent_JSON_next_and_real_reentry_keep_only_defined_state_transitions()
    {
        var fixture = Initialized();
        NativeBattleWater222Frame live = fixture.Frame;
        NativeBattleWater222Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleWater222Frame>(NativeBattleTestSnapshots.Json(live));
        NativeBattleWater222Frame first = NativeBattleWater222Controller.AdvanceA9B870(live, Input(fixture.Resources), Services(), TemporaryAddress, () => live.Memory);
        NativeBattleWater222Frame coldNext = NativeBattleWater222Controller.AdvanceA9B870(restored, Input(fixture.Resources), Services(), TemporaryAddress, () => restored.Memory);
        Assert.Equal(NativeBattleTestSnapshots.Json(first), NativeBattleTestSnapshots.Json(coldNext));
        Assert.Equal((byte)9, first.Memory.Water222!.Filter145);
        var original = Fixture();
        NativeBattleWater222Frame reentered = NativeBattleWater222Controller.InitializeA9B200(first, original.Resources, original.Inputs);
        Assert.Equal((byte)0, reentered.Memory.Water222!.Filter145);
    }

    [Fact]
    public void Unsupported_callback_replacement_does_not_fall_back_to_root_execution()
    {
        var fixture = Initialized();
        NativeBattleEffectTaskPool pool = fixture.Frame.Effect.Pools[0];
        NativeBattleModulePoolTask task = pool.Slots[0];
        task.UpdateHeader(task.Flags, task.State, task.Next, 0x1234);
        NativeBattleWater222Frame changed = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = [pool] } };
        Assert.Throws<NotSupportedException>(() => NativeBattleWater222Controller.AdvanceA9B870(changed, Input(fixture.Resources), Services(), TemporaryAddress, () => changed.Memory));
    }

    private static (NativeBattleWater222Frame Frame, NativeBattleWater222Resources Resources) Initialized()
    {
        var fixture = Fixture();
        NativeBattleWater222Frame frame = NativeBattleWater222Controller.InitializeA9B200(fixture.Frame, fixture.Resources, fixture.Inputs);
        NativeBattleScriptedEffectMemory core = frame.Memory.ScriptedEffects!.Write(0x2797554, [0, 0xFF]);
        NativeBattleEffectArena arena = frame.Memory.Arena!.Write(fixture.Inputs.Resource1.ArenaOffset + 0x58 + 0x1C, [6]);
        return (frame with { Memory = frame.Memory with { ScriptedEffects = core, Arena = arena, Water222 = new(9) } }, fixture.Resources);
    }

    private static (NativeBattleWater222Frame Frame, NativeBattleWater222Resources Resources, NativeBattleWaterInitializationInputs Inputs) Fixture()
    {
        var fixture = NativeBattleWaterInitializationTests.Fixture();
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(221);
        memory = memory with { Arena = fixture.Arena, ScriptedEffects = NativeBattleScriptedEffectMemory.FromColdProcess(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess(), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() with { PacketBank = "off_B6D070" } };
        NativeBattleOriginalEffectState effect = new(222, 221, new(222, 0xA9B200, 0xA9B1D0), new(0, 0, 1, 0, 0, 0), null);
        NativeBattleWater222Resources resources = new([new("file0", new byte[0x1799C].ToImmutableArray()), new("file1", new byte[0x26258].ToImmutableArray())], fixture.Profile);
        return (new(memory, effect, new(1)), resources, fixture.Inputs);
    }

    private static NativeBattleWaterCallbackInput Input(NativeBattleWater222Resources resources) => new(resources, 1, Enumerable.Repeat((byte)0xA5, 32).ToImmutableArray(), 0x1D8E058, 0, 216);
    private static NativeBattleWaterCallbackServices Services() => new(UnexpectedRead, UnexpectedInstruction, UnexpectedUpdate, KeepDraw, UnexpectedClear, KeepFocal);
    private static ushort UnexpectedRead(NativeBattleWaterExecutionFrame frame, uint pc) => throw new InvalidOperationException("Paused callback fetched an instruction.");
    private static NativeBattleWaterExecutionFrame UnexpectedInstruction(NativeBattleWaterExecutionFrame frame, ushort opcode) => throw new InvalidOperationException("Paused callback dispatched an instruction.");
    private static NativeBattleWaterExecutionFrame UnexpectedUpdate(NativeBattleWaterExecutionFrame frame, byte kind) => throw new InvalidOperationException("Paused callback advanced physics.");
    private static NativeBattleWaterExecutionFrame UnexpectedClear(NativeBattleWaterExecutionFrame frame, uint pointer, int length) => throw new InvalidOperationException("Manager8000 was not set.");
    private static NativeBattleWaterExecutionFrame KeepDraw(NativeBattleWaterExecutionFrame frame, byte kind) => frame;
    private static NativeBattleWaterExecutionFrame KeepFocal(NativeBattleWaterExecutionFrame frame, short focal) => frame;
    private static NativeBattleEffectMemory UnexpectedMemory() => throw new InvalidOperationException("Released callback consulted external memory.");
    private static uint Dword(NativeBattleScriptedEffectMemory core, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(core.Read(address, 4));
}
