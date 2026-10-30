using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class NativeBattleModuleCaptureAssertions
{
    internal static int AssertPrivateGeometry(BattleSnapshot snapshot, ushort effect)
    {
        if (snapshot.NativeGraphics?.OriginalEffect is not { } selected || selected.Binding.Effect != effect || snapshot.NativeGraphics.PrimitiveFrame is not { } primitive)
        {
            return 0;
        }
        uint first;
        uint end;
        if (effect == 54)
        {
            NativeBattleEffect54State state = snapshot.NativeEffectMemory!.Effect54!;
            first = state.ReadDword(NativeBattleEffect54Profile.InitializerGlobal);
            end = first == 0 ? 0 : checked(first + 0x10000);
        }
        else if (effect == 63)
        {
            NativeBattleEffect63State state = snapshot.NativeEffectMemory!.Effect63!;
            uint arena = state.ReadDword(NativeBattleEffect63Profile.WorkGlobal);
            first = arena == 0 ? 0 : checked(arena + NativeBattleEffect63Profile.FirstPacketOffset);
            end = arena == 0 ? 0 : checked(arena + NativeBattleEffect63Profile.MorphOffset);
        }
        else
        {
            throw new InvalidDataException("The module capture assertion requires its explicit physical packet allocation.");
        }
        if (first == 0)
        {
            return 0;
        }
        int count = 0;
        foreach (NativeBattleLogicalCommand command in primitive.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= first - 0x20DFAB8 && command.Packet.ByteOffset < end - 0x20DFAB8 && command.Opcode is >= 0x20 and < 0x60))
        {
            NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(command.PilotGeometry);
            Assert.Equal(effect, geometry.Identity.Effect);
            Assert.True(geometry.Identity.Generation > 0);
            Assert.InRange(geometry.CornerCount, 3, 4);
            count++;
        }
        return count;
    }
}
