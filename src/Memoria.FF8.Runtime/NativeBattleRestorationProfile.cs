using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>The original Esuna88CF70 and Full-life883110 controller operands; their private resource banks are distinct.</summary>
public sealed record NativeBattleRestorationProfile(ushort Effect, uint Entry, uint Initializer, uint InitializerGlobal, uint MainCallback, uint TargetCallback, uint GenericCallback, uint FadeInCallback, uint FadeOutCallback, uint ExecutableAddress, int DrawBankBytes, int EmitterBytes, int GeometryBytes, ushort TargetReleaseCounter, ushort RootWait, ImmutableArray<uint> Descriptors, ImmutableArray<uint> Banks)
{
    public static NativeBattleRestorationProfile Esuna { get; } = new(24, 0x88CF70, 0x88CF50, 0x2700470, 0x88D0A0, 0x88D3B0, 0x88D4C0, 0x88D290, 0x892ED0, 0x17E48E4, 0xA000, 0x10E0, 0x10680, 20, 20, [0x2700560, 0x2701010, 0x2700D70, 0x2701000], [0x2700498, 0x2701020, 0x2700580, 0x2700D80]);
    public static NativeBattleRestorationProfile FullLife { get; } = new(27, 0x883110, 0x8830F0, 0x26D8308, 0x883240, 0x883550, 0x883660, 0x883430, 0x888480, 0x17DE100, 0x6000, 0x1518, 0x16F80, 30, 10, [0x26D83F8, 0x26D8EA8, 0x26D8C08, 0x26D8E98], [0x26D8330, 0x26D8EB8, 0x26D8418, 0x26D8C18]);

    // Exact78-body normalization.
    public static NativeBattleRestorationProfile Effect57 { get; } = TranslateEsuna(57, -0x8FE20, -0xF2E80, 0x17A3DD0);
    public static NativeBattleRestorationProfile Remedy { get; } = TranslateEsuna(67, -0xAE550, -0xFFEA0, 0x178F3F4);

    public bool UsesEsunaMesh => Effect is 24 or 57 or 67;
    public uint AnimationAddress => Effect switch
    {
        24 => 0x161374C,
        27 => 0x160EC64,
        57 => 0x159E3C0,
        67 => 0x1595758,
        _ => throw new NotSupportedException("Native restoration animation requires its original effect profile.")
    };
    public uint AssetNameAddress => AnimationAddress + (Effect == 27 ? 0xA58u : 0xB28u);
    public int ExecutableLength => Effect switch
    {
        24 or 67 => 0x5C0C,
        27 => 0x67E4,
        57 => 0x6534,
        _ => throw new NotSupportedException("Native restoration executable extent requires its original effect profile.")
    };

    private static NativeBattleRestorationProfile TranslateEsuna(ushort effect, int codeDelta, int globalDelta, uint executableAddress)
    {
        uint Code(uint address) => checked((uint)((long)address + codeDelta));
        uint Global(uint address) => checked((uint)((long)address + globalDelta));
        return Esuna with
        {
            Effect = effect,
            Entry = Code(Esuna.Entry),
            Initializer = Code(Esuna.Initializer),
            InitializerGlobal = Global(Esuna.InitializerGlobal),
            MainCallback = Code(Esuna.MainCallback),
            TargetCallback = Code(Esuna.TargetCallback),
            GenericCallback = Code(Esuna.GenericCallback),
            FadeInCallback = Code(Esuna.FadeInCallback),
            FadeOutCallback = Code(Esuna.FadeOutCallback),
            ExecutableAddress = executableAddress,
            Descriptors = Esuna.Descriptors.Select(Global).ToImmutableArray(),
            Banks = Esuna.Banks.Select(Global).ToImmutableArray()
        };
    }

    public static NativeBattleRestorationProfile ForEffect(ushort effect)
    {
        return effect switch
        {
            24 => Esuna,
            27 => FullLife,
            57 => Effect57,
            67 => Remedy,
            _ => throw new NotSupportedException("The shared restoration controller requires an original24/27/57/67 binding.")
        };
    }
}

public sealed record NativeBattleRestorationResources(ushort Effect, NativeBattleEffectHeapAllocation Animation, uint Sound)
{
    public ImmutableArray<byte> OriginalExecutableDirectory { get; init; } = [];
    public NativeBattleRestorationExecutableSource? Executable { get; init; }
}

public sealed record NativeBattleRestorationState([property: JsonRequired] int SourceArenaOffset, [property: JsonRequired] int? PrimaryDrawCursor, [property: JsonRequired] int? SecondaryDrawCursor, [property: JsonRequired] int AuxiliaryCursor, [property: JsonRequired] NativeBattleActionMatrix? CopiedCamera)
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

public sealed record NativeBattleRestorationFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleRestorationState State);
public sealed record NativeBattleRestorationLeafStep(NativeBattleRestorationFrame Frame, int Result);
public sealed record NativeBattleRestorationControllerStep(NativeBattleRestorationFrame Frame, ImmutableArray<int> Contacts, ImmutableArray<NativeBattleModuleSound> Sounds);
public sealed record NativeBattleRestorationControllerInputs(NativeBattleActionPacketsState Packets, NativeBattleActionMatrix Camera, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource)
{
    public Func<NativeBattleRestorationFrame, int, NativeBattleRestorationFrame>? ApplyContact { get; init; }
}

public sealed record NativeBattleRestorationContextStep(NativeBattleRestorationFrame Frame, bool Complete);
public sealed record NativeBattleRestorationGenericInputs(Func<NativeBattleRestorationFrame, uint, NativeBattleRestorationFrame> InitializeContext, Func<NativeBattleRestorationFrame, uint, NativeBattleRestorationContextStep> AdvanceContext);
