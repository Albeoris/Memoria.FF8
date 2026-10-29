using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect213Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect213Lower;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect213StreamStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context92, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleEffect213Stream
{
    public static NativeBattleEffect213Frame Bind(NativeBattleEffect213Frame frame, NativeBattleEffect213Resources resources)
    {
        (NativeBattleEffect213State state, NativeBattleFileStaging staging) = frame.State.BindSources(frame.Staging ?? throw new NotSupportedException("The stream requires its current mutable source."), resources);
        return frame with { State = state, Staging = staging };
    }

    public static NativeBattleEffect213Frame Wobble(NativeBattleEffect213Frame frame, short angle, short phase, NativeBattleEffect213Resources resources, NativeBattleEffect213Inputs inputs)
    {
        frame = Bind(frame, resources);
        NativeBattleActionMatrix rotation = NativeBattleCuragaMatrices.RotateZ8DD960(NativeBattleCuragaMatrices.Identity8DD770(), angle);
        NativeBattleEffectArena translation = frame.State.ReadView(0x25101E8, 12);
        rotation = rotation with { X = unchecked((int)Dword(translation, 0)), Y = unchecked((int)Dword(translation, 4)), Z = unchecked((int)Dword(translation, 8)) };
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(rotation, null));
        uint source = checked(frame.State.ReadDword(ArenaGlobal) + 0x1B18);
        for (int vertex = 0; vertex < 308; vertex++)
        {
            NativeBattleEffectArena row = ReadBytes(frame, source + checked((uint)(vertex * 8)), 8);
            NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.LoadVector(Registers(frame), 0, Vector(row), SignedWord(row, 6));
            registers = NativeBattleGeometry.Transform(registers);
            frame = Publish(frame, registers);
            NativeBattleVector point = Ir(registers);
            short w = SignedWord(row, 6);
            short wave = unchecked((short)(phase + w));
            int radius = NativeBattleCameraMath.Sine(w >> 1) >> 3;
            point = point with { X = unchecked((short)(point.X + ((NativeBattleCameraMath.Cosine(wave) * radius) >> 12))), Y = unchecked((short)(point.Y - ((NativeBattleCameraMath.Sine(wave) * radius) >> 12))) };
            uint address = checked(0x117ADE0u + (uint)(vertex * 8));
            frame = frame with { Staging = frame.Staging!.Write(address, Store(NativeBattleEffectArena.Clear(), point).Read(0, 6)) };
        }
        return frame;
    }

    public static NativeBattleEffect213Frame Draw(NativeBattleEffect213Frame frame, uint temporary92, NativeBattleEffect213Resources resources, NativeBattleEffect213Inputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        frame = Bind(frame, resources);
        NativeBattleStaticMeshResource mesh = resources.ReadCurrentMesh(frame.Staging!, 0x117ADD8);
        NativeBattleEffect213StreamStep draw = DrawLoaded(frame.Memory, mesh, inputs.ReadProjection(frame), Context(frame, temporary92, 92), inputs.Colors, pilotIdentity: pilotIdentity);
        frame = PublishContext(frame with { Memory = draw.Memory }, temporary92, draw.Context92, 92);
        return inputs.Execute(frame, new NativeBattleEffect213DrawBatch(draw.Batch, CursorGlobal));
    }

    public static NativeBattleEffect213StreamStep DrawLoaded(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleEffectArena context, NativeBattleSummonColors colors, int tableBaseBucket = 17, byte depthShift = 2, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (mesh.Address != 0x117ADD8 || mesh.Bytes.IsDefaultOrEmpty || mesh.Bytes.Length < 8 || Dword(context, 0) != mesh.Address || depthShift > 31)
        {
            throw new InvalidDataException("The stream requires its current source and native context.");
        }
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("The stream requires current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("The stream requires current color registers."));
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("The stream requires current projection controls.");
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("The stream requires current renderer state.");
        NativeBattleModelRendererRegisters far = process.ModelRendererRegisters ?? throw new NotSupportedException("The stream requires current far-color controls.");
        int vertexCount = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        int cursor = checked(BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan()) + 12);
        context = Put(Put(context, 0x20, mesh.Address + 8), 0x24, checked(mesh.Address + (uint)cursor));
        int allocated = 0;
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        for (int group = 0; group < 2; group++)
        {
            int count = ReadInt(cursor);
            cursor += 4;
            bool gouraud = group != 0;
            int stride = gouraud ? 36 : 24;
            int length = gouraud ? 52 : 40;
            int screenStride = gouraud ? 12 : 8;
            if (count < 0 || count > (mesh.Bytes.Length - cursor) / stride)
            {
                throw new InvalidDataException("The stream polygon list leaves its current source.");
            }
            for (int face = 0; face < count; face++)
            {
                int current = cursor;
                NativeBattleActionMatrix? sourceMatrix = pilotIdentity.HasValue ? NativeBattleGeometry.CaptureMatrix(registers.Geometry) : null;
                System.Numerics.Vector3[]? world = pilotIdentity.HasValue ? new System.Numerics.Vector3[4] : null;
                for (int corner = 0; corner < 3; corner++)
                {
                    Load(corner, corner);
                }
                registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
                SkinWrites packet = new(length, allocated);
                packet.Word(0, gouraud ? 0x0C000000u : 0x09000000u);
                uint firstColor = Source(0);
                packet.Word(4, firstColor);
                int[] uvOffsets = gouraud ? [12, 24, 36, 48] : [12, 20, 28, 36];
                uint final = Source(20);
                uint[] uvs = [Source(12), Source(16), final, final >> 16];
                for (int index = 0; index < 4; index++)
                {
                    packet.Word(uvOffsets[index], uvs[index]);
                }
                uint flags = registers.Colors.Flags ?? throw new NotSupportedException("The stream requires actual projection flags.");
                context = Put(context, 0x34, flags);
                bool accepted = false;
                if ((flags & 0x60000) == 0)
                {
                    uint[] screen = new uint[4];
                    for (int corner = 0; corner < 3; corner++)
                    {
                        screen[corner] = registers.Geometry.Sxy[corner]!.Value;
                        packet.Word(8 + corner * screenStride, screen[corner]);
                    }
                    Load(3, 0);
                    registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
                    screen[3] = registers.Geometry.Sxy[2]!.Value;
                    packet.Word(8 + 3 * screenStride, screen[3]);
                    registers = NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("The stream requires actual ZSF4."));
                    bool outsideX = screen.All(value => unchecked((short)value) < 0 || unchecked((short)value) > 2560);
                    bool outsideY = screen.All(value => unchecked((short)(value >> 16)) < 0 || unchecked((short)(value >> 16)) > 1728);
                    if (!outsideX && !outsideY)
                    {
                        uint depth = registers.Geometry.Otz ?? throw new NotSupportedException("The stream requires current OTZ.");
                        context = Put(context, 0x30, depth);
                        uint[] vertexColors = gouraud ? [firstColor, Source(24), Source(28), Source(32)] : [firstColor];
                        uint fade = Dword(context, 8);
                        if (fade != 0)
                        {
                            byte[] rgb = context.Read(4, 3);
                            far = far.PublishFarColor45DDA0(rgb[0], rgb[1], rgb[2]);
                            if (gouraud)
                            {
                                NativeBattleScriptedColorRegisters loaded = NativeBattleScriptedDpct.LoadColors45E120(registers.Colors, vertexColors[1], vertexColors[2], vertexColors[3]) with { Ir0 = fade };
                                registers = registers with { Colors = NativeBattleScriptedDpct.Transform45F4C0(loaded, far, colors) };
                                for (int color = 1; color < 4; color++)
                                {
                                    vertexColors[color] = registers.Colors.Rgb[color - 1]!.Value;
                                }
                            }
                            NativeBattleScriptedDpcsResult first = NativeBattleScriptedDpcs.Transform45F270(registers.Colors, firstColor, fade, far, colors);
                            registers = registers with { Colors = first.Registers };
                            vertexColors[0] = first.PackedRgb;
                        }
                        for (int color = 0; color < vertexColors.Length; color++)
                        {
                            packet.Word(4 + color * 12, vertexColors[color]);
                        }
                        int amount = Word(context, 12);
                        int[] shifted = uvs.Select(value => (byte)value + amount).ToArray();
                        int adjustment = shifted.Any(value => value > 255) ? -context.Read(0x1C, 1)[0] : 0;
                        for (int corner = 0; corner < 4; corner++)
                        {
                            packet.Byte(uvOffsets[corner], unchecked((byte)(shifted[corner] + adjustment)));
                        }
                        int bucket = unchecked(tableBaseBucket + (unchecked((int)depth) >> depthShift));
                        ImmutableArray<int> payload = registers.Geometry.Sz.Select(value => unchecked((int)(value ?? throw new NotSupportedException("The stream requires all four actual depth FIFO entries.")))).ToImmutableArray();
                        NativeBattleGeometryPrimitive primitive = new(bucket, packet.Words(), payload)
                        {
                            PilotGeometry = pilotIdentity.HasValue ? new(pilotIdentity.Value with { Primitive = current }, world![0], world[1], world[2], world[3], 4) { UseSourceDepths = true, SourceDepths = new(payload[0], payload[1], payload[2], payload[3]) } : null
                        };
                        NativeBattleTextureWindow before = new(SignedWord(context, 0x10), SignedWord(context, 0x12), SignedWord(context, 0x14), SignedWord(context, 0x16));
                        NativeBattleTextureWindow after = new(SignedWord(context, 0x18), SignedWord(context, 0x1A), SignedWord(context, 0x1C), SignedWord(context, 0x1E));
                        NativeBattleGfGeometryBatch bracket = NativeBattleEffectTextureWindows.Bracket(packet.Attempt(null), primitive, before, after);
                        attempts.AddRange(bracket.Attempts);
                        allocated += bracket.AllocationBytes;
                        accepted = true;
                    }
                }
                if (!accepted)
                {
                    attempts.Add(packet.Attempt(null));
                }
                cursor += stride;
                context = Put(context, 0x24, checked(mesh.Address + (uint)cursor));

                uint Source(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan().Slice(current + offset, 4));
                void Load(int corner, int slot)
                {
                    int offset = checked(8 + BinaryPrimitives.ReadUInt16LittleEndian(mesh.Bytes.AsSpan().Slice(current + 4 + corner * 2, 2)) * 4);
                    if (offset < 8 || offset > 8 + vertexCount * 8 - 8)
                    {
                        throw new InvalidDataException("The stream corner leaves its current vertex bank.");
                    }
                    ReadOnlySpan<byte> row = mesh.Bytes.AsSpan().Slice(offset, 8);
                    NativeBattleVector vector = new(BinaryPrimitives.ReadInt16LittleEndian(row), BinaryPrimitives.ReadInt16LittleEndian(row[2..]), BinaryPrimitives.ReadInt16LittleEndian(row[4..]));
                    registers = NativeBattleGeometry.LoadVector(registers, slot, vector, BinaryPrimitives.ReadInt16LittleEndian(row[6..]));
                    if (world is not null)
                    {
                        world[corner] = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(sourceMatrix!, vector), projection);
                    }
                }
            }
            if (group == 0)
            {
                cursor += 12;
                context = Put(context, 0x24, checked(mesh.Address + (uint)cursor));
            }
        }
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors, PrimitiveProcess = process with { ModelRendererRegisters = far } }, context, new(attempts.ToImmutable(), allocated));

        int ReadInt(int offset) => BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(offset, 4));
    }
}
