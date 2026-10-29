using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect166Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect166Lower
{
    private static NativeBattleEffect166Step AdvanceHistory(NativeBattleEffect166Frame frame, uint address, NativeBattleEffect166Resources resources, NativeBattleEffect166Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        uint pathAddress = Path(task);
        NativeBattleEffectArena path = frame.State.ReadView(pathAddress, 820);
        (frame, uint temporary) = Allocate(frame, 64);
        int age = Signed(task, 12);
        int ring = age % 3;
        path = Put(path, 440 + ring * 20, 1);
        if (inputs.CapturePresentation)
        {
            NativeBattlePilotVisualLifetimes.Allocated(frame.State.Storage, pathAddress + 440, ring);
        }
        path = Copy(Copy(path, 444 + ring * 20, frame.State.ReadView(0x23C3260, 8), 0, 8), 452 + ring * 20, path, 56 + Signed(task, 24) * 8, 8);
        int maximum = age >= 21 ? 24 - age : 3;
        int count = 0;
        for (; count < maximum; count++)
        {
            if (U(path, 440 + ring * 20) == 0)
            {
                break;
            }
            path = Copy(Copy(path, 500 + count * 8, path, 444 + ring * 20, 8), 532 + count * 8, path, 452 + ring * 20, 8);
            ring = ring == 0 ? 2 : ring - 1;
        }
        if (count > 1)
        {
            (frame, uint curveAddress) = Allocate(frame, 400);
            NativeBattleEffectArena work = Context(frame, curveAddress, 400);
            for (int side = 0; side < 2; side++)
            {
                work = NativeBattleGilgameshSwordTrailCurve.Prepare571620(count, path, 500 + side * 32, work);
                for (int sample = 0; sample < 16; sample++)
                {
                    path = NativeBattleGilgameshSwordTrailCurve.Evaluate571690(count, work, sample * 4096 / 15, path, 564 + side * 128 + sample * 8);
                }
                frame = PublishContext(frame, curveAddress, work, 400);
            }
            frame = Release(frame, 400);
            frame = frame with { State = frame.State.WriteView(pathAddress, path, 820) };
            frame = LoadMatrix(frame, inputs.ReadView(frame));
            frame = ZeroFar(frame);
            NativeBattleGeometryRegisterStep registers = Registers(frame);
            NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics!;
            ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
            int allocated = 0;
            NativeBattleEffectArena scratch = Context(frame, temporary, 64);
            uint color = resources.Palette[age % 12] | 0x3A000000;
            scratch = Put(Put(scratch, 20, color), 28, color);
            for (int segment = 0; segment < 14; segment++)
            {
                scratch = Put(scratch, 16, U(scratch, 20));
                registers = LoadPoint(registers, 0, path, 564 + segment * 8);
                registers = LoadPoint(registers, 1, path, 572 + segment * 8);
                registers = LoadPoint(registers, 2, path, 692 + segment * 8);
                registers = NativeBattleGeometry.ProjectTriple(registers, inputs.ReadProjection(frame), controls.Dqa, controls.Dqb);
                SkinWrites first = new(36, allocated);
                first.Word(0, 0x08000000);
                uint flags = registers.Colors.Flags!.Value;
                scratch = Put(scratch, 12, flags);
                if ((flags & 0x60000) == 0)
                {
                    uint[] screens = new uint[4];
                    for (int corner = 0; corner < 3; corner++)
                    {
                        screens[corner] = registers.Geometry.Sxy[corner]!.Value;
                        first.Word(8 + corner * 8, screens[corner]);
                    }
                    registers = LoadPoint(registers, 0, path, 700 + segment * 8);
                    registers = NativeBattleGeometry.ProjectSingle(registers, inputs.ReadProjection(frame), controls.Dqa, controls.Dqb);
                    screens[3] = registers.Geometry.Sxy[2]!.Value;
                    first.Word(32, screens[3]);
                    registers = NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Effect166 trails require current ZSF4."));
                    uint depth = registers.Geometry.Otz!.Value;
                    scratch = Put(scratch, 8, depth);
                    NativeBattleModelRendererRegisters far = frame.Memory.PrimitiveProcess!.ModelRendererRegisters!;
                    registers = registers with { Colors = NativeBattleScriptedDpcs.Transform45F270(registers.Colors, U(scratch, 28), unchecked((uint)(segment * 4096 / 14)), far, inputs.Colors).Registers };
                    uint dim = registers.Colors.Rgb[2] ?? throw new NotSupportedException("Trail depth cue requires its current RGB FIFO.");
                    scratch = Put(scratch, 20, dim);
                    uint raw = U(scratch, 16);
                    for (int corner = 0; corner < 4; corner++)
                    {
                        first.Word(4 + corner * 8, (corner & 1) == 0 ? raw : dim);
                    }
                    int bucket = 17 + (unchecked((int)depth) >> 2);
                    ImmutableArray<int> payload = Depths(registers);
                    NativeBattlePilotVisualGeometry? historyGeometry = null;
                    if (inputs.CapturePresentation)
                    {
                        historyGeometry = new(NativeBattleEffect166VisualIdentity.History(frame, task, pathAddress, count, segment * 2), TrailWorld(registers, path, 564 + segment * 8, inputs.ReadProjection(frame)), TrailWorld(registers, path, 572 + segment * 8, inputs.ReadProjection(frame)), TrailWorld(registers, path, 692 + segment * 8, inputs.ReadProjection(frame)), TrailWorld(registers, path, 700 + segment * 8, inputs.ReadProjection(frame)), 4) { SourceDepths = new(payload[0], payload[1], payload[2], payload[3]), UseSourceDepths = true };
                    }
                    attempts.Add(first.Attempt(new(bucket, first.Words(), payload) { PilotGeometry = historyGeometry }));
                    SkinWrites second = new(36, allocated + 36);
                    second.Word(0, 0x08000000);
                    for (int corner = 0; corner < 4; corner++)
                    {
                        second.Word(8 + corner * 8, screens[corner]);
                        second.Word(4 + corner * 8, corner < 2 ? 0x3A000000u : corner == 2 ? raw : dim);
                    }
                    attempts.Add(second.Attempt(new(bucket, second.Words(), payload) { PilotGeometry = historyGeometry is { } history ? history with { Identity = history.Identity with { Primitive = unchecked(history.Identity.Primitive + 1) } } : null }));
                    allocated += 72;
                }
                else
                {
                    attempts.Add(first.Attempt(null));
                }
            }
            frame = PublishContext(Publish(frame, registers), temporary, scratch, 64);
            frame = Deliver(frame, new(attempts.ToImmutable(), allocated), inputs);
        }
        else
        {
            frame = frame with { State = frame.State.WriteView(pathAddress, path, 820) };
        }
        frame = Release(frame, 64);
        if (Paused(frame, inputs))
        {
            return new(frame, 0);
        }
        task = Read(frame, address);
        int velocity = Signed(task, 26);
        short next = unchecked((short)(Signed(task, 24) + velocity));
        task = task.WriteWord(24, unchecked((ushort)next)).WriteWord(26, unchecked((ushort)(velocity > 2 ? velocity - 1 : velocity))).WriteWord(12, unchecked((ushort)(Signed(task, 12) + 1)));
        return new(Write(frame, address, task), next >= 48 ? 2 : 0);
    }

    private static NativeBattleEffect166Step AdvanceRibbon(NativeBattleEffect166Frame frame, uint address, NativeBattleEffect166Resources resources, NativeBattleEffect166Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleEffectArena path = frame.State.ReadView(Path(task), 820);
        int samples = Signed(task, 24) + 1;
        (frame, uint temporary) = Allocate(frame, 144);
        NativeBattleEffectArena scratch = Context(frame, temporary, 144);
        if (samples >= 2)
        {
            scratch = Word(Word(Word(scratch, 48, 90), 50, 0), 52, 0);
            scratch = scratch.Write(64, resources.ReadStatic(0xD78050, 16));
            NativeBattleEffectArena rows = frame.State.ReadView(0x23C2DC8, samples * 24);
            for (int index = 0; index < samples; index++)
            {
                int current = 56 + index * 8;
                int other = index == 0 ? current + 8 : current - 8;
                scratch = Copy(Copy(scratch, 32, path, current, 8), 40, path, other, 8);
                int dx = index == 0 ? S(path, other) - S(path, current) : S(path, current) - S(path, other);
                int dz = index == 0 ? S(path, other + 4) - S(path, current + 4) : S(path, current + 4) - S(path, other + 4);
                (int x, int y, int z) = NativeBattleActionGeometryRules.NormalizeVector56BC50(dx, 0, dz);
                scratch = Put(Put(Put(scratch, 80, unchecked((uint)x)), 84, unchecked((uint)y)), 88, unchecked((uint)z));
                NativeBattleModuleAxisAngle align = NativeBattleModuleAlignment.Between571480(unchecked((int)U(scratch, 64)), unchecked((int)U(scratch, 68)), unchecked((int)U(scratch, 72)), x, y, z);
                scratch = Put(Put(Put(scratch, 96, unchecked((uint)(int)align.Axis.X)), 100, unchecked((uint)(int)align.Axis.Y)), 104, unchecked((uint)(int)align.Axis.Z));
                ImmutableArray<short> rotation = NativeBattleModuleAlignment.Rotation5714F0(align.Angle, align.Axis);
                for (int word = 0; word < 9; word++)
                {
                    scratch = Word(scratch, 112 + word * 2, rotation[word]);
                }
                NativeBattleVector side = NativeBattleGfMatrices.RotateShort56C4F0(new(rotation, 0, 0, 0, 4096), new(90, 0, 0));
                scratch = PutPoint(scratch, 56, side);
                rows = Copy(rows, index * 24, path, current, 8);
                rows = PutPoint(rows, index * 24 + 8, new(unchecked((short)(S(path, current) + side.X)), 0, unchecked((short)(S(path, current + 4) + side.Z))));
                rows = PutPoint(rows, index * 24 + 16, new(unchecked((short)(S(path, current) - side.X)), 0, unchecked((short)(S(path, current + 4) - side.Z))));
            }
            frame = frame with { State = frame.State.WriteView(0x23C2DC8, rows, samples * 24) };
            frame = ZeroFar(LoadMatrix(frame, inputs.ReadView(frame)));
            NativeBattleGeometryRegisterStep registers = Registers(frame);
            NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics!;
            uint[] rgb = [0x3A202080, 0x3A040430, 0x3A04040E, 0x3A000000];
            for (int index = 0; index < 4; index++)
            {
                scratch = Put(scratch, 16 + index * 4, rgb[index]);
            }
            int age = Signed(task, 12);
            if (age >= 24)
            {
                NativeBattleScriptedColorRegisters loaded = NativeBattleScriptedDpct.LoadColors45E120(registers.Colors, rgb[0], rgb[1], rgb[2]) with { Ir0 = unchecked((uint)((age - 24) * 341)) };
                registers = registers with { Colors = NativeBattleScriptedDpct.Transform45F4C0(loaded, frame.Memory.PrimitiveProcess!.ModelRendererRegisters!, inputs.Colors) };
                rgb = [registers.Colors.Rgb[0]!.Value | 0x3A000000, registers.Colors.Rgb[1]!.Value | 0x3A000000, registers.Colors.Rgb[2]!.Value, 0x3A000000];
                for (int index = 0; index < 3; index++)
                {
                    scratch = Put(scratch, 16 + index * 4, rgb[index]);
                }
            }
            ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
            int allocated = 0;
            for (int segment = 0; segment < samples - 1; segment++)
            {
                int row = segment * 24;
                SkinWrites first = new(36, allocated);
                SkinWrites second = new(36, allocated + 36);
                first.Word(0, 0x08000000);
                second.Word(0, 0x08000000);
                registers = LoadPoint(LoadPoint(LoadPoint(registers, 0, rows, row + 24), 1, rows, row + 8), 2, rows, row + 16);
                registers = NativeBattleGeometry.ProjectTriple(registers, inputs.ReadProjection(frame), controls.Dqa, controls.Dqb);
                uint flags = registers.Colors.Flags!.Value;
                scratch = Put(scratch, 12, flags);
                if ((flags & 0x60000) == 0)
                {
                    uint[] a = registers.Geometry.Sxy.Select(value => value!.Value).ToArray();
                    registers = NativeBattleGeometry.Average45E5C0(registers, controls.Zsf3 ?? throw new NotSupportedException("Ribbon requires current ZSF3."));
                    uint depth = registers.Geometry.Otz!.Value;
                    scratch = Put(scratch, 8, depth);
                    registers = LoadPoint(LoadPoint(LoadPoint(registers, 0, rows, row), 1, rows, row + 32), 2, rows, row + 40);
                    registers = NativeBattleGeometry.ProjectTriple(registers, inputs.ReadProjection(frame), controls.Dqa, controls.Dqb);
                    uint[] b = registers.Geometry.Sxy.Select(value => value!.Value).ToArray();
                    uint[] firstScreens = [b[1], a[0], a[1], b[0]];
                    uint[] secondScreens = [a[0], b[2], b[0], a[2]];
                    uint[] firstColors = segment == 0 ? [rgb[1], rgb[0], rgb[3], rgb[2]] : segment == samples - 2 ? [rgb[3], rgb[2], rgb[1], rgb[0]] : [rgb[1], rgb[0], rgb[1], rgb[0]];
                    uint[] secondColors = segment == 0 ? [rgb[0], rgb[1], rgb[2], rgb[3]] : segment == samples - 2 ? [rgb[2], rgb[3], rgb[0], rgb[1]] : [rgb[0], rgb[1], rgb[0], rgb[1]];
                    for (int corner = 0; corner < 4; corner++)
                    {
                        first.Word(4 + corner * 8, firstColors[corner]);
                        first.Word(8 + corner * 8, firstScreens[corner]);
                        second.Word(4 + corner * 8, secondColors[corner]);
                        second.Word(8 + corner * 8, secondScreens[corner]);
                    }
                    int bucket = 17 + (unchecked((int)depth) >> 2);
                    ImmutableArray<int> payload = Depths(registers);
                    NativeBattlePilotVisualGeometry? firstGeometry = null;
                    NativeBattlePilotVisualGeometry? secondGeometry = null;
                    if (inputs.CapturePresentation)
                    {
                        System.Numerics.Vector3 Point(int offset) => TrailWorld(registers, rows, row + offset, inputs.ReadProjection(frame));
                        System.Numerics.Vector4 depths = new(payload[0], payload[1], payload[2], payload[3]);
                        NativeBattlePilotVisualIdentity identity = NativeBattleEffect166VisualIdentity.Segment(frame, task, Path(task), segment, segment + 1, segment * 2);
                        firstGeometry = new(identity, Point(32), Point(24), Point(8), Point(0), 4) { SourceDepths = depths, UseSourceDepths = true };
                        secondGeometry = new(identity with { Primitive = unchecked(identity.Primitive + 1) }, Point(24), Point(40), Point(0), Point(16), 4) { SourceDepths = depths, UseSourceDepths = true };
                    }
                    attempts.Add(first.Attempt(new(bucket, first.Words(), payload) { PilotGeometry = firstGeometry }));
                    attempts.Add(second.Attempt(new(bucket, second.Words(), payload) { PilotGeometry = secondGeometry }));
                    allocated += 72;
                }
                else
                {
                    attempts.Add(first.Attempt(null));
                    attempts.Add(second.Attempt(null));
                }
            }
            frame = PublishContext(Publish(frame, registers), temporary, scratch, 144);
            frame = Deliver(frame, new(attempts.ToImmutable(), allocated), inputs);
        }
        frame = Release(frame, 144);
        if (Paused(frame, inputs))
        {
            return new(frame, 0);
        }
        task = Read(frame, address);
        int velocity = Signed(task, 26);
        short next = unchecked((short)(Signed(task, 24) + velocity));
        task = task.WriteWord(24, unchecked((ushort)(next >= 48 ? 47 : next))).WriteWord(26, unchecked((ushort)(velocity > 2 ? velocity - 1 : velocity)));
        return Clock(Write(frame, address, task), address, task, 36);
    }

    private static System.Numerics.Vector3 TrailWorld(NativeBattleGeometryRegisterStep registers, NativeBattleEffectArena rows, int offset, NativeBattleCameraProjection projection) => NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(NativeBattleGeometry.CaptureMatrix(registers.Geometry), Vector(rows, offset)), projection);

    private static NativeBattleGeometryRegisterStep LoadPoint(NativeBattleGeometryRegisterStep registers, int vector, NativeBattleEffectArena rows, int offset)
    {
        (byte[] bytes, bool[] known) = rows.ReadSnapshot(offset + 6, 2);
        short? w = known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
        return NativeBattleGeometry.LoadVector(registers, vector, Vector(rows, offset), w);
    }
    private static ImmutableArray<int> Depths(NativeBattleGeometryRegisterStep registers) => registers.Geometry.Sz.Select(value => unchecked((int)value!.Value)).ToImmutableArray();
    private static NativeBattleEffect166Frame ZeroFar(NativeBattleEffect166Frame frame)
    {
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Trails require their current primitive owner.");
        NativeBattleModelRendererRegisters far = process.ModelRendererRegisters ?? throw new NotSupportedException("Trails require their current renderer controls.");
        return frame with { Memory = frame.Memory with { PrimitiveProcess = process with { ModelRendererRegisters = far with { FarRed = 0, FarGreen = 0, FarBlue = 0 } } } };
    }
}
