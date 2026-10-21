using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Original Cura and HiPotion module identities over their common task and rendering contracts.</summary>
public sealed class NativeBattleCuraFamilyProfile
{
    private NativeBattleCuraFamilyProfile(ushort effect)
    {
        Effect = effect;
    }

    public ushort Effect { get; }
    public uint Entry => Effect == 25 ? 0x889D60u : 0x81C800u;
    public uint Initializer => Effect == 25 ? 0x889D40u : 0x81C7E0u;
    public uint InitializerGlobal => Effect == 25 ? 0x26E5908u : 0x2619F50u;
    public uint AnimationAddress => Effect == 25 ? 0x1611104u : 0x15AA734u;
    public uint SoundAddress => Effect == 25 ? 0x1610798u : 0x15A9DC8u;
    public uint ContextF4Address => Effect == 25 ? 0x270046Cu : 0x2634AB4u;
    public uint SceneFadeAddress => Effect == 25 ? 0x2700360u : 0x26349A8u;
    public string TextureName => "mag024-050.tim";
    public ImmutableArray<NativeBattleCureFamilyPool> Pools => Effect == 25
        ? [new(0x26E5A00, 0x26E5938, 100, 2), new(0x26FCFA0, 0x26FCFB0, 88, 4), new(0x26FD118, 0x26FD128, 200, 64), new(0x26F6B80, 0x26E69B0, 132, 500)]
        : [new(0x261A048, 0x2619F80, 100, 2), new(0x26315E8, 0x26315F8, 88, 4), new(0x2631760, 0x2631770, 200, 64), new(0x262B1C8, 0x261AFF8, 132, 500)];

    public static NativeBattleCuraFamilyProfile ForEffect(ushort effect)
    {
        return effect is 25 or 51 ? new(effect) : throw new NotSupportedException("The Cura family requires an original Cura or HiPotion module.");
    }

    public uint Callback(uint curaCallback)
    {
        uint potion = curaCallback switch
        {
            0x889E40 => 0x81C8E0,
            0x889F40 => 0x81C9E0,
            0x88A070 => 0x81CB10,
            0x88A2D0 => 0x81CD70,
            0x88A4E0 => 0x81CF80,
            0x88B950 => 0x81E3F0,
            0x88CDA0 => 0x81F840,
            0x88A740 => 0x81D1E0,
            0x88B690 => 0x81E130,
            0x88B530 => 0x81DFD0,
            0x88B810 => 0x81E2B0,
            0x88CC70 => 0x81F710,
            0x88B3D0 => 0x81DE70,
            0x88BBD0 => 0x81E670,
            _ => throw new NotSupportedException("The Cura family callback has no original task identity.")
        };
        return Effect == 25 ? curaCallback : potion;
    }

    public uint CanonicalParticleCallback(uint callback)
    {
        foreach (uint canonical in new uint[] { 0x88A070, 0x88A2D0, 0x88A4E0, 0x88B950, 0x88CDA0, 0x88A740, 0x88B690, 0x88B530, 0x88B810, 0x88CC70, 0x88B3D0, 0x88BBD0 })
        {
            if (Callback(canonical) == callback)
            {
                return canonical;
            }
        }
        throw new NotSupportedException("The Cura family particle callback belongs to another module.");
    }

    public bool IsSpriteCallback(uint callback)
    {
        return CanonicalParticleCallback(callback) is 0x88A070 or 0x88A2D0 or 0x88A4E0 or 0x88B950 or 0x88CDA0;
    }

    public uint Resource(uint curaAddress)
    {
        uint potion = curaAddress switch
        {
            0x161079C => 0x15A9DCC,
            0x1610948 => 0x15A9F78,
            0x16109BC => 0x15A9FEC,
            0x1610A10 => 0x15AA040,
            0x1610AD4 => 0x15AA104,
            0x1610C00 => 0x15AA230,
            0x1610DAC => 0x15AA3DC,
            0x1610F58 => 0x15AA588,
            0x161194C => 0x15AAF7C,
            0x1611C20 => 0x15AB250,
            0x1613684 => 0x15ACCB4,
            0x16136C0 => 0x15ACCF0,
            0x16136FC => 0x15ACD2C,
            _ => throw new NotSupportedException("The Cura family resource has no original directory identity.")
        };
        return Effect == 25 ? curaAddress : potion;
    }

    public short? ReadContextF4(NativeBattleEffectMemory memory)
    {
        return Effect == 25 ? memory.CuraContextF4 : memory.Potion51ContextF4;
    }

    public int? ReadSceneFade(NativeBattleEffectMemory memory)
    {
        return Effect == 25 ? memory.CuraSceneFade : memory.Potion51SceneFade;
    }
}
