using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleRetainedMemoryReader
{
    public static NativeBattleRetainedMemory ReadColdProcess(byte[] executable)
    {
        return ReadColdProcess(new NativeExecutableMemory(executable));
    }

    public static NativeBattleRetainedMemory ReadColdProcess(IGameNativeMemorySource memory)
    {
        string hash = memory.SourceHash;
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
        NativeBattleShivaRetainedVector ReadShivaVector(uint address)
        {
            byte[] vector = ReadLoaderBytes(address, 8);
            return new(BinaryPrimitives.ReadInt16LittleEndian(vector), BinaryPrimitives.ReadInt16LittleEndian(vector.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(vector.AsSpan(4)), BinaryPrimitives.ReadInt16LittleEndian(vector.AsSpan(6)));
        }
        NativeBattleEffectMemory effectMemory = NativeBattleEffectMemory.FromColdProcess() with
        {
            Parameter80 = ReadLoaderBytes(0x1D99A80, 1)[0],
            Parameter82 = ReadLoaderBytes(0x1D99A82, 1)[0],
            ReturnedTaskPointer1D96AAC = BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(0x1D96AAC, 4)),
            GuardianCasterBank = checked((byte)BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(0x1D96DC4, 4))),
            Shiva = new(null, null, null, BinaryPrimitives.ReadInt32LittleEndian(ReadLoaderBytes(0x22BD0E0, 4)), ReadLoaderBytes(0x22BD0E4, 1)[0], new(ReadShivaVector(0x22BC190), ReadShivaVector(0x22BC128)), new(ReadLoaderBytes(0x22BD018, 0x90).ToImmutableArray(), ReadLoaderBytes(0x22BCFD0, 0x34).ToImmutableArray(), null, null, null, null)) { LocalAnchor22BCFC8 = ReadShivaVector(0x22BCFC8), WorldAnchor22BCFC0 = ReadShivaVector(0x22BCFC0), BillboardMatrix22BD0E8 = ReadLoaderBytes(0x22BD0E8, 32).ToImmutableArray(), SpawnedChildD32508 = BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(0xD32508, 4)) },
            QuezacotlPause = BinaryPrimitives.ReadInt32LittleEndian(ReadLoaderBytes(0x25216DC, 4)),
            QuezacotlCompletion17A8 = ReadLoaderBytes(0x25217A8, 1)[0],
            QuezacotlTargetY1774 = BinaryPrimitives.ReadInt32LittleEndian(ReadLoaderBytes(0x2521774, 4)),
            ActionPhaseDEA = ReadLoaderBytes(0x1D28DEA, 1)[0],
            SequenceSine = BinaryPrimitives.ReadInt16LittleEndian(ReadLoaderBytes(0x1D981FC, 2)),
            SequenceCosine = BinaryPrimitives.ReadInt16LittleEndian(ReadLoaderBytes(0x1D981FE, 2)),
            SequenceParticlePreviousBone = ReadLoaderBytes(0x1D98208, 1)[0],
            Bio22Geometry = NativeBattleBio22Geometry.FromColdProcess(NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x1615CE4)),
            Bio22SecondaryGeometry = NativeBattleBio22Geometry.FromColdProcess(NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x1617E3C)),
            Bio22ThirdGeometry = NativeBattleBio22Geometry.FromColdProcess(NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x1619888)),
            Bio22Palette = NativeBattleBio22Palette.FromColdProcess(new("FF8_EN:16146E8", NativeBattleEffectStaticResourcesReader.Read(memory, 0x16146E8, 0x220))),
            Bio22FourthGeometry = NativeBattleBio22Geometry.FromColdProcess(NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x161F750)),
            DoubleMorph = new(NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x163FFD0u).Bytes),
            TripleMorph = new(NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x1627970u).Bytes),
            CureContextF4 = BinaryPrimitives.ReadInt16LittleEndian(ReadLoaderBytes(0x278C8DC, 2)),
            CuraContextF4 = BinaryPrimitives.ReadInt16LittleEndian(ReadLoaderBytes(0x270046C, 2)),
            CuraSceneFade = BinaryPrimitives.ReadInt32LittleEndian(ReadLoaderBytes(0x2700360, 4)),
            CuragaExecutable = new(BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(NativeBattleCuragaExecutableState.Address, 4)), null),
            EsunaExecutable = new(24, BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(NativeBattleRestorationProfile.Esuna.ExecutableAddress, 4)), null),
            FullLifeExecutable = new(27, BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(NativeBattleRestorationProfile.FullLife.ExecutableAddress, 4)), null),
            Effect57Executable = new(57, BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(NativeBattleRestorationProfile.Effect57.ExecutableAddress, 4)), null),
            RemedyExecutable = new(67, BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(NativeBattleRestorationProfile.Remedy.ExecutableAddress, 4)), null),
            RecoveryExecutables = new ushort[] { 53, 58, 59 }.Select(effect => NativeBattleRecoveryResourcesReader.ReadColdProcessState(memory, effect)).ToImmutableArray(),
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
        if (ReadLoaderBytes(0x1D97710, 6).Any(value => value != 0) || ReadLoaderBytes(0x1D97778, 44).Any(value => value != 0) || ReadLoaderBytes(0x1D8E038, 8).Any(value => value != 0) || ReadLoaderBytes(0x1CA92DC, 12).Any(value => value != 0) || camera.CurrentPose!.Eye != ReadPoint(0xB8B7F0) || camera.CurrentPose.Target != ReadPoint(0xB8B7F8))
        {
            throw new InvalidDataException("Authenticated cold camera/GTE owners contradict the loader image profile.");
        }
        byte[] emptyGeometry = ReadLoaderBytes(0x1D999C0, 4);
        if (emptyGeometry.Any(value => value != 0))
        {
            throw new InvalidDataException("Authenticated cold507F80 dummy geometry header contradicts its zero-filled loader image.");
        }
        NativeBattleRetainedMemory result = new(packets, allocation, mirrors, scratch, effectIoResult, hash) { ElementAttack = Enumerable.Range(0, 7).Select(index => (byte?)ReadLoaderBytes(checked(0x1D27BD5u + 208u * (uint)index), 1)[0]).ToImmutableArray(), EffectMemory = effectMemory, ModelTextureCache = NativeBattleModelTextureCache.FromColdProcess(), ModelCpuResources = NativeBattleModelCpuResources.FromColdProcess(), FileStaging = NativeBattleFileStaging.Unknown().Write(0x1D999C0, emptyGeometry), Stage = NativeBattleStageState.FromColdProcess(), Camera = camera, ActionMemory = actionMemory, FileIo = NativeBattleFileIoState.FromColdProcess() };
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
            return memory.ReadLoader(address, length);
        }
    }
}

public sealed partial class GameAssets
{
    private NativeBattleRetainedMemory? coldProcessNativeBattleMemory;

    public NativeBattleRetainedMemory ReadColdProcessNativeBattleMemory()
    {
        return coldProcessNativeBattleMemory ??= NativeBattleRetainedMemoryReader.ReadColdProcess(ReadNativeMemory());
    }
}
