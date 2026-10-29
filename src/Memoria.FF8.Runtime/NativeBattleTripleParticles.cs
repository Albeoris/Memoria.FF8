using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleTripleController
{
    public static NativeBattleTripleParticleStep AdvancePrivate(NativeBattleTripleFrame frame, uint address, NativeBattleTripleResources resources, NativeBattleTripleInputs inputs, NativeCrtRandomState random)
    {
        TripleExecution execution = new(frame, address, resources, inputs, random);
        execution.Advance();
        return execution.Finish();
    }

    private sealed class TripleExecution(NativeBattleTripleFrame frame, uint address, NativeBattleTripleResources resources, NativeBattleTripleInputs inputs, NativeCrtRandomState random)
    {
        public NativeBattleTripleFrame Frame { get; private set; } = frame;
        public NativeBattleEffectHierarchy Hierarchy { get; private set; } = new(frame.Effect.Pools);
        public NativeCrtRandomState Random { get; private set; } = random;
        private NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);

        private int Rand()
        {
            Random = Random.Draw(out ushort value);
            return value;
        }

        private void Put()
        {
            Hierarchy = Hierarchy.Write(address, task);
            Frame = Frame with { Effect = Frame.Effect with { Pools = Hierarchy.Pools } };
        }

        private void Execute(NativeBattleTripleOperation operation)
        {
            Put();
            Frame = inputs.Execute(Frame, operation);
            Hierarchy = new(Frame.Effect.Pools);
            task = Hierarchy.Read(address);
        }

        private uint Append(int pool, uint callback, int size, uint? parent = null)
        {
            Put();
            NativeBattleEffectParameters parameters = Frame.Effect.Parameters ?? throw new InvalidDataException("Native Triple producer lost its parameters.");
            NativeBattleEffectHierarchyAppend child = Hierarchy.Append8DC540(pool, callback, size, parent ?? address, (pointer, group, target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, group, target));
            Hierarchy = child.Hierarchy;
            task = Hierarchy.Read(address);
            if (child.Address == 0)
            {
                throw new NotSupportedException("Native Triple producer dereferenced an exhausted8DC540 allocation.");
            }
            return child.Address;
        }

        private NativeBattleModulePoolTask Anchor(NativeBattleModulePoolTask child, int source = 0x30)
        {
            NativeBattleModulePoolTask first = Hierarchy.Read(child.ReadDword(0x14));
            return child.WriteDword(0x1C, first.ReadDword(source)).WriteDword(0x20, first.ReadDword(source + 4));
        }

        public void SpawnPrimary89E1E0()
        {
            uint child = Append(4, 0x89E250, 124);
            NativeBattleModulePoolTask created = Add(Anchor(Hierarchy.Read(child)), 0x1E, -640);
            created = Scale(created, 4096).WriteWord(0x46, 0).WriteDword(0x4C, 0x1627380).WriteWord(0x64, 2048).WriteWord(0x66, 0);
            created = Color(Color(created, 0x5C, 255), 0x60, 0);
            Hierarchy = Hierarchy.Write(child, created);
            Put();
        }

        public void SpawnOverlay89F8D0()
        {
            NativeBattleEffectParameters parameters = Frame.Effect.Parameters ?? throw new InvalidDataException("Native89F8D0 requires50AFC0 parameters.");
            int header = checked(parameters.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
            int contact = inputs.Packets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Native89F8D0 requires its original first-contact pointer.");
            if ((inputs.Packets.ReadContact(contact).Primary.HitFlags3 & 4) != 0)
            {
                return;
            }
            uint child = Append(5, 0x89F950, 80);
            NativeBattleModulePoolTask created = Anchor(Hierarchy.Read(child), 0x40);
            created = created.WriteDword(0x38, created.ReadDword(0x1C)).WriteDword(0x3C, created.ReadDword(0x20)).WriteWord(0x40, 576).WriteWord(0x42, 288).WriteWord(0x44, 320).WriteWord(0x46, 244).WriteWord(0x48, 0).WriteWord(0x4A, 32).WriteWord(0x4C, 40).WriteWord(0x4E, 16);
            Hierarchy = Hierarchy.Write(child, created);
            Put();
        }

        public void Advance()
        {
            switch (task.Callback)
            {
                case 0x89E250:
                    AdvancePrimary();
                    task = task.WriteDword(0x40, task.ReadDword(0x5C)).WriteWord(0x50, task.ReadWord(0x64)).WriteDword(0x4C, 0x1627380);
                    DrawMesh();
                    task = task.WriteDword(0x40, task.ReadDword(0x60)).WriteWord(0x50, task.ReadWord(0x66)).WriteDword(0x4C, 0x1628218);
                    DrawMesh();
                    if ((task.ReadByte(0x26) & 2) != 0)
                    {
                        SpawnFan89E3C0(0);
                        SpawnFan89E3C0(1);
                        SpawnMorph89E500();
                    }
                    break;
                case 0x89E670:
                    AdvanceMorph();
                    break;
                case 0x89E7D0:
                case 0x89F180:
                    NativeBattleEnhancementSpriteMotionStep motion = task.Callback == 0x89E7D0 ? NativeBattleEnhancementSpriteMotion.AdvanceFan(task, Random) : NativeBattleEnhancementSpriteMotion.AdvanceGround(task, Random, Hierarchy.Read, true);
                    task = motion.Task;
                    Random = motion.Random;
                    DrawOrientedSprite(task.Callback == 0x89F180);
                    break;
                case 0x89EA70:
                    AdvanceSpawner();
                    break;
                case 0x89EBD0:
                    AdvanceFirstMesh();
                    break;
                case 0x89EDB0:
                    AdvanceSecondMesh();
                    break;
                case 0x89F410:
                    AdvanceMovingSprite();
                    break;
                case 0x89F5E0:
                    AdvanceBurstSprite();
                    break;
                case 0x89F950:
                    AdvanceOverlay();
                    break;
                default:
                    throw new NotSupportedException($"Native Triple private callback{task.Callback:X} has no original implementation.");
            }
        }

        public NativeBattleTripleParticleStep Finish()
        {
            task = Add(task, 0x24, 1);
            Put();
            bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
            if (complete)
            {
                Hierarchy = Hierarchy.Complete8DC530(address);
            }
            Frame = Frame with { Effect = Frame.Effect with { Pools = Hierarchy.Pools } };
            return new(Frame, Random, complete ? 2 : 0);
        }

        private void AdvancePrimary()
        {
            task = NativeBattleTripleScalars.AdvancePrimaryPhase89E560(task, resources);
        }

        private void SpawnMorph89E500()
        {
            if (Word(task, 0x24) != 0)
            {
                return;
            }
            uint child = Append(4, 0x89E670, 124);
            NativeBattleModulePoolTask created = Color(Scale(Add(Anchor(Hierarchy.Read(child)), 0x1E, -640), 4096), 0x40, 0).WriteWord(0x46, 0).WriteDword(0x4C, 0x1627970).WriteWord(0x50, 0);
            Hierarchy = Hierarchy.Write(child, created);
        }

        private void SpawnFan89E3C0(int variant)
        {
            int counter = Word(task, 0x24);
            if (counter < 2 || counter > 4)
            {
                return;
            }
            if (variant is not (0 or 1))
            {
                throw new InvalidDataException("Native89E3C0 requires its actual variant0/1 caller operand.");
            }
            for (int index = 0; index < 4; index++)
            {
                int selected = (4 * counter + index) % 4;
                uint child = Append(3, 0x89E7D0, 112);
                NativeBattleModulePoolTask created = Scale(Hierarchy.Read(child), 4096).WriteDword(0x4C, 0x1626B3C).WriteWord(0x52, 7);
                NativeBattleVector local = new((short)(Rand() % 1024 - 512), 0, -704);
                created = created.WriteWord(0x42, unchecked((ushort)(1024 * selected + (variant == 0 ? 0 : 512))));
                NativeBattleActionMatrix rotation = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), Word(created, 0x42));
                NativeBattleVector moved = NativeBattleGfMatrices.RotateShort56C4F0(rotation, local);
                created = Add(Add(Add(created, 0x1C, moved.X), 0x1E, moved.Y), 0x20, moved.Z);
                Hierarchy = Hierarchy.Write(child, created);
            }
        }

        private void AdvanceMorph()
        {
            task = NativeBattleTripleScalars.AdvanceMorphPhase89E780(task, resources);
            NativeBattleTripleMorph destination = Frame.Memory.TripleMorph ?? throw new NotSupportedException("Native89E6E0 requires its retained current1627970 destination; no immutable reset is implied by the entry.");
            Frame = Frame with { Memory = Frame.Memory with { TripleMorph = destination.Morph89E6E0(resources.MorphFirst, resources.MorphSecond, Word(task, 0x74)) } };
            DrawMesh();
        }

        private void AdvanceSpawner()
        {
            int phase = Phase(task);
            if (phase is >= 0 and <= 3)
            {
                if (Word(task, 0x24) >= phase * 3)
                {
                    SpawnPair89EB20((short)(phase * 512), 0, 0);
                    SpawnPair89EB20((short)(phase * 512 + 2048), 0, (short)(phase == 3 ? 1 : 0));
                    task = Increment(task);
                    if (phase == 3)
                    {
                        task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
                    }
                }
            }
            else if (phase != 4)
            {
                throw new InvalidDataException("Native89EA70 signed phase exceeds its five callbacks.");
            }
        }

        private void SpawnPair89EB20(short direction, short speed, short last)
        {
            uint child = Append(4, 0x89EBD0, 124);
            NativeBattleModulePoolTask created = Scale(Add(Anchor(Hierarchy.Read(child)), 0x1E, -640), 4096);
            created = created.WriteDword(0x54, created.ReadDword(0x1C)).WriteDword(0x58, created.ReadDword(0x20)).WriteWord(0x76, unchecked((ushort)speed)).WriteWord(0x6A, unchecked((ushort)last)).WriteWord(0x68, (ushort)(direction & 4095)).WriteDword(0x4C, 0x1626CF0).WriteWord(0x50, 4096);
            Hierarchy = Hierarchy.Write(child, created);
            uint sprite = Append(3, 0x89F180, 112);
            NativeBattleModulePoolTask follower = Anchor(Hierarchy.Read(sprite));
            follower = follower.WriteDword(0x58, follower.ReadDword(0x1C)).WriteDword(0x5C, follower.ReadDword(0x20)).WriteWord(0x6A, (ushort)(direction & 4095));
            Hierarchy = Hierarchy.Write(sprite, follower);
        }

        private void AdvanceFirstMesh()
        {
            task = NativeBattleEnhancementMeshPhases.First(task);
            task = task.WriteWord(0x46, (ushort)((task.ReadWord(0x46) + 320) & 4095)).WriteDword(0x1C, task.ReadDword(0x54)).WriteDword(0x20, task.ReadDword(0x58));
            task = Add(task, 0x1C, NativeBattleCameraMath.Sine(Word(task, 0x68)) / 4);
            task = Add(task, 0x20, NativeBattleCameraMath.Cosine(Word(task, 0x68)) / 4);
            if ((task.ReadByte(0x26) & 2) != 0)
            {
                if (task.ReadWord(0x24) == 3)
                {
                    SpawnSecondMesh89ED20();
                }
                SpawnMovingSprite89ECA0();
            }
            DrawMesh();
        }

        private void SpawnSecondMesh89ED20()
        {
            uint child = Append(4, 0x89EDB0, 124);
            NativeBattleModulePoolTask created = Add(Anchor(Hierarchy.Read(child)), 0x1E, -640);
            created = created.WriteWord(0x6E, 4096).WriteWord(0x70, 0).WriteWord(0x6A, task.ReadWord(0x6A)).WriteWord(0x76, task.ReadWord(0x76));
            created = created.WriteDword(0x54, created.ReadDword(0x1C)).WriteDword(0x58, created.ReadDword(0x20)).WriteWord(0x68, task.ReadWord(0x68)).WriteWord(0x46, task.ReadWord(0x68)).WriteDword(0x4C, 0x1626F00).WriteWord(0x50, 4096);
            Hierarchy = Hierarchy.Write(child, Scale(created, 16));
        }

        private void SpawnMovingSprite89ECA0()
        {
            if (Word(task, 0x24) is < 0 or > 6)
            {
                return;
            }
            uint child = Append(3, 0x89F410, 112);
            NativeBattleModulePoolTask created = Hierarchy.Read(child);
            created = created.WriteDword(0x58, created.ReadDword(0x1C)).WriteDword(0x5C, created.ReadDword(0x20)).WriteDword(0x4C, 0x16269DC).WriteWord(0x68, (ushort)((Rand() & 511) + 512)).WriteWord(0x42, (ushort)(Rand() & 4095)).WriteWord(0x52, 12);
            Hierarchy = Hierarchy.Write(child, Scale(created, 4096));
        }

        private void AdvanceSecondMesh()
        {
            AdvanceSecondMeshPhase();
            int flash = Word(task, 0x6C);
            if (flash is 1 or 3)
            {
                task = Color(task.WriteWord(0x70, 1).WriteWord(0x50, (ushort)(flash == 1 ? 1024 : 2048)).WriteWord(0x6C, (ushort)(flash + 1)), 0x40, 255);
            }
            else if (flash is 2 or 4)
            {
                task = Add(task, 0x50, flash == 2 ? -256 : -512);
                if (Word(task, 0x50) <= 0)
                {
                    task = Color(task.WriteWord(0x50, 0).WriteWord(0x70, 0).WriteWord(0x6C, 0), 0x40, 0);
                }
            }
            task = task.WriteWord(0x68, (ushort)((task.ReadWord(0x68) + task.ReadWord(0x72)) & 4095)).WriteWord(0x46, (ushort)((task.ReadWord(0x46) + 320) & 4095));
            int direction = Word(task, 0x68);
            int radius = Word(task, 0x6E);
            task = task.WriteDword(0x1C, task.ReadDword(0x54)).WriteDword(0x20, task.ReadDword(0x58));
            task = Add(task, 0x1C, NativeBattleCameraMath.Sine(direction) * radius / 16384);
            task = Add(task, 0x20, NativeBattleCameraMath.Cosine(direction) * radius / 16384);
            if (task.ReadWord(0x70) == 1)
            {
                task = task.WriteDword(0x4C, 0x1627160);
                DrawMesh();
                task = task.WriteDword(0x4C, 0x1626F00);
            }
            DrawMesh();
        }

        private void AdvanceSecondMeshPhase()
        {
            int phase = Phase(task);
            if (phase is >= 0 and <= 4)
            {
                NativeBattleEnhancementMeshPhase shared = NativeBattleEnhancementMeshPhases.SecondPrefix(task, true, Hierarchy.Read);
                task = shared.Task;
                if (shared.PublishTargetFlash)
                {
                    uint first = task.ReadDword(0x14);
                    Hierarchy = Hierarchy.Write(first, Hierarchy.Read(first).WriteWord(0x58, 1));
                }
                return;
            }
            switch (phase)
            {
                case 5:
                    if (task.ReadWord(0x6C) == 0)
                    {
                        if (Word(task, 0x50) <= 2560)
                        {
                            uint sprite = Append(3, 0x89F5E0, 112);
                            Hierarchy = Hierarchy.Write(sprite, Hierarchy.Read(sprite).WriteDword(0x4C, 0x1626C18));
                        }
                        task = Add(task, 0x50, 512);
                        if (Word(task, 0x50) >= 4096)
                        {
                            task = Increment(task.WriteWord(0x50, 4096).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5)));
                        }
                    }
                    break;
                case 6:
                    break;
                default:
                    throw new InvalidDataException("Native89EDB0 signed phase exceeds its seven callbacks.");
            }
        }

        private void AdvanceMovingSprite()
        {
            switch (Phase(task))
            {
                case 0:
                    task = AddColor(task, 0x48, 32);
                    if (task.ReadByte(0x48) >= 128)
                    {
                        task = Increment(Color(task, 0x48, 128));
                    }
                    break;
                case 1:
                    break;
                default:
                    throw new InvalidDataException("Native89F410 signed phase exceeds its two callbacks.");
            }
            task = Add(task, 0x68, -96);
            if (Word(task, 0x68) <= 0)
            {
                task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
            }
            task = task.WriteWord(0x42, (ushort)((task.ReadWord(0x42) + 96) & 4095));
            task = task.WriteDword(0x1C, task.ReadDword(0x58)).WriteDword(0x20, task.ReadDword(0x5C));
            int radius = Word(task, 0x68);
            int angle = Word(task, 0x42);
            task = Add(task, 0x1C, NativeBattleCameraMath.Sine(angle) * radius / 4096);
            task = Add(task, 0x20, NativeBattleCameraMath.Cosine(angle) * radius / 4096);
            DrawBillboard();
            task = Add(task, 0x50, 1);
            if (Word(task, 0x50) > Word(task, 0x52))
            {
                task = task.WriteWord(0x50, 0);
            }
        }

        private void AdvanceBurstSprite()
        {
            switch (Phase(task))
            {
                case 0:
                    NativeBattleModulePoolTask parent = Hierarchy.Read(task.ReadDword(0x18));
                    task = task.WriteDword(0x1C, parent.ReadDword(0x1C)).WriteDword(0x20, parent.ReadDword(0x20));
                    short yaw = (short)(Rand() & 4095);
                    short pitch = (short)(Rand() & 4095);
                    NativeBattleActionMatrix rotation = NativeBattleCuragaMatrices.RotateX8DD7E0(NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), yaw), pitch);
                    NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(rotation, new(0, 0, (short)(Rand() % 128)));
                    task = Add(Add(Add(task, 0x1C, offset.X), 0x1E, offset.Y * 2), 0x20, offset.Z);
                    task = Color(task.WriteWord(0x52, 4), 0x48, 128);
                    task = Increment(task.WriteWord(0x62, unchecked((ushort)(-8 - (Rand() & 31)))));
                    break;
                case 1:
                    task = Add(Add(task, 0x1E, Word(task, 0x62)), 0x50, 1);
                    if (Word(task, 0x50) > Word(task, 0x52))
                    {
                        task = Increment(task.WriteWord(0x50, task.ReadWord(0x52)).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5)));
                    }
                    break;
                case 2:
                    break;
                default:
                    throw new InvalidDataException("Native89F5E0 signed phase exceeds its three callbacks.");
            }
            DrawBillboard();
        }

        private void AdvanceOverlay()
        {
            task = NativeBattleEnhancementMeshPhases.Overlay(task);
            Execute(new NativeBattleTriplePackets(NativeBattleEffectOverlayPackets.Emit89F950(task, inputs.Projection, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(21, task)), 1));
        }

        private void DrawMesh()
        {
            NativeBattleGfGeometryBatch? batch = NativeBattleEnhancementDraws.EmitMesh(task, meshAddress => meshAddress == 0x1627970 ? (Frame.Memory.TripleMorph ?? throw new NotSupportedException("Native Triple draw requires current1627970 bytes.")).Bytes : resources.Meshes.Single(mesh => mesh.Address == meshAddress).Bytes, inputs.Projection, inputs.Colors, NativeBattlePilotVisualLifetimes.Identity(21, task));
            if (batch is not null)
            {
                Execute(new NativeBattleTriplePackets(batch));
            }
        }

        private void DrawOrientedSprite(bool flat)
        {
            NativeBattleModuleDraw? draw = NativeBattleEnhancementDraws.OrientedSprite(task, sprite => resources.Sprites.Single(resource => resource.Address == sprite), flat ? 0x1626CC8u : null, NativeBattlePilotVisualLifetimes.Identity(21, task));
            if (draw is not null)
            {
                Execute(new NativeBattleTripleSprite(draw));
            }
        }

        private void DrawBillboard()
        {
            NativeBattleModuleDraw? draw = NativeBattleEnhancementDraws.Billboard(task, sprite => resources.Sprites.Single(resource => resource.Address == sprite), NativeBattlePilotVisualLifetimes.Identity(21, task));
            if (draw is not null)
            {
                Execute(new NativeBattleTripleSprite(draw));
            }
        }

        private static NativeBattleModulePoolTask Scale(NativeBattleModulePoolTask value, int scale) => value.WriteDword(0x30, unchecked((uint)scale)).WriteDword(0x34, unchecked((uint)scale)).WriteDword(0x38, unchecked((uint)scale));
        private static NativeBattleModulePoolTask Color(NativeBattleModulePoolTask value, int offset, byte color) => value.WriteByte(offset, color).WriteByte(offset + 1, color).WriteByte(offset + 2, color);
        private static NativeBattleModulePoolTask AddColor(NativeBattleModulePoolTask value, int offset, int color) => value.WriteByte(offset, unchecked((byte)(value.ReadByte(offset) + color))).WriteByte(offset + 1, unchecked((byte)(value.ReadByte(offset + 1) + color))).WriteByte(offset + 2, unchecked((byte)(value.ReadByte(offset + 2) + color)));
    }
}
