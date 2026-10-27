namespace Memoria.FF8.Runtime;

public sealed record NativeBattleLionHeart165EntryStep(NativeBattleLionHeart165Frame Frame, short Facing14);

public static partial class NativeBattleLionHeart165Controller
{
    public static NativeBattleLionHeart165EntryStep Enter5E9470(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleFileStaging staging, NativeBattleActionPacketsState packets, NativeBattleLionHeart165Resources resources, NativeCrtRandomState random, NativeBattleVector actorRotation, NativeBattleVector bone241, NativeBattleVector targetCenter, ushort? actorRotationPadding = null)
    {
        if (effect.Binding is not { Effect: 165, Initializer: 0x5E9450, Entry: 0x5E9470 } || effect.Parameters is not { } parameters)
        {
            throw new InvalidDataException("Native5E9470 requires its original binding and actual action parameters.");
        }
        resources.Validate();
        NativeBattleLionHeart165State retained = memory.LionHeart165 ?? NativeBattleLionHeart165State.FromColdProcess();
        if (!retained.GlobalsInitialized)
        {
            staging = staging.Write(NativeBattleLionHeart165Resources.StaticAddress, resources.StaticData.Data.AsSpan());
            foreach (NativeBattleLionHeart165ColdRegion region in resources.ColdGlobals)
            {
                staging = staging.Write(region.Address, region.Data.AsSpan());
            }
        }
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Native5E9470 requires its physical first-contact pointer.");
        byte recipient = packets.ReadContact(first).Primary.ActorIndex;
        NativeBattleEffectResourcePointer initializer = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x23C6874) ?? throw new NotSupportedException("Native5E9470 requires its actual initializer publication.");
        staging = WriteDword(staging, 0x23C6874, checked(0x20DFAB8u + (uint)initializer.ArenaOffset));
        staging = WriteDword(staging, 0x23C32EC, recipient);
        staging = WriteDword(staging, 0x23C6870, parameters.Actor78);
        staging = WriteDword(staging, 0x23C6944, 0x20DFAB8);
        staging = WriteDword(staging, 0x23C694C, 0x209FAB8);
        staging = WriteDword(staging, 0x23C5680, 0x20DFAB8);
        staging = WriteDword(staging, 0x23C6940, 1);
        staging = WriteDword(staging, 0x23C6920, 0);
        for (uint address = 0x23C34C0; address < 0x23C4780; address += 24)
        {
            staging = WriteDword(staging, address, 0);
        }
        for (uint address = 0x23C4780; address < 0x23C5680; address += 32)
        {
            staging = WriteDword(staging, address, 0);
        }
        staging = Point(staging, 0x23C58B0, actorRotation);
        staging = actorRotationPadding is ushort padding ? WriteWord(staging, 0x23C58B6, padding) : Forget(staging, 0x23C58B6, 2);
        staging = Point(staging, 0x23C6928, bone241);
        staging = Point(staging, 0x23C6910, targetCenter);
        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(targetCenter.X - bone241.X, 0, targetCenter.Z - bone241.Z);
        NativeBattleModuleAxisAngle angle = NativeBattleModuleAlignment.Between571480(0, 0, -4096, direction.X, direction.Y, direction.Z);
        NativeBattleActionMatrix matrix = new(NativeBattleModuleAlignment.Rotation5714F0(angle.Angle, angle.Axis), bone241.X, 0, bone241.Z, 4096);
        staging = StoreMatrix(staging, 0x23C58E0, matrix);
        short facing = unchecked((short)(angle.Axis.Y < 0 ? -angle.Angle : angle.Angle));
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(0x23C34B0, 0x23C34A0, 16, 1);
        (main, int mainSlot) = main.Append(0x5F01F0);
        main = main.WriteSlot(mainSlot, main.Slots[mainSlot].WriteWord(12, 0));
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x23C5910, 0x23C5920, 36, 100);
        (children, int childSlot) = children.Append(0x5E9650);
        children = children.WriteSlot(childSlot, children.Slots[childSlot].WriteWord(12, 0));
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native5E9470 requires its actual texture owner.");
        NativeBattleEffectInitializerAsset tim = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native5E9470 requires its actual initializer texture.");
        (textures, _) = textures.EnqueueTim505E30(new(tim.Name, tim.Data));
        NativeBattleLionHeart165Frame frame = new(memory with { LionHeart165 = new(true, parameters, recipient, resources, null), Textures = textures }, effect with { Pools = [main, children], ReturnedDescriptor = 0x23C34B0, DrawArenaOffset = 0 }, staging, random);
        return new(frame, facing);
    }

    public static NativeBattleLionHeart165Frame Advance5F01F0(NativeBattleLionHeart165Frame frame, Func<NativeBattleLionHeart165Frame, NativeBattleEffectTaskPool, int, NativeBattleLionHeart165ChildStep> advanceChild)
    {
        if (frame.Effect.Pools.Length != 2 || frame.Effect.Pools[0].DescriptorAddress != 0x23C34B0 || frame.Effect.Pools[1].DescriptorAddress != 0x23C5910)
        {
            throw new InvalidDataException("Native5F01F0 requires its original task descriptors.");
        }
        NativeBattleEffectTaskStep advanced = frame.Effect.Pools[0].Advance((main, slot) =>
        {
            NativeBattleModulePoolTask task = main.Slots[slot];
            if (task.Callback != 0x5F01F0)
            {
                throw new NotSupportedException("Native Lion Heart main callback is unsupported.");
            }
            ushort counter = task.ReadWord(12);
            int offset = (counter & 1) * 0x8000;
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(0, main), DrawArenaOffset = offset }, Staging = WriteDword(frame.Staging, 0x23C6948, checked(0x209FAB8u + (uint)offset)) };
            NativeBattleEffectTaskStep children = frame.Effect.Pools[1].Advance((pool, index) =>
            {
                NativeBattleLionHeart165ChildStep child = advanceChild(frame, pool, index);
                frame = child.Frame;
                return new(child.Pool, child.Result);
            });
            main = frame.Effect.Pools[0];
            task = main.Slots[slot].WriteWord(12, unchecked((ushort)(counter + 1)));
            main = main.WriteSlot(slot, task);
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(0, main).SetItem(1, children.Pool) } };
            return new(main, children.Survivors == 0 ? 2 : 0);
        });
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(0, advanced.Pool), ReturnedDescriptor = advanced.Survivors == 0 ? 0u : 0x23C34B0u } };
    }
}
