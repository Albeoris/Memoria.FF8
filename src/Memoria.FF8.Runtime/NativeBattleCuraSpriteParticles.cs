using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public abstract record NativeBattleCuraOperation;
public sealed record NativeBattleCuraSpriteOperation(NativeBattleModuleDraw Draw) : NativeBattleCuraOperation;
public sealed record NativeBattleCuraParticleInputs(NativeBattleCuraControllerInputs Controller, NativeCrtRandomState CrtRandom, Func<NativeBattleCuraFrame, NativeBattleCuraOperation, NativeBattleCuraFrame> Execute)
{
    public NativeBattleCameraProjection? Projection { get; init; }
}
public sealed record NativeBattleCuraParticleStep(NativeBattleCuraFrame Frame, NativeCrtRandomState CrtRandomAfter, int Result);

/// <summary>Original88A070/88A2D0/88A4E0/88B950/88CDA0 sprite callbacks and their immediate draw order.</summary>
public static partial class NativeBattleCuraParticles
{
    public static NativeBattleCuraParticleStep AdvanceSprite(NativeBattleCuraFrame frame, uint address, NativeBattleCuraResources resources, NativeBattleCuraParticleInputs inputs)
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
            case 0x88A070:
                AdvanceBurst();
                DrawNormal();
                DrawGround(task.ReadDword(0x4C), 0x202020, 0, 4096);
                break;
            case 0x88A2D0:
                AdvanceStar();
                DrawNormal();
                DrawGround(task.ReadDword(0x4C), 0x505050, 0, 4096);
                break;
            case 0x88A4E0:
                AdvanceTrail();
                FinishBelowGround();
                DrawNormal();
                break;
            case 0x88B950:
                AdvanceDoubleSprite();
                DrawNormal();
                task = task.WriteWord(0x7A, unchecked((ushort)((task.ReadWord(0x7A) + 64) & 0xFFF)));
                DrawGround(task.ReadDword(0x70), 0x303030, Word(task, 0x7A), 8192);
                break;
            case 0x88CDA0:
                AdvanceRisingSprite();
                FinishBelowGround();
                DrawNormal();
                break;
            default:
                throw new NotSupportedException($"Native Cura sprite callback{task.Callback:X} requires its original owner.");
        }
        task = Add(task, 0x24, 1);
        hierarchy = hierarchy.Write(address, task);
        bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
        if (complete)
        {
            hierarchy = hierarchy.Complete8DC530(address);
        }
        return new(frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } }, random, complete ? 2 : 0);

        int Rand()
        {
            random = random.Draw(out ushort value);
            return value;
        }

        void IncrementPhase() => task = task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));

        void AdvanceBurst()
        {
            switch (Phase())
            {
                case 0:
                    task = FirstAnchor(task).WriteDword(0x4C, profile.Resource(0x1610948)).WriteWord(0x52, 3);
                    task = Add(task, 0x1E, -1536);
                    int angle = Rand() & 0xFFF;
                    for (int index = 0; index < 8; index++)
                    {
                        uint childAddress = Append(0x88A2D0);
                        NativeBattleModulePoolTask child = hierarchy.Read(childAddress).WriteDword(0x30, 4096).WriteDword(0x34, 4096).WriteDword(0x38, 4096).WriteWord(0x42, unchecked((ushort)(angle + (Rand() & 127))));
                        hierarchy = hierarchy.Write(childAddress, child);
                        angle = angle + 512 & 0xFFF;
                    }
                    IncrementPhase();
                    break;
                case 1:
                    if (AdvanceFrame())
                    {
                        CompletePhase();
                    }
                    break;
                case 2:
                    break;
                default:
                    InvalidPhase();
                    break;
            }
        }

        void AdvanceStar()
        {
            switch (Phase())
            {
                case 0:
                    task = task.WriteWord(0x78, (ushort)(Rand() & 0xFFF)).WriteDword(0x68, task.ReadDword(0x1C)).WriteDword(0x6C, task.ReadDword(0x20)).WriteDword(0x4C, profile.Resource(0x16109BC)).WriteWord(0x52, 1);
                    task = task.WriteWord(0x5A, unchecked((ushort)((Rand() & 63) - 48)));
                    short speed = (short)((Rand() & 31) + 80);
                    task = task.WriteWord(0x7C, unchecked((ushort)speed)).WriteWord(0x7E, unchecked((ushort)(-speed / 16)));
                    IncrementPhase();
                    break;
                case 1:
                    task = Add(task, 0x50, 1);
                    if (Word(task, 0x50) > Word(task, 0x52))
                    {
                        task = task.WriteWord(0x50, 0);
                    }
                    if (Word(task, 0x24) >= 16)
                    {
                        task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 16)).WriteDword(0x4C, profile.Resource(0x1610A10)).WriteWord(0x50, 0).WriteWord(0x52, 6);
                        IncrementPhase();
                    }
                    break;
                case 2:
                    if (AdvanceFrame())
                    {
                        CompletePhase();
                    }
                    break;
                case 3:
                    break;
                default:
                    InvalidPhase();
                    break;
            }
            task = Add(task, 0x7C, Word(task, 0x7E));
            if (Word(task, 0x7C) < 0)
            {
                task = task.WriteWord(0x7C, 0);
            }
            task = Add(task, 0x68, unchecked(NativeBattleCameraMath.Sine(Word(task, 0x42)) * Word(task, 0x7C)) / 4096);
            task = Add(task, 0x6C, unchecked(NativeBattleCameraMath.Cosine(Word(task, 0x42)) * Word(task, 0x7C)) / 4096);
            task = Add(task, 0x5A, 10).WriteWord(0x1C, task.ReadWord(0x68)).WriteWord(0x20, task.ReadWord(0x6C));
            task = Add(task, 0x1E, Word(task, 0x5A));
            if ((task.ReadByte(0x26) & 16) == 0 && (task.ReadByte(0x24) & 1) != 0)
            {
                uint childAddress = Append(0x88A4E0);
                NativeBattleModulePoolTask child = RandomOffset(hierarchy.Read(childAddress), 128).WriteWord(0x1E, task.ReadWord(0x1E)).WriteDword(0x30, 4096).WriteDword(0x34, 4096).WriteDword(0x38, 4096);
                hierarchy = hierarchy.Write(childAddress, child);
            }
            FinishBelowGround();
        }

        void AdvanceTrail()
        {
            switch (Phase())
            {
                case 0:
                    task = task.WriteDword(0x4C, profile.Resource(0x1610AD4)).WriteWord(0x50, 0).WriteWord(0x52, 10).WriteWord(0x62, unchecked((ushort)-3)).WriteWord(0x5A, (ushort)((Rand() & 7) + 32));
                    IncrementPhase();
                    break;
                case 1:
                    task = Add(task, 0x5A, Word(task, 0x62));
                    if (Word(task, 0x5A) < 2)
                    {
                        task = task.WriteWord(0x5A, 2);
                    }
                    task = Add(task, 0x1E, Word(task, 0x5A));
                    if (AdvanceFrame())
                    {
                        CompletePhase();
                    }
                    break;
                case 2:
                    break;
                default:
                    InvalidPhase();
                    break;
            }
        }

        void AdvanceDoubleSprite()
        {
            switch (Phase())
            {
                case 0:
                    bool first = (Rand() & 1) != 0;
                    task = task.WriteDword(0x4C, first ? profile.Resource(0x161079C) : profile.Resource(0x1610C00)).WriteDword(0x70, first ? profile.Resource(0x1610DAC) : profile.Resource(0x1610F58)).WriteWord(0x7A, (ushort)(Rand() & 0xFFF)).WriteWord(0x52, 15).WriteWord(0x54, unchecked((ushort)-512));
                    IncrementPhase();
                    break;
                case 1:
                    if (AdvanceFrame())
                    {
                        CompletePhase();
                    }
                    break;
                case 2:
                    break;
                default:
                    InvalidPhase();
                    break;
            }
        }

        void AdvanceRisingSprite()
        {
            switch (Phase())
            {
                case 0:
                    task = task.WriteDword(0x4C, profile.Resource(0x1610AD4)).WriteWord(0x52, 10).WriteWord(0x54, unchecked((ushort)-1024)).WriteWord(0x5A, (ushort)((Rand() & 7) + 4));
                    IncrementPhase();
                    break;
                case 1:
                    task = Add(task, 0x1E, Word(task, 0x5A));
                    if (AdvanceFrame())
                    {
                        CompletePhase();
                    }
                    break;
                case 2:
                    break;
                default:
                    InvalidPhase();
                    break;
            }
        }

        NativeBattleModulePoolTask FirstAnchor(NativeBattleModulePoolTask destination)
        {
            NativeBattleModulePoolTask first = hierarchy.Read(destination.ReadDword(0x14));
            return destination.WriteDword(0x1C, first.ReadDword(0x30)).WriteDword(0x20, first.ReadDword(0x34));
        }

        uint Append(uint callback)
        {
            hierarchy = hierarchy.Write(address, task);
            NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new InvalidDataException("Native Cura child append requires its50AFC0 parameters.");
            NativeBattleEffectHierarchyAppend append = hierarchy.Append8DC540(3, profile.Callback(callback), 132, address, (pointer, group, target) => NativeBattleCureController.SelectActors(parameters, inputs.Controller.Packets, pointer, group, target));
            hierarchy = append.Hierarchy;
            task = hierarchy.Read(address);
            if (append.Address == 0)
            {
                throw new NotSupportedException("Native Cura particle allocation returned null before its original field writes.");
            }
            return append.Address;
        }

        NativeBattleModulePoolTask RandomOffset(NativeBattleModulePoolTask child, short radius)
        {
            if (radius == 0)
            {
                radius = 1;
            }
            short yaw = (short)(Rand() & 0xFFF);
            short pitch = (short)(Rand() & 0xFFF);
            short distance = unchecked((short)(Rand() % radius));
            NativeBattleVector offset = NativeBattlePoseMatrices.RotateVector(new(pitch, yaw, 0), new(0, 0, distance));
            return Add(Add(Add(child, 0x1C, offset.X), 0x1E, offset.Y), 0x20, offset.Z);
        }

        int Phase() => unchecked((sbyte)task.ReadByte(0x29));
        void InvalidPhase() => throw new InvalidDataException("Native Cura sprite signed phase exceeds its original callback table.");
        void CompletePhase()
        {
            task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
            IncrementPhase();
        }

        bool AdvanceFrame()
        {
            task = Add(task, 0x50, 1);
            if (Word(task, 0x50) <= Word(task, 0x52))
            {
                return false;
            }
            task = task.WriteWord(0x50, task.ReadWord(0x52)).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 4));
            return true;
        }

        void FinishBelowGround()
        {
            if (Word(task, 0x1E) > 0)
            {
                task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5));
            }
        }

        void DrawNormal() => Draw(task.ReadDword(0x4C), new(Word(task, 0x1C), Word(task, 0x1E), Word(task, 0x20), 0), Word(task, 0x54), 0, 0x808080, []);

        void DrawGround(uint resource, uint color, short angle, int scale)
        {
            ImmutableArray<short> rotation = NativeBattlePoseMatrices.RotationWords(new(1024, angle, 0)).Select(word => unchecked((short)(word * scale >> 12))).ToImmutableArray();
            Draw(resource, new(Word(task, 0x1C), 0, Word(task, 0x20), 0), 0, 4, color, rotation);
        }

        void Draw(uint resourceAddress, NativeBattleModuleAnchor anchor, int depth, ushort flags, uint color, ImmutableArray<short> rotation)
        {
            if ((task.ReadByte(0x26) & 4) != 0)
            {
                return;
            }
            NativeBattleModuleResource resource = resources.Sprites.Single(sprite => sprite.Address == resourceAddress);
            short phase = Word(task, 0x50);
            NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, SpriteFrame = resource.SpriteFrame(phase), Phase = unchecked((ushort)phase), Anchor = anchor, DepthOffset = depth, Scale = 4096, Direction = 4096, SpriteFlags = flags, SpriteColor = color, SpriteWorldRotation = rotation, PilotIdentity = frame.Effect.Binding.Effect is 25 or 51 ? NativeBattlePilotVisualLifetimes.Identity(frame.Effect.Binding.Effect, task, flags == 0 ? 0 : 1) : null };
            hierarchy = hierarchy.Write(address, task);
            frame = inputs.Execute(frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } }, new NativeBattleCuraSpriteOperation(draw));
            hierarchy = new(frame.Effect.Pools);
            task = hierarchy.Read(address);
        }
    }

    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleModulePoolTask Add(NativeBattleModulePoolTask task, int offset, int value) => task.WriteWord(offset, unchecked((ushort)(task.ReadWord(offset) + value)));
}
