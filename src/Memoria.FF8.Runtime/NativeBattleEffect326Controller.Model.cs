using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect326Controller
{
    public static NativeBattleEffect326Step AdvanceModel62CEE0(NativeBattleEffect326Frame frame, uint address, NativeBattleEffect326Resources resources, NativeBattleEffect326Inputs inputs)
    {
        const uint model = 0x24F1268;
        NativeBattleEffect326ModelDirectorStep step = NativeBattleEffect326ModelDirector.Advance62CEE0(Read(frame, address), new(inputs.ReadFlags(frame), Execute));
        return new(Write(frame, address, Read(frame, address).WriteWord(12, step.Counter)), step.Result);

        void Execute(NativeBattleEffect326ModelOperation operation)
        {
            switch (operation.Service)
            {
                case NativeBattleEffect326ModelService.Initialize:
                    NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
                    frame = StoreModelMatrix(frame, model + 0x40, identity with { Rotation = identity.Rotation.Select(value => unchecked((short)(value * 2))).ToImmutableArray(), X = 0, Y = 0, Z = 1800 });
                    frame = InitializeModel62C790(frame, resources, model, CommonAddress, 15);
                    frame = frame with { State = frame.State.WriteWord(model, (ushort)(frame.State.ReadWord(model) | 1)).WriteDword(model + 0x7C, 7) };
                    (frame, _) = Spawn62AFB0(frame, 0x630160);
                    break;
                case NativeBattleEffect326ModelService.Scale:
                    frame = frame with { State = frame.State.WriteWord(model + 0x40, checked((ushort)operation.First)).WriteWord(model + 0x48, checked((ushort)operation.First)).WriteWord(model + 0x50, checked((ushort)operation.First)) };
                    break;
                case NativeBattleEffect326ModelService.Select:
                    frame = SelectModel62C800(frame, resources, model, checked((short)operation.First));
                    break;
                case NativeBattleEffect326ModelService.Advance:
                    frame = AdvanceModel62CBC0(frame, resources, model);
                    break;
                case NativeBattleEffect326ModelService.Draw:
                    frame = DrawOwnedModel62C820(frame, resources, inputs, model, ReadPacked(frame, NativeBattleEffect326Profile.ViewMatrix).ReadMatrix());
                    break;
                case NativeBattleEffect326ModelService.InitializeChild:
                    frame = operation.First switch
                    {
                        0x62E810 => InitializeRings62E810(frame),
                        0x62F1C0 => InitializeMorph62F1C0(frame),
                        0x62F710 => InitializeBoneParticles62F710(frame),
                        0x62FC20 => InitializePairedBoneParticles62FC20(frame),
                        0x630400 => InitializeRibbon630400(frame),
                        0x630810 => InitializeLateBurst630810(frame, inputs),
                        0x630A70 => InitializeHistoryBurst630A70(frame),
                        0x630CB0 => InitializeLatePair630CB0(frame, resources),
                        0x631150 => InitializeTargetBurst631150(frame, resources),
                        _ => throw new NotSupportedException("Effect326 model selected an unsupported child initializer.")
                    };
                    break;
                case NativeBattleEffect326ModelService.Append:
                    (frame, _) = Spawn62AFB0(frame, unchecked((uint)operation.First));
                    break;
                case NativeBattleEffect326ModelService.CaptureBone:
                    NativeBattleEffect326PointStep captured = ReadOwnedBone502170(frame, resources, model, operation.First, operation.Second, TaskView(Read(frame, address), 0x10, 8));
                    frame = Write(captured.Frame, address, CopyTask(Read(captured.Frame, address), 0x10, captured.Point8, 8));
                    break;
                case NativeBattleEffect326ModelService.TranslateXZ:
                    frame = frame with { State = frame.State.WriteDword(model + 0x54, unchecked((uint)operation.First)).WriteDword(model + 0x5C, unchecked((uint)operation.Second)) };
                    break;
                case NativeBattleEffect326ModelService.AddZ:
                    frame = frame with { State = frame.State.WriteDword(model + 0x5C, unchecked(frame.State.ReadDword(model + 0x5C) + (uint)operation.First)) };
                    break;
                case NativeBattleEffect326ModelService.SetMask:
                    frame = frame with { State = frame.State.WriteDword(model + 0x7C, unchecked((uint)operation.First)) };
                    break;
                case NativeBattleEffect326ModelService.Flash:
                    frame = inputs.Execute(frame, new NativeBattleEffect326Flash5712C0(operation.First, operation.Second, operation.Third, operation.Fourth));
                    break;
                case NativeBattleEffect326ModelService.SplitHeld:
                    frame = DrawSplitModel(frame, resources, inputs, address, operation.First, false);
                    break;
                case NativeBattleEffect326ModelService.SplitAnimated:
                    frame = DrawSplitModel(frame, resources, inputs, address, operation.First, true);
                    break;
                case NativeBattleEffect326ModelService.SetDistance:
                    frame = Write(frame, address, Read(frame, address).WriteWord(0x20, unchecked((ushort)operation.First)));
                    break;
                case NativeBattleEffect326ModelService.AddDistance:
                    frame = Write(frame, address, Read(frame, address).WriteWord(0x20, unchecked((ushort)(Read(frame, address).ReadWord(0x20) + operation.First))));
                    break;
                case NativeBattleEffect326ModelService.TargetPairFlags:
                    int actor = checked((int)frame.State.ReadDword(0x24F0C80));
                    for (int index = actor; index <= actor + 1; index++)
                    {
                        ushort flags = unchecked((ushort)((inputs.ReadModel(frame, index).Flags0 & ~8) | operation.First));
                        frame = inputs.Execute(frame, new NativeBattleEffect326WriteModelBytes(index, 0, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(flags)), 2));
                    }
                    break;
                case NativeBattleEffect326ModelService.Complete:
                    frame = frame with { State = frame.State.WriteDword(0x24F0C84, frame.State.ReadDword(0x24F0C84) | 1) };
                    break;
                default:
                    throw new NotSupportedException("Effect326 model selected an unsupported timeline service.");
            }
        }
    }

    private static NativeBattleEffect326Frame StoreModelMatrix(NativeBattleEffect326Frame frame, uint address, NativeBattleActionMatrix matrix)
    {
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(matrix, null);
        frame = WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, packed.Bytes.AsSpan().Slice(0, 18)), 18);
        return WriteBytes(frame, address + 20, NativeBattleEffectArena.Clear().Write(0, packed.Bytes.AsSpan().Slice(20, 12)), 12);
    }
}
