using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect290Profile;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect290PresentationTests
{
    [Fact]
    public void Archived_effect290_frame_records_mesh_packets_in_its_observed_arena()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT290_OBSERVATION_CURRENT");
        Assert.SkipUnless(path is not null, "Select an archived Effect290 frame containing its source mesh.");
        var (archive, _, primitive) = NativeBattleFixtureInspection.ReadArchivedEffectFrame(path!, Effect);
        NativeBattleEffect290State state = Assert.IsType<NativeBattleEffect290State>(NativeBattleFixtureState.ReadArchived<NativeBattleEffect290State>(archive.GetProperty("NativeEffectMemory"), "Effect290"));
        uint arena = state.ReadDword(ArenaGlobal);
        uint cursor = state.ReadDword(CursorGlobal);
        uint first = arena + FirstPacketOffset;
        uint second = arena + SecondPacketOffset;
        uint start = cursor >= second ? second : first;
        Assert.NotEqual(0u, arena);
        Assert.InRange(cursor, start, start + 0xC000);
        Assert.Contains(primitive.LastCommands.Select(item => item.Command.ToCommand()), command => command.Opcode is >= 0x20 and <= 0x3F &&
            command.VertexPayload4.Length == 4 && command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 &&
            command.Packet.ByteOffset >= start - 0x20DFAB8 && command.Packet.ByteOffset < cursor - 0x20DFAB8);
    }
}
