using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class BattleAssets
{
    public NativeBattleStatusRules ReadNativeStatusRules(byte battleSpeed)
    {
        byte[] durations = battleKernel.TimerSettings.StatusTimers;
        if (durations.Length != 14)
        {
            throw new InvalidDataException("Original kernel status timer table requires fourteen bytes.");
        }
        return new(battleSpeed, durations.ToImmutableArray());
    }

    public NativeBattleStatusAllocation ReadEnemyNativeStatusData(int modelId)
    {
        byte[] bytes = Read($"c0m{modelId:000}.dat");
        int count = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        if (count is not (2 or 11))
        {
            throw new NotSupportedException("Native enemy statuses require the original monster directory.");
        }
        int information = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(count == 11 ? 28 : 4));
        if (information < 0 || information > bytes.Length - 379)
        {
            throw new InvalidDataException("Native enemy status information exceeds its original resource.");
        }
        ReadOnlySpan<byte> data = bytes.AsSpan(information);
        byte[] resistance = Enumerable.Repeat((byte)100, 40).ToArray();
        int[] indices = [0, 1, 2, 3, 4, 5, 6, 8, 9, 10, 11, 12, 15, 18, 20, 21, 22, 23, 24];
        for (int index = 0; index < indices.Length; index++)
        {
            resistance[indices[index]] = data[360 + index];
        }
        byte flags = data[247];
        ushort saved = (ushort)((flags & 1) != 0 ? 0x40 : 0);
        uint battle = (flags & 2) != 0 ? 0x2000u : 0;
        if ((flags & 0x20) != 0)
        {
            battle |= 0x80;
        }
        if ((flags & 0x40) != 0)
        {
            battle |= 0x40;
        }
        if ((flags & 0x80) != 0)
        {
            battle |= 0x20;
        }
        if ((flags & 1) != 0)
        {
            resistance[6] = 255;
        }
        return new(saved, battle, resistance.ToImmutableArray());
    }

    public NativeBattleStatusAllocation ReadPlayerNativeStatusData(NativeCharacterVitals vitals, int row)
    {
        vitals.Validate();
        NativeCharacterVital character = vitals.Records[row];
        BattleNativePlayerStatRules rules = ReadPlayerNativeStatRules(vitals, row);
        ImmutableArray<byte> resistance = rules.ProjectStatusResistances(vitals.GetMagicInventory(row));
        uint passiveFlags = rules.NativeStatusPassiveFlags!.Value;
        uint battle = (passiveFlags & 0x1000) != 0 ? 0x80u : 0;
        if ((passiveFlags & 0x4000) != 0)
        {
            battle |= 0x20;
        }
        if ((passiveFlags & 0x2000) != 0)
        {
            battle |= 0x40;
        }
        if ((passiveFlags & 0x8000) != 0)
        {
            battle |= 2;
        }
        return new(character.Status, battle, resistance);
    }
}
