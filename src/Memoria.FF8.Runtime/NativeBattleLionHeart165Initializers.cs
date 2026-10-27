namespace Memoria.FF8.Runtime;

public static partial class NativeBattleLionHeart165Controller
{
    public static NativeBattleLionHeart165ChildStep RunInitializer(NativeBattleLionHeart165Frame frame, NativeBattleEffectTaskPool pool, uint initializer, NativeBattleLionHeart165Inputs inputs)
    {
        switch (initializer)
        {
            case 0x5E9DD0:
                Append(0x5E9E00);
                for (uint address = 0x23C6898; address < 0x23C6910; address += 20)
                {
                    frame = frame with { Staging = WriteDword(frame.Staging, address, 0) };
                }
                break;
            case 0x5EA850:
                NativeBattleVector actorBone = inputs.ReadBone(frame.Parameters.Actor78, 241, 0);
                frame = frame with { Staging = Point(frame.Staging, 0x23C6918, actorBone) };
                int opening = Append(0x5EA8E0);
                Set(opening, 16, actorBone.X);
                Set(opening, 18, 0);
                Set(opening, 20, actorBone.Z);
                Set(opening, 24, Rand() % 4096);
                Set(opening, 28, 6144);
                Set(opening, 34, 256);
                Set(opening, 32, 256);
                break;
            case 0x5EB890:
                Marker(CopyPoint(0x5EB8D0, 0x23C6930));
                break;
            case 0x5EBB90:
                Marker(CopyPoint(0x5EBBD0, 0x23C6930));
                break;
            case 0x5EB3E0:
                Marker(Append(0x5EB410));
                break;
            case 0x5ED440:
                int weaponColumn = Append(0x5ED300);
                NativeBattleVector weaponBone = inputs.ReadBone(frame.Parameters.Actor78, 240, 0);
                Set(weaponColumn, 16, weaponBone.X);
                Set(weaponColumn, 18, weaponBone.Y);
                Set(weaponColumn, 20, weaponBone.Z);
                Set(weaponColumn, 24, Rand() % 4096);
                Set(weaponColumn, 28, 3072);
                int rise = (Rand() % 512 + 1152) / 3;
                Set(weaponColumn, 34, rise);
                Set(weaponColumn, 32, rise);
                break;
            case 0x5EDBC0:
                CaptureTip();
                int aligned = CopyPoint(0x5EDC10, 0x23C6930);
                Set(aligned, 14, 2);
                Set(aligned, 28, 4096);
                Set(aligned, 32, 8192);
                break;
            case 0x5E9D20:
                int stage = Append(0x5E9D50);
                Set(stage, 14, 16);
                Set(stage, 24, 2048);
                Set(stage, 26, 1);
                break;
            case 0x5EAA50:
                int first = Append(0x5EAAF0);
                Marker(first);
                Set(first, 16, ReadWord(frame.Staging, 0x23C6918));
                Set(first, 18, 0);
                Set(first, 20, ReadWord(frame.Staging, 0x23C691C));
                int second = CopyPoint(0x5EAD50, 0x23C6918);
                Marker(second);
                Marker(Append(0x5EB070));
                break;
            case 0x5EB6D0:
                CaptureTip();
                int glow = CopyPoint(0x5EB790, 0x23C6930);
                Set(glow, 28, 149);
                Set(glow, 30, 149);
                break;
            case 0x5EBEF0:
                for (int side = 0; side < 2; side++)
                {
                    int flare = CopyPoint(0x5EBFC0, 0x23C6930);
                    Set(flare, 14, side);
                    Set(flare, 24, Rand() % 4096);
                    int speed = Rand() % 60 + 30;
                    Set(flare, 30, 3072);
                    Set(flare, 28, 3072);
                    Set(flare, 26, speed);
                }
                break;
            case 0x5ED270:
                int column = Append(0x5ED300);
                NativeBattleModuleAnchor position = inputs.ReadPosition(frame.FirstRecipient);
                Set(column, 16, position.X);
                Set(column, 18, position.Y);
                Set(column, 20, position.Z);
                Set(column, 24, Rand() % 4096);
                Set(column, 28, 3584);
                int height = (Rand() % 768 + 1536) / 3;
                Set(column, 34, height);
                Set(column, 32, height);
                break;
            case 0x5ED700:
                CaptureTip();
                int disk = CopyPoint(0x5ED760, 0x23C6930);
                Set(disk, 24, Rand() % 4096);
                Set(disk, 26, 64);
                Set(disk, 30, 149);
                Set(disk, 28, 149);
                break;
            case 0x5ED8A0:
                int rings = CopyPoint(0x5ED8E0, 0x23C6930);
                Marker(rings);
                break;
            case 0x5EDE90:
            case 0x5EEC00:
                return RunCompoundInitializer(frame, pool, initializer, inputs);
            default:
                throw new NotSupportedException($"Native Lion Heart initializer0x{initializer:X} has no original implementation.");
        }
        return new(frame, pool, 0);

        int Append(uint callback)
        {
            (pool, int created) = pool.Append(callback);
            pool = pool.WriteSlot(created, pool.Slots[created].WriteWord(12, 0));
            return created;
        }
        int CopyPoint(uint callback, uint source)
        {
            int created = Append(callback);
            NativeBattleModulePoolTask task = CopyStagingToTask(frame.Staging, source, pool.Slots[created], 16, 8);
            pool = pool.WriteSlot(created, task);
            return created;
        }
        void Marker(int created)
        {
            int marker = ReadDword(frame.Staging, 0x23C6940);
            Set(created, 14, marker);
            frame = frame with { Staging = WriteDword(frame.Staging, 0x23C6940, unchecked((uint)(marker + 1))) };
        }
        void CaptureTip()
        {
            NativeBattleRenzokuken160WeaponTip tip = inputs.ReadWeaponTip(frame.Parameters.Actor78);
            NativeBattleVector point = new(unchecked((short)(tip.Position.X + ((380 * tip.DirectionX) >> 12))), unchecked((short)(tip.Position.Y + ((380 * tip.DirectionY) >> 12))), unchecked((short)(tip.Position.Z + ((380 * tip.DirectionZ) >> 12))));
            frame = frame with { Staging = Point(frame.Staging, 0x23C6930, point) };
        }
        void Set(int created, int offset, int value)
        {
            pool = pool.WriteSlot(created, pool.Slots[created].WriteWord(offset, unchecked((ushort)value)));
        }
        int Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
    }
}
