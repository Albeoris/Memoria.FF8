using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect207Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect207Lower
{
    public static NativeBattleEffect207Frame Draw5700(NativeBattleEffect207Frame frame, uint source, uint vertices, int bucket, int fade, int palette, NativeBattleEffect207Resources resources, NativeBattleEffect207Inputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        ImmutableArray<byte> environment = frame.Memory.PrimitiveProcess?.Software?.EnvironmentBuffers?.CurrentGlobal ?? throw new NotSupportedException("Effect207 warp requires its current drawing environment.");
        if (environment.IsDefault || environment.Length != 92)
        {
            throw new InvalidDataException("Effect207 warp requires its complete drawing environment.");
        }
        int count = checked((int)frame.State.ReadDword(source + 4));
        if (source != NativeBattleEffect207Profile.MutableMeshAddress || vertices != source + 8 || count < 1 || count > 241)
        {
            throw new InvalidDataException("Effect207 warp requires its current copied mesh rows.");
        }
        uint output = checked(frame.State.ReadDword(NativeBattleEffect207Profile.ArenaGlobal) + 0xEE0);
        for (int index = 0; index < count; index++)
        {
            uint point = vertices + (uint)(index * 8);
            int factor = resources.PaletteWord(palette, unchecked((short)frame.State.ReadWord(point + 6)));
            short x = unchecked((short)frame.State.ReadWord(point));
            short y = unchecked((short)frame.State.ReadWord(point + 2));
            NativeBattleVector scaled = new(unchecked((short)(unchecked(x * factor) >> 12)), unchecked((short)(unchecked(y * factor) >> 12)), 0);
            NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(Registers(frame), 0, scaled, 0));
            short screenX = unchecked((short)(registers.Colors.Ir[0] ?? throw new InvalidDataException("Effect207 warp did not publish IR1.")));
            short screenY = unchecked((short)(registers.Colors.Ir[1] ?? throw new InvalidDataException("Effect207 warp did not publish IR2.")));
            uint destination = output + (uint)(index * 12);
            NativeBattleEffectArena row = L207Word(L207Word(ReadBytes(frame, destination, 12), 0, screenX), 2, screenY);
            registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, new(x, y, 0), 0));
            short u = unchecked((short)registers.Colors.Ir[0]!.Value);
            short v = unchecked((short)registers.Colors.Ir[1]!.Value);
            if (screenX < 0)
            {
                row = row.Write(8, [(byte)(row.Read(8, 1)[0] | 1)]);
            }
            else if (screenX >= 320)
            {
                row = row.Write(8, [(byte)(row.Read(8, 1)[0] | 2)]);
            }
            else
            {
                row = L207Word(row, 8, 0);
            }
            if (screenY < 0)
            {
                row = row.Write(8, [(byte)(row.Read(8, 1)[0] | 4)]);
            }
            else if (screenY >= 216)
            {
                row = row.Write(8, [(byte)(row.Read(8, 1)[0] | 8)]);
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
            row = L207Word(L207Word(row, 4, u), 6, v);
            frame = Publish(frame, registers);
            frame = WriteBytes(frame, destination, row, 12);
        }
        (frame, uint temporary) = Allocate(frame, 72);
        uint polygon = checked(source + frame.State.ReadDword(source) + 16);
        NativeBattleEffectArena context = L207Put(L207Put(L207Put(L207Put(L207Put(Context(frame, temporary, 72), 0, source), 4, output), 8, 0x240), 12, 0x100), 16, polygon).MarkUnknown(20, 4);
        frame = PublishContext(frame, temporary, context, 72);
        frame = L207DrawMode(frame, bucket, 0xE1000220, inputs);
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect207 warp requires its current primitive process.");
        NativeBattleModelRendererRegisters far = process.ModelRendererRegisters ?? throw new NotSupportedException("Effect207 warp requires its current far-color owner.");
        NativeBattleScriptedColorRegisters colors = frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect207 warp requires its current arithmetic owner.");
        frame = frame with { Memory = frame.Memory with { ScriptedColorRegisters = colors with { Ir0 = unchecked((uint)fade) }, PrimitiveProcess = process with { ModelRendererRegisters = far with { FarRed = 0, FarGreen = 0, FarBlue = 0 } } } };
        frame = L207DrawDirect(frame, temporary, false, false, bucket, inputs, pilotIdentity);
        frame = L207DrawDirect(frame, temporary, true, false, bucket, inputs, pilotIdentity);
        frame = L207DrawMode(frame, bucket, 0xE1000240, inputs);
        frame = PublishContext(frame, temporary, L207Put(Context(frame, temporary, 72), 16, polygon), 72);
        frame = L207DrawDirect(frame, temporary, false, true, bucket, inputs, pilotIdentity);
        frame = L207DrawDirect(frame, temporary, true, true, bucket, inputs, pilotIdentity);
        return Release(frame, 72);
    }

    private static NativeBattleEffect207Frame L207DrawDirect(NativeBattleEffect207Frame frame, uint temporary, bool quad, bool textured, int bucket, NativeBattleEffect207Inputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity)
    {
        NativeBattleEffectArena context = Context(frame, temporary, 72);
        uint cursor = L207U(context, 16);
        int count = checked((int)frame.State.ReadDword(cursor));
        cursor += 4;
        context = L207Put(context, 16, cursor);
        int corners = quad ? 4 : 3;
        for (int index = 0; index < count; index++)
        {
            NativeBattleEffectArena[] rows = new NativeBattleEffectArena[corners];
            uint vertices = L207U(context, 4);
            for (int corner = 0; corner < corners; corner++)
            {
                uint row = checked(vertices + frame.State.ReadWord(cursor + 4 + (uint)(corner * 2)) * 4u);
                rows[corner] = ReadBytes(frame, row, 12);
                context = CopyBytes(rows[corner], 0, context, 24 + corner * 12, 12);
            }
            NativeBattleScreenMeshFace drawn = NativeBattleScreenMeshPackets.EmitFace(frame.Memory, frame.State.ReadView(cursor, quad ? 24 : 20), rows.ToImmutableArray(), quad, textured, textured ? unchecked((int)L207U(context, 8)) : 0, textured ? unchecked((int)L207U(context, 12)) : 0, bucket, inputs.Colors);
            frame = frame with { Memory = drawn.Memory };
            if (drawn.Attempt is { } attempt)
            {
                if (attempt.Primitive is { } primitive && pilotIdentity is { } identity)
                {
                    int role = (textured ? 2 : 0) + (quad ? 1 : 0);
                    attempt = attempt with { Primitive = NativeBattleEffect210VisualCapture.Screen(primitive, identity with { Primitive = unchecked(identity.Primitive * 397 + role * 65537 + index) }, 8, true) };
                }
                frame = PublishContext(frame, temporary, context, 72);
                frame = inputs.Execute(frame, new NativeBattleEffect207DrawBatch(new([attempt], attempt.PacketLength), L207Cursor, 1));
            }
            cursor += (uint)(quad ? 24 : 20);
            context = L207Put(context, 16, cursor);
        }
        return PublishContext(frame, temporary, context, 72);
    }

    private static NativeBattleEffect207Frame L207DrawMode(NativeBattleEffect207Frame frame, int bucket, uint mode, NativeBattleEffect207Inputs inputs)
    {
        byte[] data = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(data, 0x01000000);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), mode);
        NativeBattleGeometryPrimitive primitive = new(bucket, [0x01000000, mode], [0, 0, 0, 0]);
        NativeBattleGfGeometryAttempt attempt = new(0, data.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 8).ToImmutableArray(), primitive);
        return inputs.Execute(frame, new NativeBattleEffect207DrawBatch(new([attempt], 8), L207Cursor));
    }
}
