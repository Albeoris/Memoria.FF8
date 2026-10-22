using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleDeathController
{
    private sealed partial class Execution
    {
        private void Run(uint address)
        {
            switch (profile.ReferenceCallback(Read(address).Callback))
            {
                case 0x8B6C90:
                    Emitter(address);
                    break;
                case 0x8B6E30:
                    Director(address);
                    break;
                case 0x8B6F70:
                    Creature(address);
                    break;
                case 0x8B78C0:
                    Actors(address);
                    break;
                case 0x8BD580:
                case 0x8BDF40:
                    StageFade(address);
                    break;
                case 0x8BD840:
                    Burst(address);
                    break;
                case 0x8BDBD0:
                    Spark(address);
                    break;
                default:
                    throw new NotSupportedException("Native Death reached an unproved private callback.");
            }
        }

        private void Emitter(uint address)
        {
            NativeBattleModulePoolTask task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(Read(address), Enumerable.Range(0, 7).Select(inputs.ReadModel).ToArray(), inputs.ReadPoseSource, producer => Emit(new NativeBattleDeathObserveAnchor(producer)));
            int actor = task.ReadByte(0x2D);
            NativeBattleGraphicsModel model = inputs.ReadModel(actor);
            NativeBattlePoseSource source = inputs.ReadPoseSource(actor);
            NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Native Death target bounds require its current pose.");
            NativeBattleDecodedPose pose = sequence.DecodedPose ?? source.Sample(sequence.Pose ?? throw new NotSupportedException("Native Death requires its target pose selector."), sequence.PoseFrame114 ?? throw new NotSupportedException("Native Death requires its target pose frame."), sequence.PoseFlags109);
            task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, source, pose);
            Store(address, task);
            switch (Stage(task))
            {
                case 0:
                    Place(actor);
                    Append(2, 0x8B6E30, 0x48, address);
                    Store(address, Next(Read(address)));
                    break;
                case 1:
                    if (Word(0x2742440) == 1)
                    {
                        NativeBattleActionHeaderSlot header = inputs.Packets.Headers[current.Effect.Parameters!.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A))];
                        int first = header.FirstContactIndex ?? throw new NotSupportedException("Native Death requires its original contact pointer.");
                        Emit(new NativeBattleDeathContact(checked(first + unchecked((sbyte)task.ReadByte(0x2B)))));
                        Store(address, Next(Read(address)));
                    }
                    break;
                case 2:
                    if (task.ReadByte(0x28) == 0 || Word(0x2742444) != 0)
                    {
                        Store(address, Next(task));
                    }
                    break;
                case 3:
                    Store(address, Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1))));
                    uint root = task.ReadDword(0x10);
                    Store(root, Read(root).WriteByte(0x63, 0));
                    break;
                case 4:
                    break;
                default:
                    throw new NotSupportedException("Native Death emitter reached an unproved state.");
            }
        }

        private void Place(int actor)
        {
            SetState(State with { Module = State.Module.Write(profile.ModuleOffset(0x27423F8), new byte[0x54]) });
            NativeBattleGraphicsModel model = inputs.ReadModel(actor);
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Death requires target position and size WORDs.");
            NativeBattleVector bone = NativeBattlePoseMatrices.ReadBone(model, inputs.ReadPoseSource(actor), 0xF1, 0);
            Word(0x2742400, bone.X);
            Word(0x2742402, 0);
            Word(0x2742404, bone.Z);
            int offset = unchecked(unchecked(transform.Scale38 * 125) << 4) >> 12;
            short z = unchecked((short)(transform.Position28.Z + (actor <= 2 ? -512 - offset : 512 + offset)));
            Word(0x27423F8, transform.Position28.X);
            Word(0x27423FA, 0);
            Word(0x27423FC, z);
            int yaw = NativeWorldScriptedPathMath.NativeAngle(unchecked((short)(bone.X - transform.Position28.X)), unchecked((short)(bone.Z - z)));
            Word(0x2742442, (yaw - 2048) & 4095);
        }

        private void Director(uint address)
        {
            Word(0x274243E, Word(0x274243E) + 1);
            ushort requested = Word(0x274243A);
            if (Word(0x2742438) != requested)
            {
                Word(0x2742438, requested);
                Word(0x274243E, 0);
            }
            Word(0x274243A, Word(0x274243C));
            NativeBattleModulePoolTask task = Read(address);
            switch (Stage(task))
            {
                case 0:
                    current = InitializeDirector(current with { Effect = current.Effect with { Pools = hierarchy.Pools } }, address, resources, inputs.Execute);
                    hierarchy = new(current.Effect.Pools);
                    break;
                case 1:
                    task = task.WriteWord(0x44, unchecked((ushort)(task.ReadWord(0x44) - 1)));
                    Store(address, task);
                    if (S(task, 0x44) <= 0)
                    {
                        uint child = Append(3, 0x8B6F70, 0x140, address);
                        NativeBattleEffectOwnedModelStep initialized = NativeBattleEffectOwnedModel.Initialize8DD0F0(Read(child), child, resources.Model, 3, inputs.DefaultColor);
                        Store(child, initialized.Task);
                        SetState(State with { Models = State.Models.Where(model => model.TaskAddress != child).Append(initialized.State).ToImmutableArray() });
                        Store(address, Next(Read(address)));
                    }
                    break;
                case 2:
                    Store(address, Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1))));
                    break;
                case 3:
                    break;
                default:
                    throw new NotSupportedException("Native Death director reached an unproved state.");
            }
        }

        private void StageFade(uint address)
        {
            NativeBattleModulePoolTask task = Read(address);
            bool outgoing = profile.ReferenceCallback(task.Callback) == 0x8BDF40;
            if (Stage(task) == 0)
            {
                if (S(task, 0x24) >= (outgoing ? 0 : 28))
                {
                    task = Next(task.WriteWord(0x1C, outgoing ? (ushort)0x600 : (ushort)0));
                    if (!outgoing)
                    {
                        Emit(new NativeBattleDeathStage(0, true));
                    }
                }
            }
            else if (Stage(task) == 1)
            {
                short fade = unchecked((short)(S(task, 0x1C) + (outgoing ? -256 : 512)));
                if (outgoing ? fade <= 0 : fade >= 1536)
                {
                    fade = outgoing ? (short)0 : (short)1536;
                    task = Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                }
                task = task.WriteWord(0x1C, unchecked((ushort)fade));
                Emit(new NativeBattleDeathStage(fade, false));
            }
            Store(address, task);
        }
    }
}

