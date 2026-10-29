using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleDoubleController
{
    public static NativeBattleDoubleParticleStep AdvancePrivate(NativeBattleDoubleFrame frame, uint address, NativeBattleDoubleResources resources, NativeBattleDoubleInputs inputs, NativeCrtRandomState random)
    {
        DoubleExecution execution = new(frame, address, resources, inputs, random);
        execution.Advance();
        return execution.Finish();
    }

    private sealed class DoubleExecution(NativeBattleDoubleFrame frame, uint address, NativeBattleDoubleResources resources, NativeBattleDoubleInputs inputs, NativeCrtRandomState random)
    {
        public NativeBattleDoubleFrame Frame { get; private set; } = frame;
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

        private void Execute(NativeBattleDoubleOperation operation)
        {
            Put();
            Frame = inputs.Execute(Frame, operation);
            Hierarchy = new(Frame.Effect.Pools);
            task = Hierarchy.Read(address);
        }

        private uint Append(int pool, uint callback, int size, uint? parent = null)
        {
            Put();
            NativeBattleEffectParameters parameters = Frame.Effect.Parameters ?? throw new InvalidDataException("Native Double producer lost its parameters.");
            NativeBattleEffectHierarchyAppend child = Hierarchy.Append8DC540(pool, callback, size, parent ?? address, (pointer, group, target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, group, target));
            Hierarchy = child.Hierarchy;
            task = Hierarchy.Read(address);
            if (child.Address == 0)
            {
                throw new NotSupportedException("Native Double producer dereferenced an exhausted8DC540 allocation.");
            }
            return child.Address;
        }

        private NativeBattleModulePoolTask Anchor(NativeBattleModulePoolTask child, int source = 0x30)
        {
            NativeBattleModulePoolTask first = Hierarchy.Read(child.ReadDword(0x14));
            return child.WriteDword(0x1C, first.ReadDword(source)).WriteDword(0x20, first.ReadDword(source + 4));
        }

        public void SpawnPrimary8D5360()
        {
            uint child = Append(4, 0x8D53C0, 108);
            NativeBattleModulePoolTask created = Add(Anchor(Hierarchy.Read(child)), 0x1E, -640);
            created = Scale(created, 4096).WriteWord(0x46, 409).WriteDword(0x4C, 0x163F224).WriteWord(0x50, 2048);
            created = Color(created, 0x40, 255);
            Hierarchy = Hierarchy.Write(child, created);
            Put();
        }

        public void SpawnOverlay8D6710()
        {
            NativeBattleEffectParameters parameters = Frame.Effect.Parameters ?? throw new InvalidDataException("Native8D6710 requires50AFC0 parameters.");
            int header = checked(parameters.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
            int contact = inputs.Packets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Native8D6710 requires its original first-contact pointer.");
            if ((inputs.Packets.ReadContact(contact).Primary.HitFlags3 & 4) != 0)
            {
                return;
            }
            uint child = Append(5, 0x8D6790, 80);
            NativeBattleModulePoolTask created = Anchor(Hierarchy.Read(child), 0x40);
            created = created.WriteDword(0x38, created.ReadDword(0x1C)).WriteDword(0x3C, created.ReadDword(0x20)).WriteWord(0x40, 576).WriteWord(0x42, 288).WriteWord(0x44, 320).WriteWord(0x46, 244).WriteWord(0x48, 0).WriteWord(0x4A, 32).WriteWord(0x4C, 40).WriteWord(0x4E, 16);
            Hierarchy = Hierarchy.Write(child, created);
            Put();
        }

        public void Advance()
        {
            switch (task.Callback)
            {
                case 0x8D53C0:
                    AdvancePrimary();
                    DrawMesh();
                    if ((task.ReadByte(0x26) & 2) != 0)
                    {
                        SpawnFan8D5570(0);
                        SpawnMorph8D5500();
                    }
                    break;
                case 0x8D5750:
                    AdvanceMorph();
                    break;
                case 0x8D58A0:
                case 0x8D6200:
                    NativeBattleDoubleSpriteMotionStep motion = NativeBattleDoubleSpriteMotion.Advance(task, Random, Hierarchy.Read);
                    task = motion.Task;
                    Random = motion.Random;
                    DrawOrientedSprite(task.Callback == 0x8D6200);
                    break;
                case 0x8D5B40:
                    AdvanceSpawner();
                    break;
                case 0x8D5C80:
                    AdvanceFirstMesh();
                    break;
                case 0x8D5E60:
                    AdvanceSecondMesh();
                    break;
                case 0x8D6490:
                    AdvanceMovingSprite();
                    break;
                case 0x8D6790:
                    AdvanceOverlay();
                    break;
                default:
                    throw new NotSupportedException($"Native Double private callback{task.Callback:X} has no original implementation.");
            }
        }

        public NativeBattleDoubleParticleStep Finish()
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
            switch (Phase(task))
            {
                case 0:
                    task = Add(task, 0x50, -512).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 2));
                    if (Word(task, 0x50) <= 0)
                    {
                        task = Increment(Color(task.WriteWord(0x50, 0), 0x40, 0));
                    }
                    break;
                case 1:
                    task = Add(task, 0x50, 768);
                    if (Word(task, 0x50) >= 4096)
                    {
                        task = Increment(task.WriteWord(0x50, 4096).WriteWord(0x26, (ushort)((task.ReadWord(0x26) & ~2) | 5)));
                    }
                    break;
                case 2:
                    break;
                default:
                    throw new InvalidDataException("Native8D53C0 signed phase exceeds its three callbacks.");
            }
        }

        private void SpawnMorph8D5500()
        {
            if (Word(task, 0x24) is not (0 or 2))
            {
                return;
            }
            uint child = Append(4, 0x8D5750, 108);
            NativeBattleModulePoolTask created = Color(Scale(Add(Anchor(Hierarchy.Read(child)), 0x1E, -640), 4096), 0x40, 0).WriteWord(0x46, 409).WriteDword(0x4C, 0x163FFD0).WriteWord(0x50, 0);
            Hierarchy = Hierarchy.Write(child, created);
        }

        private void SpawnFan8D5570(int variant)
        {
            int counter = Word(task, 0x24);
            if (counter < 2 || counter > 4)
            {
                return;
            }
            for (int index = 0; index < 5; index++)
            {
                int selected = (5 * counter + index) % 5;
                uint child = Append(3, 0x8D58A0, 104);
                NativeBattleModulePoolTask created = Scale(Hierarchy.Read(child), 4096).WriteDword(0x4C, 0x163EA90).WriteWord(0x52, 7);
                NativeBattleVector local;
                if (variant == 0)
                {
                    local = new((short)(Rand() % 768 - 384), 0, -704);
                    created = created.WriteWord(0x42, unchecked((ushort)(819 * selected)));
                }
                else if (variant == 1)
                {
                    local = new((short)(Rand() % 320 + 352), 0, 0);
                    created = created.WriteWord(0x42, unchecked((ushort)(819 * selected + 614)));
                }
                else
                {
                    throw new InvalidDataException("Native8D5570 requires its actual variant0/1 caller operand.");
                }
                NativeBattleActionMatrix rotation = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), Word(created, 0x42));
                NativeBattleVector moved = NativeBattleGfMatrices.RotateShort56C4F0(rotation, local);
                created = Add(Add(Add(created, 0x1C, moved.X), 0x1E, moved.Y), 0x20, moved.Z);
                Hierarchy = Hierarchy.Write(child, created);
            }
        }

        private void AdvanceMorph()
        {
            if (Phase(task) == 0)
            {
                task = Add(Add(task, 0x68, 512), 0x50, 512);
                if (Word(task, 0x50) >= 4096)
                {
                    task = Increment(task.WriteWord(0x50, 4096).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                }
            }
            else if (Phase(task) != 1)
            {
                throw new InvalidDataException("Native8D5750 signed phase exceeds its two callbacks.");
            }
            NativeBattleDoubleMorph destination = Frame.Memory.DoubleMorph ?? throw new NotSupportedException("Native8D57C0 requires its retained current163FFD0 destination; no immutable reset is implied by the entry.");
            Frame = Frame with { Memory = Frame.Memory with { DoubleMorph = destination.Morph8D57C0(resources.MorphFirst, resources.MorphSecond, Word(task, 0x68)) } };
            DrawMesh();
        }

        private void AdvanceSpawner()
        {
            int phase = Phase(task);
            if (phase is >= 0 and <= 4)
            {
                SpawnPair8D5BD0((short)(phase * 819), 0, (short)(phase == 4 ? 1 : 0));
                task = Increment(task);
                if (phase == 4)
                {
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
                }
            }
            else if (phase != 5)
            {
                throw new InvalidDataException("Native8D5B40 signed phase exceeds its six callbacks.");
            }
        }

        private void SpawnPair8D5BD0(short direction, short speed, short last)
        {
            uint child = Append(4, 0x8D5C80, 108);
            NativeBattleModulePoolTask created = Scale(Add(Anchor(Hierarchy.Read(child)), 0x1E, -640), 4096);
            created = created.WriteDword(0x54, created.ReadDword(0x1C)).WriteDword(0x58, created.ReadDword(0x20)).WriteWord(0x6A, unchecked((ushort)speed)).WriteWord(0x5E, unchecked((ushort)last)).WriteWord(0x5C, (ushort)(direction & 4095)).WriteDword(0x4C, 0x163F014).WriteWord(0x50, 4096);
            Hierarchy = Hierarchy.Write(child, created);
            uint sprite = Append(3, 0x8D6200, 104);
            NativeBattleModulePoolTask follower = Anchor(Hierarchy.Read(sprite));
            follower = follower.WriteDword(0x58, follower.ReadDword(0x1C)).WriteDword(0x5C, follower.ReadDword(0x20)).WriteWord(0x62, (ushort)(direction & 4095));
            Hierarchy = Hierarchy.Write(sprite, follower);
        }

        private void AdvanceFirstMesh()
        {
            task = NativeBattleEnhancementMeshPhases.First(task);
            task = task.WriteWord(0x46, (ushort)((task.ReadWord(0x46) + 320) & 4095)).WriteDword(0x1C, task.ReadDword(0x54)).WriteDword(0x20, task.ReadDword(0x58));
            task = Add(task, 0x1C, NativeBattleCameraMath.Sine(Word(task, 0x5C)) / 4);
            task = Add(task, 0x20, NativeBattleCameraMath.Cosine(Word(task, 0x5C)) / 4);
            if ((task.ReadByte(0x26) & 2) != 0)
            {
                if (task.ReadWord(0x24) == 3)
                {
                    SpawnSecondMesh8D5D50();
                }
                SpawnMovingSprite8D5DE0();
            }
            DrawMesh();
        }

        private void SpawnSecondMesh8D5D50()
        {
            uint child = Append(4, 0x8D5E60, 108);
            NativeBattleModulePoolTask created = Add(Anchor(Hierarchy.Read(child)), 0x1E, -640);
            created = created.WriteWord(0x62, 4096).WriteWord(0x64, 0).WriteWord(0x5E, task.ReadWord(0x5E)).WriteWord(0x6A, task.ReadWord(0x6A));
            created = created.WriteDword(0x54, created.ReadDword(0x1C)).WriteDword(0x58, created.ReadDword(0x20)).WriteWord(0x5C, task.ReadWord(0x5C)).WriteWord(0x46, task.ReadWord(0x5C)).WriteDword(0x4C, 0x163EB94).WriteWord(0x50, 4096);
            Hierarchy = Hierarchy.Write(child, Scale(created, 16));
        }

        private void SpawnMovingSprite8D5DE0()
        {
            if (Word(task, 0x24) is < 0 or > 6)
            {
                return;
            }
            uint child = Append(3, 0x8D6490, 104);
            NativeBattleModulePoolTask created = Hierarchy.Read(child);
            created = created.WriteDword(0x58, created.ReadDword(0x1C)).WriteDword(0x5C, created.ReadDword(0x20)).WriteDword(0x4C, 0x163E930).WriteWord(0x60, (ushort)((Rand() & 511) + 512)).WriteWord(0x42, (ushort)(Rand() & 4095)).WriteWord(0x52, 12);
            Hierarchy = Hierarchy.Write(child, Scale(created, 4096));
        }

        private void AdvanceSecondMesh()
        {
            AdvanceSecondMeshPhase();
            int flash = Word(task, 0x60);
            if (flash is 1 or 3)
            {
                task = Color(task.WriteWord(0x64, 1).WriteWord(0x50, (ushort)(flash == 1 ? 1024 : 2048)).WriteWord(0x60, (ushort)(flash + 1)), 0x40, 255);
            }
            else if (flash is 2 or 4)
            {
                task = Add(task, 0x50, flash == 2 ? -256 : -512);
                if (Word(task, 0x50) <= 0)
                {
                    task = Color(task.WriteWord(0x50, 0).WriteWord(0x64, 0).WriteWord(0x60, 0), 0x40, 0);
                }
            }
            task = task.WriteWord(0x5C, (ushort)((task.ReadWord(0x5C) + task.ReadWord(0x66)) & 4095)).WriteWord(0x46, (ushort)((task.ReadWord(0x46) + 320) & 4095));
            int direction = Word(task, 0x5C);
            int radius = Word(task, 0x62);
            task = task.WriteDword(0x1C, task.ReadDword(0x54)).WriteDword(0x20, task.ReadDword(0x58));
            task = Add(task, 0x1C, NativeBattleCameraMath.Sine(direction) * radius / 16384);
            task = Add(task, 0x20, NativeBattleCameraMath.Cosine(direction) * radius / 16384);
            if (task.ReadWord(0x64) == 1)
            {
                task = task.WriteDword(0x4C, 0x163EDF4);
                DrawMesh();
                task = task.WriteDword(0x4C, 0x163EB94);
            }
            DrawMesh();
        }

        private void AdvanceSecondMeshPhase()
        {
            int phase = Phase(task);
            if (phase is >= 0 and <= 4)
            {
                NativeBattleEnhancementMeshPhase shared = NativeBattleEnhancementMeshPhases.SecondPrefix(task, false, Hierarchy.Read);
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
                    if (task.ReadWord(0x60) == 0)
                    {
                        task = Add(task, 0x50, 512);
                        if (Word(task, 0x50) >= 4096)
                        {
                            task = Increment(task.WriteWord(0x50, 4096).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                        }
                    }
                    break;
                case 6:
                    break;
                default:
                    throw new InvalidDataException("Native8D5E60 signed phase exceeds its seven callbacks.");
            }
        }

        private void AdvanceMovingSprite()
        {
            task = task.WriteWord(0x42, (ushort)((task.ReadWord(0x42) + 96) & 4095));
            if (task.ReadByte(0x48) < 128)
            {
                task = AddColor(task, 0x48, 32);
            }
            task = Add(task, 0x60, -96);
            if (Word(task, 0x60) <= 0)
            {
                task = Increment(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
            }
            task = task.WriteDword(0x1C, task.ReadDword(0x58)).WriteDword(0x20, task.ReadDword(0x5C));
            int radius = Word(task, 0x60);
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

        private void AdvanceOverlay()
        {
            task = NativeBattleEnhancementMeshPhases.Overlay(task);
            Execute(new NativeBattleDoublePackets(NativeBattleEffectOverlayPackets.Emit8D6790(task, inputs.Projection, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(4, task)), 1));
        }

        private void DrawMesh()
        {
            NativeBattleGfGeometryBatch? batch = NativeBattleEnhancementDraws.EmitMesh(task, meshAddress => meshAddress == 0x163FFD0 ? (Frame.Memory.DoubleMorph ?? throw new NotSupportedException("Native Double draw requires current163FFD0 bytes.")).Bytes : resources.Meshes.Single(mesh => mesh.Address == meshAddress).Bytes, inputs.Projection, inputs.Colors, NativeBattlePilotVisualLifetimes.Identity(4, task));
            if (batch is not null)
            {
                Execute(new NativeBattleDoublePackets(batch));
            }
        }

        private void DrawOrientedSprite(bool flat)
        {
            NativeBattleModuleDraw? draw = NativeBattleEnhancementDraws.OrientedSprite(task, sprite => resources.Sprites.Single(resource => resource.Address == sprite), flat ? 0x163EB6Cu : null, NativeBattlePilotVisualLifetimes.Identity(4, task));
            if (draw is not null)
            {
                Execute(new NativeBattleDoubleSprite(draw));
            }
        }

        private void DrawBillboard()
        {
            NativeBattleModuleDraw? draw = NativeBattleEnhancementDraws.Billboard(task, sprite => resources.Sprites.Single(resource => resource.Address == sprite), NativeBattlePilotVisualLifetimes.Identity(4, task));
            if (draw is not null)
            {
                Execute(new NativeBattleDoubleSprite(draw));
            }
        }

        private static NativeBattleModulePoolTask Scale(NativeBattleModulePoolTask value, int scale) => value.WriteDword(0x30, unchecked((uint)scale)).WriteDword(0x34, unchecked((uint)scale)).WriteDword(0x38, unchecked((uint)scale));
        private static NativeBattleModulePoolTask Color(NativeBattleModulePoolTask value, int offset, byte color) => value.WriteByte(offset, color).WriteByte(offset + 1, color).WriteByte(offset + 2, color);
        private static NativeBattleModulePoolTask AddColor(NativeBattleModulePoolTask value, int offset, int color) => value.WriteByte(offset, unchecked((byte)(value.ReadByte(offset) + color))).WriteByte(offset + 1, unchecked((byte)(value.ReadByte(offset + 1) + color))).WriteByte(offset + 2, unchecked((byte)(value.ReadByte(offset + 2) + color)));
    }
}
