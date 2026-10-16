using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleRetainedMemoryReader
{
    public static NativeBattleRetainedMemory ReadColdProcess(byte[] executable)
    {
        string hash = Convert.ToHexString(SHA256.HashData(executable));
        if (hash != NativeFieldRuntimeProfile.FF8EnExecutableHash)
        {
            throw new NotSupportedException("Cold battle memory requires the authenticated English executable loader image.");
        }
        using PEReader reader = new(new MemoryStream(executable));
        byte[] counts = ReadLoaderBytes(0x1D280C0, 2);
        byte[] active = ReadLoaderBytes(0x1D99A50, 4);
        if (counts.Any(value => value != 0) || active.Any(value => value != 0))
        {
            throw new InvalidDataException("Authenticated cold battle counts/header pointer contradict the zero-filled image contract.");
        }
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromColdLoaderStorage(ReadLoaderBytes(0x1D280C4, 640), ReadLoaderBytes(0x1D28344, 1176));
        byte actionGate = ReadLoaderBytes(0x1D28DF9, 1)[0];
        byte pauseGate = ReadLoaderBytes(0x1D28DFD, 1)[0];
        if (actionGate > 1 || pauseGate > 1)
        {
            throw new InvalidDataException("Authenticated cold allocation gates exceed their original BYTE producers.");
        }
        NativeBattleAllocationControl allocation = new(ReadLoaderBytes(0x1D28E01, 1)[0], actionGate != 0, pauseGate != 0);
        byte[] saved = ReadLoaderBytes(0x1D28E30, 6);
        byte[] battle = ReadLoaderBytes(0x1D28E38, 12);
        NativeBattlePartyStatusMirrors mirrors = new([BinaryPrimitives.ReadUInt16LittleEndian(saved), BinaryPrimitives.ReadUInt16LittleEndian(saved.AsSpan(2)), BinaryPrimitives.ReadUInt16LittleEndian(saved.AsSpan(4))], [BinaryPrimitives.ReadUInt32LittleEndian(battle), BinaryPrimitives.ReadUInt32LittleEndian(battle.AsSpan(4)), BinaryPrimitives.ReadUInt32LittleEndian(battle.AsSpan(8))]);
        NativeBattleActionScratch scratch = new()
        {
            DeferredEffectsE00 = ReadLoaderBytes(0x1D28E00, 1)[0] != 0,
            GuardianFormulaE1E = ReadLoaderBytes(0x1D28E1E, 1)[0] != 0,
            GuardianBoostPercent = ReadLoaderBytes(0x1D2A23A, 1)[0],
            FinisherPendingE03 = ReadLoaderBytes(0x1D28E03, 1)[0] != 0,
            LastPublishedTargetCountE05 = ReadLoaderBytes(0x1D28E05, 1)[0],
            FinisherActionIndexE2E = ReadLoaderBytes(0x1D28E2E, 1)[0],
            ActorIndex = ReadLoaderBytes(0x1D27AD8, 1)[0],
            Command = ReadLoaderBytes(0x1D27AD9, 1)[0],
            Action = BinaryPrimitives.ReadUInt16LittleEndian(ReadLoaderBytes(0x1D27AF4, 2)),
            EventActorB0F = ReadLoaderBytes(0x1D27B0F, 1)[0],
            EventLane = ReadLoaderBytes(0x1D28DF2, 1)[0]
        };
        int effectIoResult = BinaryPrimitives.ReadInt32LittleEndian(ReadLoaderBytes(0x1D999C8, 4));
        NativeBattleRetainedMemory result = new(packets, allocation, mirrors, scratch, effectIoResult, hash) { ElementAttack = Enumerable.Range(0, 7).Select(index => (byte?)ReadLoaderBytes(checked(0x1D27BD5u + 208u * (uint)index), 1)[0]).ToImmutableArray() };
        result.Validate();
        return result;

        byte[] ReadLoaderBytes(uint address, int length)
        {
            long rva = address - (long)reader.PEHeaders.PEHeader!.ImageBase;
            SectionHeader section = reader.PEHeaders.SectionHeaders.Single(section => rva >= section.VirtualAddress && rva + length <= (long)section.VirtualAddress + section.VirtualSize);
            long sectionOffset = rva - section.VirtualAddress;
            byte[] bytes = new byte[length];
            int mapped = checked((int)Math.Clamp(section.SizeOfRawData - sectionOffset, 0, length));
            if (mapped != 0)
            {
                executable.AsSpan(checked((int)(section.PointerToRawData + sectionOffset)), mapped).CopyTo(bytes);
            }
            //The PE loader initializes the part of VirtualSize beyond SizeOfRawData to zero.
            return bytes;
        }
    }
}

public sealed partial class GameAssets
{
    private NativeBattleRetainedMemory? coldProcessNativeBattleMemory;

    public NativeBattleRetainedMemory ReadColdProcessNativeBattleMemory()
    {
        return coldProcessNativeBattleMemory ??= NativeBattleRetainedMemoryReader.ReadColdProcess(File.ReadAllBytes(Path.Combine(GameDirectory, "FF8_EN.exe")));
    }
}
