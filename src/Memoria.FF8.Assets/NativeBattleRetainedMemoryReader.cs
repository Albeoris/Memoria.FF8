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
        NativeBattleScanByteOwner ReadScanOwner(uint address, int length) => new(ReadLoaderBytes(address, length).ToImmutableArray(), Enumerable.Repeat(true, length).ToImmutableArray());
        NativeBattleScanTextProcess scanText = new(new(Enumerable.Range(0, 8).Select(index => ReadScanOwner(checked(0x1D2B330u + (uint)(index * 60)), 60)).ToImmutableArray(), Enumerable.Range(0, 8).ToImmutableDictionary(index => (byte)index, index => ReadScanOwner(checked(0x1D2B550u + (uint)(index * 64)), 64))), new(ReadScanOwner(0x1A79008, 0x2E), ReadScanOwner(0x1A79038, 0x50)), ReadLoaderBytes(0x1D7660C, 1)[0], unchecked((sbyte)ReadLoaderBytes(0x1D2B51C, 1)[0]), ReadScanOwner(0x1D2B524, 12), BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(0x1D76608, 4)), false);
        NativeBattleEffectMemory effectMemory = NativeBattleEffectMemory.FromColdProcess() with
        {
            Scan = new(null, new(new([ReadScanOwner(0x1D2B110, 196), ReadScanOwner(0x1D2B1D4, 196)]), [ReadScanOwner(0x1D2B298, 36), ReadScanOwner(0x1D2B2BC, 36)], ReadScanOwner(0x1D2B2F0, 4), ReadScanOwner(0x209CEE4, 12)), BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(0x1CD01FC, 4)), false, ReadScanOwner(0x269AAA8, 48))
            {
                Text = scanText,
                Mouse1D2B2E0 = ReadScanOwner(0x1D2B2E0, 16),
                MutableMeshes = new[] { (Address: 0x15CDDECu, Length: 0x3E8), (Address: 0x15CE1D4u, Length: 0x768) }.ToImmutableDictionary(item => item.Address, item => ReadScanOwner(item.Address, item.Length)),
                Results = new(ReadScanOwner(0x279CC68, 8), ReadScanOwner(0x279CC70, 32), BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(0x279CC90, 4)), ReadLoaderBytes(0x279CC94, 1)[0], ReadScanOwner(0x1D2A248, 48), ReadLoaderBytes(0x1D2A278, 1)[0], ReadScanOwner(0x1D29034, 768)),
                Complete269D93A = BinaryPrimitives.ReadUInt16LittleEndian(ReadLoaderBytes(0x269D93A, 2)),
                Presentation = new(ReadScanOwner(0x269A168, 0x50), ReadScanOwner(0x269A1BC, 28), ReadScanOwner(0x269A1D8, 28), ReadScanOwner(0x269CE34, 28), ReadScanOwner(0x269AA78, 24), BinaryPrimitives.ReadUInt16LittleEndian(ReadLoaderBytes(0x269AA90, 2)), BinaryPrimitives.ReadUInt16LittleEndian(ReadLoaderBytes(0x269D938, 2)), BinaryPrimitives.ReadUInt16LittleEndian(ReadLoaderBytes(0x269CE2C, 2)), BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(0x269CE30, 4)))
            },
            ModuleSpriteBillboard21DFED0 = new(ReadLoaderBytes(0x21DFED0, 32).ToImmutableArray(), Enumerable.Repeat(true, 32).ToImmutableArray()),
            GeometryRegisters = NativeBattleGeometryRegisters.Unknown() with { AnchorScratch8 = ReadLoaderBytes(0x1D97708, 8).Select(value => (byte?)value).ToImmutableArray() },
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
            Potion50ContextF4 = BinaryPrimitives.ReadInt16LittleEndian(ReadLoaderBytes(0x26464D4, 2)),
            CuraContextF4 = BinaryPrimitives.ReadInt16LittleEndian(ReadLoaderBytes(0x270046C, 2)),
            CuraSceneFade = BinaryPrimitives.ReadInt32LittleEndian(ReadLoaderBytes(0x2700360, 4)),
            Potion51ContextF4 = BinaryPrimitives.ReadInt16LittleEndian(ReadLoaderBytes(0x2634AB4, 2)),
            Potion51SceneFade = BinaryPrimitives.ReadInt32LittleEndian(ReadLoaderBytes(0x26349A8, 4)),
            CuragaExecutable = new(BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(NativeBattleCuragaExecutableState.Address, 4)), null),
            Potion52Executable = ReadPotion52Executable(),
            DrinkMagic37 = new(null, NativeBattleEffectArena.Clear().Write(0, ReadLoaderBytes(NativeBattleDrinkMagic37Profile.OrbAddress, NativeBattleDrinkMagic37Profile.OrbLength)), ReadLoaderBytes(0x26A8D80, 8).ToImmutableArray())
            {
                Counter26A8DA8 = BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(0x26A8DA8, 4)),
                Counter26A7D9C = BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(0x26A7D9C, 4))
            },
            DrinkMagic37Executable = new(BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(NativeBattleDrinkMagic37Profile.ExecutableAddress, 4)), null) { ResourceAddress = NativeBattleDrinkMagic37Profile.ExecutableAddress },
            Raldo19 = new(null, ReadLoaderBytes(NativeBattleRaldo19Profile.SharedAddress, 84).ToImmutableArray(), NativeBattleEffectArena.Clear().Write(0, ReadLoaderBytes(NativeBattleRaldo19Profile.BallAddress, 0xC4)), NativeBattleEffectArena.Clear().Write(0, ReadLoaderBytes(NativeBattleRaldo19Profile.BodyScratchAddress, NativeBattleRaldo19Profile.BodyScratchLength)), NativeBattleEffectArena.Clear().Write(0, ReadLoaderBytes(NativeBattleRaldo19Profile.DescriptorAddress, 48)), NativeBattleEffectArena.Clear().Write(0, ReadLoaderBytes(NativeBattleRaldo19Profile.VertexScratchAddress, NativeBattleRaldo19Profile.VertexScratchLength)))
            {
                Counter273048C = BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(0x273048C, 4)),
                Counter272DDDC = BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(0x272DDDC, 4))
            },
            Raldo19Executable = new(BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(NativeBattleRaldo19Profile.ExecutableAddress, 4)), null) { ResourceAddress = NativeBattleRaldo19Profile.ExecutableAddress },
            Effect20 = NativeBattleEffect20State.FromColdProcess(BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(0x27268F8, 4)), BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(0x2722960, 4))),
            Absorb56Bank = NativeBattleDrain39BankState.FromColdProcess(),
            Devour62 = NativeBattleDevour62State.FromColdProcess(BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(0x2601918, 4)), BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(0x2601770, 4))),
            MeltingBubble43 = NativeBattleMeltingBubble43State.FromColdProcess(BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(0x2656220, 4)), BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(0x265587C, 4)), NativeBattleMeltingBubble43Profile.ChannelRegions.SelectMany(region => ReadLoaderBytes(region.Address, region.Length)).ToImmutableArray()),
            MeltingBubble43Executable = new(BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(0x15C6080, 4)), ReadLoaderBytes(0x15C6080, 260).ToImmutableArray()) { ResourceAddress = 0x15C6080 },
            WindBlast29 = NativeBattleWindBlast29State.FromColdProcess(BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(0x26D74E8, 4)), BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(0x26D6B44, 4))),
            WindBlast29Executable = new(BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(0x17D27DC, 4)), null) { ResourceAddress = 0x17D27DC },
            Effect60 = NativeBattleEffect60State.FromColdProcess(ReadLoaderBytes(NativeBattleEffect60Profile.SharedAddress, 84).ToImmutableArray(), ReadLoaderBytes(NativeBattleEffect60Profile.SavedRotationAddress, 8).ToImmutableArray(), ReadLoaderBytes(NativeBattleEffect60Profile.MorphScratchAddress, NativeBattleEffect60Profile.MorphScratchLength).ToImmutableArray(), BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(0x2607050, 4)), BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(0x2605DD4, 4))),
            Effect60PrimaryExecutable = new(BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(0x1797CC0, 4)), null) { ResourceAddress = 0x1797CC0 },
            Effect60SecondaryExecutable = new(BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(0x179C998, 4)), null) { ResourceAddress = 0x179C998 },
            PsychoBlast23 = NativeBattlePsychoBlast23State.FromColdProcess(ReadLoaderBytes(NativeBattlePsychoBlast23Profile.SharedAddress, 84).ToImmutableArray(), ReadLoaderBytes(NativeBattlePsychoBlast23Profile.BasisAddress, 56).ToImmutableArray(), ReadLoaderBytes(NativeBattlePsychoBlast23Profile.MorphScratchAddress, NativeBattlePsychoBlast23Profile.MorphScratchLength).ToImmutableArray(), BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(0x27044D0, 4)), BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(0x2703654, 4))),
            PsychoBlast23Executable = new(BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(0x17EA4F0, 4)), null) { ResourceAddress = 0x17EA4F0 },
            MightyGuard73 = NativeBattleMightyGuardState.FromColdProcess(73) with { InitializationFlag = BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(0x178557C, 4)) },
            MightyGuard78 = NativeBattleMightyGuardState.FromColdProcess(78) with { InitializationFlag = BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(0x176E194, 4)) },
            DrawStock15Executable = ReadDrawStock15Executable(),
            ActorDirector16Executable = ReadActorDirectorExecutable(16),
            ActorDirector61Executable = ReadActorDirectorExecutable(61),
            ActorDirector81Executable = ReadActorDirectorExecutable(81),
            ActorDirector82Executable = ReadActorDirectorExecutable(82),
            ActorDirector81 = NativeBattleActorDirectorState.FromColdProcess(81) with { Shared84 = ReadLoaderBytes(NativeBattleActorDirectorProfile.ForEffect(81).SharedAddress, 84).ToImmutableArray() },
            ActorDirector82 = NativeBattleActorDirectorState.FromColdProcess(82) with { Shared84 = ReadLoaderBytes(NativeBattleActorDirectorProfile.ForEffect(82).SharedAddress, 84).ToImmutableArray() },
            ActorDirector16 = NativeBattleActorDirectorState.FromColdProcess(16) with { Shared84 = ReadLoaderBytes(NativeBattleActorDirectorProfile.ForEffect(16).SharedAddress, 84).ToImmutableArray() },
            ActorDirector61 = NativeBattleActorDirectorState.FromColdProcess(61) with { Shared84 = ReadLoaderBytes(NativeBattleActorDirectorProfile.ForEffect(61).SharedAddress, 84).ToImmutableArray() },
            Aqua55 = new(55, ReadLoaderBytes(0x26163B0, 84).ToImmutableArray(), memory.Read(0x15A4168, 10600).ToImmutableArray()),
            Aqua72 = new(72, ReadLoaderBytes(0x25FA370, 84).ToImmutableArray(), memory.Read(0x158A4BC, 10600).ToImmutableArray()),
            EsunaExecutable = new(24, BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(NativeBattleRestorationProfile.Esuna.ExecutableAddress, 4)), null),
            FullLifeExecutable = new(27, BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(NativeBattleRestorationProfile.FullLife.ExecutableAddress, 4)), null),
            Effect57Executable = new(57, BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(NativeBattleRestorationProfile.Effect57.ExecutableAddress, 4)), null),
            RemedyExecutable = new(67, BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(NativeBattleRestorationProfile.Remedy.ExecutableAddress, 4)), null),
            RecoveryExecutables = new ushort[] { 7, 53, 58, 59 }.Select(effect => NativeBattleRecoveryResourcesReader.ReadColdProcessState(memory, effect)).ToImmutableArray(),
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
            PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess() with { DrawMode2795BD0 = BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(0x2795BD0, 4)), TexturePage = BinaryPrimitives.ReadUInt16LittleEndian(ReadLoaderBytes(0x1C48818, 2)), Buffer80 = ReadLoaderBytes(0x1D96A80, 1)[0], TextureWindowBits = BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(0x1CA8848, 4)) }
        };
        NativeBattleCuragaExecutableState ReadActorDirectorExecutable(ushort effect)
        {
            NativeBattleActorDirectorProfile profile = NativeBattleActorDirectorProfile.ForEffect(effect);
            uint flag = BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(profile.SourceAddress, 4));
            NativeBattleCuragaExecutableState retained = new(flag, flag == 0 ? null : ReadLoaderBytes(profile.SourceAddress, profile.SourceLength).ToImmutableArray()) { ResourceAddress = profile.SourceAddress };
            retained.Validate();
            return retained;
        }

        NativeBattleCuragaExecutableState ReadDrawStock15Executable()
        {
            const uint address = 0x17FC68C;
            uint flag = BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(address, 4));
            NativeBattleCuragaExecutableState retained = new(flag, flag == 0 ? null : ReadLoaderBytes(address, 0x5944).ToImmutableArray()) { ResourceAddress = address };
            retained.Validate();
            return retained;
        }

        NativeBattleCuragaExecutableState ReadPotion52Executable()
        {
            const uint address = 0x17AB0E8;
            uint flag = BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(address, 4));
            NativeBattleCuragaExecutableState retained = new(flag, flag == 0 ? null : ReadLoaderBytes(address, NativeBattleCuragaExecutableState.Length).ToImmutableArray()) { ResourceAddress = address };
            retained.Validate();
            return retained;
        }

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
        if (ReadLoaderBytes(0x1D97710, 6).Any(value => value != 0) || ReadLoaderBytes(0x1D97778, 44).Any(value => value != 0) || ReadLoaderBytes(0x1D8E038, 8).Any(value => value != 0) || ReadLoaderBytes(0x1CA92DC, 12).Any(value => value != 0) || camera.CurrentPose!.Eye != ReadPoint(0xB8B7F0) || camera.CurrentPose.Target != ReadPoint(0xB8B7F8) || camera.CapturedPose!.Eye != ReadPoint(0xB8B800) || camera.CapturedPose.Target != ReadPoint(0xB8B808) || camera.CapturedPose.FocalLength != BinaryPrimitives.ReadInt16LittleEndian(ReadLoaderBytes(0x1D977A0, 2)) || camera.CapturedPose.Roll != BinaryPrimitives.ReadInt16LittleEndian(ReadLoaderBytes(0x1D9771C, 2)))
        {
            throw new InvalidDataException("Authenticated cold camera/GTE owners contradict the loader image profile.");
        }
        byte[] emptyGeometry = ReadLoaderBytes(0x1D999C0, 4);
        if (emptyGeometry.Any(value => value != 0))
        {
            throw new InvalidDataException("Authenticated cold507F80 dummy geometry header contradicts its zero-filled loader image.");
        }
        NativeBattleActionMatrix ReadModelWorldMatrix(uint address)
        {
            byte[] bytes = ReadLoaderBytes(address, 32);
            return new(Enumerable.Range(0, 9).Select(lane => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(lane * 2, 2))).ToImmutableArray(), BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(20)), BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(24)), BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(28)), 4096);
        }
        ImmutableArray<ImmutableArray<short?>> sequenceLocals = Enumerable.Range(0, 7).Select(index =>
        {
            byte[] bytes = ReadLoaderBytes(checked(0x1D96EE4u + 64u * (uint)index), 16);
            if (bytes.Any(value => value != 0))
            {
                throw new InvalidDataException("Authenticated cold sequence local WORDs contradict their zero-filled static header storage.");
            }
            return Enumerable.Range(0, 8).Select(lane => (short?)BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(lane * 2, 2))).ToImmutableArray();
        }).ToImmutableArray();
        NativeBattleRetainedMemory result = new(packets, allocation, mirrors, scratch, effectIoResult, hash) { FieldTexturePlaneCacheFresh = true, ModelSequenceLocals = sequenceLocals, ModelTransformRows = Enumerable.Range(0, 7).Select(index => NativeBattleModelRetainedFields.FromLoaderBytes(ReadLoaderBytes(checked(0x1D972C0u + 156u * (uint)index), 156))).ToImmutableArray(), ModelStatusTimers = Enumerable.Range(0, 7).Select(index => ReadLoaderBytes(checked(0x1D97100u + 32u * (uint)index), 32).Select(value => (byte?)value).ToImmutableArray()).ToImmutableArray(), ModelWorldMatrices64 = Enumerable.Range(0, 7).Select(index => (NativeBattleActionMatrix?)ReadModelWorldMatrix(checked(0x1D97300u + 156u * (uint)index))).ToImmutableArray(), ModelColors40 = Enumerable.Range(0, 7).Select(index => (uint?)BinaryPrimitives.ReadUInt32LittleEndian(ReadLoaderBytes(checked(0x1D972E8u + 156u * (uint)index), 4))).ToImmutableArray(), ElementAttack = Enumerable.Range(0, 7).Select(index => (byte?)ReadLoaderBytes(checked(0x1D27BD5u + 208u * (uint)index), 1)[0]).ToImmutableArray(), EffectMemory = effectMemory, ModelTextureCache = NativeBattleModelTextureCache.FromColdProcess(), ModelCpuResources = NativeBattleModelCpuResources.FromColdProcess(), FileStaging = NativeBattleFileStaging.FromColdProcess().Write(0x1D999C0, emptyGeometry).Write(NativeBattleTemporaryMemory.Address, ReadLoaderBytes(NativeBattleTemporaryMemory.Address, NativeBattleTemporaryMemory.ReachedLength)).Write(NativeBattleTemporaryMemory.Cursor, ReadLoaderBytes(NativeBattleTemporaryMemory.Cursor, 4)).Write(0x1D999D0, ReadLoaderBytes(0x1D999D0, 80)), Stage = NativeBattleStageState.FromColdProcess(), Camera = camera, ActionMemory = actionMemory, FileIo = NativeBattleFileIoState.FromColdProcess() };
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
