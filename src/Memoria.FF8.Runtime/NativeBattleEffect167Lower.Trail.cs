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
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Trail drawing requires its current primitive owner.");
        NativeBattleModelRendererRegisters far = (process.ModelRendererRegisters ?? throw new NotSupportedException("Trail drawing requires its current color controls.")) with { FarRed = 0, FarGreen = 0, FarBlue = 0 };
        memory = memory with { PrimitiveProcess = process with { ModelRendererRegisters = far } };
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Trail drawing requires current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Trail drawing requires current arithmetic registers."));
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Trail drawing requires current projection controls.");
        temporary64 = LowerPut(LowerPut(temporary64, 0x14, 0x3A903030), 0x1C, 0x3A903030);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int segment = 0; segment < 30; segment++)
        {
            temporary64 = LowerPut(temporary64, 0x10, LowerDword(temporary64, 0x14));
            SkinWrites first = new(36, allocated);
            first.Word(0, 0x08000000);
            registers = LoadTrailPoint(registers, 0, firstSamples, segment * 8);
            registers = LoadTrailPoint(registers, 1, firstSamples, segment * 8 + 8);
            registers = LoadTrailPoint(registers, 2, secondSamples, segment * 8);
            registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
            uint flags = registers.Colors.Flags ?? throw new NotSupportedException("Trail drawing requires its current projection flags.");
            temporary64 = LowerPut(temporary64, 12, flags);
            if ((flags & 0x60000) != 0)
            {
                attempts.Add(first.Attempt(null));
                continue;
            }
            uint[] screens = registers.Geometry.Sxy.Select(value => value ?? throw new NotSupportedException("Trail drawing requires its current screen FIFO.")).ToArray();
            registers = LoadTrailPoint(registers, 0, secondSamples, segment * 8 + 8);
            registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
            uint fourth = registers.Geometry.Sxy[2] ?? throw new NotSupportedException("Trail drawing requires its current fourth projection.");
            registers = NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Trail drawing requires its current ZSF4."));
            uint depth = registers.Geometry.Otz ?? throw new NotSupportedException("Trail drawing requires its current depth.");
            temporary64 = LowerPut(temporary64, 8, depth);
            NativeBattleScriptedDpcsResult tint = NativeBattleScriptedDpcs.Transform45F270(registers.Colors, LowerDword(temporary64, 0x1C), (uint)(segment * 4096 / 30), far, colors);
            registers = registers with { Colors = tint.Registers };
            temporary64 = LowerPut(temporary64, 0x14, tint.PackedRgb);
            uint previous = LowerDword(temporary64, 0x10);
            first.Word(4, previous);
            first.Word(12, tint.PackedRgb);
            first.Word(20, previous);
            first.Word(28, tint.PackedRgb);
            for (int corner = 0; corner < 3; corner++)
            {
                first.Word(8 + corner * 8, screens[corner]);
            }
            first.Word(32, fourth);
            SkinWrites second = new(36, allocated + 36);
            second.Word(0, 0x08000000);
            second.Word(4, 0x3A000000);
            second.Word(12, 0x3A000000);
            second.Word(20, previous);
            second.Word(28, tint.PackedRgb);
            for (int corner = 0; corner < 3; corner++)
            {
                second.Word(8 + corner * 8, screens[corner]);
            }
            second.Word(32, fourth);
            ImmutableArray<int> payload = registers.Geometry.Sz.Select(value => unchecked((int)(value ?? throw new NotSupportedException("Trail drawing requires all four current depths.")))).ToImmutableArray();
            int bucket = unchecked(17 + ((int)depth >> 2));
            attempts.Add(first.Attempt(new(bucket, first.Words(), payload)));
            attempts.Add(second.Attempt(new(bucket, second.Words(), payload)));
            allocated += 72;
        }
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, temporary64, new(attempts.ToImmutable(), allocated + 36) { NativeUnlinkedAllocation = true });
    }

    private static NativeBattleGeometryRegisterStep LoadTrailPoint(NativeBattleGeometryRegisterStep registers, int slot, NativeBattleEffectArena points, int offset)
    {
        (byte[] bytes, bool[] known) = points.ReadSnapshot(offset + 6, 2);
        short? w = known.All(value => value) ? System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
        return NativeBattleGeometry.LoadVector(registers, slot, LowerVector(points, offset), w);
    }
}
