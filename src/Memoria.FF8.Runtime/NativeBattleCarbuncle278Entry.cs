using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Carbuncle entry through its executed initializer, two ordered TIM loads and original root pool.</summary>
public static class NativeBattleCarbuncle278Entry
{
    public const uint MainDescriptor = 0x2508198;
    public const uint MainBank = 0x25081A8;
    public const uint MainCallback = 0x680DF0;

    public static NativeBattleCarbuncle278Frame Initialize680C50(NativeBattleCarbuncle278Frame frame, NativeBattleActionPacketsState packets, ImmutableArray<NativeBattleGraphicsModel> models, ushort encounterId)
    {
        RequireBinding(frame.Effect);
        frame.Effect.Validate();
        packets.Validate();
        NativeBattleCarbuncle278State state = RequireState(frame.Memory);
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Native Carbuncle entry requires its current action parameters.");
        if (models.IsDefault || models.Length != 7 || models.Where((model, index) => model is null || model.Index != index).Any())
        {
            throw new InvalidDataException("Native Carbuncle entry requires its seven ordered physical graphics rows.");
        }
        NativeCrtRandomState random = frame.Random.Draw(out ushort value);
        int resource = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x2508110)?.ArenaOffset ?? throw new NotSupportedException("Native Carbuncle entry requires its executed initializer publication.");
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Carbuncle entry requires its current effect arena.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Native Carbuncle entry requires its current TIM queue.");
        (textures, _) = textures.EnqueueArenaTim505E30(arena, resource);
        (textures, _) = textures.EnqueueArenaTim505E30(arena, checked(resource + 0x2A020));
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(MainDescriptor, MainBank, 0x14, 1);
        (pool, int slot) = pool.Append(MainCallback);
        NativeBattleModulePoolTask task = pool.Slots[slot].WriteDword(0xC, 0).WriteDword(0x10, 0);
        pool = pool.WriteSlot(slot, task);
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        int count = header.ReadByte(0x10);
        int sum = 0;
        if (count != 0)
        {
            int first = header.FirstContactIndex ?? throw new NotSupportedException("Native Carbuncle entry requires its current target-contact pointer.");
            for (int index = 0; index < count; index++)
            {
                int target = packets.ReadContact(checked(first + index)).Primary.ActorIndex;
                NativeBattleGraphicsModel model = Model(target);
                NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Carbuncle centroid requires the selected actor's current signed position WORD20.");
                sum = unchecked(sum + transform.Position28.Z);
            }
        }
        int centroid = sum / count;
        int floor = unchecked(centroid - 0x7D0);
        int maximum = -32767;
        for (int index = 3; index < 7; index++)
        {
            NativeBattleGraphicsModel model = models[index];
            if ((model.Flags0 & 2) != 0)
            {
                NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Carbuncle lift requires the active enemy's current signed position WORD20 and scale WORD26.");
                maximum = Math.Max(maximum, transform.Position28.Z + transform.Scale38 / 2);
            }
        }
        int lift = maximum > unchecked(floor - 0x5DC) ? unchecked(floor - maximum - 0x5DC) : 0;
        if (encounterId == 0x3F)
        {
            lift = 0;
            floor = centroid;
        }
        state = state with { Random811C = value, Action8120 = 0x1D99A78, Caster8124 = checked(0x1D972C0u + (uint)parameters.Actor78 * 0x9C), Centroid81C4 = centroid, Base81CC = floor, Lift81D0 = lift, ModelBuffer81F8 = NativeBattleEffectNodeMemory.ArenaAddress };
        return frame with { Memory = frame.Memory with { Textures = textures, Carbuncle278 = state }, Effect = frame.Effect with { Pools = [pool], ReturnedDescriptor = MainDescriptor }, Random = random };

        NativeBattleGraphicsModel Model(int index) => (uint)index < models.Length ? models[index] : throw new NotSupportedException("Native Carbuncle target pointer leaves the seven physical actor rows.");
    }

    internal static NativeBattleCarbuncle278State RequireState(NativeBattleEffectMemory memory) => memory.Carbuncle278 ?? throw new NotSupportedException("Native Carbuncle requires its retained module owner; warm state is not reconstructed.");

    internal static void RequireBinding(NativeBattleOriginalEffectState effect)
    {
        if (effect.Binding is not { Effect: 278, Entry: 0x680C50, Initializer: 0x680C60 })
        {
            throw new InvalidDataException("Native Carbuncle requires its selected278 entry and initializer.");
        }
    }
}
