using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSequenceParticleSessionTests
{
    [Fact]
    public void Contact_particle_reads_the_active_physical_cursor_and_suppresses_without_loading_a_resource()
    {
        BattleSession session = CreateSession();
        NativeBattleActionPacketsState packets = OriginalPackets();
        (packets, _) = packets.Prepare(new(OriginalOwner(0), 1, 13, 0, 1, 0, null));
        (packets, _) = packets.AppendContact(new(new(OriginalOwner(0), 0, 0, 0, 0, 0, 0), null));
        (packets, _) = packets.AppendContact(new(new(OriginalOwner(1), 0, 0, 4, 0, 0, 0), null));
        (packets, _) = packets.Publish(2);
        packets = packets.ActivateHeader(new BattleActionHeaderReference(0)).AdvanceContact();
        typeof(BattleSession).GetField("nativeActionPackets", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, packets);
        NativeBattleGraphicsState graphics = Graphics() with { ActionGraphics = new(new BattleActionHeaderReference(0), BattleRetainedModelReference.Capture(OriginalOwner(0), OriginalAliases), new BattleContactCursor(0), BattleRetainedModelSelection.Capture(BattleModelTargets.From([OriginalOwner(1)]), OriginalAliases), BattleRetainedModelSelection.Empty, null, BattleRetainedModelReference.Capture(OriginalOwner(1), OriginalAliases), 0, null, 0x50BB00) };
        var result = ((NativeBattleGraphicsState Graphics, int Offset))Invoke(session, "ScheduleNativeSequenceParticle", graphics, OriginalOwner(0), new NativeBattleAuxiliaryProgram([12, 1, 224], 0), true);
        Assert.Equal(3, result.Offset);
        Assert.Null(result.Graphics.ModuleEffects.Head);
        Assert.Same(packets, session.Presentation!.NativeActionPackets);
    }

    [Fact]
    public void Cover_uses_source_initial_xz_and_preserves_recipient_y()
    {
        BattleSession session = CreateSession();
        NativeBattleGraphicsState graphics = Graphics();
        NativeBattleGraphicsModel source = ReadOriginalModelCell(graphics, 0) with { Flags2 = 0, Transform = new(new(10, 20, 30), new(100, 200, 300), 0, 0, 4096) };
        NativeBattleGraphicsModel recipient = ReadOriginalModelCell(graphics, 1) with { Flags2 = 0, Flags8 = 0x18, Transform = new(new(1, 2, 3), new(4, 5, 6), 0, 0, 4096) };
        graphics = graphics with { ActorModels = graphics.ActorModels.Set(OriginalOwner(0), source).Set(OriginalOwner(1), recipient) };
        NativeBattleContactHalf contact = new(OriginalOwner(1), 0, 0, 8, 0, 0, 0) { CoverSource = OriginalOwner(0) };
        NativeBattleGraphicsState result = (NativeBattleGraphicsState)Invoke(session, "ApplyNativeSequenceCover5065B0", graphics, contact);
        Assert.Equal(new NativeBattleVector(10, 5, 30), ReadOriginalModelCell(result, 1).Transform!.Position28);
        Assert.Equal((byte)9, ReadOriginalModelCell(result, 0).Kind5);
        Assert.Equal(source.Transform, ReadOriginalModelCell(result, 0).Transform);
        NativeBattleGraphicsState suppressed = (NativeBattleGraphicsState)Invoke(session, "ApplyNativeSequenceCover5065B0", graphics with { Flags = 0x10 }, contact);
        Assert.Same(graphics.ActorModels, suppressed.ActorModels);
    }

    private static BattleSession CreateSession()
    {
        BattleSession session = new(new BattleDefinition { NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.Normal);
        typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeCamera))!.SetValue(session.Presentation!, NativeBattleCameraState.FromColdProcess());
        return session;
    }

    private static NativeBattleGraphicsState Graphics()
    {
        return OriginalGraphics([(0, OriginalOwner(0), new(2)), (1, OriginalOwner(1), new(2))]);
    }

    private static object Invoke(BattleSession session, string name, params object[] arguments)
    {
        return typeof(BattleSession).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, arguments)!;
    }
}
