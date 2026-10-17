using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSequenceParticleSessionTests
{
    [Fact]
    public void Contact_particle_reads_the_active_physical_cursor_and_suppresses_without_loading_a_resource()
    {
        BattleSession session = CreateSession();
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 1, 13, 0, 1, 0, null));
        (packets, _) = packets.AppendContact(new(new(0, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.AppendContact(new(new(1, 0, 0, 4, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(2);
        packets = packets.ActivateHeader(0).AdvanceContact50A690();
        typeof(BattleSession).GetField("nativeActionPackets", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, packets);
        NativeBattleGraphicsState graphics = Graphics() with { ActionGraphics = new(0, 0, 0, 2, 0, null, 1, 0, null, 0x50BB00) };
        var result = ((NativeBattleGraphicsState Graphics, int Offset))Invoke(session, "ScheduleNativeSequenceParticle", graphics, 0, new NativeBattleAuxiliaryProgram([12, 1, 224], 0), true);
        Assert.Equal(3, result.Offset);
        Assert.Null(result.Graphics.ModuleEffects.Head);
        Assert.Equal(packets, session.Capture().NativeActionPackets);
    }

    [Fact]
    public void Cover_uses_source_initial_xz_and_preserves_recipient_y()
    {
        BattleSession session = CreateSession();
        NativeBattleGraphicsState graphics = Graphics();
        NativeBattleGraphicsModel source = graphics.Models[0] with { Flags2 = 0, Transform = new(new(10, 20, 30), new(100, 200, 300), 0, 0, 4096) };
        NativeBattleGraphicsModel recipient = graphics.Models[1] with { Flags2 = 0, Flags8 = 0x18, Transform = new(new(1, 2, 3), new(4, 5, 6), 0, 0, 4096) };
        graphics = graphics with { Models = graphics.Models.SetItem(0, source).SetItem(1, recipient) };
        NativeBattleContactHalf contact = new(1, 0, 0, 8, 0, 0, 0);
        NativeBattleGraphicsState result = (NativeBattleGraphicsState)Invoke(session, "ApplyNativeSequenceCover5065B0", graphics, contact);
        Assert.Equal(new NativeBattleVector(10, 5, 30), result.Models[1].Transform!.Position28);
        Assert.Equal((byte)9, result.Models[0].Kind5);
        Assert.Equal(source.Transform, result.Models[0].Transform);
        NativeBattleGraphicsState suppressed = (NativeBattleGraphicsState)Invoke(session, "ApplyNativeSequenceCover5065B0", graphics with { Flags = 0x10 }, contact);
        Assert.Equal(graphics.Models, suppressed.Models);
    }

    private static BattleSession CreateSession()
    {
        BattleSession session = new(new BattleDefinition { NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.Normal);
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeCamera))!.SetValue(session, NativeBattleCameraState.FromColdProcess());
        return session;
    }

    private static NativeBattleGraphicsState Graphics()
    {
        return new(Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 2)).ToImmutableArray(), Enumerable.Repeat<string?>(null, 7).ToImmutableArray(), 0, 0);
    }

    private static object Invoke(BattleSession session, string name, params object[] arguments)
    {
        return typeof(BattleSession).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, arguments)!;
    }
}
