using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class DiagnosticEffectPreviewOwnersTests
{
    [Fact]
    public void Other_modules_have_no_cinematic_or_director_specific_fields()
    {
        BattleSession battle = Create(2, NativeBattleEffectMemory.FromColdProcess());
        JsonElement json = JsonSerializer.SerializeToElement(battle.ObserveDiagnosticEffectPreviewOwners());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("ScriptedEffect").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("Effect101").ValueKind);
    }

    [Theory]
    [InlineData(77)]
    [InlineData(211)]
    public void Cinematic_observation_preserves_unknown_PCs_and_detaches_known_scalar_words(ushort effect)
    {
        const uint manager = NativeBattleScriptedEffectMemory.Address;
        const uint node = NativeBattleEffectNodeMemory.ArenaAddress + 0x1000;
        NativeBattleScriptedEffectMemory core = NativeBattleScriptedEffectMemory.Unknown().Write(0x27973EC, BitConverter.GetBytes(manager)).Write(0x27973E8, BitConverter.GetBytes(node)).Write(0x2797450, BitConverter.GetBytes(0x020DFC80u)).Write(manager + 0xD0, new byte[] { 0, 1 }).Write(manager + 0x35, new byte[] { 255 });
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(0x1000, 12).Write(0x1000, BitConverter.GetBytes(0x020DFC82u));
        BattleSession battle = Create(effect, NativeBattleEffectMemory.FromColdProcess() with { ScriptedEffects = core, Arena = arena }, graphicsFlags: 1);
        object observation = battle.ObserveDiagnosticEffectPreviewOwners();
        JsonElement json = JsonSerializer.SerializeToElement(observation);
        JsonElement module = json.GetProperty("ScriptedEffect");
        Assert.Equal(0x020DFC80u, module.GetProperty("GlobalPc").GetUInt32());
        Assert.Equal(0x020DFC82u, module.GetProperty("NodePc0").GetUInt32());
        Assert.Equal(JsonValueKind.Null, module.GetProperty("NodePc1").ValueKind);
        Assert.Equal(JsonValueKind.Null, module.GetProperty("Bank").ValueKind);
        Assert.Equal(0, module.GetProperty("HeaderIndex").GetInt32());
        Assert.Equal(1, module.GetProperty("HeaderCount").GetInt32());
        Assert.Equal(255, module.GetProperty("Pause35").GetInt32());
        Assert.True(json.GetProperty("ScriptPassPaused").GetBoolean());
        Assert.Equal(JsonValueKind.Null, module.GetProperty("CurrentNodeBreakup").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("Effect101").ValueKind);
        string before = JsonSerializer.Serialize(observation);
        core.Write(0x2797450, BitConverter.GetBytes(1u));
        arena.Write(0x1000, BitConverter.GetBytes(2u));
        Assert.Equal(before, JsonSerializer.Serialize(observation));
        Assert.True(before.Length < 32768);
        Assert.DoesNotContain("Payload", before);
        Assert.DoesNotContain("ScriptedEffects", before);
    }

    [Theory]
    [InlineData(2, 3, true)]
    [InlineData(7, 3, true)]
    [InlineData(7, 3, false)]
    [InlineData(7, 513, true)]
    public void Effect211_breakup_observes_only_proved_current_draw_node_flags_with_finite_pool_budget(ushort mode, ushort capacity, bool knownFlags)
    {
        const uint basis = NativeBattleEffectNodeMemory.ArenaAddress;
        NativeBattleScriptedEffectMemory core = NativeBattleScriptedEffectMemory.Unknown().Write(0x27973E8, BitConverter.GetBytes(basis + 0x1000));
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0x101C, new byte[] { 22 }).Write(0x10B8, BitConverter.GetBytes(basis + 0x2000)).Write(0x10C0, BitConverter.GetBytes(basis + 0x3000)).Write(0x10A0, BitConverter.GetBytes(basis + 0x4000)).Write(0x2008, BitConverter.GetBytes(mode)).Write(0x202E, BitConverter.GetBytes(capacity)).Write(0x301C, BitConverter.GetBytes((ushort)7)).Write(0x4018, new byte[] { 1 }).Write(0x4050, new byte[] { 1 });
        if (!knownFlags)
        {
            arena = arena.MarkUnknown(0x4050, 1);
        }
        BattleSession battle = Create(211, NativeBattleEffectMemory.FromColdProcess() with { ScriptedEffects = core, Arena = arena });
        object observation = battle.ObserveDiagnosticEffectPreviewOwners();
        JsonElement json = JsonSerializer.SerializeToElement(observation);
        JsonElement breakup = json.GetProperty("ScriptedEffect").GetProperty("CurrentNodeBreakup");
        Assert.Equal(mode, breakup.GetProperty("Mode").GetUInt16());
        Assert.Equal(capacity, breakup.GetProperty("Capacity").GetUInt16());
        Assert.Equal(7, breakup.GetProperty("EmissionCounter").GetInt32());
        if (capacity <= 512 && knownFlags)
        {
            Assert.Equal(2, breakup.GetProperty("Active").GetInt32());
        }
        else
        {
            Assert.Equal(JsonValueKind.Null, breakup.GetProperty("Active").ValueKind);
        }
        string before = JsonSerializer.Serialize(observation);
        arena.Write(0x4018, new byte[] { 0 });
        Assert.Equal(before, JsonSerializer.Serialize(observation));
        Assert.True(before.Length < 32768);
    }

    [Fact]
    public void Director_observation_reports_actual_root_main_ages_without_dumping_banks()
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(NativeBattleEffect101Profile.RootDescriptor, NativeBattleEffect101Profile.RootBank, 20, 1);
        (root, int rootSlot) = root.Append(NativeBattleEffect101Profile.RootCallback);
        root = root.WriteSlot(rootSlot, root.Slots[rootSlot].WriteWord(12, 55).WriteByte(15, 1));
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(NativeBattleEffect101Profile.MainDescriptor, NativeBattleEffectNodeMemory.ArenaAddress + 0x1000, 0x2D0, 1);
        (main, int mainSlot) = main.Append(NativeBattleEffect101Profile.MainCallback);
        main = main.WriteSlot(mainSlot, main.Slots[mainSlot].WriteWord(12, 54));
        NativeBattleEffect101State state = NativeBattleEffect101State.FromUnknown().WriteDword(NativeBattleEffect101Profile.TargetGlobal, 0x1D972C0).WriteDword(NativeBattleEffect101Profile.WorkingGlobal, main.BankAddress);
        BattleSession battle = Create(101, NativeBattleEffectMemory.FromColdProcess() with { Effect101 = state }, [root, main]);
        object observation = battle.ObserveDiagnosticEffectPreviewOwners();
        JsonElement json = JsonSerializer.SerializeToElement(observation);
        JsonElement director = json.GetProperty("Effect101");
        Assert.Equal(0, director.GetProperty("TargetIndex").GetInt32());
        Assert.Equal(55, director.GetProperty("Pools")[0].GetProperty("Rows")[0].GetProperty("Age").GetInt32());
        Assert.Equal(1, director.GetProperty("Pools")[0].GetProperty("Rows")[0].GetProperty("Started").GetInt32());
        Assert.Equal(54, director.GetProperty("Pools")[1].GetProperty("Rows")[0].GetProperty("Age").GetInt32());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("ScriptedEffect").ValueKind);
        string before = JsonSerializer.Serialize(observation);
        root.WriteSlot(rootSlot, root.Slots[rootSlot].WriteWord(12, 99));
        Assert.Equal(before, JsonSerializer.Serialize(observation));
        Assert.DoesNotContain("Payload", before);
        Assert.True(before.Length < 32768);
    }

    private static BattleSession Create(ushort effect, NativeBattleEffectMemory memory, ImmutableArray<NativeBattleEffectTaskPool> pools = default, uint graphicsFlags = 0)
    {
        BattleSession battle = new(new BattleDefinition { Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.Normal);
        Set(battle, nameof(BattleSession.NativeEffectMemory), memory);
        Set(battle, nameof(BattleSession.NativeCamera), NativeBattleCameraState.Initialize());
        Set(battle, nameof(BattleSession.NativeFileIo), NativeBattleFileIoState.FromColdProcess());
        NativeBattleGraphicsState graphics = new(Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 3)).ToImmutableArray(), Enumerable.Repeat<string?>(null, 7).ToImmutableArray(), graphicsFlags, 0)
        {
            OriginalEffect = new(effect, effect - 1, new(effect, 1, 1), null, 0) { Pools = pools.IsDefault ? [] : pools }
        };
        Set(battle, nameof(BattleSession.NativeGraphics), graphics);
        return battle;
    }

    private static void Set(BattleSession battle, string property, object value) => typeof(BattleSession).GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(battle, value);
}
