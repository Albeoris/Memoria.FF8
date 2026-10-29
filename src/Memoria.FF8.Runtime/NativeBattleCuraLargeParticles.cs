using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCuraMeshOperation(NativeBattleModulePacketBatch Batch) : NativeBattleCuraOperation;
public sealed record NativeBattleCuraStageFadeOperation(short Fade, bool ClearRgb) : NativeBattleCuraOperation;
public sealed record NativeBattleCuraActorInitializeOperation(byte Target) : NativeBattleCuraOperation;
public sealed record NativeBattleCuraActorVisibilityOperation(byte Target, bool Hidden) : NativeBattleCuraOperation;
public sealed record NativeBattleCuraActorDrawOperation(byte Target, NativeBattleVector Center, short VerticalOffset) : NativeBattleCuraOperation
{
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}

/// <summary>Cura's original200-byte scene, emitter, stage-fade and copied-actor callbacks.</summary>
public static partial class NativeBattleCuraParticles
{
    public static NativeBattleCuraParticleStep AdvanceLarge(NativeBattleCuraFrame frame, uint address, NativeBattleCuraResources resources, NativeBattleCuraParticleInputs inputs)
    {
        NativeBattleCuraFamilyProfile profile = NativeBattleCuraFamilyProfile.ForEffect(frame.Effect.Binding.Effect);
        if (resources.Effect != profile.Effect)
        {
            throw new InvalidDataException("Native Cura family resources belong to another module.");
        }
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        NativeCrtRandomState random = inputs.CrtRandom;
        switch (profile.CanonicalParticleCallback(task.Callback))
        {
            case 0x88A740:
                SceneParticle(0x1611C20, 100, true);
                break;
            case 0x88B690:
                SceneParticle(0x161194C, 52, false);
                task = task.WriteWord(0x50, (ushort)((task.ReadWord(0x50) + task.ReadWord(0x58)) & 0xFFF)).WriteWord(0x54, (ushort)((task.ReadWord(0x54) + task.ReadWord(0x5C)) & 0xFFF));
                break;
            case 0x88B530:
                EmitScenes();
                break;
            case 0x88B810:
                EmitRising(1, 18, 0x88B950, false);
                break;
            case 0x88CC70:
                EmitRising(2, 20, 0x88CDA0, true);
                break;
            case 0x88B3D0:
                StageFade();
                break;
            case 0x88BBD0:
                CopiedActor();
                break;
            default:
                throw new NotSupportedException($"Native Cura large callback{task.Callback:X} requires its original owner.");
        }
        task = Add(task, 0x24, 1);
        Put();
        bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
        if (complete)
        {
            hierarchy = hierarchy.Complete8DC530(address);
        }
        return new(frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } }, random, complete ? 2 : 0);

        int Phase() => unchecked((sbyte)task.ReadByte(0x29));
        int Rand()
        {
            random = random.Draw(out ushort value);
            return value;
        }
        void Put() => hierarchy = hierarchy.Write(address, task);
        void Next() => task = task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
        void Complete()
        {
            task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
            Next();
        }
        void Execute(NativeBattleCuraOperation operation)
        {
            Put();
            frame = inputs.Execute(frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } }, operation);
            hierarchy = new(frame.Effect.Pools);
            task = hierarchy.Read(address);
        }
        NativeBattleModulePoolTask First() => hierarchy.Read(task.ReadDword(0x14));
        NativeBattleModulePoolTask CopyAnchor()
        {
            NativeBattleModulePoolTask first = First();
            return task.WriteDword(0x1C, first.ReadDword(0x30)).WriteDword(0x20, first.ReadDword(0x34));
        }
        NativeBattleModulePoolTask CopyMidpoint()
        {
            NativeBattleModulePoolTask first = First();
            return task.WriteDword(0x1C, first.ReadDword(0x38)).WriteDword(0x20, first.ReadDword(0x3C));
        }
        short BottomY()
        {
            NativeBattleModelTransform transform = inputs.Controller.ReadModel(task.ReadByte(0x2D)).Transform ?? throw new NotSupportedException("Native8DCAF0 requires target positionWORD30.");
            return unchecked((short)(transform.Position28.Y + First().ReadWord(0x4A)));
        }
        uint Append(int pool, uint callback, int bytes)
        {
            Put();
            NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new InvalidDataException("Native Cura append requires50AFC0 parameters.");
            NativeBattleEffectHierarchyAppend append = hierarchy.Append8DC540(pool, profile.Callback(callback), bytes, address, (pointer, group, target) => NativeBattleCureController.SelectActors(parameters, inputs.Controller.Packets, pointer, group, target));
            hierarchy = append.Hierarchy;
            task = hierarchy.Read(address);
            if (append.Address == 0)
            {
                throw new NotSupportedException("Native Cura allocation returned null before its original field writes.");
            }
            return append.Address;
        }
        int Scheduled(int table)
        {
            short counter = Word(task, 0x24);
            if (resources.EmissionSchedules.IsDefault || table >= resources.EmissionSchedules.Length || counter < 0 || counter >= resources.EmissionSchedules[table].Length)
            {
                throw new InvalidDataException("Native Cura signed emission-table counter leaves its authenticated schedule.");
            }
            return resources.EmissionSchedules[table][counter];
        }
        NativeBattleModulePoolTask RandomOffset(NativeBattleModulePoolTask child)
        {
            short yaw = (short)(Rand() & 0xFFF);
            short pitch = (short)(Rand() & 0xFFF);
            short distance = (short)(Rand() % 512);
            NativeBattleVector offset = NativeBattlePoseMatrices.RotateVector(new(pitch, yaw, 0), new(0, 0, distance));
            return Add(Add(Add(child, 0x1C, offset.X), 0x1E, offset.Y), 0x20, offset.Z);
        }
        void EmitScenes()
        {
            switch (Phase())
            {
                case 0:
                    task = CopyMidpoint();
                    Next();
                    break;
                case 1:
                    for (int index = 0; index < Scheduled(0); index++)
                    {
                        uint childAddress = Append(2, 0x88B690, 200);
                        NativeBattleModulePoolTask child = hierarchy.Read(childAddress).WriteDword(0x30, 4096).WriteDword(0x34, 4096).WriteDword(0x38, 4096);
                        child = child.WriteWord(0x50, (ushort)(Rand() & 0xFFF)).WriteWord(0x54, (ushort)(Rand() & 0xFFF)).WriteWord(0x58, (ushort)((Rand() & 63) + 32)).WriteWord(0x5C, (ushort)((Rand() & 63) + 32));
                        if ((Rand() & 1) != 0)
                        {
                            child = child.WriteWord(0x58, unchecked((ushort)(4096 - child.ReadWord(0x58))));
                        }
                        if ((Rand() & 1) != 0)
                        {
                            child = child.WriteWord(0x5C, unchecked((ushort)(4096 - child.ReadWord(0x5C))));
                        }
                        hierarchy = hierarchy.Write(childAddress, child);
                    }
                    if (Word(task, 0x24) >= 18)
                    {
                        Complete();
                    }
                    break;
                case 2:
                    break;
                default:
                    InvalidPhase();
                    break;
            }
        }
        void EmitRising(int schedule, int duration, uint callback, bool retainRandomY)
        {
            switch (Phase())
            {
                case 0:
                    task = CopyAnchor().WriteWord(0x1E, unchecked((ushort)BottomY()));
                    task = task.WriteWord(0x42, unchecked((ushort)((256 - Word(task, 0x1E)) / duration)));
                    Next();
                    break;
                case 1:
                    task = Add(task, 0x1E, Word(task, 0x42));
                    for (int index = 0; index < Scheduled(schedule); index++)
                    {
                        uint childAddress = Append(3, callback, 132);
                        NativeBattleModulePoolTask child = hierarchy.Read(childAddress);
                        if (retainRandomY)
                        {
                            child = child.WriteWord(0x1E, unchecked((ushort)(task.ReadWord(0x1E) - 384)));
                        }
                        child = RandomOffset(child);
                        if (!retainRandomY)
                        {
                            child = child.WriteWord(0x1E, task.ReadWord(0x1E)).WriteDword(0x30, 4096).WriteDword(0x34, 4096).WriteDword(0x38, 4096);
                        }
                        hierarchy = hierarchy.Write(childAddress, child);
                    }
                    if (Word(task, 0x24) >= duration)
                    {
                        Complete();
                    }
                    break;
                case 2:
                    break;
                default:
                    InvalidPhase();
                    break;
            }
        }
        void StageFade()
        {
            switch (Phase())
            {
                case 0:
                    task = task.WriteWord(0x1C, 0);
                    Execute(new NativeBattleCuraStageFadeOperation(0, true));
                    Next();
                    break;
                case 1:
                    task = Add(task, 0x1C, 192);
                    if (Word(task, 0x1C) >= 1536)
                    {
                        task = task.WriteWord(0x1C, 1536);
                        Next();
                    }
                    Execute(new NativeBattleCuraStageFadeOperation(Word(task, 0x1C), false));
                    break;
                case 2:
                    if (Word(task, 0x24) >= 20)
                    {
                        task = task.WriteWord(0x1E, 0);
                        Next();
                    }
                    break;
                case 3:
                    task = Add(task, 0x1C, -192);
                    if (Word(task, 0x1C) <= 0)
                    {
                        task = task.WriteWord(0x1C, 0);
                        Complete();
                    }
                    Execute(new NativeBattleCuraStageFadeOperation(Word(task, 0x1C), false));
                    break;
                case 4:
                    break;
                default:
                    InvalidPhase();
                    break;
            }
        }
        void SceneParticle(uint sourceAddress, int clearBytes, bool wide)
        {
            NativeBattleCuraSceneResource scene = resources.Scenes.Single(source => source.Address == profile.Resource(sourceAddress));
            NativeBattleGfAnimationSource source = new(scene.Address, scene.Data);
            NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, 12 + (0x60 - 12), task.PayloadLength - (0x60 - 12));
            switch (Phase())
            {
                case 0:
                    if (wide)
                    {
                        task = task.WriteDword(0x30, 4096).WriteDword(0x34, 4096).WriteDword(0x38, 4096);
                    }
                    WriteWorkspace(workspace.Initialize88A7D0(source, clearBytes));
                    Next();
                    break;
                case 1:
                    task = wide ? CopyAnchor() : CopyMidpoint();
                    NativeBattleCameraProjection projection = inputs.Projection ?? throw new NotSupportedException("Native Cura scene requires its previous published camera projection.");
                    NativeBattleVector rotation = wide ? new(0, Word(task, 0xC4), 0) : new(Word(task, 0x50), Word(task, 0x52), Word(task, 0x54));
                    NativeBattleActionMatrix parent = NativeBattlePoseMatrices.CuraSceneParent88A8D0(projection.View ?? throw new NotSupportedException("Native88A8D0 requires the actual camera matrix."), rotation, new(Word(task, 0x1C), Word(task, 0x1E), Word(task, 0x20)), [unchecked((int)task.ReadDword(0x30)), unchecked((int)task.ReadDword(0x34)), unchecked((int)task.ReadDword(0x38))]);
                    NativeBattleGfAnimationAdvance step = workspace.Advance88A980(source, (current, node) =>
                    {
                        WriteWorkspace(current);
                        NativeBattleModulePacketBatch batch = NativeBattleCuraScenePackets.Emit88AF90(scene, node, parent, projection, wide ? 0x40000030u : 0x30u, profile.ReadSceneFade(frame.Memory), frame.Effect.Binding.Effect is 25 or 51 ? NativeBattlePilotVisualLifetimes.Identity(frame.Effect.Binding.Effect, task, wide ? 1 : 0) : null);
                        Execute(new NativeBattleCuraMeshOperation(batch));
                        return NativeBattleGfAnimationWorkspace.Borrow(task, 0x60, task.PayloadLength - (0x60 - 12));
                    });
                    WriteWorkspace(step.Workspace);
                    if (step.Remaining == 0)
                    {
                        Complete();
                    }
                    break;
                case 2:
                    break;
                default:
                    InvalidPhase();
                    break;
            }
        }
        void WriteWorkspace(NativeBattleGfAnimationWorkspace workspace)
        {
            task = NativeBattleEffectTaskMemory.PublishAnimationWorkspace(task, 0x60, workspace);
        }
        void CopiedActor()
        {
            switch (Phase())
            {
                case 0:
                    task = Add(CopyAnchor(), 0x1E, -1536);
                    task = task.WriteWord(0x42, unchecked((ushort)((256 - Word(task, 0x1E)) / 18)));
                    Execute(new NativeBattleCuraActorInitializeOperation(task.ReadByte(0x2D)));
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 8));
                    Execute(new NativeBattleCuraActorVisibilityOperation(task.ReadByte(0x2D), true));
                    Next();
                    break;
                case 1:
                    task = Add(task, 0x1E, Word(task, 0x42));
                    if (Word(task, 0x24) >= 18)
                    {
                        Execute(new NativeBattleCuraActorVisibilityOperation(task.ReadByte(0x2D), false));
                        uint root = task.ReadDword(0x10);
                        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x63, 0));
                        task = task.WriteWord(0x26, (ushort)((task.ReadWord(0x26) & ~8) | 1));
                        Next();
                    }
                    break;
                case 2:
                    break;
                default:
                    InvalidPhase();
                    break;
            }
            if ((task.ReadByte(0x26) & 8) != 0)
            {
                NativeBattleActionMatrix camera = inputs.Projection?.View ?? throw new NotSupportedException("Native8DD7B0 requires the actual camera rotation.");
                short yaw = unchecked((short)((NativeWorldScriptedPathMath.NativeAngle(camera.Rotation[8], camera.Rotation[2]) + 1024) & 0xFFF));
                NativeBattleModulePoolTask first = First();
                task = task.WriteWord(0x52, unchecked((ushort)yaw)).WriteWord(0x1C, unchecked((ushort)(first.ReadWord(0x30) + NativeBattleCameraMath.Sine(yaw) * 1024 / 4096))).WriteWord(0x20, unchecked((ushort)(first.ReadWord(0x34) + NativeBattleCameraMath.Cosine(yaw) * 1024 / 4096)));
                Execute(new NativeBattleCuraActorDrawOperation(task.ReadByte(0x2D), new(Word(task, 0x1C), Word(task, 0x1E), Word(task, 0x20)), Word(task, 0x1E)) { PilotIdentity = frame.Effect.Binding.Effect is 25 or 51 ? NativeBattlePilotVisualLifetimes.Identity(frame.Effect.Binding.Effect, task, 10) : null });
            }
        }
        void InvalidPhase() => throw new InvalidDataException("Native Cura signed large-particle phase exceeds its original callback table.");
    }
}

public static partial class NativeBattlePoseMatrices
{
    /// <summary>88A8D0/88B740 scale the world basis before its56C2F0 camera composition.</summary>
    public static NativeBattleActionMatrix CuraSceneParent88A8D0(NativeBattleActionMatrix camera, NativeBattleVector rotation, NativeBattleVector position, int[] scale)
    {
        camera.Validate();
        if (scale.Length != 3)
        {
            throw new InvalidDataException("Native56BEF0 requires three DWORD scale components.");
        }
        Matrix world = Scale(Rotation(rotation), scale) with { X = position.X, Y = position.Y, Z = position.Z };
        Matrix result = Compose(new(camera.Rotation.ToArray(), camera.X, camera.Y, camera.Z), world);
        return new(result.Rotation.ToImmutableArray(), result.X, result.Y, result.Z, 4096);
    }
}
