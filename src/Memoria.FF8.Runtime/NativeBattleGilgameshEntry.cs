using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGilgameshEntryState([property: JsonRequired] byte SourceActor, [property: JsonRequired] byte FirstContactActor, [property: JsonRequired] NativeBattleActionMatrix World);
public sealed record NativeBattleGilgameshEntryStep(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleGilgameshEntryState State);

/// <summary>58DCF0's Excalipoor entry, using the retained actor matrix and actual initializer pointer.</summary>
public static class NativeBattleGilgameshEntry
{
    public static NativeBattleGilgameshEntryStep Initialize58DCF0(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets, ImmutableArray<NativeBattleGraphicsModel> models)
    {
        NativeBattleGilgameshProfile profile = NativeBattleGilgameshProfile.ForEffect(effect.Binding.Effect);
        effect.Validate();
        packets.Validate();
        if (effect.Binding.Entry != profile.Entry || effect.Binding.Initializer != profile.Initializer || effect.Parameters is not { } parameters || models.IsDefault || models.Length != 7)
        {
            throw new InvalidDataException("Native Excalipoor entry requires its selected330 initializer, parameters and seven physical graphics rows.");
        }
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native Gilgamesh entry requires its current571B50 arena.");
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        int firstContact = header.FirstContactIndex ?? throw new NotSupportedException("Native Gilgamesh entry requires its actual first-contact pointer.");
        byte actor = packets.ReadContact(firstContact).Primary.ActorIndex;
        if (actor >= models.Length)
        {
            throw new InvalidDataException("Native Gilgamesh first contact leaves its physical graphics bank.");
        }
        NativeBattleGraphicsModel first = models[actor];
        short yaw = first.Rotation14 ?? throw new NotSupportedException("Native Gilgamesh entry requires current actor rotation WORD0E.");
        NativeBattleActionMatrix actorMatrix = first.GeometryWorldMatrix ?? throw new NotSupportedException("Native Gilgamesh entry requires the current published actor matrix at40h.");
        NativeBattleVector center = NativeBattleActionGeometryRules.TargetCenter(models.Where((_, index) => (parameters.TargetMask7A & (1 << index)) != 0).Select(model => model.Transform?.Position28 ?? throw new NotSupportedException("Native Gilgamesh target bounds require each selected physical position row.")).ToArray());
        NativeBattleActionMatrix world = NativeBattleGfMatrices.Euler56CD50(new(0, unchecked((short)(yaw + 0x800)), 0));
        NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(actorMatrix, new(0, 0, -4500));
        world = world with { X = offset.X, Y = offset.Y, Z = unchecked(center.Z + offset.Z) };
        for (int index = 0; index < 200; index++)
        {
            arena = arena.Write(0x2E000 + index * 0x18, new byte[4]);
            arena = arena.Write(0x2F800 + index * 0x20, new byte[4]);
        }
        int timOffset = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == profile.TextureGlobal)?.ArenaOffset ?? throw new NotSupportedException("Native Gilgamesh requires its executed initializer TIM publication.");
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native Gilgamesh requires its current texture queue.");
        (textures, _) = textures.EnqueueArenaTim505E30(arena, timOffset);
        NativeBattleGuardianEffectPools pools = NativeBattleGuardianEffectPools.Gilgamesh();
        return new(memory with { Arena = arena, Textures = textures }, effect with { Pools = [pools.Main, pools.Children, pools.Particles!], ReturnedDescriptor = pools.Main.DescriptorAddress }, new(parameters.Actor78, actor, world));
    }
}
