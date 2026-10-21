namespace Memoria.FF8.Runtime;

public abstract record NativeBattleCureOperation;
public sealed record NativeBattleCureSpriteOperation(NativeBattleModuleDraw Draw) : NativeBattleCureOperation;
public sealed record NativeBattleCureActorInitializeOperation(byte Target) : NativeBattleCureOperation;
public sealed record NativeBattleCureActorVisibilityOperation(byte Target, bool Hidden) : NativeBattleCureOperation;
public sealed record NativeBattleCureActorDrawOperation(byte Target, NativeBattleVector Center, short VerticalOffset) : NativeBattleCureOperation;
public sealed record NativeBattleCureParticleInputs(NativeBattleCureControllerInputs Controller, NativeCrtRandomState CrtRandom, Func<NativeBattleCureFrame, NativeBattleCureOperation, NativeBattleCureFrame> Execute)
{
    public NativeBattleCameraProjection? Projection { get; init; }
}
public sealed record NativeBattleCureParticleStep(NativeBattleCureFrame Frame, NativeCrtRandomState CrtRandomAfter, int Result);

/// <summary>Cure's8D6D80/8D7F60/8D80B0 task callbacks; packet/actor operations execute at their native callback point.</summary>
public static partial class NativeBattleCureParticles
{
    public static NativeBattleCureParticleStep AdvancePrivate(NativeBattleCureFrame frame, uint address, NativeBattleCureResources resources, NativeBattleCureParticleInputs inputs)
    {
        NativeBattleCureFamilyProfile profile = NativeBattleCureFamilyProfile.ForEffect(frame.Effect.Binding.Effect);
        if (resources.Effect != profile.Effect)
        {
            throw new InvalidDataException("Native Cure family resources belong to another module.");
        }
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new InvalidDataException("Native Cure particles require50AFC0 parameters.");
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        NativeCrtRandomState random = inputs.CrtRandom;
        NativeBattleCureFrame current = frame;
        switch (profile.CanonicalParticleCallback(task.Callback))
        {
            case 0x8DCC20:
                return AdvanceWedge8DCC20(frame, address, inputs);
            case 0x8D6D80:
                AdvanceRing();
                break;
            case 0x8D7F60:
                AdvanceMovingSprite(false);
                DrawSprite();
                break;
            case 0x8D80B0:
                AdvanceMovingSprite(true);
                DrawSprite();
                break;
            default:
                throw new NotSupportedException($"Native Cure private particle callback{task.Callback:X} has no original owner.");
        }
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        Put();
        bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
        if (complete)
        {
            hierarchy = hierarchy.Complete8DC530(address);
        }
        return new(current with { Effect = current.Effect with { Pools = hierarchy.Pools } }, random, complete ? 2 : 0);

        int Rand()
        {
            random = random.Draw(out ushort value);
            return value;
        }

        void Put() => hierarchy = hierarchy.Write(address, task);

        void Execute(NativeBattleCureOperation operation)
        {
            Put();
            current = inputs.Execute(current with { Effect = current.Effect with { Pools = hierarchy.Pools } }, operation);
            hierarchy = new(current.Effect.Pools);
            task = hierarchy.Read(address);
        }

        NativeBattleModulePoolTask FirstLevel(NativeBattleModulePoolTask source) => hierarchy.Read(source.ReadDword(0x14));

        short SpanXZ(NativeBattleModulePoolTask source)
        {
            NativeBattleModulePoolTask first = FirstLevel(source);
            short width = unchecked((short)(first.ReadWord(0x50) - first.ReadWord(0x48)));
            short depth = unchecked((short)(first.ReadWord(0x54) - first.ReadWord(0x4C)));
            return unchecked((short)(Math.Max(width, depth) / 2));
        }

        short TopY(NativeBattleModulePoolTask source)
        {
            NativeBattleModelTransform transform = inputs.Controller.ReadModel(source.ReadByte(0x2D)).Transform ?? throw new NotSupportedException("Native8DCAC0 requires target positionWORD30.");
            return unchecked((short)(transform.Position28.Y + FirstLevel(source).ReadWord(0x52)));
        }

        NativeBattleModulePoolTask CopyFirstAnchor(NativeBattleModulePoolTask destination, int offset)
        {
            NativeBattleModulePoolTask first = FirstLevel(destination);
            return destination.WriteDword(offset, first.ReadDword(0x30)).WriteDword(offset + 4, first.ReadDword(0x34));
        }

        NativeBattleEffectHierarchyAppend Append(int pool, uint callback, int bytes)
        {
            Put();
            NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(pool, profile.Callback(callback), bytes, address, (pointer, group, target) => NativeBattleCureController.SelectActors(parameters, inputs.Controller.Packets, pointer, group, target));
            hierarchy = appended.Hierarchy;
            task = hierarchy.Read(address);
            return appended;
        }

        void AdvanceRing()
        {
            switch (unchecked((sbyte)task.ReadByte(0x29)))
            {
                case 0:
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 2)).WriteDword(0x4C, profile.Sprite(0x1641F78));
                    task = CopyFirstAnchor(task, 0x60).WriteWord(0x62, unchecked((ushort)TopY(task)));
                    short radius = unchecked((short)(2 * SpanXZ(task)));
                    task = task.WriteWord(0x68, unchecked((ushort)(radius > 1024 ? 1024 : radius)));
                    NativeBattleModulePoolTask first = FirstLevel(task);
                    short height = unchecked((short)(first.ReadWord(0x52) - first.ReadWord(0x4A)));
                    short division = unchecked((short)((height + 256) / -20));
                    task = task.WriteWord(0x5A, unchecked((ushort)(division / 4)));
                    Execute(new NativeBattleCureActorInitializeOperation(task.ReadByte(0x2D)));
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 8));
                    Execute(new NativeBattleCureActorVisibilityOperation(task.ReadByte(0x2D), true));
                    IncrementPhase();
                    break;
                case 1:
                    if (Word(task, 0x24) >= 20)
                    {
                        task = task.WriteDword(0x4C, profile.Sprite(0x1641FCC)).WriteWord(0x52, 4);
                        IncrementPhase();
                    }
                    break;
                case 2:
                    if (AdvanceSpriteFrame())
                    {
                        uint rootAddress = task.ReadDword(0x10);
                        NativeBattleModulePoolTask root = hierarchy.Read(rootAddress);
                        hierarchy = hierarchy.Write(rootAddress, root.WriteByte(0x63, 0));
                        Execute(new NativeBattleCureActorVisibilityOperation(task.ReadByte(0x2D), false));
                        task = task.WriteByte(0x26, (byte)((task.ReadByte(0x26) & 0xF5) | 1));
                        IncrementPhase();
                    }
                    break;
                case 3:
                    break;
                default:
                    throw new InvalidDataException("Native8D6D80 signed phase exceeds its four original callbacks.");
            }
            for (int point = 0; point < 4; point++)
            {
                task = task.WriteDword(0x1C, task.ReadDword(0x60)).WriteDword(0x20, task.ReadDword(0x64));
                ushort step = unchecked((ushort)((task.ReadWord(0x24) << 2) + point));
                task = Add(task, 0x1E, unchecked((short)(task.ReadWord(0x5A) * step)));
                int angle = step << 7 & 0xFFF;
                task = task.WriteWord(0x42, (ushort)angle);
                task = Add(task, 0x1C, unchecked(NativeBattleCameraMath.Sine(angle) * Word(task, 0x68)) / 4096);
                task = Add(task, 0x20, unchecked(NativeBattleCameraMath.Cosine(angle) * Word(task, 0x68)) / 4096);
                if ((task.ReadByte(0x26) & 2) != 0 && Word(task, 0x24) is > 0 and < 17)
                {
                    _ = Append(2, 0x8D80B0, 108);
                }
            }
            if ((task.ReadByte(0x26) & 2) != 0)
            {
                SpawnSparks();
                SpawnWedges();
            }
            if ((task.ReadByte(0x26) & 8) != 0)
            {
                Execute(new NativeBattleCureActorDrawOperation(task.ReadByte(0x2D), new(Word(task, 0x1C), Word(task, 0x1E), Word(task, 0x20)), Word(task, 0x1E)));
            }
            DrawSprite();
            if (Word(task, 0x24) < 20 && (task.ReadByte(0x26) & 4) == 0)
            {
                byte gray = unchecked((byte)(60 - 3 * task.ReadByte(0x24)));
                uint color = (uint)(gray | gray << 8 | gray << 16);
                Draw(profile.Sprite(0x1642504), 0, new(Word(task, 0x1C), 0, Word(task, 0x20), 0), 0, 4, color, NativeBattlePoseMatrices.RotationWords(new(1024, 0, 0)));
            }
        }

        void SpawnSparks()
        {
            short counter = Word(task, 0x24);
            if (counter <= 1 || counter >= 32767)
            {
                return;
            }
            int count = counter > 10 ? 2 : counter > 8 ? 3 : counter > 6 ? 2 : 1;
            for (int particle = 0; particle < count; particle++)
            {
                int angle = Rand() & 0xFFF;
                NativeBattleEffectHierarchyAppend appended = Append(2, 0x8D7F60, 108);
                NativeBattleModulePoolTask child = CopyFirstAnchor(hierarchy.Read(appended.Address), 0x1C).WriteWord(0x1E, task.ReadWord(0x1E));
                int difference = Word(task, 0x1E) - TopY(task);
                int offset = unchecked(difference * (Rand() & 31)) / -48;
                child = Add(child, 0x1E, offset);
                short radius = unchecked((short)(3 * SpanXZ(task) / 2));
                if (radius > 1024)
                {
                    radius = 1024;
                }
                child = child.WriteWord(0x68, unchecked((ushort)radius));
                child = Add(child, 0x1C, unchecked(radius * NativeBattleCameraMath.Sine(angle)) / 4096);
                child = Add(child, 0x20, unchecked(radius * NativeBattleCameraMath.Cosine(angle)) / 4096);
                child = child.WriteDword(0x58, task.ReadDword(0x58)).WriteDword(0x5C, task.ReadDword(0x5C));
                hierarchy = hierarchy.Write(appended.Address, child);
            }
        }

        void SpawnWedges()
        {
            short counter = Word(task, 0x24);
            if (counter <= 3 || counter >= 11)
            {
                return;
            }
            int count = counter > 8 ? 4 : counter > 6 ? 3 : 2;
            for (int particle = 0; particle < count; particle++)
            {
                NativeBattleEffectHierarchyAppend appended = Append(3, 0x8DCC20, 92);
                NativeBattleModulePoolTask child = hierarchy.Read(appended.Address).WriteWord(0x56, 8).WriteByte(0x4C, 16).WriteByte(0x4D, 16).WriteByte(0x4E, 16).WriteByte(0x50, 0).WriteByte(0x51, 0).WriteByte(0x52, 16).WriteWord(0x3C, 256).WriteWord(0x3E, 16).WriteWord(0x40, 64).WriteWord(0x42, 16).WriteWord(0x58, unchecked((ushort)((Rand() & 1) != 0 ? 64 : -64)));
                hierarchy = hierarchy.Write(appended.Address, child);
            }
        }

        void AdvanceMovingSprite(bool trail)
        {
            switch (unchecked((sbyte)task.ReadByte(0x29)))
            {
                case 0:
                    task = task.WriteDword(0x4C, profile.Sprite(trail ? 0x1642238u : 0x164205Cu)).WriteWord(0x52, trail ? (ushort)8 : (ushort)16);
                    if (trail)
                    {
                        task = task.WriteWord(0x58, unchecked((ushort)((Rand() & 31) - 16))).WriteWord(0x5C, unchecked((ushort)((Rand() & 31) - 16))).WriteWord(0x5A, (ushort)((Rand() & 15) + 16));
                    }
                    else
                    {
                        task = task.WriteWord(0x58, 0).WriteWord(0x5C, 0).WriteWord(0x5A, unchecked((ushort)(-8 - (Rand() & 15))));
                    }
                    IncrementPhase();
                    break;
                case 1:
                    task = Add(task, 0x1C, Word(task, 0x58));
                    task = Add(task, 0x1E, Word(task, 0x5A));
                    task = Add(task, 0x20, Word(task, 0x5C));
                    if (AdvanceSpriteFrame())
                    {
                        task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
                        IncrementPhase();
                    }
                    break;
                case 2:
                    break;
                default:
                    throw new InvalidDataException("Native Cure moving-sprite signed phase exceeds its three callbacks.");
            }
        }

        bool AdvanceSpriteFrame()
        {
            task = task.WriteWord(0x50, unchecked((ushort)(task.ReadWord(0x50) + 1)));
            if (Word(task, 0x50) <= Word(task, 0x52))
            {
                return false;
            }
            task = task.WriteWord(0x50, task.ReadWord(0x52)).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 4));
            return true;
        }

        void IncrementPhase() => task = task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));

        void DrawSprite()
        {
            if ((task.ReadByte(0x26) & 4) == 0)
            {
                Draw(task.ReadDword(0x4C), Word(task, 0x50), new(Word(task, 0x1C), Word(task, 0x1E), Word(task, 0x20), 0), Word(task, 0x54), 0, 0x808080, []);
            }
        }

        void Draw(uint resourceAddress, int phase, NativeBattleModuleAnchor anchor, int depthOffset, ushort flags, uint color, System.Collections.Immutable.ImmutableArray<short> rotation)
        {
            NativeBattleModuleResource resource = resources.Sprites.Single(resource => resource.Address == resourceAddress);
            NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, SpriteFrame = resource.SpriteFrame(phase), Anchor = anchor, Phase = unchecked((ushort)phase), Scale = 4096, DepthOffset = depthOffset, Direction = 4096, SpriteFlags = flags, SpriteColor = color, SpriteWorldRotation = rotation };
            Execute(new NativeBattleCureSpriteOperation(draw));
        }
    }

    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleModulePoolTask Add(NativeBattleModulePoolTask task, int offset, int value) => task.WriteWord(offset, unchecked((ushort)(task.ReadWord(offset) + value)));
}
