using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleShivaEntryState([property: JsonRequired] NativeBattleVector TargetCenter, [property: JsonRequired] NativeBattleActionMatrix World, [property: JsonRequired] int Counter22BD010);
public sealed record NativeBattleShivaEntryStep(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleShivaEntryState State);

/// <summary>5C0D50's entry and physical workspace initialization; its task callbacks remain separate contracts.</summary>
public static class NativeBattleShivaEntry
{
    public const int FirstWorkspaceOffset = 0x2C000;
    public const int SecondWorkspaceOffset = 0x2D000;
    public const int ThirdWorkspaceOffset = 0x2E000;
    public const int FourthWorkspaceOffset = 0x30800;
    public const int EndWorkspaceOffset = 0x33000;

    public static NativeBattleShivaEntryStep Initialize5C0D50(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets, BattleActorModels models)
    {
        ArgumentNullException.ThrowIfNull(models);
        effect.Validate(models.Roster);
        packets.Validate();
        if (effect.Binding is not { Effect: 185, Entry: 0x5C0D50, Initializer: 0x5C0D30 } || effect.Parameters is not { } parameters)
        {
            throw new InvalidDataException("Native Shiva entry requires its selected185 initializer, parameters and bound graphics models.");
        }
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native Shiva entry requires its retained571B50 arena.");
        NativeBattleActionHeaderSlot header = packets.ReadHeader(parameters.Header);
        BattleContactCursor firstContact = header.FirstContact;
        BattleModelOwner target = packets.ReadContact(firstContact).Primary.Recipient;
        NativeBattleGraphicsModel model = models.Get(target);
        short yaw = model.Rotation14 ?? throw new NotSupportedException("Native Shiva entry requires current first-contact rotation WORD0E.");
        NativeBattleActionMatrix actorMatrix = model.GeometryWorldMatrix ?? throw new NotSupportedException("Native Shiva entry requires the current first-contact matrix at40h.");
        NativeBattleVector center = NativeBattleActionGeometryRules.TargetCenter(parameters.TargetActors.Enumerate().Select(models.Get).Select(selected => selected.Transform?.Position28 ?? throw new NotSupportedException("Native Shiva target bounds require every selected model position.")).ToArray());
        NativeBattleActionMatrix world = NativeBattleGfMatrices.Euler56CD50(new(0, unchecked((short)(yaw + 0x800)), 0));
        NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(actorMatrix, new(0, 0, -4000));
        world = world with { X = offset.X, Y = offset.Y, Z = unchecked(center.Z + offset.Z) };
        for (int index = 0; index < 150; index++)
        {
            arena = arena.Write(FirstWorkspaceOffset + index * 0x18, new byte[4]);
        }
        for (int index = 0; index < 100; index++)
        {
            arena = arena.Write(SecondWorkspaceOffset + index * 0x18, new byte[4]);
        }
        int timOffset = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x22BC150)?.ArenaOffset ?? throw new NotSupportedException("Native Shiva entry requires its executed initializer TIM publication.");
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native Shiva entry requires its retained texture queue.");
        (textures, _) = textures.EnqueueArenaTim505E30(arena, timOffset);
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(0x22BC180, 0x22BC160, 0x10, 2);
        int mainSlot;
        (main, mainSlot) = main.Append(0x5C7F50);
        main = main.WriteSlot(mainSlot, main.Slots[mainSlot].WriteWord(0x0C, 0));
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x22BC198, 0x22BC1A8, 0x24, 100);
        int childSlot;
        (children, childSlot) = children.Append(0x5C0F30);
        children = children.WriteSlot(childSlot, children.Slots[childSlot].WriteWord(0x0C, 0));
        return new(memory with { Arena = arena, Textures = textures }, effect with { Pools = [main, children], ReturnedDescriptor = main.DescriptorAddress }, new(center, world, 0));
    }
}
