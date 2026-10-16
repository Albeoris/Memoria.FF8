using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private static uint NativeParticipantAddress(int index)
    {
        return checked(0x1D27B10u + 208u * (uint)index);
    }

    private static NativeBattleGraphicsState QueueNativeDescriptor(NativeBattleGraphicsState graphics, ushort command, uint argument, ushort? index = null, ushort? value = null, byte priority = 128)
    {
        return graphics with { Descriptors = graphics.Descriptors.Enqueue(command, priority, argument, index, value).State };
    }

    private NativeBattleGraphicsState QueueNativeEntryLoads(NativeBattleGraphicsState graphics)
    {
        ushort stage = checked((ushort)(definition.StageId ?? throw new NotSupportedException("Native1002 entry requires its actual formation stage BYTE.")));
        graphics = QueueNativeDescriptor(graphics, 1002, 0, stage);
        graphics = QueueNativeDescriptor(graphics, 1, 0);
        graphics = QueueNativeDescriptor(graphics, 1003, 0);
        graphics = QueueNativeDescriptor(graphics, 9, 0);
        foreach (BattleCombatant actor in combatants.Where(actor => !actor.IsPlayer && actor.IsActive).OrderBy(actor => actor.SceneSlot))
        {
            graphics = QueueNativeDescriptor(graphics, 102, NativeParticipantAddress(actor.NativeIndex), checked((ushort)actor.NativeIndex), checked((ushort)NativeModelId(actor)));
        }
        return QueueNativeDescriptor(graphics, 10, 0x47DD80);
    }

    private NativeBattleGraphicsState QueueNativeEntryModels(NativeBattleGraphicsState graphics)
    {
        foreach (BattleCombatant actor in combatants.Where(actor => !actor.IsPlayer && actor.IsActive).OrderBy(actor => actor.SceneSlot))
        {
            graphics = QueueNativeDescriptor(graphics, 103, NativeParticipantAddress(actor.NativeIndex), checked((ushort)actor.NativeIndex), (ushort)(1 | (actor.Hidden ? 0x100 : 0)));
        }
        foreach (BattleCombatant actor in combatants.Where(actor => actor.IsPlayer && actor.IsActive).OrderBy(actor => actor.NativeIndex))
        {
            graphics = QueueNativeDescriptor(graphics, 102, NativeParticipantAddress(actor.NativeIndex), checked((ushort)actor.NativeIndex), checked((ushort)NativeModelId(actor)));
        }
        graphics = QueueNativeDescriptor(graphics, 13, 0);
        foreach (BattleCombatant actor in combatants.Where(actor => actor.IsPlayer && actor.IsActive).OrderBy(actor => actor.NativeIndex))
        {
            graphics = QueueNativeDescriptor(graphics, 103, NativeParticipantAddress(actor.NativeIndex), checked((ushort)actor.NativeIndex), 0);
        }
        graphics = QueueNativeDescriptor(graphics, 10, 0x4A94B0);
        (NativeBattleDescriptorState descriptors, int barrier) = graphics.Descriptors.Enqueue(112, 128, 0);
        graphics = graphics with { Descriptors = descriptors, EntryBarrierPacket = barrier, EntryLifecycle = 2 };
        return QueueNativeDescriptor(graphics, 10, 0x47DD70);
    }

    private NativeBattleGraphicsState AdvanceNativeDescriptors(NativeBattleGraphicsState graphics)
    {
        NativeBattleDescriptorState final = graphics.Descriptors.Advance(Dispatch, Poll);
        return graphics with { Descriptors = final };

        NativeBattleDescriptorDispatch Dispatch(NativeBattleDescriptorState descriptors, int slot)
        {
            graphics = graphics with { Descriptors = descriptors };
            NativeBattleDescriptorPacket packet = descriptors.Packets[slot];
            NativeBattleDescriptorTaskKind kind;
            switch (packet.Command)
            {
                case 1:
                    graphics = graphics with { Flags = graphics.Flags | 4 };
                    return new(graphics.Descriptors, 15);
                case 9:
                    kind = NativeBattleDescriptorTaskKind.Map500AC0;
                    break;
                case 10:
                    graphics = InvokeNativeDescriptorCallback(graphics, packet.Argument, packet.Data);
                    return new(graphics.Descriptors, 15);
                case 13:
                    kind = NativeBattleDescriptorTaskKind.Appearance500B60;
                    break;
                case 102:
                    kind = NativeBattleDescriptorTaskKind.Load502670;
                    break;
                case 103:
                    graphics = CompleteNativeGraphicsModel(graphics, packet);
                    return new(graphics.Descriptors, 15);
                case 105:
                    NativeBattleGraphicsModel removed = graphics.Models[RequireDescriptorModel(packet.Index)];
                    if ((removed.Flags0 & 2) == 0)
                    {
                        return new(graphics.Descriptors, 15);
                    }
                    graphics = graphics with { Models = graphics.Models.SetItem(removed.Index, removed with { Flags0 = (ushort)(removed.Flags0 | 0x10) }) };
                    kind = NativeBattleDescriptorTaskKind.Remove502ED0;
                    break;
                case 106:
                    int index = RequireDescriptorModel(packet.Index);
                    NativeBattleGraphicsModel model = SetNativeGraphicsFlags(graphics.Models[index], 0);
                    graphics = graphics with { Models = graphics.Models.SetItem(index, NativeBattleGraphicsModels.SelectSequence(model, 3)) };
                    return new(graphics.Descriptors, 15);
                case 112:
                    kind = NativeBattleDescriptorTaskKind.Barrier5085F0;
                    break;
                case 113:
                    kind = NativeBattleDescriptorTaskKind.Callback502F30;
                    break;
                case 1002:
                    kind = NativeBattleDescriptorTaskKind.Stage506CF0;
                    break;
                case 1003:
                    kind = NativeBattleDescriptorTaskKind.Texture506DE0;
                    break;
                default:
                    throw new NotSupportedException($"Native graphics descriptor{packet.Command} requires its original dispatcher.");
            }
            return new(graphics.Descriptors.AppendTask(kind, slot).State, 8);
        }

        NativeBattleDescriptorCompletion Poll(NativeBattleDescriptorState descriptors, int taskSlot)
        {
            graphics = graphics with { Descriptors = descriptors };
            NativeBattleDescriptorTask task = descriptors.Tasks[taskSlot];
            int slot = task.PacketSlot;
            NativeBattleDescriptorPacket packet = descriptors.Packets[slot];
            bool completed = false;
            bool completePacket = true;
            byte phase = task.Phase;
            switch (task.Kind)
            {
                case NativeBattleDescriptorTaskKind.Stage506CF0:
                    // TODO(TD-009): The authenticated stage is already loaded; retain native506CF0 phases while materializing its IO-ready input
                    if (phase == 1)
                    {
                        InitializeNativeStageCamera();
                        graphics = graphics with { Flags = graphics.Flags | 8 };
                    }
                    completed = phase == 3;
                    phase++;
                    break;
                case NativeBattleDescriptorTaskKind.Texture506DE0:
                    completed = true;
                    phase = 1;
                    break;
                case NativeBattleDescriptorTaskKind.Map500AC0:
                    // TODO(TD-009): The original map/texture resources are immutable inputs; native500AC0's IO-ready endpoint is explicit here
                    completed = phase == 1;
                    phase++;
                    break;
                case NativeBattleDescriptorTaskKind.Load502670:
                    int modelIndex = RequireDescriptorModel(packet.Index);
                    NativeBattleGraphicsModel loading = graphics.Models[modelIndex];
                    if (phase == 0)
                    {
                        if ((loading.Flags0 & 1) != 0)
                        {
                            return new(descriptors, false);
                        }
                        BattleCombatant actor = combatants.Single(candidate => candidate.NativeIndex == modelIndex);
                        graphics = graphics with { Models = graphics.Models.SetItem(modelIndex, loading with { Flags0 = (ushort)(loading.Flags0 | 1), ModelId4 = checked((byte)packet.Value) }), ActorIds = graphics.ActorIds.SetItem(modelIndex, actor.Id), ModelLoadCount = graphics.ModelLoadCount + 1 };
                        phase = 2;
                    }
                    else
                    {
                        graphics = graphics with { ModelLoadCount = graphics.ModelLoadCount - 1 };
                        completed = true;
                    }
                    break;
                case NativeBattleDescriptorTaskKind.Remove502ED0:
                    int removedIndex = RequireDescriptorModel(packet.Index);
                    NativeBattleGraphicsModel removed = graphics.Models[removedIndex];
                    if (phase == 0 && removed.Kind5 == 0)
                    {
                        graphics = graphics with { Models = graphics.Models.SetItem(removedIndex, NativeBattleGraphicsModels.StartService(removed, 5)) };
                        phase = 1;
                    }
                    else if (phase == 1 && removed.Kind5 == 0)
                    {
                        completed = true;
                    }
                    break;
                case NativeBattleDescriptorTaskKind.Appearance500B60:
                    completePacket = false;
                    if (phase == 0)
                    {
                        graphics = graphics with { Flags = graphics.Flags | 0x80, EntryGatePhase = 1, Descriptors = descriptors.SetPacketState(slot, 255) };
                        phase = 1;
                    }
                    else if (!ModelsBusy(graphics.Models))
                    {
                        graphics = graphics with { Flags = graphics.Flags & ~0x80u, EntryGatePhase = 2 };
                        completed = true;
                    }
                    break;
                case NativeBattleDescriptorTaskKind.Barrier5085F0:
                    byte completion = 0;
                    _ = NativeBattleGraphicsCompletion.PollTask112(graphics.Effects.PublishedCount, graphics.Models.Select(model => model.CompletionInputs()).ToArray(), NativeCameraTaskMask, ref completion);
                    completed = completion == 255;
                    if (slot == graphics.EntryBarrierPacket)
                    {
                        graphics = graphics with { EntryBarrierCompletion = completion };
                    }
                    break;
                case NativeBattleDescriptorTaskKind.Callback502F30:
                    int index = RequireDescriptorModel(packet.Index);
                    NativeBattleGraphicsModel model = graphics.Models[index];
                    if ((model.Flags0 & 2) != 0 && (model.Flags0 & 8) == 0 && (model.Flags8!.Value & 26) == 0 && (model.Kind5 != 0 || (model.ResourceFlags44!.Value & 64) == 0))
                    {
                        return new(descriptors, false);
                    }
                    graphics = InvokeNativeDescriptorCallback(graphics, packet.Argument, unchecked((ushort)packet.Index));
                    completed = true;
                    break;
                default:
                    throw new NotSupportedException("Native descriptor callback requires its original task owner.");
            }
            descriptors = graphics.Descriptors;
            byte[] payload = descriptors.Tasks[taskSlot].Payload.ToArray();
            payload[1] = phase;
            descriptors = descriptors with { Tasks = descriptors.Tasks.SetItem(taskSlot, descriptors.Tasks[taskSlot] with { Payload = payload.ToImmutableArray() }) };
            return new(completed && completePacket ? descriptors.SetPacketState(slot, 255) : descriptors, completed);
        }
    }

    private NativeBattleGraphicsState InvokeNativeDescriptorCallback(NativeBattleGraphicsState graphics, uint callback, uint argument)
    {
        return callback switch
        {
            0 => graphics,
            0x47DDA0 or 0x47DE70 => RunNativeDescriptorActionCallback(graphics, callback),
            0x47DD80 => graphics with { EntryLifecycle = 1 },
            0x4A94B0 => graphics with { NativeMenuControl942Bit8 = true },
            0x47DD70 => graphics with { EntryLifecycle = 3, EntryReady = true },
            0x48AD10 => CompleteNativeAllocationStatistics(graphics),
            0x487670 => CompleteNativeActivation(graphics, checked((int)argument)),
            0x4876B0 => CompleteNativeAllocationDescriptor(graphics),
            _ => throw new NotSupportedException($"Native graphics callback{callback:X} requires its original participant/global owner.")
        };
    }

    private NativeBattleGraphicsState CompleteNativeGraphicsModel(NativeBattleGraphicsState graphics, NativeBattleDescriptorPacket packet)
    {
        int index = RequireDescriptorModel(packet.Index);
        BattleCombatant actor = FindNativeActor(index);
        NativeBattleGraphicsBinding binding = FindNativeGraphicsBinding(actor.Id) ?? throw new InvalidDataException("Native103 has no original allocation/resource binding.");
        NativeGlobalRandomState randomState = nativeGlobalRandom ?? throw new NotSupportedException("Native103 requires saved534AA0 random state.");
        uint flags = definition.NativeGraphics!.Rules.Project(NativeSavedStatusBits(actor), NativeBattleStatusBits(actor));
        NativeBattleGraphicsModel model = NativeBattleGraphicsModels.CompleteLoad(graphics.Models[index], binding.ModelId, flags, ref randomState);
        if (binding.InitialPosition is NativeBattleVector position)
        {
            NativeBattlePoseSource pose = binding.Resource.PoseSource ?? throw new NotSupportedException("Native103 transform requires its original skeleton scaleWORD2.");
            short scale = unchecked((short)(System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(pose.Skeleton.AsSpan().Slice(2)) * 4));
            model = model with { Transform = new(position, position, 0, 0, scale) };
        }
        nativeGlobalRandom = randomState;
        nativeGlobalRandomCalls = checked(nativeGlobalRandomCalls + 3);
        byte standing = actor.IsPlayer ? NativeStandingSequence(flags) : (byte)1;
        model = model with { Flags0 = (packet.Value & 0x100) != 0 ? (ushort)(model.Flags0 | 4) : model.Flags0, SequenceState = new() { Default2 = standing, Pending3 = actor.IsPlayer && standing != 8 ? standing : (byte)0 } };
        return graphics with { Models = graphics.Models.SetItem(index, model), ActorIds = graphics.ActorIds.SetItem(index, actor.Id) };
    }

    private static int RequireDescriptorModel(int index)
    {
        if ((uint)index >= 7)
        {
            throw new InvalidDataException("Native descriptor retained payload does not identify a physical graphics model.");
        }
        return index;
    }
}
