using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect80Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect80Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect80Trails
{
    private static NativeBattleEffect80Frame Bind(NativeBattleEffect80Frame frame, NativeBattleEffect80Resources resources)
    {
        return frame with { State = frame.State.BindInitializedSource(resources, TrailPointerAddress).BindInitializedSource(resources, TrailAddress) };
    }

    public static NativeBattleEffect80Frame Initialize7B43F0(NativeBattleEffect80Frame frame, NativeBattleEffect80Resources resources)
    {
        frame = Bind(frame, resources);
        uint pointer = frame.State.ReadDword(TrailPointerAddress);
        NativeBattleEffect80State state = frame.State.Write(TrailActiveGlobal, [0]).WriteWord(0x25DF31C, 10).WriteWord(0x25DF31E, 16).WriteWord(0x25DF320, 0).WriteWord(0x25DF322, 0xFFE0).WriteDword(TrailPointCursorGlobal, pointer).WriteDword(TrailRecordCursorGlobal, pointer + 0x3000);
        for (uint index = 0; index < 512; index++)
        {
            state = state.WriteWord(pointer + index * 24 + 6, 0xFFFF);
        }
        for (uint index = 0; index < 64; index++)
        {
            state = state.WriteDword(pointer + 0x3000 + index * 48, 0);
        }
        return frame with { State = state };
    }

    public static (NativeBattleEffect80Frame Frame, uint PointAddress) AllocatePoint7AFD80(NativeBattleEffect80Frame frame, bool capturePresentation = false)
    {
        uint pointer = frame.State.ReadDword(TrailPointerAddress);
        uint selected = frame.State.ReadDword(TrailPointCursorGlobal);
        if (frame.State.ReadWord(selected + 6) != 0xFFFF)
        {
            selected = 0;
            for (uint index = 0; index < 512; index++)
            {
                uint candidate = pointer + index * 24;
                if (frame.State.ReadWord(candidate + 6) == 0xFFFF)
                {
                    selected = candidate;
                    break;
                }
            }
        }
        if (selected == 0)
        {
            return (frame, 0);
        }
        if (capturePresentation)
        {
            NativeBattlePilotVisualLifetimes.Allocated(frame.State.InitializedSources[TrailAddress], pointer, checked((int)((selected - pointer) / 24)));
        }
        uint next = selected >= pointer + 0x2FE8 ? pointer : selected + 24;
        return (frame with { State = frame.State.WriteDword(TrailPointCursorGlobal, next) }, selected);
    }

    public static (NativeBattleEffect80Frame Frame, uint PointAddress) Birth7AFDD0(NativeBattleEffect80Frame frame, NativeBattleEffectArena descriptor48, NativeBattleEffect80Resources resources, bool capturePresentation = false)
    {
        frame = Bind(frame, resources);
        uint pointer = frame.State.ReadDword(TrailPointerAddress);
        uint record = frame.State.ReadDword(TrailRecordCursorGlobal);
        if (frame.State.ReadDword(record) != 0)
        {
            record = 0;
            for (uint index = 0; index < 64; index++)
            {
                uint candidate = pointer + 0x3000 + index * 48;
                if (frame.State.ReadDword(candidate) == 0)
                {
                    record = candidate;
                    break;
                }
            }
        }
        if (record == 0)
        {
            return (frame, 0);
        }
        frame = frame with { State = frame.State.WriteDword(TrailRecordCursorGlobal, record >= pointer + 0x3BD0 ? pointer + 0x3000 : record + 48) };
        (frame, uint point) = AllocatePoint7AFD80(frame, capturePresentation);
        if (point == 0)
        {
            return (frame, 0);
        }
        NativeBattleEffect80State state = frame.State.WriteDword(point + 0x14, 0).WriteDword(point + 8, 0).WriteDword(point + 12, 0);
        for (int offset = 8; offset <= 0x28; offset += 4)
        {
            state = state.WriteDword(record + (uint)offset, BinaryPrimitives.ReadUInt32LittleEndian(descriptor48.Read(offset, 4)));
        }
        state = state.WriteDword(point, state.ReadDword(record + 8)).WriteDword(point + 4, state.ReadDword(record + 12)).WriteDword(point + 0x10, state.ReadDword(record + 0x28)).WriteDword(record, point).WriteDword(record + 4, point).WriteWord(record + 0x2C, 1).WriteWord(record + 0x2E, BinaryPrimitives.ReadUInt16LittleEndian(descriptor48.Read(0x2E, 2))).Write(TrailActiveGlobal, [2]);
        return (frame with { State = state }, point);
    }

    public static (NativeBattleEffect80Frame Frame, int Value) Random7AFEA0(NativeBattleEffect80Frame frame)
    {
        uint value = unchecked(frame.State.ReadDword(TrailRandomGlobal) * 125 + 14) & 0x7FFF;
        return (frame with { State = frame.State.WriteDword(TrailRandomGlobal, value) }, (int)value);
    }

    public static NativeBattleEffect80Frame Advance7AF4C0(NativeBattleEffect80Frame frame, NativeBattleEffect80Resources resources, NativeBattleEffect80Inputs inputs)
    {
        return Update7AF9F0(Draw7AF4D0(frame, resources, inputs), resources, inputs.CapturePresentation);
    }

    private static short SignedWord(NativeBattleEffect80State state, uint address) => unchecked((short)state.ReadWord(address));
    private static NativeBattleEffect80State AddWord(NativeBattleEffect80State state, uint address, int value) => state.WriteWord(address, unchecked((ushort)(state.ReadWord(address) + value)));
}
