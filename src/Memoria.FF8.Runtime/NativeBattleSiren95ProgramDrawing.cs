using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSiren95ProgramDrawServices(Func<NativeBattleSiren95Frame, NativeBattleGfGeometryBatch, uint?, NativeBattleSiren95Frame> Submit);

public sealed record NativeBattleSiren95ProgramAdvance(NativeBattleSiren95Frame Frame, int Remaining);

public static class NativeBattleSiren95ProgramDrawing
{
    private static readonly (int Source, uint Destination)[] Copy73EB10 =
    [
        (0x48, 0x258e9a0),
        (0x4c, 0x258e9a4),
        (0x58, 0x258e9a8),
        (0x5c, 0x258e9ac),
        (0x68, 0x258e9b0),
        (0x6c, 0x258e9b4),
        (0x78, 0x258e9b8),
        (0x7c, 0x258e9bc),
        (0x88, 0x258e9c0),
        (0x8c, 0x258e9c4),
        (0x98, 0x258e9c8),
        (0x9c, 0x258e9cc),
        (0xa8, 0x258e9d0),
        (0xac, 0x258e9d4),
        (0xb8, 0x258e9d8),
        (0xbc, 0x258e9dc),
        (0xc8, 0x258e9e0),
        (0xcc, 0x258e9e4),
        (0xd8, 0x258e9e8),
        (0xdc, 0x258e9ec),
        (0xe8, 0x258e9f0),
        (0xec, 0x258e9f4),
        (0xf8, 0x258e9f8),
        (0xfc, 0x258e9fc),
        (0x108, 0x258ea00),
        (0x10c, 0x258ea04),
        (0x118, 0x258ea08),
        (0x11c, 0x258ea0c),
        (0x128, 0x258ea10),
        (0x12c, 0x258ea14),
        (0x138, 0x258ea18),
        (0x13c, 0x258ea1c),
        (0x0, 0x258ea20),
        (0x4, 0x258ea24),
        (0x40, 0x258e8a0),
        (0x44, 0x258e8a4),
        (0x50, 0x258e8a8),
        (0x54, 0x258e8ac),
        (0x60, 0x258e8b0),
        (0x64, 0x258e8b4),
        (0x70, 0x258e8b8),
        (0x74, 0x258e8bc),
        (0x80, 0x258e8c0),
        (0x84, 0x258e8c4),
        (0x90, 0x258e8c8),
        (0x94, 0x258e8cc),
        (0xa0, 0x258e8d0),
        (0xa4, 0x258e8d4),
        (0xb0, 0x258e8d8),
        (0xb4, 0x258e8dc),
        (0xc0, 0x258e8e0),
        (0xc4, 0x258e8e4),
        (0xd0, 0x258e8e8),
        (0xd4, 0x258e8ec),
        (0xe0, 0x258e8f0),
        (0xe4, 0x258e8f4),
        (0xf0, 0x258e8f8),
        (0xf4, 0x258e8fc),
        (0x100, 0x258e900),
        (0x104, 0x258e904),
        (0x110, 0x258e908),
        (0x114, 0x258e90c),
        (0x120, 0x258e910),
        (0x124, 0x258e914),
        (0x130, 0x258e918),
        (0x134, 0x258e91c),
        (0x8, 0x258e920),
        (0xc, 0x258e924),
    ];

    private static readonly (int Source, uint Destination)[] Copy73E7F0 =
    [
        (0x88, 0x258baa0),
        (0x8c, 0x258baa4),
        (0x98, 0x258baa8),
        (0x9c, 0x258baac),
        (0xa8, 0x258bab0),
        (0xac, 0x258bab4),
        (0xb8, 0x258bab8),
        (0xbc, 0x258babc),
        (0xc8, 0x258bac0),
        (0xcc, 0x258bac4),
        (0xd8, 0x258bac8),
        (0xdc, 0x258bacc),
        (0xe8, 0x258bad0),
        (0xec, 0x258bad4),
        (0xf8, 0x258bad8),
        (0xfc, 0x258badc),
        (0x108, 0x258bae0),
        (0x10c, 0x258bae4),
        (0x118, 0x258bae8),
        (0x11c, 0x258baec),
        (0x128, 0x258baf0),
        (0x12c, 0x258baf4),
        (0x138, 0x258baf8),
        (0x13c, 0x258bafc),
        (0x148, 0x258bb00),
        (0x14c, 0x258bb04),
        (0x158, 0x258bb08),
        (0x15c, 0x258bb0c),
        (0x168, 0x258bb10),
        (0x16c, 0x258bb14),
        (0x178, 0x258bb18),
        (0x17c, 0x258bb1c),
        (0x188, 0x258bb20),
        (0x18c, 0x258bb24),
        (0x198, 0x258bb28),
        (0x19c, 0x258bb2c),
        (0x1a8, 0x258bb30),
        (0x1ac, 0x258bb34),
        (0x1b8, 0x258bb38),
        (0x1bc, 0x258bb3c),
        (0x1c8, 0x258bb40),
        (0x1cc, 0x258bb44),
        (0x1d8, 0x258bb48),
        (0x1dc, 0x258bb4c),
        (0x1e8, 0x258bb50),
        (0x1ec, 0x258bb54),
        (0x1f8, 0x258bb58),
        (0x1fc, 0x258bb5c),
        (0x200, 0x258bb60),
        (0x204, 0x258bb64),
    ];

