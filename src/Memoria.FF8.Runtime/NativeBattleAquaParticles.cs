using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAquaParticleStep(NativeBattleAquaFrame Frame, NativeBattleEffectHierarchy Hierarchy, int Result);

public static partial class NativeBattleAquaParticles
{
    public static NativeBattleAquaParticleStep SpawnPhase1(NativeBattleAquaFrame frame, NativeBattleEffectHierarchy hierarchy, uint parent, NativeBattleAquaInputs inputs)
    {
        if (frame.State.Word(0x40) != 1)
        {
            return new(frame, hierarchy, 0);
        }
        NativeBattleAquaProfile profile = frame.Profile;
        hierarchy = Add(frame, hierarchy, 4, 0x809250, 96, parent, inputs).Hierarchy;
        hierarchy = Add(frame, hierarchy, 3, 0x80B730, 340, parent, inputs).Hierarchy;
        NativeBattleModulePoolTask task = hierarchy.Read(parent);
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("The Aqua emitter requires its captured action context.");
        NativeBattleActionHeaderSlot header = inputs.Packets.Headers[parameters.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A))];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("The Aqua emitter requires its captured contact pointer.");
        int count = header.ReadByte(0x10);
        foreach (uint callback in new uint[] { 0x80C1B0, 0x80C5A0 })
        {
            for (int index = 0; index < count; index++)
            {
                NativeBattleEffectHierarchyAppend child = Add(frame, hierarchy, 3, callback, 340, parent, inputs);
                byte recipient = inputs.Packets.ReadContact(first + index).Primary.ActorIndex;
                hierarchy = child.Hierarchy.Write(child.Address, child.Hierarchy.Read(child.Address).WriteByte(0x2D, recipient));
            }
        }
        return new(frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } }, hierarchy, 0);
    }

    public static NativeBattleAquaParticleStep Advance(NativeBattleAquaFrame frame, NativeBattleEffectHierarchy hierarchy, uint address, NativeBattleAquaResources resources, NativeBattleAquaInputs inputs)
    {
        NativeBattleAquaFrame current = frame;
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        uint callback = frame.Profile.CanonicalCallback(task.Callback);
        int phase = unchecked((sbyte)task.ReadByte(0x29));
        int age = Word(task, 0x24);
        switch (callback)
        {
            case 0x809250:
                AdvanceWave();
                break;
            case 0x80B730:
                AdvanceCasterEmitter();
                break;
            case 0x80B8E0:
                AdvanceCasterParticle();
                break;
            case 0x80C1B0:
            case 0x80C5A0:
                AdvanceTargetEmitter(callback == 0x80C5A0);
                break;
            case 0x80C430:
            case 0x80C7B0:
                AdvanceTargetParticle(callback == 0x80C7B0);
                break;
            default:
                throw new NotSupportedException($"The Aqua particle callback{task.Callback:X} requires its original private body.");
        }
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        hierarchy = hierarchy.Write(address, task);
        bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
        if (complete)
        {
            hierarchy = hierarchy.Complete8DC530(address);
        }
        current = current with { Effect = current.Effect with { Pools = hierarchy.Pools } };
        return new(current, hierarchy, complete ? 2 : 0);

        void Publish(NativeBattleAquaOperation operation)
        {
            hierarchy = hierarchy.Write(address, task);
            current = inputs.Execute(current with { Effect = current.Effect with { Pools = hierarchy.Pools } }, operation);
            hierarchy = new(current.Effect.Pools);
            task = hierarchy.Read(address);
        }

        void InitializeYaw()
        {
            short yaw = inputs.ReadModel(task.ReadByte(0x2C)).Rotation14 ?? throw new NotSupportedException("The Aqua emitter requires its actual caster yaw.");
            NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
            Publish(new NativeBattleAquaRotate(identity, 'Y', yaw) { Padding18 = ReadPadding(task, 0x58) });
            task = NativeBattleReflectController.WriteMatrix(task, 0x58, NativeBattleCuragaMatrices.RotateY8DD8A0(identity, yaw));
        }

        void AdvanceCasterEmitter()
        {
            switch (phase)
            {
                case 0:
                    InitializeYaw();
                    task = Next(task);
                    break;
                case 1:
                    task = CasterAnchor(task, current.Profile, inputs, Publish);
                    hierarchy = hierarchy.Write(address, task);
                    NativeBattleEffectHierarchyAppend child = Add(current, hierarchy, 3, 0x80B8E0, 340, address, inputs);
                    hierarchy = child.Hierarchy;
                    task = hierarchy.Read(address);
                    NativeCrtRandomState random = current.Random;
                    NativeBattleModulePoolTask born = InitializeCasterBirth(task, hierarchy.Read(child.Address), ref random, (matrix, axis, angle) => Publish(new NativeBattleAquaRotate(matrix, axis, angle) { Padding18 = ReadPadding(task, 0x58) }));
                    current = current with { Random = random };
                    hierarchy = hierarchy.Write(child.Address, born);
                    task = hierarchy.Read(address);
                    if (age > 15)
                    {
                        task = Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                    }
                    break;
                case 2:
                    break;
                default:
                    throw new InvalidDataException("The Aqua caster emitter leaves its three phases.");
            }
        }

        void AdvanceTargetEmitter(bool spray)
        {
            switch (phase)
            {
                case 0:
                    if (age >= (spray ? 20 : 4))
                    {
                        InitializeYaw();
                        if (!spray)
                        {
                            NativeCrtRandomState random = current.Random;
                            byte sequence = (byte)(Draw(ref random) % 3 + 4);
                            current = current with { Random = random };
                            Publish(new NativeBattleAquaSequence(task.ReadByte(0x2D), sequence));
                        }
                        task = Next(task);
                    }
                    break;
                case 1:
                    if (((task.ReadByte(0x2D) + task.ReadByte(0x24)) & 1) != 0)
                    {
                        hierarchy = hierarchy.Write(address, task);
                        NativeBattleEffectHierarchyAppend child = Add(current, hierarchy, 3, spray ? 0x80C7B0u : 0x80C430u, 340, address, inputs);
                        hierarchy = child.Hierarchy;
                        task = hierarchy.Read(address);
                        int target = task.ReadByte(0x2D);
                        int bones = inputs.ReadPoseSource(target).BoneCount;
                        NativeCrtRandomState random = current.Random;
                        NativeBattleModulePoolTask born = InitializeTargetBirth(task, hierarchy.Read(child.Address), spray, bones, (selector, fraction) =>
                        {
                            Publish(new NativeBattleAquaAnchor(target, selector, fraction));
                            return inputs.ReadAnchor502170(target, selector, fraction);
                        }, ref random, (matrix, axis, angle) => Publish(new NativeBattleAquaRotate(matrix, axis, angle) { Padding18 = ReadPadding(task, 0x58) }), (matrix, point) => Publish(new NativeBattleAquaTransform(matrix, point, false) { Padding18 = ReadPadding(task, 0x58) }));
                        current = current with { Random = random };
                        hierarchy = hierarchy.Write(child.Address, born);
                        task = hierarchy.Read(address);
                    }
                    if (age >= (spray ? 28 : 18))
                    {
                        task = Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                    }
                    break;
                case 2:
                    break;
                default:
                    throw new InvalidDataException("The Aqua target emitter leaves its three phases.");
            }
        }

        void AdvanceCasterParticle()
        {
            switch (phase)
            {
                case 0:
                    task = Next(task.WriteDword(0x4C, current.Profile.SpriteAddress(0x15A3A3C)).WriteWord(0x56, 0xFFF0).WriteWord(0x52, 12).WriteWord(0x152, unchecked((ushort)(-current.State.Dword(0x3C) / 12))));
                    break;
                case 1:
                    task = AdvanceAnimation(task);
                    break;
                case 2:
                    break;
                default:
                    throw new InvalidDataException("The Aqua caster particle leaves its three phases.");
            }
            task = task.WriteWord(0x150, unchecked((ushort)(task.ReadWord(0x150) + 16)));
            uint[] sources = [0x15A38DC, 0x15A3A3C, 0x15A3B9C, 0x15A4008];
            for (int index = 0; index < 4; index++)
            {
                int angleOffset = 0x148 + index * 2;
                int travelOffset = 0x140 + index * 2;
                short angle = (short)((task.ReadWord(angleOffset) + 0x100) & 0xFFF);
                task = task.WriteWord(angleOffset, unchecked((ushort)angle)).WriteWord(travelOffset, unchecked((ushort)(task.ReadWord(travelOffset) + task.ReadWord(0x152))));
                NativeBattleActionMatrix basis = NativeBattleReflectController.ReadMatrix(task, 0xC0 + index * 32);
                Publish(new NativeBattleAquaRotate(basis, 'Z', angle) { Padding18 = ReadPadding(task, 0xC0 + index * 32) });
                NativeBattleActionMatrix rotated = NativeBattleCuragaMatrices.RotateZ8DD960(basis, angle);
                NativeBattleVector point = new(0, Word(task, 0x150), Word(task, travelOffset));
                Publish(new NativeBattleAquaTransform(rotated, point, false) { Padding18 = ReadPadding(task, 0xC0 + index * 32) });
                NativeBattleVector position = NativeBattleCuragaMatrices.RotateVector45DF80(rotated, point);
                position = new(unchecked((short)(position.X + Word(task, 0x78))), unchecked((short)(position.Y + Word(task, 0x7A))), unchecked((short)(position.Z + Word(task, 0x7C))));
                task = WriteVector(task, 0x1C, position).WriteDword(0x4C, current.Profile.SpriteAddress(sources[index]));
                Publish(new NativeBattleAquaSpriteTask(task));
            }
        }

        void AdvanceTargetParticle(bool spray)
        {
            switch (phase)
            {
                case 0:
                    task = Next(task.WriteDword(0x4C, current.Profile.SpriteAddress(spray ? 0x15A3B9Cu : 0x15A3E5Cu)).WriteWord(0x56, spray ? (ushort)0xFFF0 : (ushort)0xFFC0).WriteWord(0x52, spray ? (ushort)12 : (ushort)15));
                    break;
                case 1:
                    task = AdvanceAnimation(task);
                    break;
                case 2:
                    break;
                default:
                    throw new InvalidDataException("The Aqua target particle leaves its three phases.");
            }
            uint[] sources = [0x15A38DC, 0x15A3A3C, 0x15A3B9C, 0x15A3CFC];
            for (int index = 0; index < (spray ? 4 : 2); index++)
            {
                task = task.WriteBytes(0x1C, task.ReadBytes(0x80 + index * 8, 8));
                if (spray)
                {
                    task = task.WriteDword(0x4C, current.Profile.SpriteAddress(sources[index]));
                }
                Publish(new NativeBattleAquaSpriteTask(task));
                task = AdvanceMotionRecord(task, index, !spray);
            }
        }

        void AdvanceWave()
        {
            switch (phase)
            {
                case 0:
                    int distance = current.State.Dword(0x3C);
                    short yaw = inputs.ReadModel(task.ReadByte(0x2C)).Rotation14 ?? throw new NotSupportedException("The Aqua wave requires its actual caster yaw.");
                    task = task.WriteDword(0x30, unchecked((uint)distance)).WriteDword(0x34, unchecked((uint)distance)).WriteDword(0x38, unchecked((uint)distance)).WriteDword(0x4C, current.Profile.MeshAddress).WriteWord(0x46, unchecked((ushort)yaw)).WriteWord(0x50, 0x1000);
                    task = Next(CasterAnchor(task, current.Profile, inputs, Publish));
                    break;
                case 1:
                    task = CasterAnchor(task, current.Profile, inputs, Publish).WriteWord(0x50, unchecked((ushort)(task.ReadWord(0x50) - 0x200)));
                    if (Word(task, 0x50) <= 0)
                    {
                        task = Next(task.WriteWord(0x50, 0).WriteWord(0x5C, 15));
                    }
                    break;
                case 2:
                    task = CasterAnchor(task, current.Profile, inputs, Publish).WriteWord(0x5C, unchecked((ushort)(task.ReadWord(0x5C) - 1)));
                    if (Word(task, 0x5C) <= 0)
                    {
                        task = Next(task);
                    }
                    break;
                case 3:
                    task = task.WriteWord(0x50, unchecked((ushort)(task.ReadWord(0x50) + 0x200)));
                    if (Word(task, 0x50) >= 0x1000)
                    {
                        task = Next(task.WriteWord(0x50, 0x1000).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                    }
                    break;
                case 4:
                    break;
                default:
                    throw new InvalidDataException("The Aqua wave leaves its five phases.");
            }
            task = task.WriteWord(0x54, (ushort)((task.ReadWord(0x54) + 0xA0) & 0xFFF)).WriteWord(0x58, (ushort)((task.ReadWord(0x58) + 0xA0) & 0xFFF));
            Publish(new NativeBattleAquaRotate(NativeBattleCuragaMatrices.Identity8DD770(), 'Z', Word(task, 0x58)));
            NativeBattleAquaDeformation deformation = DeformWave(current.State.WaveMesh ?? throw new NotSupportedException("The Aqua wave requires its retained mutable mesh."), resources.WaveVertices, Word(task, 0x54), Word(task, 0x58));
            current = current.WithState(current.State with { WaveMesh = deformation.Mesh });
            Publish(new NativeBattleAquaTransforms(deformation.Rotation, deformation.Inputs, false) { Ws = deformation.InputWs });
            task = task.WriteWord(0x5E, (ushort)((unchecked((byte)task.ReadWord(0x5E)) + 8) & 0x7F));
            Publish(new NativeBattleAquaWaveTask(task));
        }
    }

    private static NativeBattleEffectHierarchyAppend Add(NativeBattleAquaFrame frame, NativeBattleEffectHierarchy hierarchy, int pool, uint callback, int size, uint parent, NativeBattleAquaInputs inputs)
    {
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("The Aqua hierarchy requires its original captured action.");
        NativeBattleEffectHierarchyAppend result = hierarchy.Append8DC540(pool, frame.Profile.Callback(callback), size, parent, (pointer, group, target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, group, target));
        if (result.Address == 0)
        {
            throw new InvalidOperationException("The Aqua callback exhausted an unguarded original task pool.");
        }
        return result;
    }

    private static NativeBattleModulePoolTask CasterAnchor(NativeBattleModulePoolTask task, NativeBattleAquaProfile profile, NativeBattleAquaInputs inputs, Action<NativeBattleAquaOperation> publish)
    {
        int actor = task.ReadByte(0x2C);
        publish(new NativeBattleAquaAnchor(actor, profile.AnchorSelector, profile.AnchorFraction));
        NativeBattleModuleAnchor anchor = inputs.ReadAnchor502170(actor, profile.AnchorSelector, profile.AnchorFraction);
        short yaw = inputs.ReadModel(actor).Rotation14 ?? throw new NotSupportedException("The Aqua anchor requires its actual caster yaw.");
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
        publish(new NativeBattleAquaRotate(identity, 'Y', yaw));
        NativeBattleActionMatrix rotation = NativeBattleCuragaMatrices.RotateY8DD8A0(identity, yaw);
        NativeBattleVector offset = profile.Effect == 55 ? new(0, 176, -32) : new(0, 0, 0);
        publish(new NativeBattleAquaTransform(rotation, offset, false));
        offset = NativeBattleCuragaMatrices.RotateVector45DF80(rotation, offset);
        return WriteVector(task, 0x1C, new(anchor.X, anchor.Y, anchor.Z)).WriteWord(0x1C, unchecked((ushort)(anchor.X + offset.X))).WriteWord(0x1E, unchecked((ushort)(anchor.Y + offset.Y))).WriteWord(0x20, unchecked((ushort)(anchor.Z + offset.Z)));
    }
}
