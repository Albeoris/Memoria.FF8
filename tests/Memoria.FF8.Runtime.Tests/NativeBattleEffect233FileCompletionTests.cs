using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect233FileCompletionTests
{
    [Fact]
    public void Selected_profile_completion_obeys_the_scheduler_and_clears_only_the_busy_byte()
    {
        const uint callback = 0xA57CA0;
        BattleSession session = Fixture(callback, 32768);
        NativeBattleEffectMemory memory = session.NativeEffectMemory!;
        NativeBattleScriptedEffectMemory before = memory.ScriptedEffects!;
        Set(session, nameof(BattleSession.NativeFileIo), JsonSerializer.Deserialize<NativeBattleFileIoState>(JsonSerializer.Serialize(session.NativeFileIo))!);
        Pump(session);
        Assert.Equal((short?)1, session.NativeFileIo!.ActiveCount);
        Assert.Equal((short?)0, session.NativeFileIo.Rows[0].Countdown);
        Assert.Equal((byte)0xFF, session.NativeEffectMemory!.ScriptedEffects!.Read(0x2798219, 1)[0]);
        Set(session, nameof(BattleSession.NativeFileIo), JsonSerializer.Deserialize<NativeBattleFileIoState>(JsonSerializer.Serialize(session.NativeFileIo))!);
        Pump(session);
        Assert.Equal((short?)0, session.NativeFileIo.ActiveCount);
        Assert.Equal((uint?)0, session.NativeFileIo.Rows[0].SchedulerCallback);
        Assert.Equal((uint?)callback, session.NativeFileIo.Rows[0].CompletionCallback);
        Assert.Equal((int?)32768, session.NativeFileIo.Rows[0].FileResult);
        Assert.Equal(JsonSerializer.Serialize(before.Write(0x2798219, [0])), JsonSerializer.Serialize(session.NativeEffectMemory.ScriptedEffects));
        Assert.Same(memory.Arena, session.NativeEffectMemory.Arena);
        Assert.Same(memory.PrimitiveProcess, session.NativeEffectMemory.PrimitiveProcess);
        Assert.Throws<NotSupportedException>(() => session.NativeEffectMemory.ScriptedEffects!.Read(0x279821A, 1));
    }

    [Fact]
    public void Adjacent_unproved_completion_remains_unsupported_without_clearing_busy()
    {
        BattleSession session = Fixture(0xA57CA1, 0);
        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() => Pump(session));
        Assert.IsType<NotSupportedException>(exception.InnerException);
        Assert.Equal((byte)0xFF, session.NativeEffectMemory!.ScriptedEffects!.Read(0x2798219, 1)[0]);
    }

    private static BattleSession Fixture(uint callback, int length)
    {
        BattleSession session = new(new BattleDefinition
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.Normal);
        NativeBattleEffectMemory memory = session.NativeEffectMemory!;
        NativeBattleScriptedEffectMemory core = NativeBattleScriptedEffectMemory.FromColdProcess().Write(0x2798218, [0x31, 0xFF, 0x72]).MarkUnknown(0x279821A, 1);
        Set(session, nameof(BattleSession.NativeEffectMemory), memory with { ScriptedEffects = core });
        Set(session, nameof(BattleSession.NativeFileIo), NativeBattleFileIoState.FromColdProcess().Queue482610(length, callback, null));
        return session;
    }

    private static void Set(BattleSession session, string property, object value)
    {
        typeof(BattleSession).GetProperty(property, BindingFlags.Instance | BindingFlags.Public)!.SetValue(session, value);
    }
    private static void Pump(BattleSession session)
    {
        typeof(BattleSession).GetMethod("PumpNativeFileIo482590", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, null);
    }
}
