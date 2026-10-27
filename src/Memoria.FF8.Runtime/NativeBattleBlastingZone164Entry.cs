namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBlastingZone164EntryStep(NativeBattleBlastingZone164Frame Frame, short Facing14);

public static partial class NativeBattleBlastingZone164Controller
{
    public static NativeBattleBlastingZone164EntryStep Enter5F0260(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleFileStaging staging, NativeBattleActionPacketsState packets, NativeBattleBlastingZone164Resources resources, NativeCrtRandomState random, NativeBattleVector actorRotation, NativeBattleVector bone241, NativeBattleVector targetCenter, ushort? actorRotationPadding = null)
    {
        if (effect.Binding is not { Effect: 164, Initializer: 0x5F0240, Entry: 0x5F0260 } || effect.Parameters is not { } parameters)
        {
            throw new InvalidDataException("Native5F0260 requires its original binding and actual action parameters.");
        }
        resources.Validate();
        NativeBattleBlastingZone164State retained = memory.BlastingZone164 ?? NativeBattleBlastingZone164State.FromColdProcess();
        if (!retained.GlobalsInitialized)
        {
            staging = staging.Write(NativeBattleBlastingZone164Resources.StaticAddress, resources.StaticData.Data.AsSpan());
            foreach (NativeBattleBlastingZone164ColdRegion region in resources.ColdGlobals)
            {
                staging = staging.Write(region.Address, region.Data.AsSpan());
            }
        }
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Native5F0260 requires its physical first-contact pointer.");
        byte recipient = packets.ReadContact(first).Primary.ActorIndex;
        NativeBattleEffectResourcePointer initializer = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x23C9D8C) ?? throw new NotSupportedException("Native5F0260 requires its actual initializer publication.");
        staging = WriteDword(staging, 0x23C9D8C, checked(0x20DFAB8u + (uint)initializer.ArenaOffset));
        staging = WriteDword(staging, 0x23C6950, recipient);
        staging = WriteDword(staging, 0x23C9D88, parameters.Actor78);
        staging = WriteDword(staging, 0x23CA114, 0x20DFAB8);
        for (uint address = 0x23C6978; address < 0x23C8EF8; address += 24)
        {
            staging = WriteDword(staging, address, 0);
        }
        staging = Point(staging, 0x23C8F18, actorRotation);
        staging = actorRotationPadding is ushort padding ? WriteWord(staging, 0x23C8F1E, padding) : Forget(staging, 0x23C8F1E, 2);
        staging = Point(staging, 0x23C9DA0, bone241);
        staging = Point(staging, 0x23C9D90, targetCenter);
        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(targetCenter.X - bone241.X, 0, targetCenter.Z - bone241.Z);
        NativeBattleModuleAxisAngle angle = NativeBattleModuleAlignment.Between571480(0, 0, -4096, direction.X, direction.Y, direction.Z);
        NativeBattleActionMatrix matrix = new(NativeBattleModuleAlignment.Rotation5714F0(angle.Angle, angle.Axis), bone241.X, 0, bone241.Z, 4096);
        staging = StoreMatrix(staging, 0x23C8F20, matrix);
        short facing = unchecked((short)(angle.Axis.Y < 0 ? -angle.Angle : angle.Angle));
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(0x23C6968, 0x23C6958, 16, 1);
        (main, int mainSlot) = main.Append(0x5F5B10);
        main = main.WriteSlot(mainSlot, main.Slots[mainSlot].WriteWord(12, 0));
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x23C8F40, 0x23C8F50, 36, 100);
        (children, int childSlot) = children.Append(0x5F0430);
        children = children.WriteSlot(childSlot, children.Slots[childSlot].WriteWord(12, 0));
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native5F0260 requires its actual texture owner.");
        NativeBattleEffectInitializerAsset tim = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native5F0260 requires its actual initializer texture.");
        (textures, _) = textures.EnqueueTim505E30(new(tim.Name, tim.Data));
        NativeBattleBlastingZone164Frame frame = new(memory with { BlastingZone164 = new(true, parameters, recipient, resources, null), Textures = textures }, effect with { Pools = [main, children], ReturnedDescriptor = 0x23C6968, DrawArenaOffset = 0 }, staging, random);
        return new(frame, facing);
    }

    public static NativeBattleBlastingZone164Frame Advance5F5B10(NativeBattleBlastingZone164Frame frame, Func<NativeBattleBlastingZone164Frame, NativeBattleEffectTaskPool, int, NativeBattleBlastingZone164ChildStep> advanceChild)
    {
        if (frame.Effect.Pools.Length != 2 || frame.Effect.Pools[0].DescriptorAddress != 0x23C6968 || frame.Effect.Pools[1].DescriptorAddress != 0x23C8F40)
        {
            throw new InvalidDataException("Native5F5B10 requires its original task descriptors.");
        }
        NativeBattleEffectTaskStep advanced = frame.Effect.Pools[0].Advance((main, slot) =>
        {
            NativeBattleModulePoolTask task = main.Slots[slot];
            if (task.Callback != 0x5F5B10)
            {
                throw new NotSupportedException("Native Blasting Zone main callback is unsupported.");
            }
            ushort counter = task.ReadWord(12);
            int offset = (counter & 1) * 0x10000;
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(0, main), DrawArenaOffset = offset }, Staging = WriteDword(frame.Staging, 0x23CA118, checked(0x20DFAB8u + (uint)offset)) };
            NativeBattleEffectTaskStep children = frame.Effect.Pools[1].Advance((pool, index) =>
            {
                NativeBattleBlastingZone164ChildStep child = advanceChild(frame, pool, index);
                frame = child.Frame;
                return new(child.Pool, child.Result);
            });
            main = frame.Effect.Pools[0];
            task = main.Slots[slot].WriteWord(12, unchecked((ushort)(counter + 1)));
            main = main with { Slots = main.Slots.SetItem(slot, task) };
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(0, main).SetItem(1, children.Pool) } };
            return new(main, children.Survivors == 0 ? 2 : 0);
        });
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(0, advanced.Pool), ReturnedDescriptor = advanced.Survivors == 0 ? 0u : 0x23C6968u } };
    }
}
