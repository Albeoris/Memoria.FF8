using static Memoria.FF8.Runtime.NativeBattleEffect76Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect76Controller
{
    public static NativeBattleEffect76Step AdvanceStartup61FA80(NativeBattleEffect76Frame frame, uint address, NativeBattleEffect76Resources resources, NativeBattleEffect76Inputs inputs)
    {
        uint flags = inputs.ReadFlags(frame);
        if ((flags & 0x201) != 0)
        {
            if ((flags & 1) != 0)
            {
                return new(frame, 0);
            }
            NativeBattleEffect76Step io = inputs.ReadStep508500(frame);
            frame = io.Frame;
            if (io.Result < 0)
            {
                return new(frame, 0);
            }
        }
        int age = Signed(Read(frame, address), 12);
        int actor = checked((int)frame.State.ReadDword(CasterGlobal));
        if (age == 0)
        {
            frame = WriteBytes(frame, SavedPointAddress, inputs.ReadPosition8(frame, actor), 8);
            frame = inputs.Execute(frame, new NativeBattleEffect76Stage5713E0(4096, 0));
            frame = inputs.Execute(frame, new NativeBattleEffect76ModelPointY(actor, 5000, false));
            frame = inputs.Execute(frame, new NativeBattleEffect76ModelFlags(actor, 0xFF, 4, false));
            frame = inputs.Execute(frame, new NativeBattleEffect76SetMode4A8480(0));
            frame = inputs.Execute(frame, new NativeBattleEffect76MusicLoad501A20(MusicAddress, MusicCompletionGlobal));
        }
        if (frame.State.Read(MusicCompletionGlobal, 1)[0] == 0 && age != 0)
        {
            return new(frame, 0);
        }
        if (age == 10)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect76QueueStage512AA0(1, frame.State.ReadDword(ArenaGlobal)));
        }
        if (age == 38)
        {
            NativeBattleEffect76Step io = inputs.ReadStep508500(frame);
            frame = io.Frame;
            if (io.Result < 0)
            {
                return new(frame, 0);
            }
            frame = inputs.Execute(frame, new NativeBattleEffect76ReloadStage512AC0(1, frame.State.ReadDword(ArenaGlobal)));
            frame = inputs.Execute(frame, new NativeBattleEffect76SyncModelColors61FC60(inputs.ReadColor(frame, 0xB8B9A8)));
        }
        if (age == 39)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect76WaveRequest46CBB0(0, "field2.fs", 127));
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1))));
        if (Signed(Read(frame, address), 12) < 40)
        {
            return new(frame, 0);
        }
        NativeBattleEffect76FactoryStep next = Create61FC40(frame, 0x61FC90);
        return new(next.Frame, 2);
    }

    public static NativeBattleEffect76FactoryStep Create61FC40(NativeBattleEffect76Frame frame, uint callback)
    {
        (frame, uint address) = Append(frame, ChildDescriptor, callback);
        if (address == 0)
        {
            throw new NotSupportedException("Effect76 exhausted its required native task bank.");
        }
        return new(Write(frame, address, Read(frame, address).WriteWord(12, 0).WriteWord(14, 0)), address);
    }

    public static NativeBattleEffect76Step AdvanceDelay61FC90(NativeBattleEffect76Frame frame, uint address, NativeBattleEffect76Inputs inputs)
    {
        if ((inputs.ReadFlags(frame) & 0x201) != 0)
        {
            return new(frame, 0);
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1))));
        if (Signed(Read(frame, address), 12) < 50)
        {
            return new(frame, 0);
        }
        return new(Create61FC40(frame, DriverCallback).Frame, 2);
    }

    public static NativeBattleEffect76FactoryStep BeginWhite6200A0(NativeBattleEffect76Frame frame, bool skipFirst, ushort duration)
    {
        NativeBattleEffect76FactoryStep created = Create61FC40(frame, skipFirst ? 0x620140u : 0x6200F0u);
        return created with { Frame = Write(created.Frame, created.Address, Read(created.Frame, created.Address).WriteWord(14, duration)) };
    }

    public static NativeBattleEffect76Step AdvanceWhite(NativeBattleEffect76Frame frame, uint address, NativeBattleEffect76Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback == 0x6200F0 || Signed(task, 12) > 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect76PcRender47CF50(0, 255, 255, 255, 63));
        }
        if ((inputs.ReadFlags(frame) & 0x201) != 0)
        {
            return new(frame, 0);
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1))));
        return new(frame, Signed(Read(frame, address), 12) >= Signed(Read(frame, address), 14) ? 2 : 0);
    }
}
