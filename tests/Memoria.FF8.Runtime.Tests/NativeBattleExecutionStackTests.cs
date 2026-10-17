using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleExecutionStackTests
{
    private const string CaptureHash = "B6ADB6030051CE54CBA41C8098904FC6D73A430EA7322A93DBC17F55976E8B41";

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Captured_common_callsite_resolves_all_proved_outer_variants_and_the_exact_physical_pointer_value(byte entryLifecycle)
    {
        NativeBattleExecutionStack profile = Profile();
        NativeBattleStackFrame entry = NativeBattleQuezacotlRibbon.ResolveEntryStack6C8850(profile, entryLifecycle);
        Assert.Equal(0x1A5058u, entry.EntryEsp);
        Assert.Equal(0x1A5020u, entry.AddressAtOffset(-0x38));
        Assert.Equal(0x1A4FECu, entry.AddressAtOffset(-0x6C));
        Assert.Equal((short)20512, unchecked((short)entry.AddressAtOffset(-0x38)));
        Assert.Equal((short)26, unchecked((short)(entry.AddressAtOffset(-0x38) >> 16)));
    }

    [Fact]
    public void Cdecl_keeps_saved_registers_and_pending_arguments_distinct_at_every_actual_callsite()
    {
        NativeBattleStackFrame battleFrame = Profile().ReadCommonFrame();
        Assert.Equal(0x1A50DCu, battleFrame.EntryEsp);
        NativeBattleStackFrame outer = battleFrame.EnterCdecl(0x1C, 2, 0);
        Assert.Equal(0x1A50B4u, outer.EntryEsp);
        NativeBattleStackFrame graphics = outer.EnterCdecl(0, 2, 0);
        Assert.Equal(0x1A50A8u, graphics.EntryEsp);
        NativeBattleStackFrame mainPool = graphics.EnterCdecl(0, 0, 4);
        Assert.Equal(0x1A50A0u, mainPool.EntryEsp);
        NativeBattleStackFrame main = mainPool.EnterCdecl(0, 4, 4);
        Assert.Equal(0x1A5088u, main.EntryEsp);
        NativeBattleStackFrame ribbonPool = main.EnterCdecl(0, 2, 12);
        Assert.Equal(0x1A5070u, ribbonPool.EntryEsp);
        Assert.Equal(0x1A5058u, ribbonPool.EnterCdecl(0, 4, 4).EntryEsp);
        //Phase1's additional live PUSH EDI changes the origin; it cannot silently use the active-phase rule.
        Assert.Equal(0x1A50A4u, outer.EnterCdecl(0, 3, 0).EntryEsp);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(255)]
    public void Unproved_outer_scope_stays_unsupported(byte entryLifecycle)
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleQuezacotlRibbon.ResolveEntryStack6C8850(Profile(), entryLifecycle));
    }

    [Theory]
    [InlineData(-4, 0, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(0, 0, -4)]
    [InlineData(2, 0, 0)]
    [InlineData(0, 0, 2)]
    public void Cdecl_rejects_negative_or_unaligned_operands(int locals, int registers, int pendingArguments)
    {
        Assert.Throws<InvalidDataException>(() => new NativeBattleStackFrame(0x1000).EnterCdecl(locals, registers, pendingArguments));
    }

    [Fact]
    public void Physical_address_arithmetic_refuses_underflow_and_overflow_instead_of_wrapping()
    {
        Assert.Throws<OverflowException>(() => new NativeBattleStackFrame(0).EnterCdecl(0, 0, 0));
        Assert.Throws<OverflowException>(() => new NativeBattleStackFrame(16).EnterCdecl(0, int.MaxValue, 0));
        Assert.Throws<OverflowException>(() => new NativeBattleStackFrame(0).AddressAtOffset(-1));
        Assert.Throws<OverflowException>(() => new NativeBattleStackFrame(uint.MaxValue).AddressAtOffset(1));
    }

    [Fact]
    public void Profile_requires_real_callsite_alignment_executable_capture_and_native_lineage()
    {
        NativeBattleExecutionStack valid = Profile();
        NativeBattleExecutionStack[] invalid = [
            valid with { FrameCallbackEsp = 0xFC },
            valid with { FrameCallbackEsp = valid.FrameCallbackEsp + 1 },
            valid with { FrameReturnAddress = 0x569C3C },
            valid with { ExecutableHash = new string('0', 64) },
            valid with { CaptureSha256 = "missing" },
            valid with { CaptureSha256 = new string('G', 64) },
            valid with { CapturedProcessId = 0 },
            valid with { CapturedThreadId = 0 }
        ];
        foreach (NativeBattleExecutionStack profile in invalid)
        {
            Assert.Throws<InvalidDataException>(profile.Validate);
            Assert.Throws<InvalidDataException>(() => profile.ReadCommonFrame());
        }
    }

    [Fact]
    public void Selected_profile_survives_json_but_cold_or_missing_old_memory_never_gains_one()
    {
        NativeBattleExecutionStack profile = Profile();
        NativeBattleExecutionStack independent = JsonSerializer.Deserialize<NativeBattleExecutionStack>(JsonSerializer.Serialize(profile))!;
        independent.Validate();
        Assert.Equal(profile, independent);
        Assert.Equal(NativeBattleQuezacotlRibbon.ResolveEntryStack6C8850(profile, 4), NativeBattleQuezacotlRibbon.ResolveEntryStack6C8850(independent, 4));
        NativeBattleEffectMemory cold = NativeBattleEffectMemory.FromColdProcess();
        Assert.Null(cold.ExecutionStack);
        JsonObject oldMemory = JsonNode.Parse(JsonSerializer.Serialize(cold))!.AsObject();
        Assert.False(oldMemory.ContainsKey("ExecutionStack"));
        Assert.Null(JsonSerializer.Deserialize<NativeBattleEffectMemory>(oldMemory.ToJsonString())!.ExecutionStack);
        NativeBattleEffectMemory selected = cold with { ExecutionStack = profile };
        Assert.Equal(profile, JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(selected))!.ExecutionStack);
        JsonObject missingLineage = JsonNode.Parse(JsonSerializer.Serialize(profile))!.AsObject();
        missingLineage.Remove("CapturedThreadId");
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeBattleExecutionStack>(missingLineage.ToJsonString()));
    }

    private static NativeBattleExecutionStack Profile()
    {
        //Explicit external capture selection, not a cold-loader/default owner or a historical reconstruction of another save.
        return new(0x1A50DC, 0x569C3D, NativeFieldRuntimeProfile.FF8EnExecutableHash, CaptureHash, 5592, 8028);
    }
}
