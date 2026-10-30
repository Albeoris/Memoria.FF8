using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class NativeBattleExplicitComponentCapture
{
    internal static NativeBattleLogicalCommand[] Commands(BattleSnapshot current, ushort effectId)
    {
        NativeBattleOriginalEffectState effect = current.NativeGraphics!.OriginalEffect!;
        int start;
        int end;
        if (effectId == 101)
        {
            NativeBattleEffect101State state = current.NativeEffectMemory!.Effect101!;
            if (effect.Pools[0].Slots[0].ReadWord(12) == 0)
            {
                return [];
            }
            uint working = state.ReadDword(NativeBattleEffect101Profile.WorkingGlobal);
            uint pointer = state.ReadDword(NativeBattleEffect101Profile.CursorGlobal);
            NativeBattleModulePoolTask root = effect.Pools[0].Slots[0];
            start = checked((int)(working - NativeBattleEffect101Profile.ArenaAddress)) + (root.ReadDword(16) == 0 ? NativeBattleEffect101Profile.FirstPacketOffset : NativeBattleEffect101Profile.SecondPacketOffset);
            end = checked((int)(pointer - NativeBattleEffect101Profile.ArenaAddress));
        }
        else if (effectId == 110)
        {
            int buffer = effect.Control110!.BufferOffset;
            start = buffer + (effect.Pools[0].Slots[0].ReadDword(16) == 0 ? 0x1E9C : 0x11E9C);
            end = effect.DrawArenaOffset ?? start;
        }
        else
        {
            Assert.Equal((ushort)112, effectId);
            start = effect.Pools[0].Slots[0].ReadDword(16) == 0 ? 0x1E88 : 0x11E88;
            end = effect.DrawArenaOffset ?? start;
        }
        Assert.True(end >= start);
        return current.NativeGraphics.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= start && command.Packet.ByteOffset < end && command.Opcode is >= 0x20 and < 0x80).ToArray();
    }

    internal static int AssertFrame(BattleSnapshot current, ushort effectId)
    {
        NativeBattleLogicalCommand[] commands = Commands(current, effectId);
        foreach (NativeBattleLogicalCommand command in commands)
        {
            NativeBattlePilotVisualGeometry visual = Assert.IsType<NativeBattlePilotVisualGeometry>(command.PilotGeometry);
            Assert.Equal(effectId, visual.Identity.Effect);
            Assert.True(visual.Identity.Generation > 0);
            Assert.InRange(visual.CornerCount, (byte)2, (byte)4);
            for (int corner = 0; corner < visual.CornerCount; corner++)
            {
                System.Numerics.Vector3 point = visual.Corner(corner);
                Assert.True(float.IsFinite(point.X) && float.IsFinite(point.Y) && float.IsFinite(point.Z));
            }
            if (visual.ScreenSpace)
            {
                Assert.False(visual.Continuous);
                Assert.Equal((byte)0x2A, command.Opcode);
            }
            else
            {
                Assert.True(visual.Identity.ResourceGeneration > 0);
            }
        }
        return commands.Length;
    }
}
