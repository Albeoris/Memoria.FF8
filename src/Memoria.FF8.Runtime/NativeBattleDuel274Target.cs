using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel274SelectedServices(NativeBattleDuel274ActorServices Actors, Func<int, NativeBattleGraphicsModel> ReadModel, Action<int, NativeBattleGraphicsModel> WriteModel, Func<int, NativeBattleModuleAnchor> ReadAnchor571400, Func<NativeBattleDuel274Frame, int, uint, NativeBattleDuel274Frame> Bind5022C0, Func<NativeBattleDuel274Frame, int, byte, NativeBattleDuel274Frame> Select505C00, Func<NativeBattleDuel274Frame, byte, NativeBattleDuel274Frame> Camera503520, Func<NativeBattleDuel274Frame, NativeBattleEffectFileRequest, NativeBattleDuel274Frame> QueueFile, Func<NativeBattleDuel274Frame, int, NativeBattleDuel274Frame> Contact506690, Func<NativeBattleDuel274Frame, uint, uint, NativeBattleDuel274Frame> Texture508630, Func<NativeBattleDuel274Frame, int, byte, NativeBattleDuel274Frame> DrawVariant, Action<int> Stage5713E0, Func<NativeBattleDuel274Frame, byte, NativeBattleDuel274Frame> Draw68D4B0);

