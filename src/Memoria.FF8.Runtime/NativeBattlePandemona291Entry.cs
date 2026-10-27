namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePandemona291Resources(NativeBattleEffectHeapAllocation FirstTim, NativeBattleEffectHeapAllocation SecondTim)
{
    public System.Collections.Immutable.ImmutableArray<NativeBattlePandemona291Animation>? AnimationSources { get; init; }
    public NativeVibrationBank? Vibration { get; init; }
    public System.Collections.Immutable.ImmutableArray<NativeBattleEffectFileAsset> Files { get; init; } = [];
    public uint? OpeningSound { get; init; }
    public uint? BodySound { get; init; }
    public NativeBattleGfAnimationSource? SurfaceProgram { get; init; }
    public NativeBattleGfAnimationSource? RisingProgram { get; init; }
    public System.Collections.Immutable.ImmutableArray<byte> BodyAnchorInput { get; init; } = [];
    public System.Collections.Immutable.ImmutableArray<byte> LateTextureKinds { get; init; } = [];
    public NativeBattleGfAnimationSource? FoldingProgram { get; init; }
    public NativeBattleStaticMeshResource? EndingRingGeometry { get; init; }
    public System.Collections.Immutable.ImmutableArray<byte> EndingRingParameters { get; init; } = [];
    public NativeBattleStaticMeshResource? LateBackdrop { get; init; }
    public NativeBattlePandemona291SpriteResource? LateSprite { get; init; }
    public System.Collections.Immutable.ImmutableArray<NativeBattleStaticMeshResource> LateLayerGeometry { get; init; } = [];
    public System.Collections.Immutable.ImmutableArray<uint> EndingSounds { get; init; } = [];
    public System.Collections.Immutable.ImmutableArray<NativeBattleStaticMeshResource> TrailModels { get; init; } = [];
    public System.Collections.Immutable.ImmutableArray<byte> TrailTrigonometry { get; init; } = [];
    public System.Collections.Immutable.ImmutableArray<byte> SurfaceTextureKinds { get; init; } = [];

    public System.Collections.Immutable.ImmutableArray<NativeBattleStaticMeshResource> LayerGeometry { get; init; } = [];
    public System.Collections.Immutable.ImmutableArray<NativeBattleStaticMeshResource> LayerTargets { get; init; } = [];
    public NativeBattlePandemona291SpriteResource? LayerSprite { get; init; }
    public NativeBattleStaticMeshResource? VortexOverlayGeometry { get; init; }
    public NativeBattleStaticMeshResource? VortexFloorGeometry { get; init; }
    public System.Collections.Immutable.ImmutableArray<byte> VortexFloorTarget { get; init; } = [];

    public void Validate()
    {
        if (FirstTim is null || FirstTim.Data.IsDefault || FirstTim.Data.Length != 0x31620 || SecondTim is null || SecondTim.Data.IsDefault || SecondTim.Data.Length != 0x2220)
        {
            throw new InvalidDataException("Native Pandemona requires both complete static TIM owners.");
        }
    }
}

public static class NativeBattlePandemona291Entry
{
    public const uint MainDescriptor = 0x25562B8;
    public const uint MainBank = 0x25562C8;
    public const uint MainCallback = 0x6ED350;

    public static NativeBattlePandemona291Frame Initialize6ED250(NativeBattlePandemona291Frame frame, NativeBattlePandemona291Resources resources, NativeBattleActionPacketsState packets, Func<int, NativeBattleGraphicsModel> readModel)
    {
        if (frame.Effect.Binding is not { Effect: 291, Entry: 0x6ED250 })
        {
            throw new InvalidDataException("Native Pandemona requires its selected original entry.");
        }
        resources.Validate();
        NativeBattlePandemona291State state = frame.Memory.Pandemona291 ?? throw new NotSupportedException("Native Pandemona entry requires its retained module owner.");
        state = state with { Scratch25562EC = 0 };
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Native Pandemona requires its current TIM queue.");
        (textures, _) = textures.EnqueueTim505E30(resources.FirstTim);
        (textures, _) = textures.EnqueueTim505E30(resources.SecondTim);
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Native Pandemona requires its current action parameters.");
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Pandemona requires its current model buffer.");
        arena.Validate();
        state = state with { ModelBuffer25562F4 = NativeBattleEffectNodeMemory.ArenaAddress, Action255625C = 0x1D99A78, Caster2556260 = checked(0x1D972C0u + (uint)parameters.Actor78 * 0x9C) };
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(MainDescriptor, MainBank, 0x14, 1);
        (pool, int slot) = pool.Append(MainCallback);
        NativeBattleModulePoolTask root = pool.Slots[slot].WriteDword(0xC, 0).WriteDword(0x10, 0);
        pool = pool.WriteSlot(slot, root);
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        int count = header.ReadByte(0x10);
        int sum = 0;
        if (count > 0)
        {
            int first = header.FirstContactIndex ?? throw new NotSupportedException("Native Pandemona requires its actual contact array.");
            for (int index = 0; index < count; index++)
            {
                int actor = packets.ReadContact(checked(first + index)).Primary.ActorIndex;
                NativeBattleGraphicsModel model = readModel(actor);
                NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Pandemona requires the selected actor's current position WORD20.");
                sum = unchecked(sum + transform.Position28.Z);
            }
        }
        int ground = sum / count;
        state = state with { Ground25562E0 = ground, Center25562E8 = unchecked(ground + 4000) };
        return frame with { Memory = frame.Memory with { Pandemona291 = state, Textures = textures }, Effect = frame.Effect with { Pools = [pool], ReturnedDescriptor = MainDescriptor } };
    }
}
