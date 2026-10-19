using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Protect's six ordered hierarchy queues and its independent shield/particle lifecycle.</summary>
public static class NativeBattleProtectController
{
    public const uint RootDescriptor = 0x26C3A40;

    public static NativeBattleProtectFrame Initialize(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets, NativeBattleProtectResources resources, NativeCrtRandomState random)
    {
        if (effect.Binding is not { Effect: 32, Entry: 0x86B9A0 } || effect.Parameters is not { } parameters)
        {
            throw new InvalidDataException("Native Protect requires its original entry and parameter block.");
        }
        NativeBattleProtectState state = memory.Protect32 ?? throw new NotSupportedException("Native Protect requires its retained module owner; migrate an authenticated standalone source.");
        int source = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x26C3954)?.ArenaOffset ?? throw new NotSupportedException("Native Protect requires its executed initializer allocation.");
        if (source > NativeBattleEffectArena.Length - 0x17000)
        {
            throw new InvalidDataException("Native Protect packet buffers leave the original effect allocation.");
        }
        NativeBattleEffectHierarchy hierarchy = new([
            NativeBattleEffectTaskPool.Initialize(RootDescriptor, 0x26C3978, 100, 2),
            NativeBattleEffectTaskPool.Initialize(0x26CD978, 0x26CD988, 88, 4),
            NativeBattleEffectTaskPool.Initialize(0x26C3890, 0x26C38A0, 60, 3),
            NativeBattleEffectTaskPool.Initialize(0x26C3A50, 0x26C3A60, 136, 200),
            NativeBattleEffectTaskPool.Initialize(0x26CD6D0, 0x26CA4A8, 1284, 10),
            NativeBattleEffectTaskPool.Initialize(0x26CD968, 0x26CD6E8, 64, 10)
        ]);
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, 0x86BB00, 100);
        hierarchy = root.Hierarchy;
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Native Protect requires its actual first contact.");
        byte targets = header.ReadByte(16);
        byte groups = header.ReadByte(17);
        byte maximum = Math.Max(unchecked((byte)(targets - 1)), groups);
        NativeBattleModulePoolTask task = hierarchy.Read(root.Address).WriteDword(12, 0x1D99A78).WriteByte(0x2C, header.ReadByte(0)).WriteByte(0x2D, packets.ReadContact(first).Primary.ActorIndex).WriteWord(0x5A, targets).WriteWord(0x58, groups).WriteByte(0x2F, maximum);
        hierarchy = hierarchy.Write(root.Address, task);
        if ((parameters.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native Protect requires its retained texture queue.");
            textures = textures.Register5099A0(resources.Animation);
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Native Protect requires its loaded texture arena."), source);
            memory = memory with { Textures = textures };
        }
        return new(memory with { Protect32 = state with { Source = source } }, effect with { Pools = hierarchy.Pools, ReturnedDescriptor = RootDescriptor }, random);
    }

    public static NativeBattleProtectFrame Advance(NativeBattleProtectFrame frame, NativeBattleProtectResources resources, NativeBattleProtectInputs inputs)
    {
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        NativeBattleProtectFrame current = frame;
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Native Protect lost its action context.");
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleEffectHierarchyStep root = hierarchy.Advance(0, (state, address) =>
        {
            NativeBattleModulePoolTask task = state.Read(address);
            NativeBattleProtectState owner = current.Memory.Protect32 ?? throw new NotSupportedException("Native Protect lost its module state.");
            int source = owner.Source ?? throw new NotSupportedException("Native Protect lost its executed initializer pointer.");
            bool odd = (task.ReadWord(0x5C) & 1) != 0;
            current = current with { Memory = current.Memory with { Protect32 = owner with { Cursor = source + (odd ? 0 : 0xB800), SecondaryCursor = source + (odd ? 0xB800 : 0x17000) } } };
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
                        state = Add(state, 5, 0x86BCA0, 64, address).Hierarchy;
                        state = Next(state, address);
                    }
                    break;
                case 3:
                    state = state.Write(address, task.WriteByte(0x63, 1));
                    state = Add(state, 1, 0x86BDC0, 88, address).Hierarchy;
                    state = Next(state, address);
                    break;
                case 4:
                    if (task.ReadByte(0x63) == 0)
                    {
                        if (unchecked((sbyte)task.ReadByte(0x2A)) < unchecked((short)task.ReadWord(0x58)))
                        {
                            task = task.WriteByte(0x2A, unchecked((byte)(task.ReadByte(0x2A) + 1))).WriteByte(0x2E, unchecked((byte)(task.ReadByte(0x2E) + 1))).WriteByte(0x29, 3);
                        }
                        else
                        {
                            task = task.WriteWord(0x60, 7).WriteByte(0x29, 5);
                        }
                        state = state.Write(address, task);
                    }
                    break;
                case 5:
                    task = task.WriteWord(0x60, unchecked((ushort)(task.ReadWord(0x60) - 1)));
                    state = state.Write(address, task);
                    if (Word(task, 0x60) <= 0)
                    {
                        state = Add(state, 5, 0x86C890, 64, address).Hierarchy;
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
                    state = Next(state.Write(address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1))), address);
                    break;
                case 10:
                    break;
                default:
                    throw new InvalidDataException("Native Protect root leaves its eleven states.");
            }
            int survivors = 0;
            for (int index = 1; index < 6; index++)
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
                case 0x86BDC0:
                    task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(task, Models(), inputs.ReadPoseSource);
                    int target = task.ReadByte(0x2D);
                    NativeBattlePoseSource poseSource = inputs.ReadPoseSource(target);
                    NativeBattleSequenceState pose = inputs.ReadModel(target).SequenceState ?? throw new NotSupportedException("Native Protect emitter requires its current target pose.");
                    task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, poseSource, poseSource.Sample(pose.Pose ?? throw new NotSupportedException("Native Protect target pose is unknown."), pose.PoseFrame114 ?? throw new NotSupportedException("Native Protect pose frame is unknown."), pose.PoseFlags109));
                    state = state.Write(address, task);
                    if (phase == 0)
                    {
                        NativeBattleEffectHierarchyAppend shield = Add(state, 4, 0x86C000, 0x504, address);
                        state = shield.Hierarchy.Write(shield.Address, shield.Hierarchy.Read(shield.Address).WriteDword(0x74, resources.Shield.Address).WriteDword(0x78, 0x470));
                        state = Add(state, 3, 0x86C3A0, 0x88, address).Hierarchy;
                        state = Next(state, address);
                    }
                    else if (phase == 1 && age == 18)
                    {
                        uint parent = task.ReadDword(0x10);
                        state = Next(state.Write(parent, state.Read(parent).WriteByte(0x63, 0)), address);
                    }
                    else if (phase == 2 && age >= 18)
                    {
                        int header = parameters.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A));
                        int contact = checked((inputs.Packets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Native Protect result lost its contact pointer.")) + unchecked((sbyte)task.ReadByte(0x2B)));
                        Emit(ref state, new NativeBattleProtectContact(contact));
                        state = Done(state, address);
                    }
                    else if (phase is < 0 or > 3)
                    {
                        throw new InvalidDataException("Native Protect emitter leaves its four states.");
                    }
                    if (age == 0)
                    {
                        Emit(ref state, new NativeBattleProtectSound(new(resources.Sound, 0, null) { Pan = 128 }));
                    }
                    if (age == 22)
                    {
                        NativeBattleEffectHierarchyAppend flash = Add(state, 2, 0x86BE70, 60, address);
                        state = flash.Hierarchy.Write(flash.Address, flash.Hierarchy.Read(flash.Address).WriteDword(0x30, 0x15F3B14));
                    }
                    break;
                case 0x86BCA0:
                case 0x86C890:
                    NativeBattleStageFadeStep fade = NativeBattleStageFadeTask.Advance(task, task.Callback == 0x86BCA0);
                    state = state.Write(address, fade.Task);
                    if (fade.Write is { } write)
                    {
                        Emit(ref state, new NativeBattleProtectStage(write));
                    }
                    break;
                case 0x86C000:
                    if (phase == 0)
                    {
                        NativeBattleGfAnimationWorkspace workspace = new(task.Payload.AsSpan().Slice(0x94 - 12, 0x470).ToArray().ToImmutableArray());
                        task = StoreWorkspace(task, workspace.Initialize7016B0(resources.Shield, 0x470));
                        NativeBattleVector point = Bone(task, 0xF1);
                        task = WriteVector(task, point with { Y = Math.Min(point.Y, (short)-512) });
                        state = Next(state.Write(address, task), address);
                    }
                    else if (phase == 1)
                    {
                        NativeBattleActionMatrix world = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), Facing(task)) with { X = Word(task, 0x1C), Y = Word(task, 0x1E), Z = Word(task, 0x20) };
                        NativeBattleActionMatrix placement = NativeBattleGfMatrices.Compose56C2F0(inputs.Camera, world);
                        NativeBattleGfAnimationWorkspace workspace = new(task.Payload.AsSpan().Slice(0x94 - 12, 0x470).ToArray().ToImmutableArray());
                        NativeBattleEffectHierarchy local = state;
                        NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(resources.Shield, 0, (owner, node) =>
                        {
                            NativeBattleProtectState retained = current.Memory.Protect32!;
                            NativeBattleShieldPartDraw drawn = NativeBattleShieldPart.Draw(resources.Shield, node, placement, retained.Blend, inputs.Projection, inputs.Colors, true);
                            current = current with { Memory = current.Memory with { Protect32 = retained with { Blend = drawn.Blend } } };
                            PublishFar(drawn.FarColor);
                            Emit(ref local, new NativeBattleProtectPackets(drawn.Batch));
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
                        throw new InvalidDataException("Native Protect shield leaves its three states.");
                    }
                    break;
                case 0x86C3A0:
                    if (phase == 0)
                    {
                        NativeBattleVector point = Bone(task, 0xF1);
                        point = point with { Y = Math.Min(point.Y, (short)-512) };
                        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), Facing(task)) with { X = point.X, Y = point.Y, Z = point.Z };
                        task = NativeBattleReflectController.WriteMatrix(WriteVector(task, point), 0x58, matrix);
                        state = Next(state.Write(address, task), address);
                    }
                    else if (phase == 1 && age >= 20)
                    {
                        state = Next(state, address);
                    }
                    else if (phase == 2)
                    {
                        int count = resources.ParticleCounts[age];
                        NativeBattleActionMatrix matrix = NativeBattleReflectController.ReadMatrix(task, 0x58);
                        for (int index = 0; index < count; index++)
                        {
                            NativeBattleEffectHierarchyAppend particle = Add(state, 3, 0x86C5C0, 0x88, address);
                            state = particle.Hierarchy;
                            matrix = NativeBattleCuragaMatrices.RotateZ8DD960(matrix, unchecked((short)(Roll() & 4095)));
                            NativeBattleActionMatrix placed = NativeBattleGfMatrices.Compose56C2F0(matrix, NativeBattleCuragaMatrices.Identity8DD770() with { Y = -640, Z = -640 });
                            NativeBattleModulePoolTask child = state.Read(particle.Address).WriteWord(0x1C, unchecked((ushort)placed.X)).WriteWord(0x1E, unchecked((ushort)placed.Y)).WriteWord(0x20, unchecked((ushort)placed.Z));
                            placed = NativeBattleCuragaMatrices.RotateX8DD7E0(placed, 256);
                            int speed = (Roll() & 511) + 16;
                            child = child.WriteWord(0x78, unchecked((ushort)(placed.Rotation[2] * speed / 4096))).WriteWord(0x7A, unchecked((ushort)(placed.Rotation[5] * speed / 4096))).WriteWord(0x7C, unchecked((ushort)(placed.Rotation[8] * speed / 4096)));
                            state = state.Write(particle.Address, child);
                        }
                        state = state.Write(address, NativeBattleReflectController.WriteMatrix(state.Read(address), 0x58, matrix));
                        if (age > 24)
                        {
                            state = Done(state, address);
                        }
                    }
                    else if (phase is < 0 or > 3)
                    {
                        throw new InvalidDataException("Native Protect spawner leaves its four states.");
                    }
                    break;
                case 0x86C5C0:
                    if (phase == 0)
                    {
                        task = task.WriteDword(0x4C, resources.Particle.Address).WriteWord(0x52, 11).WriteByte(0x29, 1);
                    }
                    else if (phase == 1)
                    {
                        for (int axis = 0; axis < 3; axis++)
                        {
                            int delta = Word(task, 0x78 + axis * 2) / -5;
                            ushort speed = unchecked((ushort)(task.ReadWord(0x78 + axis * 2) + delta));
                            task = task.WriteWord(0x80 + axis * 2, unchecked((ushort)delta)).WriteWord(0x78 + axis * 2, speed).WriteWord(0x1C + axis * 2, unchecked((ushort)(task.ReadWord(0x1C + axis * 2) + speed)));
                        }
                        task = task.WriteWord(0x50, unchecked((ushort)(task.ReadWord(0x50) + 1)));
                        if (Word(task, 0x50) > Word(task, 0x52))
                        {
                            task = task.WriteWord(0x50, task.ReadWord(0x52)).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5)).WriteByte(0x29, 2);
                        }
                    }
                    else if (phase != 2)
                    {
                        throw new InvalidDataException("Native Protect particle leaves its three states.");
                    }
                    state = state.Write(address, task);
                    if ((task.ReadByte(0x26) & 4) == 0)
                    {
                        int phaseIndex = Word(task, 0x50);
                        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resources.Particle, SpriteFrame = resources.Particle.SpriteFrame(phaseIndex), Phase = checked((ushort)phaseIndex), Anchor = new(Word(task, 0x1C), Word(task, 0x1E), Word(task, 0x20), 0), Scale = 4096, Direction = 4096, DepthOffset = Word(task, 0x54), SpriteFlags = 0, SpriteColor = 0x808080 };
                        Emit(ref state, new NativeBattleProtectSprite(draw));
                    }
                    break;
                case 0x86BE70:
                    int script = Word(task, 0x38);
                    uint color = resources.FlashScript[script];
                    byte command = (byte)(color >> 24);
                    if (command == 255)
                    {
                        state = state.Write(address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                    }
                    else
                    {
                        if (command != 254 || age >= unchecked((short)(color >> 16 & 255)))
                        {
                            int next = script + 1;
                            task = task.WriteWord(0x38, checked((ushort)next)).WriteDword(0x34, command == 254 ? resources.FlashScript[next] : color);
                        }
                        state = state.Write(address, task);
                        NativeBattleProtectFlash.Emit(task.ReadDword(0x34), batch => Emit(ref state, batch));
                    }
                    break;
                default:
                    throw new NotSupportedException($"Native Protect private callback{task.Callback:X} has no proved implementation.");
            }
            return Finish(state, address);
        }

        NativeBattleEffectHierarchyAppend Add(NativeBattleEffectHierarchy state, int pool, uint callback, int size, uint parent)
        {
            NativeBattleEffectHierarchyAppend child = state.Append8DC540(pool, callback, size, parent, (pointer, group, target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, group, target));
            if (child.Address == 0)
            {
                throw new InvalidOperationException("Native Protect exhausted an unguarded original task pool.");
            }
            return child;
        }
        void Emit(ref NativeBattleEffectHierarchy state, NativeBattleProtectOperation operation)
        {
            current = inputs.Execute(current with { Effect = current.Effect with { Pools = state.Pools } }, operation);
            state = new(current.Effect.Pools);
        }
        ushort Roll()
        {
            NativeCrtRandomState random = current.Random.Draw(out ushort value);
            current = current with { Random = random };
            return value;
        }
        NativeBattleGraphicsModel[] Models() => Enumerable.Range(0, 7).Select(inputs.ReadModel).ToArray();
        NativeBattleVector Bone(NativeBattleModulePoolTask task, byte selector) => NativeBattlePoseMatrices.ReadBone(inputs.ReadModel(task.ReadByte(0x2D)), inputs.ReadPoseSource(task.ReadByte(0x2D)), selector, 0);
        short Facing(NativeBattleModulePoolTask task) => inputs.ReadModel(task.ReadByte(0x2D)).Rotation14 ?? throw new NotSupportedException("Native Protect requires target facingWORD0E.");
        void PublishFar(uint? color)
        {
            if (color is uint far)
            {
                NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Protect requires its retained color registers.");
                NativeBattleModelRendererRegisters registers = process.ModelRendererRegisters ?? new(null, null, null, null, null);
                current = current with { Memory = current.Memory with { PrimitiveProcess = process with { ModelRendererRegisters = registers.PublishModelColor(far) } } };
            }
        }
    }

    private static NativeBattleEffectHierarchy Next(NativeBattleEffectHierarchy state, uint address) => state.Write(address, state.Read(address).WriteByte(0x29, unchecked((byte)(state.Read(address).ReadByte(0x29) + 1))));
    private static NativeBattleEffectHierarchy Done(NativeBattleEffectHierarchy state, uint address) => Next(state.Write(address, state.Read(address).WriteByte(0x26, (byte)(state.Read(address).ReadByte(0x26) | 1))), address);
    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleModulePoolTask WriteVector(NativeBattleModulePoolTask task, NativeBattleVector point) => task.WriteWord(0x1C, unchecked((ushort)point.X)).WriteWord(0x1E, unchecked((ushort)point.Y)).WriteWord(0x20, unchecked((ushort)point.Z));
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
