using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleSonic139Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleSonic139Controller
{
    public static (NativeBattleSonic139Frame Frame, int Result) AdvanceMain(NativeBattleSonic139Frame frame, uint address, NativeBattleSonic139Resources resources, NativeBattleSonic139Inputs inputs)
    {
        int age = Signed(Read(frame, address), 12);
        if ((uint)age < 36)
        {
            NativeBattleSonic139PointStep emitter = inputs.ReadEmitter6A9200(frame, NativeBattleEffectArena.Clear().Write(0, resources.Anchor.AsSpan()));
            frame = emitter.Frame;
            if (age == 0)
            {
                frame = WriteBytes(frame, address + 0x20, emitter.Point8, 8);
            }
            NativeBattleVector point = Point(emitter.Point8);
            NativeBattleActionMatrix local = NativeBattlePandemona291Restoration.Rotation701270(unchecked((short)-inputs.ReadHeading(frame, frame.State.ReadDword(CasterGlobal)))) with { X = point.X, Y = point.Y, Z = point.Z };
            NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
            NativeBattleSonic139ActStep act = inputs.AdvanceAct(frame, address + 0x30, resources.MainAct, new(NativeBattlePackedMatrix32.FromMatrix(composed, null), -128, frame.State.ReadDword(WorkingGlobal) + MorphOffset) { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(139, Read(frame, address), 0x30) });
            frame = act.Frame;
        }
        if ((uint)age < 30)
        {
            int phase = checked(age * 4096 / 30);
            NativeBattleModulePoolTask task = Read(frame, address);
            NativeBattleWeightedPointsStep blend = NativeBattleWeightedPoints56CB90.Blend(frame.Memory, new(Signed(task, 0x20), Signed(task, 0x22), Signed(task, 0x24)), new(Signed(task, 0x28), Signed(task, 0x2A), Signed(task, 0x2C)), checked((short)(4096 - phase)), checked((short)phase), inputs.Colors);
            frame = frame with { Memory = blend.Memory };
            (frame, uint screen, short depth) = Project(frame, blend.Position, inputs.ReadView(frame), inputs);
            if (depth > 0)
            {
                short scale = unchecked((short)(0x18000 / depth));
                frame = SetScreenMatrix(frame, scale, scale, unchecked((short)screen) >> 3, unchecked((short)(screen >> 16)) >> 3);
                frame = LoadMatrix(frame, Packed(frame.State.ReadView(MatrixAddress, 32)));
                int bank = (age * 512 >> 12) & 3;
                int ratio = age * 512 & 4095;
                uint destination = frame.State.ReadDword(WorkingGlobal) + MorphOffset;
                frame = NativeBattleSonic139Morph.Morph6A9BB0(frame, Frame0Address + (uint)(bank * FrameLength), Frame0Address + (uint)(((bank + 1) & 3) * FrameLength), destination, ratio, inputs, resources);
                int wave = NativeBattleCameraMath.Sine(phase >> 1);
                frame = NativeBattleSonic139Warp.Draw6A9260(frame, CopiedMeshAddress, destination, ((depth - 512) >> 2) + 17, 4096 - wave, wave * 2, phase, inputs, resources, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(139, Read(frame, address), 0x10000) with { ResourceGeneration = CopiedMeshAddress });
            }
        }
        int later = age - 30;
        if ((uint)later < 40)
        {
            frame = LoadMatrix(frame, inputs.ReadView(frame));
            NativeBattleSonic139Bounds bounds = NativeBattleSonic139Bounds.Initial;
            NativeBattleModulePoolTask task = Read(frame, address);
            int count = Signed(task, 14);
            for (int index = 0; index < count; index++)
            {
                (frame, bounds) = IncludeActor6A9AD0(frame, inputs.ReadModel(frame, Read(frame, address).ReadDword(20 + index * 4)), bounds, inputs);
            }
            int bucketDepth = unchecked(bounds.MinimumDepth - 512);
            if (bounds.Visible && bucketDepth > 0)
            {
                short width = unchecked((short)(bounds.MaxX - bounds.MinX));
                short height = unchecked((short)(bounds.MaxY - bounds.MinY));
                short centerX = unchecked((short)(bounds.MinX + width / 2));
                short centerY = unchecked((short)(bounds.MinY + height / 2));
                task = Read(frame, address);
                (frame, _, short depth) = Project(frame, new(Signed(task, 0x28), Signed(task, 0x2A), Signed(task, 0x2C)), inputs.ReadView(frame), inputs, OptionalWord(ReadBytes(frame, address + 0x28, 8), 6));
                if (depth == 0)
                {
                    throw new InvalidDataException("Sonic Wave reaches its original projected-scale zero divisor.");
                }
                short minimum = unchecked((short)(0x18000 / depth));
                width = Math.Max(width, minimum);
                height = Math.Max(height, minimum);
                frame = SetScreenMatrix(frame, width, height, centerX, centerY);
                frame = LoadMatrix(frame, Packed(frame.State.ReadView(MatrixAddress, 32)));
                int phase = later * 4096 / 40;
                int wave = NativeBattleCameraMath.Sine(phase >> 1);
                frame = NativeBattleSonic139Warp.Draw6A9260(frame, CopiedMeshAddress, CopiedMeshAddress + 8, (bucketDepth >> 2) + 17, 4096 - wave, wave, phase, inputs, resources, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(139, Read(frame, address), 0x20000) with { ResourceGeneration = CopiedMeshAddress });
            }
        }
        if (age == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleSonic139Sound(resources.Sound, 0, 128));
        }
        later = Signed(Read(frame, address), 12) - 30;
        if ((uint)later < 16 && (later & 3) == 0)
        {
            int index = later >> 2;
            NativeBattleModulePoolTask main = Read(frame, address);
            if (index < Signed(main, 14))
            {
                (frame, uint childAddress) = Append(frame, 2, TargetCallback);
                if (childAddress != 0)
                {
                    uint actor = main.ReadDword(20 + index * 4);
                    uint color = inputs.ReadModel(frame, actor).Color40 ?? throw new NotSupportedException("Sonic Wave requires the actual target color DWORD.");
                    NativeBattleModulePoolTask child = Read(frame, childAddress).WriteWord(12, 0).WriteWord(14, main.ReadByte(16 + index)).WriteDword(16, actor).WriteDword(20, color);
                    frame = Write(frame, childAddress, child);
                    NativeBattleSonic139PointStep anchor = inputs.ReadAnchor571400(frame, actor);
                    frame = WriteBytes(anchor.Frame, childAddress + 24, anchor.Point8, 8);
                    child = Read(frame, childAddress);
                    NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(child, 32, 0x278).Initialize7016B0(resources.TargetAct, 0x278);
                    frame = Write(frame, childAddress, NativeBattleEffectTaskMemory.PublishAnimationWorkspace(child, 32, workspace));
                }
            }
        }
        NativeBattleModulePoolTask current = Read(frame, address);
        age = Signed(current, 12);
        if (age >= 70)
        {
            if (frame.Effect.Pools[2].Head is not null)
            {
                return (frame, 0);
            }
            if (age == 70)
            {
                frame = inputs.Execute(frame, new NativeBattleSonic139RequestCommonCompletion(frame.State.ReadDword(WorkingGlobal) + FirstPacketOffset, address + 0x13));
                frame = Write(frame, address, Read(frame, address).WriteWord(12, 71));
            }
            return (frame, Read(frame, address).ReadByte(0x13) != 0 ? 2 : 0);
        }
        return (Write(frame, address, current.WriteWord(12, unchecked((ushort)(age + 1)))), 0);
    }

    public static (NativeBattleSonic139Frame Frame, int Result) AdvanceTarget(NativeBattleSonic139Frame frame, uint address, NativeBattleSonic139Resources resources, NativeBattleSonic139Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattlePackedMatrix32 view = inputs.ReadView(frame);
        NativeBattleVector point = new(Signed(task, 24), Signed(task, 26), Signed(task, 28));
        NativeBattleGeometryRegisterStep registers = Registers(frame);
        NativeBattleGeometryRegisters rotation = NativeBattleGeometry.LoadMatrix(registers.Geometry, view.ReadMatrix(), true, false, null);
        rotation = rotation with { Rotation20 = Enumerable.Range(0, 20).Select(index => view.Known[index] ? (byte?)view.Bytes[index] : null).ToImmutableArray() };
        registers = NativeBattleGeometry.LoadVector(registers with { Geometry = rotation }, 0, point, OptionalWord(ReadBytes(frame, address + 24, 8), 6));
        registers = NativeBattleGeometry.Transform(registers, false);
        frame = Publish(frame, registers);
        NativeBattleActionMatrix matrix = view.ReadMatrix() with { X = unchecked(view.ReadMatrix().X + registers.Colors.Mac[0]!.Value), Y = unchecked(view.ReadMatrix().Y + registers.Colors.Mac[1]!.Value), Z = unchecked(view.ReadMatrix().Z + registers.Colors.Mac[2]!.Value) };
        NativeBattleSonic139ActStep act = inputs.AdvanceAct(frame, address + 32, resources.TargetAct, new(NativeBattlePackedMatrix32.FromMatrix(matrix, view.Known[18] && view.Known[19] ? BitConverter.ToUInt16(view.Bytes.AsSpan().Slice(18)) : null), 0, frame.State.ReadDword(WorkingGlobal) + MorphOffset) { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(139, Read(frame, address), 0x20) });
        frame = act.Frame;
        task = Read(frame, address);
        uint actor = task.ReadDword(16);
        if (act.Remaining == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleSonic139WriteActorColor(actor, task.ReadDword(20)));
            frame = inputs.Execute(frame, new NativeBattleSonic139SetActorFlags(actor, 0, 0x800));
            frame = inputs.Execute(frame, new NativeBattleSonic139ContactForActor(actor));
            return (frame, 2);
        }
        frame = inputs.Execute(frame, new NativeBattleSonic139SetActorFlags(actor, 0x800, 0));
        int age = Signed(task, 12);
        int phase = unchecked(age * 3 * 4096 / 26);
        int palette = phase >> 12;
        if (palette < 0 || palette + 1 >= resources.Colors.Length)
        {
            throw new InvalidDataException("Sonic Wave tint selector leaves its actual addressed palette view.");
        }
        int mix = phase & 4095;
        NativeBattleWeightedColorStep first = NativeBattleWeightedColor56CC00.Blend(frame.Memory, resources.Colors[palette], resources.Colors[palette + 1], unchecked((short)(4096 - mix)), unchecked((short)mix), 0, inputs.Colors);
        frame = frame with { Memory = first.Memory };
        int wave = NativeBattleCameraMath.Sine(phase / 6);
        uint currentColor = inputs.ReadModel(frame, actor).Color40 ?? throw new NotSupportedException("Sonic Wave requires its current target color destination.");
        NativeBattleWeightedColorStep blended = NativeBattleWeightedColor56CC00.Blend(frame.Memory, task.ReadDword(20), first.Color, unchecked((short)(4096 - wave)), unchecked((short)wave), currentColor, inputs.Colors);
        frame = inputs.Execute(frame with { Memory = blended.Memory }, new NativeBattleSonic139WriteActorColor(actor, blended.Color));
        return (Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1)))), 0);
    }

    private static NativeBattleSonic139Frame SetScreenMatrix(NativeBattleSonic139Frame frame, short x, short y, int centerX, int centerY) => frame with { State = frame.State.WriteWord(MatrixAddress, unchecked((ushort)x)).WriteWord(MatrixAddress + 8, unchecked((ushort)y)).WriteDword(MatrixAddress + 20, unchecked((uint)centerX)).WriteDword(MatrixAddress + 24, unchecked((uint)centerY)) };
    private static NativeBattlePackedMatrix32 Packed(NativeBattleEffectArena source)
    {
        (byte[] bytes, bool[] known) = source.ReadSnapshot(0, 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }
    private static NativeBattleGeometryRegisterStep Registers(NativeBattleSonic139Frame frame) => new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Sonic Wave requires its current geometry registers."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Sonic Wave requires its current color registers."));
    private static NativeBattleSonic139Frame Publish(NativeBattleSonic139Frame frame, NativeBattleGeometryRegisterStep registers) => frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
    private static short? OptionalWord(NativeBattleEffectArena source, int offset)
    {
        (byte[] bytes, bool[] known) = source.ReadSnapshot(offset, 2);
        return known.All(value => value) ? BitConverter.ToInt16(bytes) : null;
    }
    private static (NativeBattleSonic139Frame Frame, uint Screen, short Depth) Project(NativeBattleSonic139Frame frame, NativeBattleVector point, NativeBattlePackedMatrix32 matrix, NativeBattleSonic139Inputs inputs, short? w = null)
    {
        frame = LoadMatrix(frame, matrix);
        NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.LoadVector(Registers(frame), 0, point, w);
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Sonic Wave requires its current projection controls.");
        registers = NativeBattleGeometry.ProjectSingle(registers, inputs.ReadProjection(frame), controls.Dqa, controls.Dqb);
        return (Publish(frame, registers), registers.Geometry.Sxy[2]!.Value, unchecked((short)(unchecked((int)registers.Geometry.Sz[3]!.Value) >> 2)));
    }
}
