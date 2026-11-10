using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record CrossoverNativeFireState(int HostTarget, NativeBattleActionPacketsState Packets, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffectTextureState Textures, bool ContactPublished, ImmutableArray<NativeBattleModuleDraw> Draws)
{
    public bool Complete => Effect.ReturnedDescriptor == 0;
}

internal sealed record CrossoverNativeFireStep(CrossoverNativeFireState State, bool Contact, ImmutableArray<NativeBattleFireSound> Sounds);

/// <summary>Original Fire2 with one encounter target aliased to native graphics slot3.</summary>
internal static class CrossoverNativeFireProgram
{
    public static CrossoverNativeFireState Begin(GameAssets assets, int hostTarget, NativeCrtRandomState random, NativeBattleContactHalf contact, NativeBattleEffectTextureState? existingTextures = null)
    {
        if (hostTarget is < 4 or > 9 || contact.ActorIndex != 3)
        {
            throw new InvalidDataException("Mixed Fire requires one foreign enemy occurrence mapped to native target slot3.");
        }
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[640], new byte[1176]);
        (packets, _) = packets.Prepare(new(0, 2, 11, -1, 1, 2, null));
        (packets, _) = packets.AppendContact(new(contact, new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState selected = NativeBattleOriginalEffects.Select(2, assets.ReadNativeEffectCatalogForEffects([2]));
        selected = selected with { Parameters = new(0, 0, 8, 0, 0, 0) };
        (NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect) = NativeBattleOriginalEffects.Initialize(NativeBattleEffectMemory.FromColdProcess(), selected);
        NativeBattleEffectProgramResources resources = effect.Binding.ProgramResources ?? throw new InvalidDataException("Mixed Fire lacks its original executable program resources.");
        NativeBattleEffectInitializerAsset tim = effect.Binding.InitializerAsset ?? throw new InvalidDataException("Mixed Fire lacks its original texture resource.");
        NativeBattleEffectTextureState textures = (existingTextures ?? memory.Textures!).Register5099A0(resources.Animation);
        (textures, _) = textures.EnqueueTim505E30(new(tim.Name, tim.Data));
        textures = textures.Drain505D20(memory.Arena).State;
        effect = NativeBattleFireEffect.InitializePools(effect, packets);
        return new(hostTarget, packets, effect, random, textures, false, []);
    }

    public static CrossoverNativeFireStep Advance(CrossoverNativeFireState source, Func<int, NativeBattleModuleAnchor> readAnchor, Func<int, NativeBattleModelTransform> readTransform, ImmutableArray<NativeBattleModuleResource> sharedSprites)
    {
        if (source.Complete)
        {
            return new(source, false, []);
        }
        NativeBattleEffectProgramResources resources = source.Effect.Binding.ProgramResources ?? throw new InvalidDataException("Mixed Fire lost its immutable program binding.");
        NativeBattleFireStep step = NativeBattleFireEffect.Step(source.Effect, new(0, source.Packets, source.Random, readAnchor, readTransform, resources.Sprites.AddRange(sharedSprites)));
        if (step.Contacts.Length > 1 || step.Contacts.Any(index => index != 0) || source.ContactPublished && !step.Contacts.IsEmpty)
        {
            throw new InvalidDataException("Mixed Fire emitted a repeated or foreign contact pointer.");
        }
        bool contact = !step.Contacts.IsEmpty;
        return new(source with { Effect = step.Effect, Random = step.CrtRandomAfter, ContactPublished = source.ContactPublished || contact, Draws = step.Draws }, contact, step.Sounds);
    }

    public static CrossoverNativeFireState Capture(CrossoverNativeFireState source) => source with { Textures = NativeBattleEffectComponentStorage.Capture(source.Textures), Effect = NativeBattleEffectComponentStorage.Capture(source.Effect) };
    public static CrossoverNativeFireState Restore(CrossoverNativeFireState source) => source with { Textures = NativeBattleEffectComponentStorage.Restore(source.Textures), Effect = NativeBattleEffectComponentStorage.Restore(source.Effect) };
}
