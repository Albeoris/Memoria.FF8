using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect227Profile;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect227PresentationTests
{
    [Fact]
    public void Selected_Effect227_archived_boundary_attributes_mesh_packets_to_the_observed_arena()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT227_OBSERVATION_CURRENT");
        Assert.SkipUnless(path is not null, "Select an archived effect227 observation at a visible source-mesh boundary.");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path!));
        JsonElement graphics = document.RootElement.GetProperty("NativeGraphics");
        JsonElement effect = graphics.GetProperty("OriginalEffect");
        Assert.Equal(Effect, effect.GetProperty("Binding").GetProperty("Effect").GetUInt16());
        Assert.True(effect.GetProperty("ReturnedDescriptor").GetUInt32() > 0);

        JsonElement memory = document.RootElement.GetProperty("NativeEffectMemory");
        NativeBattleEffect227State state = Assert.IsType<NativeBattleEffect227State>(NativeBattleFixtureState.ReadArchived<NativeBattleEffect227State>(memory, "Effect227"));
        uint arena = state.ReadDword(ArenaGlobal);
        uint cursor = state.ReadDword(CursorGlobal);
        uint end = state.ReadDword(SecondCursorGlobal);
        Assert.NotEqual(0u, arena);
        uint start = end == arena + ThirdPacketOffset ? arena + SecondPacketOffset : arena + FirstPacketOffset;
        Assert.InRange(cursor, start, end);

        NativeBattlePrimitiveFrame primitive = Assert.IsType<NativeBattlePrimitiveFrame>(NativeBattleFixtureState.ReadArchived<NativeBattlePrimitiveFrame>(graphics, "PrimitiveFrame"));
        Assert.Contains(primitive.LastCommands.Select(item => item.Command.ToCommand()), command => command.Opcode is >= 0x20 and <= 0x3F &&
            command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= start - 0x20DFAB8 && command.Packet.ByteOffset < cursor - 0x20DFAB8);
    }
}
