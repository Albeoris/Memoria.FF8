using static Memoria.FF8.Runtime.NativeBattleEffect218Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect218Controller
{
    public static (NativeBattleEffect218Frame Frame, uint Address) AppendMain632E30(NativeBattleEffect218Frame frame, uint callback)
    {
        (frame, uint address) = AppendRequired(frame, MainDescriptor, callback);
        return (Write(frame, address, Read(frame, address).WriteWord(12, 0).WriteWord(14, 0)), address);
    }

    public static (NativeBattleEffect218Frame Frame, uint Address) AppendSecondary632E50(NativeBattleEffect218Frame frame, uint callback)
    {
        (frame, uint address) = AppendRequired(frame, SecondaryDescriptor, callback);
        return (Write(frame, address, Read(frame, address).WriteWord(12, 0).WriteWord(14, 0)), address);
    }

    public static NativeBattleEffect218Frame SpawnPair632FF0(NativeBattleEffect218Frame frame)
    {
        (frame, uint first) = AppendRequired(frame, MainDescriptor, 0x633090);
        frame = Write(frame, first, Read(frame, first).WriteWord(12, 0).WriteWord(14, 0));
        frame = Roll(frame, out ushort angle);
        frame = Write(frame, first, Read(frame, first).WriteWord(24, unchecked((ushort)((angle & 2047) - 1024))).WriteWord(26, unchecked((ushort)-16)).WriteWord(28, 3200).WriteWord(32, 1536));
        (frame, uint second) = AppendRequired(frame, MainDescriptor, 0x633090);
        frame = Write(frame, second, Read(frame, second).WriteWord(12, 0).WriteWord(14, 1));
        frame = Roll(frame, out angle);
        return Write(frame, second, Read(frame, second).WriteWord(24, (ushort)(angle & 511)).WriteWord(26, unchecked((ushort)-8)).WriteWord(28, 3072).WriteWord(32, 2560));
    }

    public static NativeBattleEffect218Frame SpawnRay6331A0(NativeBattleEffect218Frame frame)
    {
        (frame, uint address) = AppendRequired(frame, MainDescriptor, 0x633200);
        frame = Write(frame, address, Read(frame, address).WriteWord(12, 0).WriteWord(16, 0).WriteWord(18, unchecked((ushort)-10000)).WriteWord(20, 0));
        NativeBattleActionMatrix basis = NativeBattleArenaPose.ReadMatrix(frame.State.ReadView(CameraBasis, 32), 0);
        NativeBattleVector point = NativeBattleGfMatrices.RotateShort56C4F0(basis, new(0, -10000, 0));
        NativeBattleModulePoolTask task = Read(frame, address).WriteWord(16, unchecked((ushort)(point.X + Word(frame, 0x24F235C)))).WriteWord(18, unchecked((ushort)(point.Y + Word(frame, 0x24F2360)))).WriteWord(20, unchecked((ushort)(point.Z + Word(frame, 0x24F2364))));
        return Write(frame, address, task);
    }

    public static NativeBattleEffect218Frame SpawnSpin633500(NativeBattleEffect218Frame frame)
    {
        (frame, uint address) = AppendRequired(frame, MainDescriptor, 0x633560);
        frame = Write(frame, address, Read(frame, address).WriteWord(12, 0).WriteWord(16, 0).WriteWord(18, unchecked((ushort)-10000)).WriteWord(20, 0));
        frame = Roll(frame, out ushort angle);
        frame = Write(frame, address, Read(frame, address).WriteWord(24, (ushort)(angle & 511)));
        frame = Roll(frame, out ushort velocity);
        return Write(frame, address, Read(frame, address).WriteWord(26, (ushort)(velocity % 30 + 25)));
    }

    public static NativeBattleEffect218Frame SpawnShaft634070(NativeBattleEffect218Frame frame)
    {
        (frame, uint address) = AppendRequired(frame, MainDescriptor, 0x6340A0);
        return Write(frame, address, Read(frame, address).WriteWord(12, 0).WriteWord(28, 0x2400).WriteWord(32, 0x1E00));
    }

    public static NativeBattleEffect218Frame SpawnBurst6345E0(NativeBattleEffect218Frame frame)
    {
        (frame, uint center) = AppendRequired(frame, MainDescriptor, 0x634750);
        frame = Write(frame, center, ClearBurstPosition218(Read(frame, center)).WriteWord(28, 0x900).WriteWord(30, 0x900));
        for (int index = 0; index < 2; index++)
        {
            (frame, uint address) = AppendRequired(frame, MainDescriptor, 0x634890);
            frame = Write(frame, address, ClearBurstPosition218(Read(frame, address)));
            frame = Roll(frame, out ushort angle);
            frame = Write(frame, address, Read(frame, address).WriteWord(24, unchecked((ushort)((angle & 2047) - 1024))));
            frame = Roll(frame, out ushort velocity);
            frame = Write(frame, address, Read(frame, address).WriteWord(26, unchecked((ushort)(-30 - velocity % 90))).WriteWord(28, 0xB80).WriteWord(30, 0xB80));
            frame = Roll(frame, out ushort growth);
            ushort increment = (ushort)(((growth & 4095) + 0x1600) / 10);
            frame = Write(frame, address, Read(frame, address).WriteWord(32, increment).WriteWord(34, increment));
        }
        (frame, uint rim) = AppendRequired(frame, MainDescriptor, 0x634A10);
        frame = Write(frame, rim, ClearBurstPosition218(Read(frame, rim)));
        frame = Roll(frame, out ushort rimAngle);
        frame = Write(frame, rim, Read(frame, rim).WriteWord(24, unchecked((ushort)((rimAngle & 1023) - 512))));
        frame = Roll(frame, out ushort rimVelocity);
        frame = Write(frame, rim, Read(frame, rim).WriteWord(26, (ushort)(rimVelocity % 120 + 15)).WriteWord(28, 0x740).WriteWord(30, 0x740).WriteWord(32, 0x3AA).WriteWord(34, 0x3AA));
        (frame, uint flash) = AppendRequired(frame, MainDescriptor, 0x634B90);
        return Write(frame, flash, Read(frame, flash).WriteWord(12, 0).WriteWord(14, 40));
    }

    private static NativeBattleModulePoolTask ClearBurstPosition218(NativeBattleModulePoolTask task) => task.WriteWord(12, 0).WriteWord(16, 0).WriteWord(18, 0).WriteWord(20, 0);

    private static (NativeBattleEffect218Frame Frame, uint Address) AppendRequired(NativeBattleEffect218Frame frame, uint descriptor, uint callback)
    {
        (frame, uint address) = Append(frame, descriptor, callback);
        if (address == 0)
        {
            throw new InvalidDataException("Effect218 mandatory native task allocation failed.");
        }
        return (frame, address);
    }

    private static NativeBattleEffect218Frame Roll(NativeBattleEffect218Frame frame, out ushort value) => frame with { Random = frame.Random.Draw(out value) };
}
