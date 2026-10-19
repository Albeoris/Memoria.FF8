using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleShellController
{
    public const uint RootDescriptor = 0x26C0470;

    public static NativeBattleShellFrame Initialize(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets, NativeBattleShellResources resources, NativeCrtRandomState random)
    {
        if (effect.Binding is not { Effect: 33, Entry: 0x866010 } || effect.Parameters is not { } parameters)
        {
            throw new InvalidDataException("Native Shell requires its original entry and parameter block.");
        }
        NativeBattleShellState owner = memory.Shell33 ?? throw new NotSupportedException("Native Shell requires its retained module owner; migrate an authenticated standalone source.");
        if (owner.PendingColdActorBank)
        {
            if (!owner.ActorBank.IsEmpty)
            {
                throw new InvalidDataException("Native Shell pending cold bank already has mutable resource bytes.");
            }
            owner = owner with { ActorBank = resources.ActorBank, PendingColdActorBank = false };
        }
        if (owner.ActorBank.Length != NativeBattleShellResources.ActorBankLength || owner.Channels.Length != NativeBattleShellResources.ChannelLength || owner.Blend.Length != 0x2E0)
        {
            throw new InvalidDataException("Native Shell requires its complete current executable and channel owners.");
        }
        int source = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x26C0380)?.ArenaOffset ?? throw new NotSupportedException("Native Shell requires its executed initializer allocation.");
        if (source > NativeBattleEffectArena.Length - 0x14994)
        {
            throw new InvalidDataException("Native Shell auxiliary banks leave the effect arena.");
        }
        NativeBattleEffectHierarchy hierarchy = new([
            NativeBattleEffectTaskPool.Initialize(RootDescriptor, 0x26C03A8, 100, 2),
            NativeBattleEffectTaskPool.Initialize(0x26C2C68, 0x26C2C78, 88, 4),
            NativeBattleEffectTaskPool.Initialize(0x26C29C8, 0x26C21D8, 676, 3),
            NativeBattleEffectTaskPool.Initialize(0x26C21C0, 0x26C0488, 748, 10),
            NativeBattleEffectTaskPool.Initialize(0x26C2C58, 0x26C29D8, 64, 10)
        ]);
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, 0x866150, 100);
        hierarchy = root.Hierarchy;
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Native Shell requires its actual first contact.");
        byte targets = header.ReadByte(16);
        byte groups = header.ReadByte(17);
        byte maximum = Math.Max(unchecked((byte)(targets - 1)), groups);
        NativeBattleModulePoolTask task = hierarchy.Read(root.Address).WriteDword(12, 0x1D99A78).WriteByte(0x2C, header.ReadByte(0)).WriteByte(0x2D, packets.ReadContact(first).Primary.ActorIndex).WriteWord(0x5A, targets).WriteWord(0x58, groups).WriteByte(0x2F, maximum);
        hierarchy = hierarchy.Write(root.Address, task);
        if ((parameters.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native Shell requires its retained texture queue.");
            textures = textures.Register5099A0(resources.Animation);
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Native Shell requires its loaded texture arena."), source);
            memory = memory with { Textures = textures };
        }
        return new(memory with { Shell33 = owner with { Source = source } }, effect with { Pools = hierarchy.Pools, ReturnedDescriptor = RootDescriptor }, random);
    }

    public static NativeBattleShellFrame Advance(NativeBattleShellFrame frame, NativeBattleShellResources resources, NativeBattleShellInputs inputs)
    {
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        NativeBattleShellFrame current = frame;
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Native Shell lost its action context.");
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleEffectHierarchyStep root = hierarchy.Advance(0, (state, address) =>
        {
            NativeBattleModulePoolTask task = state.Read(address);
            NativeBattleShellState owner = current.Memory.Shell33 ?? throw new NotSupportedException("Native Shell lost its retained module state.");
            int source = owner.Source ?? throw new NotSupportedException("Native Shell lost its executed initializer pointer.");
            bool odd = (task.ReadWord(0x5C) & 1) != 0;
            current = current with { Memory = current.Memory with { Shell33 = owner with { Cursor = source + (odd ? 0 : 0x6000), SecondaryCursor = source + (odd ? 0x6000 : 0xC000), PassEmitters = 0, PassGeometry = 0 } } };
            task = NativeBattleEffectModelHelpers.UpdateCaster8DC740(task, parameters, Models(), inputs.ReadPoseSource);
            state = state.Write(address, task);
            switch (unchecked((sbyte)task.ReadByte(0x29)))
            {
                case 0:
                case 1:
                case 7:
                case 8:
                    state = Next(state, address);
                    break;
                case 2:
                    if (task.ReadByte(0x28) == 0)
                    {
                        int emitters = checked(source + 0xC000);
                        int geometry = checked(emitters + 0x654);
                        NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Native Shell requires its current auxiliary arena.");
                        arena = arena.Write(emitters, new byte[0x654]).Write(geometry, new byte[0x8340]);
                        current = current with { Memory = current.Memory with { Arena = arena, Shell33 = current.Memory.Shell33! with { Emitters = emitters, Geometry = geometry, EmitterScan = 0, GeometryScan = 0 } } };
                        state = Add(state, 4, 0x866350, 64, address).Hierarchy;
                        state = Next(state, address);
                    }
                    break;
                case 3:
                    state = state.Write(address, task.WriteByte(0x63, 1));
                    state = Add(state, 1, 0x866470, 88, address).Hierarchy;
                    state = Next(state, address);
                    break;
                case 4:
                    if (task.ReadByte(0x63) == 0)
                    {
                        if (unchecked((sbyte)task.ReadByte(0x2A)) < Word(task, 0x58))
                        {
                            task = task.WriteByte(0x2A, unchecked((byte)(task.ReadByte(0x2A) + 1))).WriteByte(0x2E, unchecked((byte)(task.ReadByte(0x2E) + 1))).WriteByte(0x29, 3);
                        }
                        else
                        {
                            task = task.WriteWord(0x60, 10).WriteByte(0x29, 5);
                        }
                        state = state.Write(address, task);
                    }
                    break;
                case 5:
                    task = task.WriteWord(0x60, unchecked((ushort)(task.ReadWord(0x60) - 1)));
                    state = state.Write(address, task);
                    if (Word(task, 0x60) <= 0)
                    {
                        state = Add(state, 4, 0x86B850, 64, address).Hierarchy;
                        state = Next(state, address);
                    }
                    break;
                case 6:
                    if (task.ReadWord(0x5E) == 0)
                    {
                        state = Next(state, address);
                    }
                    break;
                case 9:
                    state = Done(state, address);
                    break;
                case 10:
                    break;
                default:
                    throw new InvalidDataException("Native Shell root leaves its eleven states.");
            }
            int survivors = 0;
            for (int index = 1; index < 5; index++)
            {
                NativeBattleEffectHierarchyStep children = state.Advance(index, Child);
                state = children.Hierarchy;
                survivors += children.Survivors;
            }
            task = state.Read(address).WriteWord(0x5E, unchecked((ushort)survivors)).WriteWord(0x5C, unchecked((ushort)(task.ReadWord(0x5C) + 1)));
            return Finish(state.Write(address, task), address);
        });
        return current with { Effect = current.Effect with { Pools = root.Hierarchy.Pools, ReturnedDescriptor = root.Survivors == 0 ? 0u : RootDescriptor } };

        NativeBattleEffectHierarchyCallback Child(NativeBattleEffectHierarchy state, uint address)
        {
            NativeBattleModulePoolTask task = state.Read(address);
            int phase = unchecked((sbyte)task.ReadByte(0x29));
            int age = Word(task, 0x24);
            switch (task.Callback)
            {
                case 0x866470:
                    task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(task, Models(), inputs.ReadPoseSource);
                    int target = task.ReadByte(0x2D);
                    NativeBattlePoseSource poseSource = inputs.ReadPoseSource(target);
                    NativeBattleSequenceState pose = inputs.ReadModel(target).SequenceState ?? throw new NotSupportedException("Native Shell emitter requires its current target pose.");
                    task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, poseSource, poseSource.Sample(pose.Pose ?? throw new NotSupportedException("Native Shell target pose is unknown."), pose.PoseFrame114 ?? throw new NotSupportedException("Native Shell pose frame is unknown."), pose.PoseFlags109));
                    state = state.Write(address, task);
                    if (phase == 0)
                    {
                        NativeBattleEffectHierarchyAppend shield = Add(state, 3, 0x86B390, 0x2EC, address);
                        state = shield.Hierarchy.Write(shield.Address, shield.Hierarchy.Read(shield.Address).WriteDword(0x74, resources.Shield.Address).WriteDword(0x78, 0x258));
                        NativeBattleEffectHierarchyAppend actor = Add(state, 2, 0x8665A0, 0x2A4, address);
                        state = actor.Hierarchy.Write(actor.Address, actor.Hierarchy.Read(actor.Address).WriteDword(0x30, NativeBattleShellResources.ActorDirectory).WriteWord(0x29E, 45));
                        state = Next(state, address);
                    }
                    else if (phase == 1 && age == 30)
                    {
                        uint parent = task.ReadDword(0x10);
                        state = Next(state.Write(parent, state.Read(parent).WriteByte(0x63, 0)), address);
                    }
                    else if (phase == 2 && age >= 30)
                    {
                        int header = parameters.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A));
                        int contact = checked((inputs.Packets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Native Shell result lost its actual contact pointer.")) + unchecked((sbyte)task.ReadByte(0x2B)));
                        Emit(ref state, new NativeBattleShellContact(contact));
                        state = Done(state, address);
                    }
                    else if (phase is < 0 or > 3)
                    {
                        throw new InvalidDataException("Native Shell emitter leaves its four states.");
                    }
                    if (age == 0)
                    {
                        Emit(ref state, new NativeBattleShellSound(new(resources.Sound, 0, null) { Pan = 128 }));
                    }
                    break;
                case 0x866350:
                case 0x86B850:
                    NativeBattleStageFadeStep fade = NativeBattleStageFadeTask.Advance(task, task.Callback == 0x866350);
                    state = state.Write(address, fade.Task);
                    if (fade.Write is { } write)
                    {
                        Emit(ref state, new NativeBattleShellStage(write));
                    }
                    break;
                case 0x8665A0:
                    if (phase == 0 && age >= Word(task, 0x298))
                    {
                        current = NativeBattleShellLifecycle.InitializeContext(current with { Effect = current.Effect with { Pools = state.Pools } }, address, inputs);
                        var first = NativeBattleShellLifecycle.Advance(current, address, inputs);
                        current = first.Frame;
                        state = Next(new(current.Effect.Pools), address);
                    }
                    else if (phase == 1)
                    {
                        var advanced = NativeBattleShellLifecycle.Advance(current with { Effect = current.Effect with { Pools = state.Pools } }, address, inputs);
                        current = advanced.Frame;
                        state = new(current.Effect.Pools);
                        if (advanced.Complete)
                        {
                            state = Done(state, address);
                        }
                    }
                    else if (phase is < 0 or > 2)
                    {
                        throw new InvalidDataException("Native Shell actor director leaves its three states.");
                    }
                    break;
                case 0x86B390:
                    if (phase == 0)
                    {
                        NativeBattleGfAnimationWorkspace workspace = new(task.Payload.AsSpan().Slice(0x94 - 12, 0x258).ToArray().ToImmutableArray());
                        task = StoreWorkspace(task, workspace.Initialize7016B0(resources.Shield, 0x258));
                        NativeBattleGraphicsModel model = inputs.ReadModel(task.ReadByte(0x2D));
                        NativeBattleEffectLocalBounds bounds = model.GeometryBounds52 ?? throw new NotSupportedException("Native Shell shield requires its current native geometry bounds.");
                        if (task.ReadWord(0x80) == 0)
                        {
                            NativeBattleVector point = NativeBattlePoseMatrices.ReadBone(model, inputs.ReadPoseSource(task.ReadByte(0x2D)), 0xF1, 0) with { Y = bounds.Maximum.Y };
                            task = task.WriteWord(0x1C, unchecked((ushort)point.X)).WriteWord(0x1E, unchecked((ushort)point.Y)).WriteWord(0x20, unchecked((ushort)point.Z));
                        }
                        short height = unchecked((short)(bounds.Maximum.Y - bounds.Minimum.Y));
                        short scale = Math.Clamp(unchecked((short)(height * 3)), (short)1024, (short)16384);
                        task = task.WriteDword(0x50, unchecked((uint)scale)).WriteDword(0x54, unchecked((uint)scale)).WriteDword(0x58, unchecked((uint)scale));
                        NativeBattleShellState owner = current.Memory.Shell33!;
                        NativeBattleModulePoolTask context = state.Read(owner.ContextTask);
                        context = context.WriteDword(0x34 + 0x214, unchecked((uint)scale)).WriteDword(0x34 + 0x218, unchecked((uint)scale)).WriteDword(0x34 + 0x21C, unchecked((uint)scale)).WriteDword(0x34 + 0x220, task.ReadDword(0x5C));
                        state = Next(state.Write(owner.ContextTask, context).Write(address, task), address);
                    }
                    else if (phase == 1)
                    {
                        short facing = inputs.ReadModel(task.ReadByte(0x2D)).Rotation14 ?? throw new NotSupportedException("Native Shell requires its current target facing.");
                        NativeBattleActionMatrix world = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), facing) with { X = Word(task, 0x1C), Y = Word(task, 0x1E), Z = Word(task, 0x20) };
                        world = NativeBattlePoseMatrices.Scale56BEF0(world, unchecked((int)task.ReadDword(0x50)), unchecked((int)task.ReadDword(0x54)), unchecked((int)task.ReadDword(0x58)));
                        NativeBattleActionMatrix placement = NativeBattleGfMatrices.Compose56C2F0(inputs.Camera, world);
                        NativeBattleGfAnimationWorkspace workspace = new(task.Payload.AsSpan().Slice(0x94 - 12, 0x258).ToArray().ToImmutableArray());
                        NativeBattleEffectHierarchy local = state;
                        NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(resources.Shield, 0, (owner, node) =>
                        {
                            NativeBattleShellState retained = current.Memory.Shell33!;
                            NativeBattleShieldPartDraw drawn = NativeBattleShieldPart.Draw(resources.Shield, node, placement, retained.Blend, inputs.Projection, inputs.Colors, false);
                            current = current with { Memory = current.Memory with { Shell33 = retained with { Blend = drawn.Blend } } };
                            if (drawn.FarColor is uint color)
                            {
                                NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Shell requires its retained color registers.");
                                NativeBattleModelRendererRegisters registers = process.ModelRendererRegisters ?? new(null, null, null, null, null);
                                current = current with { Memory = current.Memory with { PrimitiveProcess = process with { ModelRendererRegisters = registers.PublishModelColor(color) } } };
                            }
                            Emit(ref local, new NativeBattleShellPackets(drawn.Batch));
                            return owner;
                        });
                        state = local.Write(address, StoreWorkspace(local.Read(address), advanced.Workspace));
                        if (advanced.Remaining == 0)
                        {
                            state = Done(state, address);
                        }
                    }
                    else if (phase != 2)
                    {
                        throw new InvalidDataException("Native Shell shield leaves its three states.");
                    }
                    break;
                default:
                    throw new NotSupportedException($"Native Shell callback{task.Callback:X} has no proved implementation.");
            }
            return Finish(state, address);
        }

        NativeBattleEffectHierarchyAppend Add(NativeBattleEffectHierarchy state, int pool, uint callback, int size, uint parent)
        {
            NativeBattleEffectHierarchyAppend child = state.Append8DC540(pool, callback, size, parent, (pointer, group, target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, group, target));
            if (child.Address == 0)
            {
                throw new InvalidOperationException("Native Shell exhausted an unguarded original task pool.");
            }
            return child;
        }
        void Emit(ref NativeBattleEffectHierarchy state, NativeBattleShellOperation operation)
        {
            current = inputs.Execute(current with { Effect = current.Effect with { Pools = state.Pools } }, operation);
            state = new(current.Effect.Pools);
        }
        NativeBattleGraphicsModel[] Models() => Enumerable.Range(0, 7).Select(inputs.ReadModel).ToArray();
    }

    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleEffectHierarchy Next(NativeBattleEffectHierarchy state, uint address) => state.Write(address, state.Read(address).WriteByte(0x29, unchecked((byte)(state.Read(address).ReadByte(0x29) + 1))));
    private static NativeBattleEffectHierarchy Done(NativeBattleEffectHierarchy state, uint address) => Next(state.Write(address, state.Read(address).WriteByte(0x26, (byte)(state.Read(address).ReadByte(0x26) | 1))), address);
    private static NativeBattleModulePoolTask StoreWorkspace(NativeBattleModulePoolTask task, NativeBattleGfAnimationWorkspace workspace)
    {
        byte[] bytes = task.Payload.ToArray();
        workspace.Data.AsSpan().CopyTo(bytes.AsSpan(0x94 - 12));
        return task with { Payload = bytes.ToImmutableArray() };
    }
    private static NativeBattleEffectHierarchyCallback Finish(NativeBattleEffectHierarchy state, uint address)
    {
        NativeBattleModulePoolTask task = state.Read(address);
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        state = state.Write(address, task);
        bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
        return new(complete ? state.Complete8DC530(address) : state, complete ? 2 : 0);
    }
}
