using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleLogicalDynamicPassTests
{
    [Fact]
    public void Software_delivery_precedes_normal_filtering_and_state_replay_uses_live_mode_in_each_pass()
    {
        Context context = new() { LiveMode = 1 };
        context.Software = command => SetMode(command, context);
        context.Normal = (command, _) => SetMode(command, context);
        var result = NativeBattleLogicalPasses.Execute(Ordering(0xE3, 0x20, 0xE4, 0x20, 0xE5, 0x20), Tables(), new(null), context);
        Assert.Equal(new[] { "N0:E3", "S:20", "S:E4", "N0:E4", "S:20", "N0:20", "S:E5", "N0:E5", "N0:20", "N1:E3", "N1:E4", "N1:20", "N1:E5", "N1:20", "N2:E3", "N2:E4", "N2:20", "N2:E5", "N2:20", "END" }, context.Log.ToArray());
        Assert.Equal(15, result.Commands.Length);
        Assert.Equal(4, context.Log.Count(item => item.StartsWith("S:", StringComparison.Ordinal)));
        Assert.Equal(1u, context.LiveMode);
        Assert.Equal(1, context.Ends);
    }

    [Fact]
    public void Hardware_disabled_keeps_pass0_software_and_state_but_skips_both_later_traversals()
    {
        Context context = new() { LiveMode = 3, HardwareEnabled = false };
        var result = NativeBattleLogicalPasses.Execute(Ordering(0xE4, 0x20), Tables(), new(null), context);
        Assert.Equal(new[] { "S:E4", "N0:E4", "S:20", "END" }, context.Log.ToArray());
        NativeBattleLogicalResolvedCommand state = Assert.Single(result.Commands);
        Assert.Equal((byte)0xE4, state.Command.Opcode);
        Assert.Equal((byte)0, state.Pass);
    }

    [Fact]
    public void Hardware_is_not_rechecked_inside_the_two_enabled_later_passes()
    {
        Context context = new() { LiveMode = 1 };
        context.Normal = (_, pass) =>
        {
            if (pass == 1)
            {
                context.HardwareEnabled = false;
            }
        };
        var result = NativeBattleLogicalPasses.Execute(Ordering(0xE3, 0x20), Tables(), new(null), context);
        Assert.Equal(new byte[] { 0, 0, 1, 1, 2, 2 }, result.Commands.Select(command => command.Pass).ToArray());
        Assert.DoesNotContain(context.Log, entry => entry.StartsWith("S:", StringComparison.Ordinal));
        Assert.Equal(1, context.Ends);
    }

    [Fact]
    public void Software_only_textured_polygon_updates_shared_page_without_normal_texture_preprocessing()
    {
        NativeBattlePcTextureWindowCache original = Cache();
        Context context = new() { LiveMode = 2, Cache = original };
        context.Software = command => context.SharedPage = unchecked((ushort)(command.Words[4] >> 16));
        var result = NativeBattleLogicalPasses.Execute(Ordering(0x24), Tables(), new(0x20), context);
        Assert.Empty(result.Commands);
        Assert.Equal((ushort)0x60, result.Final.TexturePage);
        Assert.Equal(new[] { "S:24", "END" }, context.Log.ToArray());
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(result.Cache));
    }

    [Fact]
    public void E4_invalidation_observes_prior_preprocessing_inline_and_later_pass_preparation_is_not_posthoc()
    {
        Context context = new() { LiveMode = 1, Cache = Cache() };
        List<uint> used = [];
        List<bool> prepared = [];
        context.Normal = (command, pass) =>
        {
            if (command.Opcode == 0xE4)
            {
                used.Add(context.Cache!.Rows[0].UsedMask);
                context.Cache = context.Cache.Invalidate464850(0, 0, 63, 255);
            }
            if (command.Opcode == 0x24 && pass == 1)
            {
                prepared.Add(context.Cache!.Rows[0].Regions[0].CurrentPixels);
            }
        };
        NativeBattlePrimitiveTables tables = Tables() with { SecondHandlers = Tables().SecondHandlers.SetItem(0x24, 0) };
        NativeBattleLogicalPasses.Execute(Ordering(0x24, 0xE4), tables, new(0) { TextureWindowBits = 0 }, context);
        Assert.Equal(new uint[] { 1, 1, 0 }, used.ToArray());
        Assert.Equal(new[] { true }, prepared.ToArray());
        Assert.Equal(1, context.Ends);
    }

    [Fact]
    public void Static_wrapper_and_explicit_texture_selection_share_the_same_preparation_and_resolution_algorithm()
    {
        NativeBattleLogicalOrdering ordering = Ordering(0xE1, 0x24, 0xE2, 0x20);
        NativeBattlePrimitiveDrawMode initial = new(0) { TextureWindowBits = 0 };
        var unselected = NativeBattleLogicalPasses.Execute(ordering, Tables(), initial, 1);
        var selected = NativeBattleLogicalPasses.SelectTextures(unselected.Commands, Cache());
        var inline = NativeBattleLogicalPasses.Execute(ordering, Tables(), initial, 1, Cache());
        Assert.Equal(JsonSerializer.Serialize(selected.Commands), JsonSerializer.Serialize(inline.Commands));
        Assert.Equal(JsonSerializer.Serialize(selected.Cache), JsonSerializer.Serialize(inline.Cache));
        Assert.Equal(unselected.Final, inline.Final);
    }

    [Fact]
    public void Proxy_capacity_skip_performs_only_one_end_batch_cleanup()
    {
        Context context = new() { LiveMode = 3 };
        NativeBattleLogicalOrdering ordering = Ordering(0x20);
        using (NativeBattleLogicalOrdering.Builder builder = new(ordering))
        {
            for (int index = 1; index < NativeBattlePrimitiveOrdering.ProxyCapacity; index++)
            {
                builder.Prepend(17, new("off_B6D070", 0, index * 64), [0x01000000, 0xE1000020], 0, []);
            }
            ordering = builder.ToImmutable();
        }
        var result = NativeBattleLogicalPasses.Execute(ordering, Tables(), new(null), context);
        Assert.Empty(result.Commands);
        Assert.Equal(new[] { "END" }, context.Log.ToArray());
    }

    [Fact]
    public void Proxy_metadata_rejects_a_cursor_beyond_its_actual_allocation()
    {
        NativeBattleLogicalOrdering ordering = Ordering(0x20);
        Assert.Throws<InvalidDataException>(() => ordering.UpdateMetadata(16385, ordering.TableBuffer));
        Assert.Equal(1, ordering.ProxyCursor);
    }

    [Fact]
    public void Failed_delivery_does_not_run_successful_batch_cleanup()
    {
        Context context = new() { LiveMode = 2, Software = _ => throw new NotSupportedException("Explicit unsupported software delivery.") };
        Assert.Throws<NotSupportedException>(() => NativeBattleLogicalPasses.Execute(Ordering(0x20), Tables(), new(null), context));
        Assert.Equal(0, context.Ends);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(3, 3)]
    public void Debug_delivers_two_extra_software_calls_after_preprocessing_only(uint liveMode, int deliveries)
    {
        Context context = new() { LiveMode = liveMode, LiveDebug = 1 };
        int writes = 0;
        context.Software = _ => writes++;
        var result = NativeBattleLogicalPasses.Execute(Ordering(0x20), Tables(), new(null), context);
        string[] expected = liveMode == 1 ? ["N0:20", "S:20", "S:20", "N1:20", "N2:20", "END"] : ["S:20", "N0:20", "S:20", "S:20", "N1:20", "N2:20", "END"];
        Assert.Equal(expected, context.Log.ToArray());
        Assert.Equal(deliveries, writes);
        Assert.Equal(3, result.Commands.Length);
        Assert.Equal(1, context.Ends);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void Debug_calls_do_not_depend_on_hardware_or_a_normal_preprocessing_handler(bool hardware, bool normalHandler)
    {
        Context context = new() { LiveMode = 1, LiveDebug = 1, HardwareEnabled = hardware };
        NativeBattlePrimitiveTables tables = Tables();
        if (!normalHandler)
        {
            tables = tables with { PreprocessingHandlers = tables.PreprocessingHandlers.SetItem(0x20, 0) };
        }
        var result = NativeBattleLogicalPasses.Execute(Ordering(0x20), tables, new(null), context);
        Assert.Equal(new[] { "S:20", "S:20" }, context.Log.Take(2).ToArray());
        Assert.DoesNotContain("N0:20", context.Log);
        Assert.Equal(hardware ? 2 : 0, result.Commands.Length);
        Assert.Equal(hardware ? new[] { "S:20", "S:20", "N1:20", "N2:20", "END" } : new[] { "S:20", "S:20", "END" }, context.Log.ToArray());
    }

    [Fact]
    public void Debug_is_read_again_after_the_first_extra_software_call()
    {
        Context context = new() { LiveMode = 1, LiveDebug = 1 };
        context.Software = _ => context.LiveDebug = 0;
        NativeBattleLogicalPasses.Execute(Ordering(0x20), Tables(), new(null), context);
        Assert.Equal(new[] { "N0:20", "S:20", "N1:20", "N2:20", "END" }, context.Log.ToArray());
    }

    [Fact]
    public void Debug_is_read_after_normal_preprocessing_without_retesting_its_prior_mode_gate()
    {
        Context context = new() { LiveMode = 1, LiveDebug = 0 };
        context.Normal = (_, pass) =>
        {
            if (pass == 0)
            {
                context.LiveDebug = 1;
                context.LiveMode = 2;
            }
        };
        NativeBattleLogicalPasses.Execute(Ordering(0x20), Tables(), new(null), context);
        Assert.Equal(new[] { "N0:20", "S:20", "S:20", "END" }, context.Log.ToArray());
    }

    [Fact]
    public void Initial_software_delivery_can_disable_the_mode_gate_before_debug_delivery()
    {
        Context context = new() { LiveMode = 3, LiveDebug = 1 };
        context.Software = _ => context.LiveMode = 2;
        NativeBattleLogicalPasses.Execute(Ordering(0x20), Tables(), new(null), context);
        Assert.Equal(new[] { "S:20", "END" }, context.Log.ToArray());
    }

    [Fact]
    public void Debug_does_not_add_deliveries_for_state_commands()
    {
        Context context = new() { LiveMode = 1, LiveDebug = 1 };
        NativeBattleLogicalPasses.Execute(Ordering(0xE3), Tables(), new(null), context);
        Assert.Equal(new[] { "N0:E3", "N1:E3", "N2:E3", "END" }, context.Log.ToArray());
    }

    [Fact]
    public void Debug_preserves_a_null_software_handler()
    {
        Context context = new() { LiveMode = 1, LiveDebug = 1, SoftwareTable = new(new uint[256].ToImmutableArray()) };
        NativeBattleLogicalPasses.Execute(Ordering(0x20), Tables(), new(null), context);
        Assert.Equal(new[] { "N0:20", "N1:20", "N2:20", "END" }, context.Log.ToArray());
    }

    private static void SetMode(NativeBattleLogicalCommand command, Context context)
    {
        if (command.Opcode is 0xE3 or 0xE4 or 0xE5)
        {
            context.LiveMode = command.Opcode == 0xE3 ? 2u : command.Opcode == 0xE4 ? 3u : 1u;
        }
    }

    private static NativeBattleLogicalOrdering Ordering(params byte[] opcodes)
    {
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        for (int index = opcodes.Length - 1; index >= 0; index--)
        {
            byte opcode = opcodes[index];
            ImmutableArray<uint> words = opcode switch
            {
                0x20 => [0x04000000, 0x20808080, 0, 1, 2],
                0x24 => [0x07000000, 0x24808080, 0, 0, 80, 0x00600000, 80u << 16, 0],
                0xE1 => [0x01000000, 0xE1000020],
                _ => [0x01000000, (uint)opcode << 24]
            };
            ordering = ordering.Prepend(17, new("off_B6D070", 0, index * 64), words, 0, [100, 100, 100, 0]);
        }
        return ordering;
    }

    private static NativeBattlePrimitiveTables Tables()
    {
        byte[] lengths = new byte[256];
        byte[] flagged = new byte[256];
        uint[] handlers = new uint[256];
        foreach (byte opcode in new byte[] { 0x20, 0x24, 0xE1, 0xE2, 0xE3, 0xE4, 0xE5 })
        {
            lengths[opcode] = opcode == 0x20 ? (byte)4 : opcode == 0x24 ? (byte)7 : (byte)1;
            flagged[opcode] = opcode is 0x20 or 0x24 ? (byte)1 : (byte)0;
            handlers[opcode] = 1;
        }
        return new(lengths.ToImmutableArray(), flagged.ToImmutableArray(), handlers.ToImmutableArray(), handlers.ToImmutableArray(), handlers.ToImmutableArray());
    }

    private static NativeBattlePcTextureWindowCache Cache() => NativeBattlePcTextureWindowCache.Reset4647A0(null) with { ScaleU = 1, ScaleV = 1 };

    private sealed class Context : INativeBattleLogicalExecutionContext
    {
        public uint LiveMode { get; set; }
        public uint LiveDebug { get; set; }
        public bool HardwareEnabled { get; set; } = true;
        public NativeSoftwarePrimitiveTable SoftwareTable { get; init; } = new(Tables().PreprocessingHandlers);
        public NativeBattlePcTextureWindowCache? Cache { get; set; }
        public List<string> Log { get; } = [];
        public Action<NativeBattleLogicalCommand>? Software { get; set; }
        public Action<NativeBattleLogicalCommand, byte>? Normal { get; set; }
        public ushort? SharedPage { get; set; }
        public int Ends { get; private set; }

        public void ExecuteSoftware(NativeBattleLogicalCommand command)
        {
            Log.Add($"S:{command.Opcode:X2}");
            Software?.Invoke(command);
        }
        public void ExecuteNormal(NativeBattleLogicalCommand command, NativeBattlePrimitiveDrawMode mode, byte pass)
        {
            Log.Add($"N{pass}:{command.Opcode:X2}");
            SharedPage = mode.TexturePage;
            Normal?.Invoke(command, pass);
        }
        public NativeBattlePrimitiveDrawMode SynchronizeDrawMode(NativeBattlePrimitiveDrawMode mode) => SharedPage is ushort page ? mode with { TexturePage = page } : mode;
        public void EndBatch()
        {
            Ends++;
            Log.Add("END");
        }
    }
}
