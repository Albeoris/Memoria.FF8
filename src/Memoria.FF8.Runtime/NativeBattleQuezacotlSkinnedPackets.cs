using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>6C6A00's dual cursor projected overlay, original body layer and height-fade packets.</summary>
public static class NativeBattleQuezacotlSkinnedPackets
{
    public static NativeBattleQuezacotlSkinBatch Emit6C6A00(ReadOnlySpan<byte> face, IReadOnlyList<NativeBattleQuezacotlSkinnedVertex> vertices, NativeBattleQuezacotlSkinContext context, NativeBattleSummonColors colors, int commonOffset = 0, int effectOffset = 0)
    {
        bool quad = face.Length == 20;
        int count = quad ? 4 : 3;
        if (face.Length is not (16 or 20) || vertices.Count != count || context.DepthShift > 31 || context.WindowBefore3.IsDefault || context.WindowBefore3.Length != 3 || context.WindowAfter3.IsDefault || context.WindowAfter3.Length != 3)
        {
            throw new InvalidDataException("Native6C6A00 requires its original FT3/FT4 face, projected vertices and E2 templates.");
        }
        byte combined = 0;
        byte shared = 255;
        foreach (NativeBattleQuezacotlSkinnedVertex vertex in vertices)
        {
            combined |= vertex.Flags;
            shared &= vertex.Flags;
        }
        if (combined >= 0x10 || shared != 0)
        {
            return new([], 0, 0);
        }
        uint[] screens = vertices.Select(vertex => vertex.Screen ?? throw new NotSupportedException("Native6C6A00 requires its executed6C74D0 screen DWORD.")).ToArray();
        short[] fades = vertices.Select(vertex => vertex.Fade ?? throw new NotSupportedException("Native6C6A00 requires its actual height-fade WORD.")).ToArray();
        if (fades.Aggregate(0, (value, factor) => value | unchecked((ushort)factor)) == 0 || NativeBattleModulePackets.Nclip(screens[0], screens[1], screens[2]) < 0)
        {
            return new([], 0, 0);
        }
        ushort[] depths = vertices.Select(vertex => vertex.QuarterDepth ?? throw new NotSupportedException("Native6C6A00 requires its quarter-depth WORDs.")).ToArray();
        uint average = unchecked((uint)depths.Sum(depth => (int)depth) * (quad ? 0x100u : 0x155u)) >> 12;
        int bucket = checked(context.TableBaseBucket + ((int)average >> context.DepthShift));
        ImmutableArray<int> payload = quad ? [depths[0], depths[1], depths[2], depths[3]] : [depths[0], depths[1], depths[2], 0];
        ImmutableArray<NativeBattleQuezacotlSkinWrite>.Builder writes = ImmutableArray.CreateBuilder<NativeBattleQuezacotlSkinWrite>();
        uint sourceUv0 = BinaryPrimitives.ReadUInt32LittleEndian(face[8..]);
        uint sourceUv1 = BinaryPrimitives.ReadUInt32LittleEndian(face[12..]);
        ushort sourceUv2 = BinaryPrimitives.ReadUInt16LittleEndian(face[(quad ? 16 : 6)..]);
        ushort sourceUv3 = quad ? BinaryPrimitives.ReadUInt16LittleEndian(face[18..]) : (ushort)0;
        int effect = effectOffset;
        int packetLayer = 0;
        int commonLength = quad ? 40 : 32;
        Window(context.WindowBefore3);
        SkinWrites overlay = new(commonLength, commonOffset);
        overlay.Word(0, (uint)(commonLength / 4 - 1) << 24);
        overlay.Word(4, context.OverlayRgb | (quad ? 0x2E000000u : 0x26000000u));
        int[] us = Adjust(vertices.Select(vertex => (int)(vertex.U ?? throw new NotSupportedException("Native6C6A00 requires its generated IR1 texture BYTE."))).ToArray(), context.UOffset, context.UPeriod);
        int[] vs = Adjust(vertices.Select(vertex => (int)(vertex.V ?? throw new NotSupportedException("Native6C6A00 requires its generated IR2 texture BYTE."))).ToArray(), context.VOffset, context.VPeriod);
        for (int corner = 0; corner < count; corner++)
        {
            overlay.Word(8 + corner * 8, screens[corner]);
            overlay.Byte(12 + corner * 8, unchecked((byte)us[corner]));
            overlay.Byte(13 + corner * 8, unchecked((byte)vs[corner]));
        }
        overlay.Word16(14, context.Clut);
        overlay.Word16(22, context.TexturePage);
        Link(NativeBattleQuezacotlSkinBank.Common, overlay, payload);
        Window(context.WindowAfter3);
        int intersection = fades.Aggregate(0xFFFF, (value, factor) => value & unchecked((ushort)factor));
        if (intersection == 4096)
        {
            SkinWrites body = new(commonLength, effect);
            body.Word(0, (uint)(commonLength / 4 - 1) << 24);
            body.Word(4, context.ModelRgb | (quad ? 0x2C000000u : 0x24000000u));
            Body(body, false);
            Link(NativeBattleQuezacotlSkinBank.Effect, body, payload);
            effect = checked(effect + commonLength);
        }
        else
        {
            int texturedLength = quad ? 52 : 40;
            int flatLength = quad ? 36 : 28;
            SkinWrites body = new(texturedLength, effect);
            SkinWrites glow = new(flatLength, checked(effect + texturedLength));
            body.Word(0, (uint)(texturedLength / 4 - 1) << 24);
            body.Word(4, quad ? 0x3E000000u : 0x36000000u);
            glow.Word(0, (uint)(flatLength / 4 - 1) << 24);
            Body(body, true);
            for (int corner = 0; corner < count; corner++)
            {
                glow.Word(8 + corner * 8, screens[corner]);
            }
            writes.Add(new(NativeBattleQuezacotlSkinBank.Effect, body.Attempt(null)));
            writes.Add(new(NativeBattleQuezacotlSkinBank.Effect, glow.Attempt(null)));
            SkinWrites bodyColors = new(texturedLength, effect);
            int[] ir = [(byte)context.ModelRgb, (byte)(context.ModelRgb >> 8), (byte)(context.ModelRgb >> 16)];
            for (int corner = 0; corner < count; corner++)
            {
                //45E0E0 loads RGB once. Each45E9D0 uses the preceding IR result, so attenuation is cumulative.
                for (int component = 0; component < 3; component++)
                {
                    ir[component] = Math.Clamp(NativePrecision53.RoundInteger(ir[component] * (fades[corner] * (double)colors.FixedScale)), short.MinValue, short.MaxValue);
                    bodyColors.Byte(4 + corner * 12 + component, unchecked((byte)ir[component]));
                    body.Byte(4 + corner * 12 + component, unchecked((byte)ir[component]));
                }
            }
            Link(NativeBattleQuezacotlSkinBank.Effect, bodyColors, payload, body.Words());
            SkinWrites glowColors = new(flatLength, checked(effect + texturedLength));
            for (int corner = 0; corner < count; corner++)
            {
                int level = fades[corner] >> 5;
                uint rgb = unchecked((uint)(level * 0x010101));
                glowColors.Word(4 + corner * 8, rgb + (corner == 0 ? quad ? 0x3A000000u : 0x32000000u : 0));
                glow.Word(4 + corner * 8, rgb + (corner == 0 ? quad ? 0x3A000000u : 0x32000000u : 0));
            }
            Link(NativeBattleQuezacotlSkinBank.Effect, glowColors, payload, glow.Words());
            effect = checked(effect + texturedLength + flatLength);
            SkinWrites mode = new(8, effect);
            mode.Word(0, 0x01000000);
            mode.Word(4, 0xE1000240);
            Link(NativeBattleQuezacotlSkinBank.Effect, mode, [0, 0, 0, 0]);
            effect = checked(effect + 8);
        }
        return new(writes.ToImmutable(), commonLength, effect - effectOffset);

        void Window(ImmutableArray<uint> template)
        {
            SkinWrites window = new(12, effect);
            for (int index = 0; index < 3; index++)
            {
                window.Word(index * 4, template[index]);
            }
            Link(NativeBattleQuezacotlSkinBank.Effect, window, [0, 0, 0, 0]);
            effect = checked(effect + 12);
        }

        void Link(NativeBattleQuezacotlSkinBank bank, SkinWrites packet, ImmutableArray<int> vertexPayload, ImmutableArray<uint> words = default)
        {
            ImmutableArray<uint> command = words.IsDefault ? packet.Words() : words;
            uint opcode = command[1] >> 24 & 0xFC;
            // 45C7A0 selects the current depth FIFO by opcode, including command bits ORed from the model color.
            if (opcode is >= 0x20 and <= 0x3C)
            {
                if ((opcode & 8) != 0)
                {
                    vertexPayload = quad ? [depths[0], depths[1], depths[2], depths[3]] : [context.PriorDepthFifo ?? throw new NotSupportedException("Native45C7A0 quad opcode after only three quarter-depth loads requires the retained SZ0 WORD."), depths[0], depths[1], depths[2]];
                }
                else
                {
                    vertexPayload = quad ? [depths[1], depths[2], depths[3], 0] : [depths[0], depths[1], depths[2], 0];
                }
            }
            else
            {
                vertexPayload = [0, 0, 0, 0];
            }
            NativeBattleGeometryPrimitive primitive = new(bucket, command, vertexPayload);
            if ((command[1] >> 24 & 0xE0) == 0x20 && context.PilotIdentity is { } identity && vertices.All(vertex => vertex.World.HasValue) && (((command[1] >> 24 & 8) != 0) == quad))
            {
                primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 397 + (int)bank * 31 + packetLayer++) }, vertices[0].World!.Value, vertices[1].World!.Value, vertices[2].World!.Value, quad ? vertices[3].World!.Value : default, (byte)count) { DepthQuantum = 4, DepthDivisor = 4 } };
            }
            writes.Add(new(bank, packet.Attempt(primitive)));
        }

        void Body(SkinWrites packet, bool gouraud)
        {
            for (int corner = 0; corner < count; corner++)
            {
                packet.Word(8 + corner * (gouraud ? 12 : 8), screens[corner]);
            }
            packet.Word(12, sourceUv0);
            packet.Word(gouraud ? 24 : 20, sourceUv1);
            packet.Word16(gouraud ? 36 : 28, sourceUv2);
            if (quad)
            {
                packet.Word16(gouraud ? 48 : 36, sourceUv3);
            }
        }
    }

    private static int[] Adjust(int[] coordinates, int offset, byte period)
    {
        int minimum = coordinates.Min();
        if (coordinates.Aggregate(0, (combined, value) => combined | (value - minimum)) > 128)
        {
            coordinates = coordinates.Select(value => (value - 128) & 255).ToArray();
        }
        coordinates = coordinates.Select(value => unchecked(value + offset)).ToArray();
        if (coordinates.Aggregate(0, (combined, value) => combined | value) > 255)
        {
            coordinates = coordinates.Select(value => unchecked((byte)(value - period))).Select(value => (int)value).ToArray();
        }
        return coordinates;
    }
}
