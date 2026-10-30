using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect65Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect65Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect65Mesh
{
    public static NativeBattleEffect65Frame Draw6E4350(NativeBattleEffect65Frame frame, int depth, int x, int y, int z, int cutoff, uint cursorGlobal, NativeBattleEffect65Resources resources, NativeBattleEffect65Inputs inputs)
    {
        (frame, uint address) = Allocate(frame, 100);
        NativeBattleEffectArena temporary = Context(frame, address, 100);
        NativeBattleActionMatrix view = inputs.ReadView(frame).ReadMatrix();
        (int tx, int ty, int tz) = NativeBattleGfMatrices.RotateLong56C600(view, x, y, z);
        NativeBattleActionMatrix basis = view with { X = unchecked(tx + view.X), Y = unchecked(ty + view.Y), Z = unchecked(tz + view.Z) };
        temporary = StoreMatrix(temporary, 0, basis);
        uint source = checked(frame.State.ReadDword(ArenaGlobal) + 0xDB8);
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect65 point projection requires current projection controls.");
        for (int bank = 0; bank < 8; bank++)
        {
            uint literal = checked(LiteralBase + (uint)(bank * 16));
            NativeBattleActionMatrix first = NativeBattlePandemona291Restoration.Rotation701220(unchecked((short)frame.State.ReadWord(literal + 4)));
            NativeBattleActionMatrix second = NativeBattlePandemona291Restoration.Rotation701270(unchecked((short)frame.State.ReadWord(literal + 6)));
            temporary = temporary.Write(0x20, new byte[20]).Write(0x40, new byte[20]);
            temporary = StoreMatrix(temporary, 0x20, second, false);
            temporary = StoreMatrix(temporary, 0x40, first, false);
            NativeBattleActionMatrix combined = NativeBattleGfMatrices.Multiply56C090(second, first);
            temporary = StoreMatrix(temporary, 0x40, combined, false);
            combined = NativeBattleGfMatrices.Multiply56C090(basis, combined);
            temporary = StoreMatrix(temporary, 0x40, combined, false);
            NativeBattleGeometryRegisterStep registers = Registers(frame);
            registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, combined, true, false, 0) };
            registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, basis, false, true, null) };
            frame = Publish(frame, registers);
            for (int index = 0; index < 128; index++, source += 16)
            {
                NativeBattleEffectArena row = ReadBytes(frame, source, 16);
                if (Word(row, 6) >= cutoff)
                {
                    continue;
                }
                registers = NativeBattleGeometry.LoadVector(Registers(frame), 0, new(Word(row, 0), Word(row, 2), Word(row, 4)), Word(row, 6));
                System.Numerics.Vector3 world = inputs.CapturePresentation ? NativeBattleEffect65Presentation.LoadedWorld(registers, new(Word(row, 0), Word(row, 2), Word(row, 4)), inputs.ReadProjection(frame)) : default;
                registers = NativeBattleGeometry.ProjectSingle(registers, inputs.ReadProjection(frame), controls.Dqa, controls.Dqb);
                NativeBattleEffect65PacketWrites packet = new(12);
                packet.Dword(8, registers.Geometry.Sxy[2]!.Value);
                int quarterDepth = unchecked((int)registers.Geometry.Sz[3]!.Value) >> 2;
                temporary = Put(temporary, 0x60, unchecked((uint)quarterDepth));
                int? bucket = null;
                if (quarterDepth > 0)
                {
                    uint color;
                    byte blend = row.Read(0xB, 1)[0];
                    if (blend == 0)
                    {
                        color = Dword(row, 8);
                    }
                    else
                    {
                        registers = WeightPointColor(registers, Dword(row, 8), blend << 4, false, inputs.Colors);
                        registers = WeightPointColor(registers, Dword(row, 12), 4096 - (blend << 4), true, inputs.Colors);
                        color = unchecked((uint)(byte)registers.Colors.Ir[0]!.Value | ((uint)(byte)registers.Colors.Ir[1]!.Value << 8) | ((uint)(byte)registers.Colors.Ir[2]!.Value << 16));
                    }
                    packet.Dword(0, 0x02000000);
                    packet.Dword(4, (color & 0xFFFFFF) | 0x68000000);
                    int biased = Math.Max(0, unchecked((quarterDepth >> 2) + depth));
                    temporary = Put(temporary, 0x60, unchecked((uint)biased));
                    bucket = unchecked(17 + biased);
                }
                frame = PublishContext(Publish(frame, registers), address, temporary, 100);
                NativeBattleGfGeometryAttempt attempt = packet.Attempt(0, bucket, bucket is null ? [] : [0, 0, 0, 0]);
                if (attempt.Primitive is { } primitive && NativeBattleEffect65Presentation.Source(frame, inputs, source, bank * 128 + index) is { } identity)
                {
                    System.Numerics.Vector3 projected = NativeBattlePilotVisualGeometry.Project(world, inputs.ReadProjection(frame));
                    uint screen = registers.Geometry.Sxy[2]!.Value;
                    System.Numerics.Vector2 delta = new(unchecked((short)screen) - projected.X, unchecked((short)(screen >> 16)) - projected.Y);
                    attempt = attempt with { Primitive = primitive with { PilotGeometry = new(identity, world, world, world, world, 4) { ScreenOffsets = [delta, delta + new System.Numerics.Vector2(8, 0), delta + new System.Numerics.Vector2(0, 8), delta + new System.Numerics.Vector2(8, 8)], SourceDepths = System.Numerics.Vector4.Zero, UseSourceDepths = true } } };
                }
                frame = inputs.Execute(frame, new NativeBattleEffect65DeliverBatch(new([attempt], bucket is null ? 0 : 12), cursorGlobal));
                temporary = Context(frame, address, 100);
            }
        }
        return Release(frame, 100);
    }

    private static NativeBattleGeometryRegisterStep WeightPointColor(NativeBattleGeometryRegisterStep registers, uint color, int factor, bool accumulate, NativeBattleSummonColors constants)
    {
        registers = NativeBattleGeometry.LoadIrColumn(registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)factor) } }, (ushort)(color & 255), (ushort)((color >> 8) & 255), (ushort)((color >> 16) & 255));
        return NativeBattleRaldo19BallRendering.Weighted(registers, accumulate, constants);
    }
}
