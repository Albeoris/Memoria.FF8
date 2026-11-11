using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record CrossoverNativeThunderState(NativeBattleActionPacketsState Packets, NativeBattleOriginalEffectState Effect, NativeBattleEffectTextureState Textures, bool ContactPublished, ImmutableArray<NativeBattleModuleDraw> Draws)
{
    public bool Complete => Effect.ReturnedDescriptor == 0;
}
internal sealed record CrossoverNativeThunderStep(CrossoverNativeThunderState State, bool Contact, ImmutableArray<NativeBattleModuleSound> Sounds);

/// <summary>Original Thunder task banks with one host enemy aliased to native slot3.</summary>
internal static class CrossoverNativeThunderProgram
{
    public static CrossoverNativeThunderState Begin(GameAssets game, NativeBattleContactHalf contact, NativeBattleEffectTextureState textures)
    {
        if (contact.ActorIndex != 3)
        {
            throw new InvalidDataException("Mixed Thunder requires one bound enemy contact.");
        }
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[640], new byte[1176]);
        (packets, _) = packets.Prepare(new(0, 2, 11, -1, 7, 3, null));
        (packets, _) = packets.AppendContact(new(contact, new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState selected = NativeBattleOriginalEffects.Select(3, game.ReadNativeEffectCatalogForEffects([3]));
        selected = selected with { Parameters = new(0, 0, 8, 0, 0, 0) };
        (NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect) = NativeBattleOriginalEffects.Initialize(NativeBattleEffectMemory.FromColdProcess(), selected);
        NativeBattleEffect3Resources resources = effect.Binding.Effect3Resources ?? throw new InvalidDataException("Mixed Thunder lacks its original executable resources.");
        textures = textures.Register5099A0(resources.Animation);
        (textures, _) = textures.EnqueueTim505E30(resources.Texture);
        textures = textures.Drain505D20(memory.Arena).State;
        return new(packets, NativeBattleEffect3.InitializePools(effect), textures, false, []);
    }

    public static CrossoverNativeThunderStep Advance(CrossoverNativeThunderState source, Func<NativeBattleVector> readTargetBone, Func<NativeBattleModelTransform> readTargetTransform)
    {
        if (source.Complete)
        {
            return new(source, false, []);
        }
        NativeBattleEffect3Step step = NativeBattleEffect3.Step(source.Effect, new(source.Packets, actor => actor == 3 ? readTargetBone() : throw new InvalidDataException("Thunder queried an unbound native target bone."), actor => actor == 3 ? readTargetTransform() : throw new InvalidDataException("Thunder queried an unbound native transform.")));
        if (step.Contacts.Length > 1 || step.Contacts.Any(index => index != 0) || source.ContactPublished && !step.Contacts.IsEmpty)
        {
            throw new InvalidDataException("Mixed Thunder emitted a repeated or foreign contact.");
        }
        bool contact = !step.Contacts.IsEmpty;
        return new(source with { Effect = step.Effect, Draws = step.Draws, ContactPublished = source.ContactPublished || contact }, contact, step.Sounds.Select(sound => new NativeBattleModuleSound(sound.Sound, sound.Flags, sound.Position)).ToImmutableArray());
    }

    public static CrossoverNativeThunderState Capture(CrossoverNativeThunderState source) => source with { Effect = NativeBattleEffectComponentStorage.Capture(source.Effect), Textures = NativeBattleEffectComponentStorage.Capture(source.Textures) };
    public static CrossoverNativeThunderState Restore(CrossoverNativeThunderState source) => source with { Effect = NativeBattleEffectComponentStorage.Restore(source.Effect), Textures = NativeBattleEffectComponentStorage.Restore(source.Textures) };
}