public static class NativeBattleDuel274Target
{
    public static NativeBattleDuel274TaskStep AdvanceSelected689AC0(NativeBattleDuel274Frame frame, int slot, NativeBattleDuel274Resources resources, NativeBattleDuel274SelectedServices inputs)
    {
        NativeBattleDuel274TransitionStep transition = NativeBattleDuel274Transition.Advance689AC0(frame, slot, inputs.Actors, inputs.Stage5713E0, inputs.Draw68D4B0);
        frame = transition.Frame;
        if (!transition.ContinueSelected)
        {
            return Result(0);
        }
        NativeBattleModulePoolTask task = frame.Effect.Pools[1].Slots[slot];
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Native Duel target requires its actual parameter owner.");
        int actor = parameters.Actor78;
        int target = NativeBattleDuel274Controller.Index(task.ReadDword(20));
        if (frame.Staging.Read(0x25084D8, 1)[0] != 0)
        {
            frame = NativeBattleDuel274Banks.DetachUnscheduledPrivateBanks(frame);
            int next = 2356;
            frame = frame with { Staging = Dword(frame.Staging, 0x2508434, Pointer(next)) };
            if (task.ReadByte(16) != 0)
            {
                SaveModels();
                Store();
                frame = inputs.Bind5022C0(frame, actor, checked(frame.Effect.Pools[1].BankAddress + (uint)(slot * frame.Effect.Pools[1].Stride) + 56));
                task = frame.Effect.Pools[1].Slots[slot];
                frame = frame with { Memory = NativeBattleEffectIo.Initialize534110(frame.Memory, next) };
                next += 24;
                frame = frame with { Memory = NativeBattleEffectIo.Queue5341D0(frame.Memory, 715, Pointer(next), 1) };
                frame = frame with { Memory = NativeBattleEffectIo.Queue5341D0(frame.Memory, 716, Pointer(next), 16) };
                next += 34816;
                frame = frame with { Staging = Dword(frame.Staging, 0x2508434, Pointer(next)) };
            }
            byte selector = task.ReadByte(15);
            if (selector is < 1 or > 8)
            {
                throw new NotSupportedException("Native Duel selected an unsupported move selector.");
            }
            NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Duel buffer publication requires its actual arena.");
            arena = arena.Write(2348, BitConverter.GetBytes(Pointer(next)));
            int bufferBytes = selector == 6 ? 81920 : 57344;
            next = checked(next + bufferBytes);
            arena = arena.Write(2352, BitConverter.GetBytes(Pointer(next)));
            next = checked(next + bufferBytes);
            frame = frame with { Memory = frame.Memory with { Arena = arena }, Staging = Dword(frame.Staging, 0x2508434, Pointer(next)) };
            NativeBattleGraphicsModel actorModel = inputs.ReadModel(actor);
            inputs.WriteModel(actor, actorModel with { Rotation14 = 0 });
            RestoreTarget();
            Store();
            frame = inputs.Select505C00(frame, actor, selector);
            task = frame.Effect.Pools[1].Slots[slot];
            byte category = inputs.ReadModel(target).SequenceByte145 ?? throw new NotSupportedException("Native Duel camera selection requires its actual target BYTE145.");
            frame = inputs.Camera503520(frame, unchecked((byte)(category + 16 * (selector - 2))));
            task = frame.Effect.Pools[1].Slots[slot];
            task = task.WriteByte(17, resources.Read(checked(0x110E3E0u + selector), 1)[0]).WriteWord(12, 0);
            frame = frame with { Staging = frame.Staging.Write(0x25084D8, [0]) };
            Store();
        }
        task = frame.Effect.Pools[1].Slots[slot];
        if (task.ReadByte(16) != 0)
        {
            NativeBattleEffectIoAdvance io = NativeBattleEffectIo.Advance534210(frame.Memory);
            frame = frame with { Memory = io.Memory };
            if (io.Request is { } request)
            {
                frame = inputs.QueueFile(frame, request);
            }
        }
        task = frame.Effect.Pools[1].Slots[slot];
        short counter = Signed(task, 12);
        int duration = task.ReadByte(17);
        if (counter <= duration)
        {
            byte selector = task.ReadByte(15);
            if (selector == 8)
            {
                if (counter == 0)
                {
                    RestoreActor();
                    RestoreTarget();
                    if (inputs.ReadModel(target).ModelId4 == 84)
                    {
                        for (int index = 0; index < 3; index++)
                        {
                            NativeBattleGraphicsModel model = inputs.ReadModel(index);
                            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Duel party restoration requires the actual position fields.");
                            short z = BinaryPrimitives.ReadInt16LittleEndian(frame.Staging.Read(checked(0x25084E0u + (uint)index * 2), 2));
                            inputs.WriteModel(index, model with { Transform = transform with { Position28 = transform.Position28 with { Z = z } } });
                        }
                    }
                }
                if ((frame.Effect.Parameters!.Parameter82 ?? throw new NotSupportedException("Native Duel closing requires its actual BYTE82.")) != 0)
                {
                    Ready();
                    return Result(2);
                }
                if (counter == 0)
                {
                    Store();
                    frame = inputs.Contact506690(frame, task.ReadByte(14));
                    task = frame.Effect.Pools[1].Slots[slot].WriteByte(96, 0);
                    Store();
                    uint destination = BinaryPrimitives.ReadUInt32LittleEndian((frame.Memory.Arena ?? throw new NotSupportedException("Native Duel closing requires its current texture destination.")).Read(2348, 4));
                    frame = inputs.Texture508630(frame, destination, checked(frame.Effect.Pools[1].BankAddress + (uint)(slot * frame.Effect.Pools[1].Stride) + 96));
                    task = frame.Effect.Pools[1].Slots[slot];
                }
                if (task.ReadByte(96) != 0 && Signed(task, 12) >= duration)
                {
                    Ready();
                    return Result(2);
                }
                task = task.WriteWord(12, unchecked((ushort)(Signed(task, 12) + 1)));
                Store();
                return Result(0);
            }
            if (selector is >= 2 and <= 7)
            {
                Store();
                frame = inputs.DrawVariant(frame, slot, selector);
                task = frame.Effect.Pools[1].Slots[slot];
            }
        }
        counter = unchecked((short)(Signed(task, 12) + 1));
        task = task.WriteWord(12, unchecked((ushort)counter));
        Store();
        if (counter == duration)
        {
            if (task.ReadByte(16) != 0)
            {
                NativeBattleEffectIoPoll poll = NativeBattleEffectIo.Poll534270(frame.Memory);
                frame = frame with { Memory = poll.Memory };
                if (poll.Request is { } request)
                {
                    frame = inputs.QueueFile(frame, request);
                }
                task = frame.Effect.Pools[1].Slots[slot];
                if (poll.Busy)
                {
                    task = task.WriteWord(12, unchecked((ushort)(Signed(task, 12) - 1)));
                    Store();
                    return Result(0);
                }
            }
            task = task.WriteByte(16, 0).WriteByte(18, 1).WriteByte(19, 0);
            Store();
            Ready();
        }
        task = frame.Effect.Pools[1].Slots[slot];
        if (Signed(task, 12) > duration)
        {
            task = task.WriteWord(12, checked((ushort)duration));
            Store();
        }
        return Result(0);

        void SaveModels()
        {
            NativeBattleGraphicsModel actorModel = inputs.ReadModel(actor);
            NativeBattleModelTransform actorTransform = actorModel.Transform ?? throw new NotSupportedException("Native Duel setup requires its actual actor position fields.");
            task = task.WriteDword(24, Pack(actorTransform.Position28.X, actorTransform.Position28.Y)).WriteDword(28, Pack(actorTransform.Position28.Z, actorTransform.Vertical34));
            task = task.WriteWord(32, unchecked((ushort)(actorModel.Rotation12 ?? throw new NotSupportedException("Native Duel setup requires actor WORD12.")))).WriteWord(34, unchecked((ushort)(actorModel.Rotation14 ?? throw new NotSupportedException("Native Duel setup requires actor WORD14.")))).WriteWord(36, unchecked((ushort)(actorModel.Rotation16 ?? throw new NotSupportedException("Native Duel setup requires actor WORD16."))));
            task = actorModel.RotationPadding18 is ushort padding ? task.WriteWord(38, padding) : task.MarkPayloadUnknown(38, 2);
            NativeBattleGraphicsModel targetModel = inputs.ReadModel(target);
            NativeBattleModelTransform targetTransform = targetModel.Transform ?? throw new NotSupportedException("Native Duel setup requires its actual target position fields.");
            task = task.WriteDword(48, Pack(targetTransform.Position28.X, targetTransform.Position28.Y)).WriteDword(52, Pack(targetTransform.Position28.Z, targetTransform.Vertical34));
            NativeBattleModuleAnchor anchor = inputs.ReadAnchor571400(target);
            task = task.WriteDword(40, Pack(anchor.X, anchor.Y)).WriteDword(44, Pack(anchor.Z, anchor.W));
            byte modelId = targetModel.ModelId4 ?? throw new NotSupportedException("Native Duel target positioning requires its actual BYTE4.");
            if (modelId == 140)
            {
                task = task.WriteWord(44, unchecked((ushort)(Signed(task, 44) - 3200)));
            }
            else if (modelId == 84)
            {
                for (int index = 0; index < 3; index++)
                {
                    NativeBattleGraphicsModel model = inputs.ReadModel(index);
                    NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Duel party positioning requires its actual position fields.");
                    frame = frame with { Staging = frame.Staging.Write(checked(0x25084E0u + (uint)index * 2), BitConverter.GetBytes(transform.Position28.Z)) };
                    inputs.WriteModel(index, model with { Transform = transform with { Position28 = transform.Position28 with { Z = unchecked((short)(transform.Position28.Z + 1000)) } } });
                }
                task = task.WriteWord(44, unchecked((ushort)(Signed(task, 44) - 2000)));
            }
            task = task.WriteWord(42, task.ReadWord(50));
        }
        void RestoreActor()
        {
            NativeBattleGraphicsModel model = inputs.ReadModel(actor);
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Duel restoration requires its actual actor position fields.");
            bool unknownPadding = task.UnknownPayloadRanges is { } unknown && unknown.Any(range => range.Offset < 40 && range.Offset + range.Length > 38);
            inputs.WriteModel(actor, model with { Transform = transform with { Position28 = transform.Position28 with { X = Signed(task, 24), Y = Signed(task, 26), Z = Signed(task, 28) }, Vertical34 = Signed(task, 30) }, Rotation12 = Signed(task, 32), Rotation14 = Signed(task, 34), Rotation16 = Signed(task, 36), RotationPadding18 = unknownPadding ? null : task.ReadWord(38) });
        }
        void RestoreTarget()
        {
            NativeBattleGraphicsModel model = inputs.ReadModel(target);
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Duel restoration requires its actual target position fields.");
            inputs.WriteModel(target, model with { Transform = transform with { Position28 = transform.Position28 with { X = Signed(task, 48), Y = Signed(task, 50), Z = Signed(task, 52) }, Vertical34 = Signed(task, 54) } });
            HashSet<int> visited = [];
            int current = target;
            do
            {
                if (!visited.Add(current))
                {
                    throw new InvalidDataException("Native Duel restoration model ring does not return to its source.");
                }
                ushort flags = inputs.Actors.ReadFlags(current);
                if ((flags & 2) != 0)
                {
                    ushort original = BinaryPrimitives.ReadUInt16LittleEndian(frame.Staging.Read(checked(0x2508414u + (uint)current * 4), 2));
                    inputs.Actors.WriteFlags(current, (ushort)(flags ^ ((flags ^ original) & 0x1004)));
                }
                uint linked = inputs.Actors.ReadLinkedModel140(current);
                if (linked == 0)
                {
                    break;
                }
                current = NativeBattleDuel274Controller.Index(linked);
            }
            while (current != target);
        }
        void Store()
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool with { Slots = pool.Slots.SetItem(slot, task) }) } };
        }
        void Ready()
        {
            NativeBattleEffectParameters current = frame.Effect.Parameters ?? throw new NotSupportedException("Native Duel readiness requires its actual parameter owner.");
            frame = frame with { Effect = frame.Effect with { Parameters = current with { Parameter81 = 1 } } };
        }
        NativeBattleDuel274TaskStep Result(int result) => new(frame, frame.Effect.Pools[1], result);
    }

    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static uint Pack(short first, short second) => unchecked((ushort)first) | unchecked((uint)(ushort)second << 16);
    private static uint Pointer(int offset) => checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset);
    private static NativeBattleFileStaging Dword(NativeBattleFileStaging staging, uint address, uint value) => staging.Write(address, BitConverter.GetBytes(value));
}
