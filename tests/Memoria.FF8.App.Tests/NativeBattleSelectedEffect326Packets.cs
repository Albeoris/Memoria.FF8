using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class NativeBattleSelectedEffect326Packets
{
    public static NativeBattleLogicalCommand[] CurrentCommands(BattleSnapshot snapshot)
    {
        NativeBattleEffect326State state = snapshot.NativeEffectMemory?.Effect326 ?? throw new InvalidDataException("Effect326 visibility requires its actual retained output interval.");
        int first = state.CommonPacketStart ?? throw new InvalidDataException("Effect326 visibility requires its actual callback COMMON start.");
        int end = state.CommonPacketEnd ?? throw new InvalidDataException("Effect326 visibility requires its actual callback COMMON end.");
        NativeBattlePrimitiveProcess process = snapshot.NativeEffectMemory!.PrimitiveProcess ?? throw new InvalidDataException("Effect326 visibility requires its actual COMMON process.");
        Assert.InRange(first, 0, process.CommonPacketWriteCapacity);
        Assert.InRange(end, first, process.CommonPacketWriteCapacity);
        NativeBattlePrimitiveFrame primitive = snapshot.NativeGraphics?.PrimitiveFrame ?? throw new InvalidDataException("Effect326 visibility requires its current physical commands.");
        int bankOffset = checked(process.PacketBuffer * process.CommonPacketNominalCapacity);
        return primitive.LastCommands.Select(value => value.Command).Where(IsOwned).ToArray();

        bool IsOwned(NativeBattleLogicalCommand command)
        {
            if (command.Packet.Bank == "20DFAB8" || command.Packet.ByteOffset < 0)
            {
                return false;
            }
            NativeBattlePrimitiveReference normalized = NativeBattlePrimitiveProcess.NormalizeCommonReference(command.Packet);
            int offset = normalized.ByteOffset - bankOffset;
            if (normalized.Bank != process.PacketBank || normalized.Buffer != 0 || offset < first || offset >= end)
            {
                return false;
            }
            NativeBattlePrimitiveProcess physical = process with { PacketBank = command.Packet.Bank, PacketBuffer = command.Packet.Buffer };
            byte[] tag = physical.ReadCommonPacket(command.Packet.ByteOffset, 4);
            int length = checked((tag[3] + 1) * 4);
            Assert.True(offset + length <= end, "Effect326 physical command leaves its emitted COMMON callback interval.");
            return true;
        }
    }
}