    public static NativeBattleSiren95Frame Advance73C280(NativeBattleSiren95Frame frame, uint address, NativeBattleGfAnimationSource source, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattleSiren95ProgramDrawServices services)
    {
        return AdvanceWithResult73C280(frame, address, source, view, projection, colors, services).Frame;
    }

    public static NativeBattleSiren95ProgramAdvance AdvanceWithResult73C280(NativeBattleSiren95Frame frame, uint address, NativeBattleGfAnimationSource source, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattleSiren95ProgramDrawServices services)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (task.ReadDword(0x74) != source.Address || task.PayloadLength < 0x94 - 12)
        {
            throw new InvalidDataException("Native Siren programme requires its actual source and retained workspace.");
        }
        NativeBattleActionMatrix context = NativeBattleSearchProgramDrawing.ComposeContext758D70(task, view);
        NativeBattleGfAnimationWorkspace workspace = new(task.ReadRawPayloadSpan().Slice(0x94 - 12).ToArray().ToImmutableArray());
        int frozen = unchecked((int)State(frame).ReadDword(0x2585E38));
        NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, frozen, (current, node) =>
        {
            if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
            {
                return current;
            }
            int geometry = ReadInt(source, checked(8 + 4 * unchecked((short)node.GeometryIndex)));
            int count = ReadInt(source, checked(geometry + 4));
            if (count < 1 || count > source.Data.Length / 8)
            {
                throw new NotSupportedException("Native Siren mesh requires its consumed positive vertex extent.");
            }
            bool morph = node.Word40 != node.Word42 && node.Scalar38 is not (0 or 4096);
            int selected = 0;
            if (morph)
            {
                if ((long)count * 8 > 0x258FB40 - 0x258EF98)
                {
                    throw new NotSupportedException("Native Siren morph leaves its retained addressed scratch window.");
                }
                using NativeBattleEffectArenaWriter output = new(State(frame).Globals);
                uint destination = 0x258EF98;
                foreach (NativeBattleVector vertex in NativeBattleGfMorphGeometry.MorphVertices701390(source.Data, geometry, node.Word40, node.Word42, node.Scalar38))
                {
                    byte[] xyz = new byte[6];
                    BinaryPrimitives.WriteInt16LittleEndian(xyz, vertex.X);
                    BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(2), vertex.Y);
                    BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(4), vertex.Z);
                    output.Write(checked((int)(destination - NativeBattleSiren95State.GlobalAddress)), xyz);
                    destination = unchecked(destination + 8);
                }
                frame = frame with { Memory = frame.Memory with { Siren95 = State(frame) with { Globals = output.Freeze() } } };
            }
            else
            {
                int shape = node.Word40 == node.Word42 || node.Scalar38 == 0 ? node.Word40 : node.Word42;
                selected = checked(geometry + 12 + shape * count * 8);
            }
            NativeBattleActionMatrix matrix = NativeBattleChoco97ProgramDrawing.ComposeNodeMatrix(node, context, false, false);
            uint flags = (node.Header & 0x4000) != 0 ? 0x2000u : 0x2030u;
            if ((node.Header & 0x2000) != 0)
            {
                flags |= 12;
            }
            int fade = node.Scalar36;
            uint? far = fade != 0 ? node.Color32 : null;
            if (fade == 0 && task.ReadDword(0x7C) != 0)
            {
                fade = unchecked((int)task.ReadDword(0x7C));
                far = task.ReadDword(0x70);
            }
            if (fade != 0)
            {
                flags |= 0xC0;
            }
            NativeBattleTextureWindow full = new(0, 0, 256, 256);
            NativeBattleTextureWindow after = full;
            ushort u = 0;
            short mode = unchecked((short)task.ReadWord(0x80));
            if (mode is 0 or 1 || mode == 2 && node.NodeIndex == 3)
            {
                u = task.ReadWord(mode == 1 || node.NodeIndex == 0 || mode == 2 ? 0x8C : 0x90);
                after = new(0, 0, 128, 256);
            }
            frame = frame with { Memory = frame.Memory with { NodeScratch2793E30 = NativeBattleSiren95Scratch.Programme(frame.Memory.NodeScratch2793E30, u, after) } };
            NativeBattleEsunaMeshContext operands = new(flags, far, fade, unchecked((short)task.ReadWord(0x88)), u, 0, full, after, 17, 2);
            int geometryEnd = node.GeometryIndex + 1 < ReadInt(source, 0) ? ReadInt(source, checked(12 + 4 * node.GeometryIndex)) : ReadInt(source, 4);
            ImmutableArray<byte> mesh = Source(geometry, checked(geometryEnd - geometry)).ToArray().ToImmutableArray();
            NativeBattleGfGeometryBatch batch = NativeBattleRestorationMeshPackets.EmitBatch88E240(mesh, matrix, projection, operands, colors, offset =>
            {
                if (offset < 0 || (offset & 7) != 0 || offset > count * 8 - 8)
                {
                    throw new InvalidDataException("Native Siren vertex read leaves its selected shape.");
                }
                byte[] xyz = Selected(offset, 6);
                return new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4)));
            });
            frame = services.Submit(frame, batch, far);
            if (mode == 1 || mode == 0 && node.NodeIndex == 0)
            {
                NativeBattleSiren95State state = State(frame);
                state = state.Write(0x258FB80, node.Data.AsSpan().Slice(8, 6));
                state = state.WriteDword(0x258EC60, unchecked((uint)(int)node.Vector24.X)).WriteDword(0x258EC64, unchecked((uint)(int)node.Vector24.Y)).WriteDword(0x258EC68, unchecked((uint)(int)node.Vector24.Z));
                frame = frame with { Memory = frame.Memory with { Siren95 = state } };
                using NativeBattleEffectArenaWriter output = new(state.Globals);
                foreach ((int input, uint destination) in mode == 1 ? Copy73E7F0 : Copy73EB10)
                {
                    if (input < 0 || input > count * 8 - 4)
                    {
                        throw new NotSupportedException("Native Siren selected vertex publication leaves its actual consumed shape.");
                    }
                    bool unknown = morph && State(frame).Globals.EnumerateUnknownRanges().Any(region => region.Offset < 0x258EF98 - NativeBattleSiren95State.GlobalAddress + input + 4 && region.Offset + region.Length > 0x258EF98 - NativeBattleSiren95State.GlobalAddress + input);
                    int target = checked((int)(destination - NativeBattleSiren95State.GlobalAddress));
                    if (!unknown)
                    {
                        output.Write(target, Selected(input, 4));
                    }
                    else
                    {
                        for (int lane = 0; lane < 4; lane++)
                        {
                            int origin = checked((int)(0x258EF98 - NativeBattleSiren95State.GlobalAddress + input + lane));
                            if (State(frame).Globals.EnumerateUnknownRanges().Any(region => origin >= region.Offset && origin < region.Offset + region.Length))
                            {
                                output.MarkUnknown(target + lane, 1);
                            }
                            else
                            {
                                output.Write(target + lane, Selected(input + lane, 1));
                            }
                        }
                    }
                }
                frame = frame with { Memory = frame.Memory with { Siren95 = State(frame) with { Globals = output.Freeze() } } };
            }
            return current;

            ReadOnlySpan<byte> Source(int offset, int length)
            {
                if (source.Data.IsDefault || offset < 0 || length < 0 || offset > source.Data.Length - length)
                {
                    throw new InvalidDataException("Native Siren geometry read leaves its supplied source.");
                }
                return source.Data.AsSpan().Slice(offset, length);
            }
            byte[] Selected(int offset, int length) => morph ? State(frame).Read(unchecked(0x258EF98u + (uint)offset), length) : Source(checked(selected + offset), length).ToArray();
        });
        task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
        task = task.WriteRetainedBytes(0x94, advanced.Workspace.Data.AsSpan());
        return new(frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } }, advanced.Remaining);
    }

    private static int ReadInt(NativeBattleGfAnimationSource source, int offset)
    {
        if (source.Data.IsDefault || offset < 0 || offset > source.Data.Length - 4)
        {
            throw new InvalidDataException("Native Siren source DWORD read leaves its current supplied span.");
        }
        return BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(offset, 4));
    }

    private static NativeBattleSiren95State State(NativeBattleSiren95Frame frame) => frame.Memory.Siren95 ?? throw new NotSupportedException("Native Siren programme requires its raw module owner.");
}
