using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Carbuncle entry through its executed initializer, two ordered TIM loads and original root pool.</summary>
public static class NativeBattleCarbuncle278Entry
{
    public const uint MainDescriptor = 0x2508198;
    public const uint MainBank = 0x25081A8;
    public const uint MainCallback = 0x680DF0;

    public static NativeBattleCarbuncle278Frame Initialize680C50(NativeBattleCarbuncle278Frame frame, NativeBattleActionPacketsState packets, BattleActorModels models, ushort encounterId)
    {
        return Initialize680C50(frame, packets, models, NativeBattleEffectSceneContext.FromNativeEncounter(encounterId));
    }

    public static NativeBattleCarbuncle278Frame Initialize680C50(NativeBattleCarbuncle278Frame frame, NativeBattleActionPacketsState packets, BattleActorModels models, NativeBattleEffectSceneContext sceneContext, ImmutableArray<NativeBattleModelTransform>? opposingTransforms = null)
    {
        sceneContext.Validate();
        RequireBinding(frame.Effect);
        frame.Effect.Validate();
        packets.Validate();
        NativeBattleCarbuncle278State state = RequireState(frame.Memory);
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Native Carbuncle entry requires its current action parameters.");
        if (opposingTransforms is { } opposing && (opposing.IsDefault || opposing.Any(transform => transform is null)))
        {
            throw new InvalidDataException("Native Carbuncle clearance requires initialized opposing scene transforms.");
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
        NativeBattleActionHeaderSlot header = packets.ReadHeader(parameters.Header);
        int count = header.ReadContactCount();
        int sum = 0;
        if (count != 0)
        {
            BattleContactCursor first = header.FirstContact;
            for (int index = 0; index < count; index++)
            {
                BattleModelOwner target = packets.ReadContact(first.Advance(index)).Primary.Recipient;
                NativeBattleGraphicsModel model = models.Get(target);
                NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Carbuncle centroid requires the selected actor's current signed position WORD20.");
                sum = unchecked(sum + transform.Position28.Z);
            }
        }
        int centroid = sum / count;
        int floor = unchecked(centroid - 0x7D0);
        int maximum = -32767;
        if (opposingTransforms is { } sceneOpponents)
        {
            foreach (NativeBattleModelTransform transform in sceneOpponents)
            {
                maximum = Math.Max(maximum, transform.Position28.Z + transform.Scale38 / 2);
            }
        }
        else
        {
            foreach (BattleModelEntry<BattleEnemyModelOwner> enemy in models.Enemies.Entries)
            {
                NativeBattleGraphicsModel model = enemy.State;
                if ((model.Flags0 & 2) != 0)
                {
                    NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Carbuncle lift requires the active enemy's current signed position WORD20 and scale WORD26.");
                    maximum = Math.Max(maximum, transform.Position28.Z + transform.Scale38 / 2);
                }
            }
        }
        int lift = maximum > unchecked(floor - 0x5DC) ? unchecked(floor - maximum - 0x5DC) : 0;
        if (sceneContext.FloorMode == NativeBattleSummonFloorMode.TargetPlane)
        {
            lift = 0;
            floor = centroid;
        }
        state = state with { Random811C = value, Action8120 = 0x1D99A78, Caster8124 = packets.ModelAliases.EncodeAddress(parameters.Source) is { } encoded ? System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan()) : null, Centroid81C4 = centroid, Base81CC = floor, Lift81D0 = lift, ModelBuffer81F8 = NativeBattleEffectNodeMemory.ArenaAddress };
        return frame with { Memory = frame.Memory with { Textures = textures, Carbuncle278 = state }, Effect = frame.Effect with { Pools = [pool], ReturnedDescriptor = MainDescriptor }, Random = random };

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
