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
        byte[] effectAllocator = ReadLoaderBytes(0x21DFAB8, 8 + 256 * 4);
        if (effectAllocator.Any(value => value != 0))
        {
            throw new InvalidDataException("Authenticated cold effect allocator unexpectedly contains a live heap pointer or cursor.");
        }
        RequireSingle(0xB69544, 1);
        RequireSingle(0xB69568, 0);
        RequireSingle(0xB695E0, 1f / 65536);
        RequireSingle(0xB69608, NativeBattlePrimitiveDepthState.DepthBias);
        RequireSingle(0xB69618, NativeBattlePrimitiveDepthState.VertexDepthScale);
        RequireSingle(0xB7CEE0, 1f / 65536);
        RequireSingle(0xB7CEE4, 0.99f);
        RequireSingle(0x1CA9E9C, 0);
        RequireSingle(0x1CB6038, 0);
        RequireSingle(0x1CB603C, 0);
        if (BitConverter.ToDouble(ReadLoaderBytes(0xB695E8, 8)) != 0.9985 || BitConverter.ToDouble(ReadLoaderBytes(0xB695F0, 8)) != 1d / 65536 || BitConverter.ToDouble(ReadLoaderBytes(0xB69610, 8)) != 0.99999999 || BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(0x1CFF834, 4)) != 0 || BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(0x1CA8A00, 4)) != 0 || BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(0x1CA89E8, 4)) != 0)
        {
            throw new InvalidDataException("Authenticated cold primitive depth/OT reset globals contradict the executable profile.");
        }
        NativeBattleEffectMemory effectMemory = NativeBattleEffectMemory.FromColdProcess() with
        {
            CureContextF4 = BinaryPrimitives.ReadInt16LittleEndian(ReadLoaderBytes(0x278C8DC, 2)),
            CuraContextF4 = BinaryPrimitives.ReadInt16LittleEndian(ReadLoaderBytes(0x270046C, 2)),
            CuraSceneFade = BinaryPrimitives.ReadInt32LittleEndian(ReadLoaderBytes(0x2700360, 4)),
            CuragaExecutable = new(BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(NativeBattleCuragaExecutableState.Address, 4)), null),
            EsunaExecutable = new(24, BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(NativeBattleRestorationProfile.Esuna.ExecutableAddress, 4)), null),
            FullLifeExecutable = new(27, BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(NativeBattleRestorationProfile.FullLife.ExecutableAddress, 4)), null),
            QuezacotlExecutableMemory = new(ReadLoaderBytes(NativeBattleQuezacotlExecutableMemory.Address, NativeBattleQuezacotlExecutableMemory.Length).ToImmutableArray()),
            Active = BinaryPrimitives.ReadInt32LittleEndian(ReadLoaderBytes(0x1D99A64, 4)),
            SelectedIndex = BinaryPrimitives.ReadInt32LittleEndian(ReadLoaderBytes(0x1D99A68, 4)),
            SoundPendingFlags = BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(0x1D96DB4, 4)),
            SoundPublishedFlags = BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(0x1D96DB8, 4)),
            SharedEffectRandom = new(BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(0x2557098, 4))),
            ModelColorOverrides = new(Enumerable.Range(0, 2).Select(index =>
            {
                uint packed = BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(0xB8BA00u + (uint)index * 4, 4));
                return new NativeBattleModelColorOverride((byte)(packed >> 24), packed & 0xFFFFFF);
            }).ToImmutableArray()),
            PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess() with { TexturePage = BinaryPrimitives.ReadUInt16LittleEndian(ReadLoaderBytes(0x1C48818, 2)), Buffer80 = ReadLoaderBytes(0x1D96A80, 1)[0], TextureWindowBits = BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(0x1CA8848, 4)) }
        };
        if (effectMemory.SharedEffectRandom!.State != 0)
        {
            throw new InvalidDataException("Authenticated cold7059E0 shared random DWORD2557098 contradicts its zero-filled loader storage.");
        }
        if (effectMemory.ModelColorOverrides!.Slots[0] != new NativeBattleModelColorOverride(255, 0) || effectMemory.ModelColorOverrides.Slots[1] != new NativeBattleModelColorOverride(0, 0))
        {
            throw new InvalidDataException("Authenticated cold B8BA00/B8BA04 color overrides contradict their distinct FF000000/00000000 image values.");
        }
        if (ReadLoaderBytes(0x209FAB8, NativeBattleGuardianWorkBuffer.Length).Any(value => value != 0) || ReadLoaderBytes(0x1DCD6E4, 12).Any(value => value != 0) || ReadLoaderBytes(0x1D96DC4, 4).Any(value => value != 0) || ReadLoaderBytes(0x25216DC, 4).Any(value => value != 0) || ReadLoaderBytes(0x2521774, 4).Any(value => value != 0) || ReadLoaderBytes(0x1D96DB0, 4).Any(value => value != 0) || ReadLoaderBytes(0x1D96DC0, 4).Any(value => value != 0) || new uint[] { 0x2521728, 0x2521718, 0x2521708, 0x25216F8, 0x25216E8 }.Any(address => ReadLoaderBytes(address, 4).Any(value => value != 0)))
        {
            throw new InvalidDataException("Authenticated cold571B70 work bank/effect IO pointer globals contradict their separate zero-filled loader storage.");
        }
        if (ReadLoaderBytes(0x1D29638, 16 * 4).Any(value => value != 0) || ReadLoaderBytes(0x1D29A08, 16 * 12).Any(value => value != 0) || ReadLoaderBytes(0x1D29AD0, 2).Any(value => value != 0) || ReadLoaderBytes(0x1D2A200, 2).Any(value => value != 0) || ReadLoaderBytes(0x1D2A1FA, 2).Any(value => value != 0))
        {
            throw new InvalidDataException("Authenticated cold482590 callback/row/count/current/delay globals contradict their zero-filled process image.");
        }
        NativeBattleActionMemory actionMemory = new(Enumerable.Range(0, 7).Select(index => ReadLoaderBytes(checked(0x1D27B98u + 208u * (uint)index), 8).Select(value => (byte?)value).ToImmutableArray()).ToImmutableArray());
        if (ReadLoaderBytes(0x1D99768, 11 * 52).Any(value => value != 0) || ReadLoaderBytes(0x1D98B54, 2).Any(value => value != 0))
        {
            throw new InvalidDataException("Authenticated cold texture-resource globals contradict their zero-filled process image.");
        }
        if (ReadLoaderBytes(0x1D98B60, 0xC00).Any(value => value != 0))
        {
            throw new InvalidDataException("Authenticated cold507120 saved-palette rows contradict their six512-byte zero-filled loader allocations.");
        }
        if (ReadLoaderBytes(0x1D98988, 0xCC).Any(value => value != 0) || ReadLoaderBytes(0x1D96DA4, 4).Any(value => value != 0))
        {
            throw new InvalidDataException("Authenticated cold stage globals contradict the loader's zero-filled process image.");
        }
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess();
        if (new uint[] { 0xB8B7F6, 0xB8B7FE, 0xB8B806, 0xB8B80E, 0x1D977C2, 0x1D977CA, 0x1D97CE6, 0x1D97CEE }.Any(address => BinaryPrimitives.ReadUInt16LittleEndian(ReadLoaderBytes(address, 2)) != 0))
        {
            throw new InvalidDataException("Authenticated camera vector padding contradicts its separate static/BSS WORD owners.");
        }
        if (ReadLoaderBytes(0x1D97778, 44).Any(value => value != 0) || ReadLoaderBytes(0x1D8E038, 8).Any(value => value != 0) || ReadLoaderBytes(0x1CA92DC, 12).Any(value => value != 0) || camera.CurrentPose!.Eye != ReadPoint(0xB8B7F0) || camera.CurrentPose.Target != ReadPoint(0xB8B7F8))
        {
            throw new InvalidDataException("Authenticated cold camera/GTE owners contradict the loader image profile.");
        }
        NativeBattleRetainedMemory result = new(packets, allocation, mirrors, scratch, effectIoResult, hash) { ElementAttack = Enumerable.Range(0, 7).Select(index => (byte?)ReadLoaderBytes(checked(0x1D27BD5u + 208u * (uint)index), 1)[0]).ToImmutableArray(), EffectMemory = effectMemory, ModelTextureCache = NativeBattleModelTextureCache.FromColdProcess(), ModelCpuResources = NativeBattleModelCpuResources.FromColdProcess(), Stage = NativeBattleStageState.FromColdProcess(), Camera = camera, ActionMemory = actionMemory, FileIo = NativeBattleFileIoState.FromColdProcess() };
        result.Validate();
        return result;

        NativeBattleCameraPoint ReadPoint(uint address)
        {
            byte[] bytes = ReadLoaderBytes(address, 6);
            return new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)));
        }

        void RequireSingle(uint address, float expected)
        {
            if (BitConverter.ToSingle(ReadLoaderBytes(address, 4)) != expected)
            {
                throw new InvalidDataException($"Authenticated primitive depth constant{address:X8} contradicts the executable profile.");
            }
        }

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
