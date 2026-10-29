using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGravija214BoundsStep(NativeBattleGravija214Frame Frame, NativeBattleEffectArena Rect8, int MinimumDepth, bool Included);

public static partial class NativeBattleGravija214Controller
{
    public static NativeBattleGravija214Frame DrawWarp69E020(NativeBattleGravija214Frame frame, uint mesh, uint vertices, int bucket, int cue, int amplitude, int phase, NativeBattleGravija214Resources resources, NativeBattleGravija214Inputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        int count = checked((int)frame.State.ReadDword(mesh + 4));
        if (mesh != 0x25089BC || vertices != mesh + 8 || count != 241)
        {
            throw new InvalidDataException("Gravija warp requires its actual preprocessed copied source.");
        }
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Gravija warp requires its current loaded geometry."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Gravija warp requires its current arithmetic owner."));
        for (int vertex = 0; vertex < count; vertex++)
        {
            uint source = vertices + (uint)(vertex * 8);
            uint destination = 0x20DFAB8 + 0xE24 + (uint)(vertex * 12);
            uint packed = frame.State.ReadDword(source + 4);
            int index = unchecked(((int)packed >> 18) & ~3) + phase;
            int factor = unchecked(resources.SineTable[index & 4095] * amplitude) >> 12;
            int x = (int)(packed << 22) >> 22;
            int y = (int)(packed << 12) >> 22;
            short originalX = unchecked((short)frame.State.ReadWord(source));
            short originalY = unchecked((short)frame.State.ReadWord(source + 2));
            NativeBattleVector deformed = new(unchecked((short)(originalX + (unchecked(x * factor) >> 12))), unchecked((short)(originalY + (unchecked(y * factor) >> 12))), 0);
            registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, deformed, 0));
            short screenX = unchecked((short)registers.Colors.Ir[0]!.Value);
            short screenY = unchecked((short)registers.Colors.Ir[1]!.Value);
            NativeBattleEffectArena row = ReadBytes(frame, destination, 12).Write(0, BitConverter.GetBytes(screenX)).Write(2, BitConverter.GetBytes(screenY));
            registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, new(originalX, originalY, 0), 0));
            short u = unchecked((short)registers.Colors.Ir[0]!.Value);
            short v = unchecked((short)registers.Colors.Ir[1]!.Value);
            if (screenX < 0)
            {
                row = row.Write(8, new byte[] { (byte)(row.Read(8, 1)[0] | 1) });
            }
            else if (screenX >= 320)
            {
                row = row.Write(8, new byte[] { (byte)(row.Read(8, 1)[0] | 2) });
            }
            else
            {
                row = row.Write(8, BitConverter.GetBytes((ushort)0));
            }
            if (screenY < 0)
            {
                row = row.Write(8, new byte[] { (byte)(row.Read(8, 1)[0] | 4) });
            }
            else if (screenY >= 216)
            {
                row = row.Write(8, new byte[] { (byte)(row.Read(8, 1)[0] | 8) });
            }
            if (u < 0)
            {
                u = 0;
            }
            else if (u >= 320)
            {
                u = 319;
            }
            else if (v < 0)
            {
                v = 0;
            }
            else if (v >= 216)
            {
                v = 215;
            }
            row = row.Write(4, BitConverter.GetBytes(u)).Write(6, BitConverter.GetBytes(v));
            frame = WriteBytes(frame, destination, row, 12);
        }
        frame = frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
        (frame, uint temporary) = Allocate(frame, 72);
        NativeBattleEffectArena context = Context(frame, temporary, 72).Write(0, BitConverter.GetBytes(mesh)).Write(4, BitConverter.GetBytes(0x20E08DCu)).Write(8, BitConverter.GetBytes(576)).Write(12, BitConverter.GetBytes(256));
        uint ordering = (inputs.ReadOrderingTable ?? throw new NotSupportedException("Gravija warp requires its actual current ordering-table pointer."))(frame);
        context = context.Write(20, BitConverter.GetBytes(ordering + unchecked((uint)(bucket * 4))));
        uint group = mesh + frame.State.ReadDword(mesh) + 16;
        context = context.Write(16, BitConverter.GetBytes(group));
        frame = PublishContext(frame, temporary, context, 72);
        frame = DrawMode214(frame, inputs, bucket, 0xE1000220);
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Gravija warp requires its current far-color owner.");
        NativeBattleModelRendererRegisters far = process.ModelRendererRegisters ?? throw new NotSupportedException("Gravija warp requires its current far-color controls.");
        NativeBattleScriptedColorRegisters colors = frame.Memory.ScriptedColorRegisters!;
        frame = frame with { Memory = frame.Memory with { ScriptedColorRegisters = colors with { Ir0 = unchecked((uint)cue) }, PrimitiveProcess = process with { ModelRendererRegisters = far with { FarRed = 0, FarGreen = 0, FarBlue = 0 } } } };
        frame = DrawDirectGroup214(frame, temporary, false, false, bucket, inputs, pilotIdentity);
        frame = DrawDirectGroup214(frame, temporary, true, false, bucket, inputs, pilotIdentity);
        frame = DrawMode214(frame, inputs, bucket, 0xE1000240);
        context = Context(frame, temporary, 72).Write(16, BitConverter.GetBytes(group));
        frame = PublishContext(frame, temporary, context, 72);
        frame = DrawDirectGroup214(frame, temporary, false, true, bucket, inputs, pilotIdentity);
        frame = DrawDirectGroup214(frame, temporary, true, true, bucket, inputs, pilotIdentity);
        return Release(frame, 72);
    }

    public static NativeBattleGravija214BoundsStep Bounds69E860(NativeBattleGravija214Frame frame, uint modelAddress, NativeBattleEffectArena rect8, int minimumDepth, NativeBattleGravija214Inputs inputs)
    {
        NativeBattleGraphicsModel model = inputs.ReadModel(frame, modelAddress);
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Gravija bounds require its current model transform.");
        short size = unchecked((short)(transform.Scale38 + 256));
        NativeBattleVector bottom = transform.Position28;
        NativeBattleVector top = bottom with { Y = unchecked((short)(bottom.Y - size)) };
        bool included = false;
        int radius = size;
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Gravija bounds require current projection controls.");
        for (int index = 1; index >= 0; index--)
        {
            NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Gravija bounds require loaded geometry."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Gravija bounds require arithmetic registers."));
            registers = NativeBattleGeometry.ProjectSingle(NativeBattleGeometry.LoadVector(registers, 0, index == 1 ? top : bottom, null), inputs.ReadProjection(frame), controls.Dqa, controls.Dqb);
            frame = frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
            uint screen = registers.Geometry.Sxy[2]!.Value;
            short depth = unchecked((short)(unchecked((int)registers.Geometry.Sz[3]!.Value) >> 2));
            if (index == 1)
            {
                radius = unchecked(size * 54) / depth;
            }
            short x = unchecked((short)(unchecked((short)screen) >> 3));
            short y = unchecked((short)(unchecked((short)(screen >> 16)) >> 3));
            short left = unchecked((short)(x - radius));
            Include(left, y, depth);
            Include(unchecked((short)(left + radius * 2)), y, depth);
        }
        return new(frame, rect8, minimumDepth, included);

        void Include(short x, short y, short depth)
        {
            if (depth <= 0)
            {
                return;
            }
            short minX = BinaryPrimitives.ReadInt16LittleEndian(rect8.Read(0, 2));
            short minY = BinaryPrimitives.ReadInt16LittleEndian(rect8.Read(2, 2));
            short maxX = BinaryPrimitives.ReadInt16LittleEndian(rect8.Read(4, 2));
            short maxY = BinaryPrimitives.ReadInt16LittleEndian(rect8.Read(6, 2));
            rect8 = rect8.Write(0, BitConverter.GetBytes(Math.Min(minX, x))).Write(2, BitConverter.GetBytes(Math.Min(minY, y))).Write(4, BitConverter.GetBytes(Math.Max(maxX, x))).Write(6, BitConverter.GetBytes(Math.Max(maxY, y)));
            minimumDepth = Math.Min(minimumDepth, depth);
            included = true;
        }
    }
}
