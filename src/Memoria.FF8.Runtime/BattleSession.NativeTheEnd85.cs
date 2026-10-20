using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeTheEnd85(NativeBattleGraphicsState graphics, bool initialize) => new NativeTheEnd85Runner(this, graphics).Run(initialize);

    private void CompleteNativeTheEnd85File(uint callback)
    {
        NativeBattleTheEnd85Frame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native The End file completion requires its current memory."), NativeGraphics?.OriginalEffect ?? throw new NotSupportedException("Native The End file completion requires its selected effect."), nativeCrtRandom ?? throw new NotSupportedException("Native The End file completion requires its CRT stream."));
        NativeEffectMemory = NativeBattleTheEnd85Io.Complete(frame, callback).Memory;
    }

    private sealed partial class NativeTheEnd85Runner
    {
        private readonly BattleSession owner;
        private NativeBattleGraphicsState graphics;
        private NativeBattleTheEnd85Frame frame;
        private readonly NativeBattleTheEnd85Resources resources;
        private NativeBattleTheEnd85State State
        {
            get => frame.Memory.TheEnd85 ?? throw new NotSupportedException("Native The End requires its retained process owner.");
            set => frame = frame with { Memory = frame.Memory with { TheEnd85 = value } };
        }
        private NativeBattleCameraState Camera => owner.NativeCamera ?? throw new NotSupportedException("Native The End requires its current camera.");
        private NativeBattleCameraProjection Projection => Camera.Projection ?? throw new NotSupportedException("Native The End requires its current projection.");
        private NativeBattleSummonColors Colors => owner.definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native The End requires its current color rules.");
        private NativeBattleFileStaging Staging
        {
            get => owner.NativeFileStaging ?? throw new NotSupportedException("Native The End requires its retained temporary storage.");
            set => owner.NativeFileStaging = value;
        }

        internal NativeTheEnd85Runner(BattleSession owner, NativeBattleGraphicsState graphics)
        {
            this.owner = owner;
            this.graphics = graphics;
            NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native The End requires its selected action.");
            resources = effect.Binding.TheEnd85Resources ?? throw new NotSupportedException("Native The End requires its complete original resources.");
            frame = new(owner.NativeEffectMemory ?? throw new NotSupportedException("Native The End requires its effect memory."), effect, owner.nativeCrtRandom ?? throw new NotSupportedException("Native The End requires its current CRT stream."));
        }

        internal NativeBattleGraphicsState Run(bool initialize)
        {
            if (initialize)
            {
                if (((frame.Effect.Parameters ?? throw new NotSupportedException("Native The End entry requires its current action parameters.")).Flags79 & 1) == 0)
                {
                    NativeBattleEffectTextureState textures = (frame.Memory.Textures ?? throw new NotSupportedException("Native The End requires its current texture queue.")).Register5099A0(resources.Camera);
                    owner.NativeCamera = Camera.RegisterControlResource(textures.Animation);
                    frame = frame with { Memory = frame.Memory with { Textures = textures } };
                }
                frame = NativeBattleTheEnd85Controller.Initialize784480(frame);
                frame = NativeBattleTheEnd85Controller.Initialize7844A0(frame, owner.nativeActionPackets);
            }
            else
            {
                frame = NativeBattleTheEnd85Controller.Advance784630(frame, new(owner.nativeActionPackets, Camera.CapturePackedView1D97778(), ReadModel, ReadPose, InitializeTarget, AdvanceLeaf, Contact, Execute, resources.DirectorAddress));
            }
            owner.NativeEffectMemory = frame.Memory;
            owner.nativeCrtRandom = frame.Random;
            return graphics with { OriginalEffect = frame.Effect };
        }

        private NativeBattleGraphicsModel ReadModel(int actor) => graphics.Models[actor];
        private NativeBattlePoseSource ReadPose(int actor) => owner.NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Native The End requires its current actor pose source.");
        private NativeBattleModulePoolTask Task(uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
        private void Put(uint address, NativeBattleModulePoolTask task) => frame = frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
        private ImmutableArray<byte> Context => State.Read(resources.DirectorAddress, 84).ToImmutableArray();
        private void PutContext(ImmutableArray<byte> context) => State = State.Write(resources.DirectorAddress, context.AsSpan());
        private NativeBattleTheEnd85Frame InitializeTarget(NativeBattleTheEnd85Frame current, uint address)
        {
            frame = current;
            byte[] context = new byte[84];
            BinaryPrimitives.WriteInt16LittleEndian(context.AsSpan(4), 4096);
            int angle = NativeWorldScriptedPathMath.NativeAngle(0, -4096);
            BinaryPrimitives.WriteUInt16LittleEndian(context.AsSpan(74), unchecked((ushort)((angle - 2048) & 4095)));
            State = State.Write(resources.DirectorAddress, context);
            return NativeBattleTheEnd85Io.Initialize784960(frame, resources.Script);
        }

        private uint Append(uint descriptor, uint callback, int size, uint parent, Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask>? initialize = null)
        {
            NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
            int pool = Enumerable.Range(0, hierarchy.Pools.Length).Single(index => hierarchy.Pools[index].DescriptorAddress == descriptor);
            NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(pool, callback, size, parent, (pointer, header, target) => NativeBattleCureController.SelectActors(frame.Effect.Parameters ?? throw new NotSupportedException("Native The End child requires its actual action parameters."), owner.nativeActionPackets, pointer, header, target));
            if (appended.Address == 0)
            {
                throw new NotSupportedException("Native The End exhausted its required physical task slots.");
            }
            frame = frame with { Effect = frame.Effect with { Pools = appended.Hierarchy.Pools } };
            if (initialize is not null)
            {
                Put(appended.Address, initialize(Task(appended.Address)));
            }
            return appended.Address;
        }

        private NativeBattleTheEnd85Frame Contact(NativeBattleTheEnd85Frame current, int contact)
        {
            owner.NativeEffectMemory = current.Memory;
            ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
            graphics = owner.ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [owner.nativeActionPackets.ReadContact(contact)] });
            graphics = graphics with { PendingContacts = pending };
            return current with { Memory = owner.NativeEffectMemory ?? throw new InvalidDataException("Native The End contact lost its process memory.") };
        }

        private NativeBattleTheEnd85Frame Execute(NativeBattleTheEnd85Frame current, NativeBattleChoco97Operation operation)
        {
            if (operation is not NativeBattleChoco97CommonTextureLoad load)
            {
                throw new NotSupportedException("Native The End master requires its actual resource completion operation.");
            }
            (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(checked(0x20DFAB8u + (uint)load.DestinationArenaOffset), load.CompletionAddress, (address, value) => current = current with { Effect = NativeBattleTheEnd85Controller.WriteCompletionByte(current.Effect, address, value) });
            graphics = graphics with { Auxiliary = auxiliary };
            return current;
        }
    }
}
