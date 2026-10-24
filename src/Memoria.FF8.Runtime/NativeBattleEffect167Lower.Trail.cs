using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect167Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect167Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect167TrailStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Temporary64, NativeBattleGfGeometryBatch Batch);

public static partial class NativeBattleEffect167Lower
{
    public static NativeBattleEffect167Step AdvanceTrail(NativeBattleEffect167Frame frame, uint address, NativeBattleEffect167Resources resources, NativeBattleEffect167Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        (frame, uint temporary) = Allocate(frame, 64);
        NativeBattleEffectArena context = Context(frame, temporary, 64);
        context = LowerPut(LowerPut(LowerPut(context, 0x30, 6656), 0x34, 6656), 0x38, 6656);
        int actor = checked((int)frame.State.ReadDword(CasterGlobal));
        NativeBattleActionMatrix scaled = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(1024, 0, 2048)), 6656, 6656, 6656);
        NativeBattleActionMatrix boneWorld = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadWorld(frame, actor).ReadMatrix(), inputs.ReadCurrentBoneMatrix32(frame, actor, 59).ReadMatrix());
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(boneWorld, scaled);
        NativeBattleEffectArena endpoints = frame.State.ReadView(EndpointsAddress, 16);
        NativeBattleVector[] local = [new(0, 1000, -1000), new(0, 6200, 600)];
        for (int side = 0; side < 2; side++)
        {
            NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(matrix, local[side]);
            NativeBattleVector point = new(unchecked((short)(rotated.X + matrix.X)), unchecked((short)(rotated.Y + matrix.Y)), unchecked((short)(rotated.Z + matrix.Z)));
            endpoints = LowerStore(endpoints, side * 8, point);
        }
        frame = frame with { State = frame.State.WriteView(EndpointsAddress, endpoints, 16) };
        int age = Signed(task, 12);
        int ring = age % 10;
        NativeBattleEffectArena history = frame.State.ReadView(SecondHistoryAddress, 200);
        history = LowerPut(history, ring * 20, 1);
        history = CopyBytes(endpoints, 0, history, ring * 20 + 4, 16);
        NativeBattleEffectArena firstControls = frame.State.ReadView(FirstControlAddress, 80);
        NativeBattleEffectArena secondControls = frame.State.ReadView(SecondControlAddress, 80);
        int count = 0;
        for (; count < 10; count++)
        {
            if (LowerDword(history, ring * 20) == 0)
            {
                break;
            }
            firstControls = CopyBytes(history, ring * 20 + 4, firstControls, count * 8, 8);
            secondControls = count == 0 ? CopyBytes(endpoints, 8, secondControls, 0, 8) : CopyBytes(history, ring * 20 + 12, secondControls, count * 8, 8);
            ring = ring == 0 ? 9 : ring - 1;
        }
        frame = frame with { State = frame.State.WriteView(SecondHistoryAddress, history, 200).WriteView(FirstControlAddress, firstControls, 80).WriteView(SecondControlAddress, secondControls, 80) };
        if (count > 1)
        {
            (frame, uint workAddress) = Allocate(frame, 400);
            NativeBattleEffectArena work = Context(frame, workAddress, 400);
            for (int side = 0; side < 2; side++)
            {
                NativeBattleEffectArena controls = side == 0 ? firstControls : secondControls;
                work = NativeBattleGilgameshSwordTrailCurve.Prepare571620(count, controls, 0, work);
                uint destination = side == 0 ? FirstSamplesAddress : SecondSamplesAddress;
                NativeBattleEffectArena samples = frame.State.ReadView(destination, 256);
                for (int sample = 0; sample < 32; sample++)
                {
                    samples = NativeBattleGilgameshSwordTrailCurve.Evaluate571690(count, work, sample * 4096 / 31, samples, sample * 8);
                }
                frame = frame with { State = frame.State.WriteView(destination, samples, 256) };
                frame = PublishContext(frame, workAddress, work, 400);
            }
            frame = Release(frame, 400);
            frame = LoadMatrix(frame, inputs.ReadView(frame));
            NativeBattleEffect167TrailStep drawn = DrawTrail(frame.Memory, frame.State.ReadView(FirstSamplesAddress, 256), frame.State.ReadView(SecondSamplesAddress, 256), context, inputs.ReadProjection(frame), inputs.Colors);
            frame = frame with { Memory = drawn.Memory };
            context = drawn.Temporary64;
            frame = PublishContext(frame, temporary, context, 64);
            frame = inputs.Execute(frame, new NativeBattleEffect167DrawBatch(drawn.Batch, CursorGlobal));
        }
        else
        {
            frame = PublishContext(frame, temporary, context, 64);
        }
        frame = Release(frame, 64);
        if ((inputs.ReadFlags(frame) & 0x201) != 0)
        {
            return new(frame, 0);
        }
        short next = unchecked((short)(age + 1));
        return new(Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)next))), next >= 20 ? 2 : 0);
    }

    public static NativeBattleEffect167TrailStep DrawTrail(NativeBattleEffectMemory memory, NativeBattleEffectArena firstSamples, NativeBattleEffectArena secondSamples, NativeBattleEffectArena temporary64, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        NativeBattleRibbonPair72Step result = NativeBattleRibbonPair72.DrawLoaded(memory, firstSamples, secondSamples, temporary64, projection, colors, 0x3A903030);
        return new(result.Memory, result.Temporary64, result.Batch);
    }
}
