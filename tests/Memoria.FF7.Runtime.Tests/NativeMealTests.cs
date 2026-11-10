using System.Buffers.Binary;
using Memoria.FF7.Runtime;

internal static class NativeMealTests
{
    public static void Run()
    {
        byte[] record = new byte[132];
        for (int offset = 64; offset < 128; offset += 4)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(offset), uint.MaxValue);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(44), 100);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(46), 340);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(48), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(50), 62);
        Ff7Progress progress = new() { InitialKernelData = [], Characters = [new(2, "Member", 1, 0, 100, 10, record)], Party = [2, 255, 255], Inventory = [], Materia = [] };
        short[] modifiers = new short[17 * 8];
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(56), ushort.MaxValue);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(58), ushort.MaxValue);
        Ff7PartyRecoveryResult published = Ff7NativePartyRecovery.Publish(progress, [], modifiers);
        Check(published.Progress.Characters[0].HitPoints == 100 && published.Progress.Characters[0].MagicPoints == 10 && BinaryPrimitives.ReadUInt16LittleEndian(published.Progress.Characters[0].NativeRecord.AsSpan(56)) == 340 && BinaryPrimitives.ReadUInt16LittleEndian(published.Progress.Characters[0].NativeRecord.AsSpan(58)) == 62, "Native menu statistics must replace missing maximum caches without healing.");
        Check(BinaryPrimitives.ReadUInt16LittleEndian(record.AsSpan(56)) == ushort.MaxValue, "Native menu statistics mutated an older source cache.");
        Ff7PartyRecoveryResult result = Ff7NativePartyRecovery.AddHitPoints(progress, 2, 10000, [], modifiers);
        Check(result.Progress.Characters[0].HitPoints == 340 && result.Progress.Characters[0].MagicPoints == 10, "HP+ must clamp to the native materia-adjusted maximum without recovering MP.");
        Check(progress.Characters[0].HitPoints == 100 && BinaryPrimitives.ReadUInt16LittleEndian(record.AsSpan(44)) == 100, "HP+ mutated its source record.");
        Check(Ff7NativePartyRecovery.AddHitPoints(progress, 255, 10000, [], modifiers).Progress.Characters[0].HitPoints == 100, "An empty script-party target must only publish native party statistics.");
        Check(Ff7NativePartyRecovery.AddHitPoints(progress, 2, -101, [], modifiers).Progress.Characters[0].HitPoints == 65535, "Native HP+ must preserve signed-word addition and its upper-bound-only clamp.");
        Check(Ff7NativePartyRecovery.AddHitPoints(progress, 2, short.MaxValue, [], modifiers).Progress.Characters[0].HitPoints == unchecked((ushort)(short)(100 + short.MaxValue)), "Native HP+ overflow must wrap before signed comparison.");
        Console.WriteLine("PASS native party HP publication, signed-word arithmetic, cap, absent target and source purity");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
