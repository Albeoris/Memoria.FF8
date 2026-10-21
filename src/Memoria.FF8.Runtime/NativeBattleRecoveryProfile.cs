using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>One normalized original recovery controller with explicit identity, resource and timing operands.</summary>
public sealed record NativeBattleRecoveryProfile(ushort Effect, uint Entry, uint Initializer, uint InitializerGlobal, uint MainCallback, uint TargetCallback, uint GenericCallback, uint FadeInCallback, uint FadeOutCallback, uint ExecutableAddress, int ExecutableLength, uint DirectoryAddress, uint FilenameAddress, uint SoundAddress, string TextureName, int DrawBankBytes, int EmitterBytes, int GeometryBytes, ushort TargetReleaseCounter, short ContactCounter, short SoundCounter, ushort RootWait, ImmutableArray<uint> Descriptors, ImmutableArray<uint> Banks)
{
    public static NativeBattleRecoveryProfile MegaPhoenix { get; } = new(7, 0x8CE3D0, 0x8CE3B0, 0x275FB80, 0x8CE4F0, 0x8CE800, 0x8CE910, 0x8CE6E0, 0x8D4300, 0x163ACD4, 0x35C8, 0x163E198, 0x163E29C, 0x163ACD0, "mag006.tim", 0xA000, 0xA8C, 0xD200, 7, 10, 0, 13, [0x275FC70, 0x27609C0, 0x2760720, 0x27609B0], [0x275FBA8, 0x27609D0, 0x275FC90, 0x2760730]);
    public static NativeBattleRecoveryProfile MegaPotion { get; } = new(53, 0x811F70, 0x811F50, 0x2617168, 0x812090, 0x8123A0, 0x8124B0, 0x812280, 0x8172C0, 0x15A76C4, 0x1B38, 0x15A90F8, 0x15A91FC, 0x15A76C0, "mag052.tim", 0x6000, 0xEC4, 0x10680, 10, 10, 0, 30, [0x2617258, 0x2617FA8, 0x2617D08, 0x2617F98], [0x2617190, 0x2617FB8, 0x2617278, 0x2617D18]);
    public static NativeBattleRecoveryProfile Elixir { get; } = Translate(58, -0x1A2C0, -0xCE40, 0x159AE04, 0x159E2A8, 0x159E3AC, 0x159AE00, 8, 12, 0, 22);
    public static NativeBattleRecoveryProfile Megalixir { get; } = Translate(59, -0x1F760, -0x10108, 0x1597844, 0x159ACE8, 0x159ADEC, 0x1597840, 18, 18, 8, 12);

    public uint ChannelAddress => Effect switch
    {
        7 => 0x2760B64,
        53 => 0x261814C,
        58 => 0x260B30C,
        59 => 0x2608044,
        _ => throw new NotSupportedException("Native recovery channel bank requires its original profile.")
    };
    public int ChannelLength => Effect switch
    {
        7 => 0xD3C,
        53 => 0x1284,
        58 or 59 => 0x22E4,
        _ => throw new NotSupportedException("Native recovery channel extent requires its original profile.")
    };

    public static NativeBattleRecoveryProfile ForEffect(ushort effect)
    {
        return effect switch
        {
            7 => MegaPhoenix,
            53 => MegaPotion,
            58 => Elixir,
            59 => Megalixir,
            _ => throw new NotSupportedException("Native recovery profile requires original7/53/58/59.")
        };
    }

    private static NativeBattleRecoveryProfile Translate(ushort effect, int codeDelta, int globalDelta, uint executable, uint directory, uint filename, uint sound, ushort release, short contact, short soundCounter, ushort wait)
    {
        uint Code(uint address) => checked((uint)((long)address + codeDelta));
        uint Global(uint address) => checked((uint)((long)address + globalDelta));
        return MegaPotion with
        {
            Effect = effect,
            Entry = Code(MegaPotion.Entry),
            Initializer = Code(MegaPotion.Initializer),
            InitializerGlobal = Global(MegaPotion.InitializerGlobal),
            MainCallback = Code(MegaPotion.MainCallback),
            TargetCallback = Code(MegaPotion.TargetCallback),
            GenericCallback = Code(MegaPotion.GenericCallback),
            FadeInCallback = Code(MegaPotion.FadeInCallback),
            FadeOutCallback = Code(MegaPotion.FadeOutCallback),
            ExecutableAddress = executable,
            ExecutableLength = 0x35A8,
            DirectoryAddress = directory,
            FilenameAddress = filename,
            SoundAddress = sound,
            TextureName = "mag057-058.tim",
            DrawBankBytes = 0x9000,
            EmitterBytes = 0x12FC,
            GeometryBytes = 0xD200,
            TargetReleaseCounter = release,
            ContactCounter = contact,
            SoundCounter = soundCounter,
            RootWait = wait,
            Descriptors = MegaPotion.Descriptors.Select(Global).ToImmutableArray(),
            Banks = MegaPotion.Banks.Select(Global).ToImmutableArray()
        };
    }
}

public sealed record NativeBattleRecoveryResources(ushort Effect, uint Sound, NativeBattleRecoveryExecutableSource Executable);

public sealed record NativeBattleRecoveryState([property: JsonRequired] int SourceArenaOffset, [property: JsonRequired] int? PrimaryDrawCursor, [property: JsonRequired] int? SecondaryDrawCursor, [property: JsonRequired] int AuxiliaryCursor, [property: JsonRequired] NativeBattleActionMatrix? CopiedCamera)
{
    public int EntryCounterA { get; init; }
    public int EntryCounterB { get; init; }
    public int? Emitters { get; init; }
    public int? Geometry { get; init; }
    public ushort? EmitterScanCursor { get; init; }
    public ushort? GeometryScanCursor { get; init; }
    public ushort? EmitterResetWord { get; init; }
    public ushort? GeometryResetWord { get; init; }
    public ushort? PassEmitterCount { get; init; }
    public ushort? PassGeometryCount { get; init; }
}

public sealed record NativeBattleRecoveryFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleRecoveryState State);
public sealed record NativeBattleRecoveryLeafStep(NativeBattleRecoveryFrame Frame, int Result);
public sealed record NativeBattleRecoveryControllerStep(NativeBattleRecoveryFrame Frame, ImmutableArray<int> Contacts, ImmutableArray<NativeBattleModuleSound> Sounds);
public sealed record NativeBattleRecoveryControllerInputs(NativeBattleActionPacketsState Packets, NativeBattleActionMatrix Camera, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource)
{
    public Func<NativeBattleRecoveryFrame, int, NativeBattleRecoveryFrame>? ApplyContact { get; init; }
}

public sealed record NativeBattleRecoveryContextStep(NativeBattleRecoveryFrame Frame, bool Complete);
public sealed record NativeBattleRecoveryGenericInputs(Func<NativeBattleRecoveryFrame, uint, NativeBattleRecoveryFrame> InitializeContext, Func<NativeBattleRecoveryFrame, uint, NativeBattleRecoveryContextStep> AdvanceContext);
