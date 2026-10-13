using System.Buffers.Binary;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class WorldAssets
{
    private NativeBoatWorldFootRecord? scriptedBoatFootRecord;

    public NativeBoatWorldFootRecord ReadScriptedBoatFootRecord(ushort story)
    {
        if (story is < 34 or >= 205)
        {
            throw new NotSupportedException("Native boat story lies outside the proved original allocation clause.");
        }
        if (scriptedBoatFootRecord is not null)
        {
            return scriptedBoatFootRecord;
        }
        ReadOnlySpan<byte> program = wmset.AsSpan(sections[9], sections[10] - sections[9]);
        if (program.Length < 132 || BinaryPrimitives.ReadInt32LittleEndian(program) != 84 || BinaryPrimitives.ReadInt32LittleEndian(program[4..]) != 108)
        {
            throw new NotSupportedException("Native scripted boat requires the proved original section9 first two story clauses.");
        }
        ReadOnlySpan<byte> firstClause = program.Slice(84, 24);
        ReadOnlySpan<byte> clause = program.Slice(108, 24);
        ushort[] expectedFirst = [0xFF01, 0, 0xFF02, 0, 0xFF03, 34, 0xFF04, 0, 0xFF13, 0, 0xFF05, 0];
        ushort[] expected = [0xFF01, 0, 0xFF02, 34, 0xFF03, 205, 0xFF04, 0, 0xFF13, 4, 0xFF05, 0];
        for (int index = 0; index < expected.Length; index++)
        {
            if (BinaryPrimitives.ReadUInt16LittleEndian(firstClause[(index * 2)..]) != expectedFirst[index] || BinaryPrimitives.ReadUInt16LittleEndian(clause[(index * 2)..]) != expected[index])
            {
                throw new NotSupportedException("Native boat static foot-record allocation differs from its original selected story clause.");
            }
        }
        if (story is < 34 or >= 205 || sections[11] - sections[10] < 16)
        {
            throw new NotSupportedException("Native boat story or original coordinate table lies outside the proved allocation.");
        }
        ReadOnlySpan<byte> coordinate = wmset.AsSpan(sections[10], 16);
        int x = BinaryPrimitives.ReadInt32LittleEndian(coordinate);
        int height = BinaryPrimitives.ReadInt32LittleEndian(coordinate[4..]);
        int graphicsZ = BinaryPrimitives.ReadInt32LittleEndian(coordinate[8..]);
        // 544860 writes angles via v88;549E80 stores the record's highWORD yaw.
        ushort yaw = BinaryPrimitives.ReadUInt16LittleEndian(coordinate[12..]);
        var result = new NativeBoatWorldFootRecord(1, NativeBoatWorldFootRecord.AllocationRule, Convert.ToHexString(SHA256.HashData(program)), Convert.ToHexString(SHA256.HashData(coordinate)), 34, 205, new NativeSavedWorldPose(x, unchecked(-graphicsZ), unchecked((short)height), yaw));
        result.Validate();
        scriptedBoatFootRecord = result;
        return result;
    }
}
