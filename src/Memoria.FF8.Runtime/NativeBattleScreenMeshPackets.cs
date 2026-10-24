using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScreenMeshFace(NativeBattleEffectMemory Memory, NativeBattleGfGeometryAttempt? Attempt);

public static class NativeBattleScreenMeshPackets
{
    public static NativeBattleScreenMeshFace EmitFace(NativeBattleEffectMemory memory, NativeBattleEffectArena face, ImmutableArray<NativeBattleEffectArena> rows, bool quad, bool textured, int textureX, int textureY, int bucket, NativeBattleSummonColors constants)
    {
        int corners = quad ? 4 : 3;
        if (rows.IsDefault || rows.Length != corners)
        {
            throw new InvalidDataException("Screen mesh requires its actual corner rows.");
        }
        if (textured)
        {
            int commonClip = 255;
            foreach (NativeBattleEffectArena row in rows)
            {
                commonClip &= row.Read(8, 1)[0];
            }
            if (commonClip != 0)
            {
                return new(memory, null);
            }
        }
        int length = textured ? (quad ? 40 : 32) : (quad ? 36 : 28);
        byte[] bytes = new byte[length];
        NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[length];
        Dword(0, (uint)(length / 4 - 1) << 24);
        if (textured)
        {
            Dword(4, quad ? 0x2E808080u : 0x26808080u);
            int u0 = BinaryPrimitives.ReadUInt16LittleEndian(rows[0].Read(4, 2));
            int baseU = u0 & 0xC0;
            int x = unchecked(textureX + u0);
            ushort page = unchecked((ushort)(((x >> 6) & 15) | (((textureY & 0x200) | 0x40) << 2) | ((textureY >> 4) & 0x10)));
            Store(22, BitConverter.GetBytes(page));
            for (int corner = 0; corner < corners; corner++)
            {
                Dword(8 + corner * 8, BinaryPrimitives.ReadUInt32LittleEndian(rows[corner].Read(0, 4)));
                byte u = unchecked((byte)(BinaryPrimitives.ReadUInt16LittleEndian(rows[corner].Read(4, 2)) - baseU));
                byte v = rows[corner].Read(6, 1)[0];
                Store(12 + corner * 8, [u, v]);
            }
        }
        else
        {
            NativeBattleScriptedColorRegisters colors = memory.ScriptedColorRegisters ?? throw new NotSupportedException("Screen color pass requires its current arithmetic owner.");
            NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Screen color pass requires its current primitive process.");
            NativeBattleModelRendererRegisters far = process.ModelRendererRegisters ?? throw new NotSupportedException("Screen color pass requires its current far-color owner.");
            uint cue = colors.Ir0 ?? throw new NotSupportedException("Screen color pass requires its current IR0.");
            for (int corner = 0; corner < corners; corner++)
            {
                uint color = BinaryPrimitives.ReadUInt32LittleEndian(face.Read(corner == 0 ? 0 : 8 + corner * 4, 4));
                colors = NativeBattleScriptedDpcs.Transform45F270(colors, color, cue, far, constants).Registers;
                uint shaded = colors.Rgb[2] ?? throw new InvalidDataException("Screen color pass did not publish RGB2.");
                if (corner == 0)
                {
                    shaded = (shaded & 0xFFFFFF) | (quad ? 0x3A000000u : 0x32000000u);
                }
                Dword(4 + corner * 8, shaded);
                Dword(8 + corner * 8, BinaryPrimitives.ReadUInt32LittleEndian(rows[corner].Read(4, 4)));
            }
            memory = memory with { ScriptedColorRegisters = colors };
        }
        ImmutableArray<uint> words = Enumerable.Range(0, bytes.Length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToImmutableArray();
        NativeBattleGeometryPrimitive primitive = new(bucket, words, []);
        NativeBattleGfGeometryAttempt attempt = new(0, bytes.ToImmutableArray(), mask.ToImmutableArray(), primitive);
        return new(memory, attempt);

        void Store(int offset, ReadOnlySpan<byte> data)
        {
            data.CopyTo(bytes.AsSpan(offset));
            Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, data.Length);
        }
        void Dword(int offset, uint value) => Store(offset, BitConverter.GetBytes(value));
    }
}
