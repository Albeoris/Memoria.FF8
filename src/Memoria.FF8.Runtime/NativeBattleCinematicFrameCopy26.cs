using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicFrameCopy26Services(NativeBattleCinematicCapture96Services Capture, Func<NativeBattleWaterExecutionFrame, uint, uint, NativeBattleGfGeometryBatch, NativeBattleWaterExecutionFrame> Append);

/// <summary>A first-frame five-strip capture followed by its grayscale screen overlay.</summary>
public static class NativeBattleCinematicFrameCopy26
{
    public static NativeBattleWaterExecutionFrame Draw(NativeBattleWaterExecutionFrame frame, NativeBattleCinematicFrameCopy26Services services, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        uint payload = SlotDword(0xB8);
        frame = frame.WriteTemporaryDword(0x90, unchecked((uint)(int)SignedWord(payload)));
        frame = frame.WriteTemporaryDword(0x94, unchecked((uint)(int)SignedWord(payload + 2)));
        if (SlotDword(0xC4) == 0)
        {
            frame = WriteSlotDword(frame, 0xC4, uint.MaxValue);
            frame = CaptureDestination(frame);
            return NativeBattleCinematicCapture96.Capture(frame, services.Capture);
        }
        uint color = SlotDword(0xCC);
        if ((byte)color != (byte)(color >> 8) || (byte)(color >> 8) != (byte)(color >> 16))
        {
            return frame;
        }
        int x = unchecked((int)frame.ReadTemporaryDword(0x90));
        int y = unchecked((int)frame.ReadTemporaryDword(0x94));
        uint page = unchecked((uint)((((x >> 2) & 0xF0) | (y & 0x100)) >> 4));
        page |= unchecked((uint)(int)(short)SlotWord(frame, 0x92));
        page |= 0x100;
        frame = frame.WriteTemporaryDword(0x80, page).WriteTemporaryDword(0x84, color | 0x2A000000);
        frame = frame.WriteTemporaryDword(0x60, unchecked((uint)(int)(short)SlotWord(frame, 0x94))).WriteTemporaryDword(0x64, unchecked((uint)(int)(short)SlotWord(frame, 0x96)));
        uint manager = CoreDword(frame, 0x27973EC);
        uint output = CoreDword(frame, unchecked(manager + 0x7C));
        uint ordering = unchecked(frame.ReadTemporaryDword(0x14C) + 8);
        for (int strip = 0; strip < 5; strip++)
        {
            SkinWrites packet = new(24, 0);
            packet.Byte(3, 5);
            packet.Word(4, frame.ReadTemporaryDword(0x84));
            short left = unchecked((short)(frame.ReadTemporaryWord(0x60) + strip * 64));
            short right = unchecked((short)(left + 64));
            short top = unchecked((short)frame.ReadTemporaryWord(0x64));
            short bottom = unchecked((short)(top + 224));
            packet.Word(8, Screen(left, top));
            packet.Word(12, Screen(right, top));
            packet.Word(16, Screen(left, bottom));
            packet.Word(20, Screen(right, bottom));
            NativeBattlePilotVisualGeometry geometry = new(new(0, 0, 0, 0, strip), new(left, top, 0), new(right, top, 0), new(left, bottom, 0), new(right, bottom, 0), 4, false) { ScreenSpace = true, ScreenScale = 8 };
            if (pilotIdentity is { } identity)
            {
                geometry = geometry with { Identity = identity with { Primitive = unchecked(identity.Primitive * 397 + strip) }, SourceDepths = System.Numerics.Vector4.Zero, UseSourceDepths = true };
            }
            NativeBattleGeometryPrimitive primitive = new(0, packet.Words(), []) { PilotGeometry = geometry };
            frame = services.Append(frame, output, ordering, new([packet.Attempt(primitive)], 24));
            output = unchecked(output + 24);
        }
        SkinWrites mode = new(12, 0);
        mode.Byte(3, 2);
        mode.Word(4, 0xE1000000);
        mode.Word(8, 0xE2000000);
        frame = services.Append(frame, output, ordering, new([mode.Attempt(new(0, mode.Words(), []))], 12));
        output = unchecked(output + 12);
        manager = CoreDword(frame, 0x27973EC);
        frame = WriteCoreDword(frame, unchecked(manager + 0x7C), output);
        return CaptureDestination(frame);

        short SignedWord(uint address) => BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(ArenaOffset(address, 2), 2));
        uint SlotDword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, offset, 4));
        static uint Screen(short px, short py) => unchecked((ushort)px | ((uint)(ushort)py << 16));
        static NativeBattleWaterExecutionFrame CaptureDestination(NativeBattleWaterExecutionFrame current) => current.WriteTemporaryDword(0xF0, current.ReadTemporaryDword(0x90)).WriteTemporaryDword(0xF4, current.ReadTemporaryDword(0x94));
    }
}
