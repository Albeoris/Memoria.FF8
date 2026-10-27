using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Diablos's initialized TIM publication, target centroid and root bank.</summary>
public static class NativeBattleDiablos325Entry
{
    public const uint MainDescriptor = 0x25051B0;
    public const uint MainBank = 0x25051C0;
    public const uint MainCallback = 0x654350;

    public static NativeBattleDiablos325Frame Initialize6541E0(NativeBattleDiablos325Frame frame, NativeBattleActionPacketsState packets, ImmutableArray<NativeBattleGraphicsModel> models)
    {
        RequireBinding(frame.Effect);
        frame.Effect.Validate();
        packets.Validate();
        NativeBattleDiablos325State state = RequireState(frame.Memory);
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Native Diablos entry requires its current action parameters.");
        if (models.IsDefault || models.Length != 7 || models.Where((model, index) => model is null || model.Index != index).Any())
        {
            throw new InvalidDataException("Native Diablos entry requires seven ordered physical graphics rows.");
        }
        int resource = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x250517C)?.ArenaOffset ?? throw new NotSupportedException("Native Diablos entry requires its executed initializer publication.");
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Diablos entry requires its current effect arena.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Native Diablos entry requires its current TIM queue.");
        (textures, _) = textures.EnqueueArenaTim505E30(arena, resource);
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(MainDescriptor, MainBank, 0x10, 1);
        (pool, int slot) = pool.Append(MainCallback);
        pool = pool.WriteSlot(slot, pool.Slots[slot].WriteDword(0xC, 0));
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        int count = header.ReadByte(0x10);
        int sumZ = 0;
        int sumX = 0;
        if (count != 0)
        {
            int first = header.FirstContactIndex ?? throw new NotSupportedException("Native Diablos entry requires its current target-contact pointer.");
            for (int index = 0; index < count; index++)
            {
                int target = packets.ReadContact(checked(first + index)).Primary.ActorIndex;
                if ((uint)target >= models.Length)
                {
                    throw new NotSupportedException("Native Diablos target pointer leaves the seven physical actor rows.");
                }
                NativeBattleModelTransform transform = models[target].Transform ?? throw new NotSupportedException("Native Diablos centroid requires its target's signed physical position WORDs1C/20.");
                sumZ = unchecked(sumZ + transform.Position28.Z);
                sumX = unchecked(sumX + transform.Position28.X);
            }
        }
        int centerZ = sumZ / count;
        int centerX = sumX / count;
        state = state with { Pause5180 = 0, Action5184 = 0x1D99A78, Caster5188 = checked(0x1D972C0u + (uint)parameters.Actor78 * 0x9C), CenterZ51D4 = centerZ, CenterX51D8 = centerX, FarZ51DC = unchecked(centerZ + 4000), MidZ51E0 = unchecked(centerZ + 2000), Control51E4 = 0, ModelBuffer5208 = NativeBattleEffectNodeMemory.ArenaAddress };
        return frame with { Memory = frame.Memory with { Textures = textures, Diablos325 = state }, Effect = frame.Effect with { Pools = [pool], ReturnedDescriptor = MainDescriptor } };
    }

    internal static NativeBattleDiablos325State RequireState(NativeBattleEffectMemory memory) => memory.Diablos325 ?? throw new NotSupportedException("Native Diablos requires its retained module owner; warm state is not reconstructed.");

    internal static void RequireBinding(NativeBattleOriginalEffectState effect)
    {
        if (effect.Binding is not { Effect: 325, Entry: 0x6541E0, Initializer: 0x6541F0 })
        {
            throw new InvalidDataException("Native Diablos requires its selected325 entry and initializer.");
        }
    }
}
