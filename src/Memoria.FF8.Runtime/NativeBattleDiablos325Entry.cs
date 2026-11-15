using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

/// <summary>Diablos's initialized TIM publication, target centroid and root bank.</summary>
public static class NativeBattleDiablos325Entry
{
    public const uint MainDescriptor = 0x25051B0;
    public const uint MainBank = 0x25051C0;
    public const uint MainCallback = 0x654350;

    public static NativeBattleDiablos325Frame Initialize6541E0(NativeBattleDiablos325Frame frame, NativeBattleActionPacketsState packets, BattleActorModels models)
    {
        RequireBinding(frame.Effect);
        frame.Effect.Validate(packets.Roster);
        packets.Validate();
        NativeBattleDiablos325State state = RequireState(frame.Memory);
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Native Diablos entry requires its current action parameters.");
        _ = models.Get(parameters.Source);
        NativeBattleActionHeaderSlot header = packets.ReadHeader(parameters.Header);
        int count = header.ReadContactCount();
        if (count == 0)
        {
            throw new InvalidDataException("Native Diablos entry requires at least one recipient.");
        }
        int resource = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x250517C)?.ArenaOffset ?? throw new NotSupportedException("Native Diablos entry requires its executed initializer publication.");
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Diablos entry requires its current effect arena.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Native Diablos entry requires its current TIM queue.");
        (textures, _) = textures.EnqueueArenaTim505E30(arena, resource);
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(MainDescriptor, MainBank, 0x10, 1);
        (pool, int slot) = pool.Append(MainCallback);
        pool = pool.WriteSlot(slot, pool.Slots[slot].WriteDword(0xC, 0));
        int sumZ = 0;
        int sumX = 0;
        BattleContactCursor first = header.FirstContact;
        for (int index = 0; index < count; index++)
        {
            BattleModelOwner target = packets.ReadContact(first.Advance(index)).Primary.Recipient;
            NativeBattleModelTransform transform = models.Get(target).Transform ?? throw new NotSupportedException("Native Diablos centroid requires its target's signed physical position WORDs1C/20.");
            sumZ = unchecked(sumZ + transform.Position28.Z);
            sumX = unchecked(sumX + transform.Position28.X);
        }
        int centerZ = sumZ / count;
        int centerX = sumX / count;
        state = state with { Pause5180 = 0, Action5184 = 0x1D99A78, Caster5188 = packets.ModelAliases.EncodeAddress(parameters.Source) is { } encoded ? BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan()) : null, CenterZ51D4 = centerZ, CenterX51D8 = centerX, FarZ51DC = unchecked(centerZ + 4000), MidZ51E0 = unchecked(centerZ + 2000), Control51E4 = 0, ModelBuffer5208 = NativeBattleEffectNodeMemory.ArenaAddress, Recipients = [] };
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
