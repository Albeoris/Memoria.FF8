using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDiablos325NodeDraw(NativeBattleEffectMemory Memory, NativeBattleGfGeometryBatch Packets);

public static partial class NativeBattleDiablos325Programme
{
    public static NativeBattleDiablos325NodeDraw Emit656B70(NativeBattleEffectMemory memory, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattleVector position, int depthBias, int fade, ImmutableArray<byte>? primitiveKinds, ImmutableArray<byte>? textureKinds, NativeBattleActionMatrix? effectCamera = null, NativeBattleVector? contextScale = null, byte texturePhase = 0)
    {
        if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
        {
            return new(memory, new([], 0));
        }
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Diablos programme requires its current camera basis.");

        ImmutableArray<byte> kindBytes = primitiveKinds ?? throw new NotSupportedException("Native Diablos programme requires its original primitive selector bytes.");
        ImmutableArray<byte> textureBytes = textureKinds ?? throw new NotSupportedException("Native Diablos programme requires its original texture selector bytes.");
        int index = unchecked((short)node.GeometryIndex);
        if ((uint)index >= kindBytes.Length || (uint)index >= textureBytes.Length)
        {
            throw new InvalidDataException("Native Diablos programme geometry index leaves its original selector tables.");
        }
        int geometry = Int(source.Data, checked(8 + index * 4));
        int count = Int(source.Data, checked(geometry + 4));
        int selected = 0;
        ImmutableArray<NativeBattleVector> morph = [];
        bool morphed = false;
        if (node.Word40 == node.Word42 || node.Scalar38 is 0 or 4096)
        {
            int shape = node.Word40 == node.Word42 || node.Scalar38 == 0 ? node.Word40 : node.Word42;
            selected = checked(geometry + 12 + shape * count * 8);
        }
        else
        {
            morphed = true;
            morph = NativeBattleGfMorphGeometry.MorphVertices701390(source.Data, geometry, node.Word40, node.Word42, node.Scalar38);
            using NativeBattleEffectArenaWriter writer = new(memory.Arena ?? throw new NotSupportedException("Native Diablos morph requires its current effect arena."));
            for (int vertex = 0; vertex < morph.Length; vertex++)
            {
                byte[] xyz = new byte[6];
                BinaryPrimitives.WriteInt16LittleEndian(xyz, morph[vertex].X);
                BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(2), morph[vertex].Y);
                BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(4), morph[vertex].Z);
                writer.Write(checked(0xD94 + vertex * 8), xyz);
            }
            memory = memory with { Arena = writer.Freeze() };
        }
        uint flags = (node.Header & 0x4000) != 0 ? 0x2000u : 0x2030u;
        if ((node.Header & 0x2000) != 0)
        {
            flags |= 12;
        }
        uint? far = node.Scalar36 != 0 ? node.Color32 : null;
        int cue = node.Scalar36;
        if (node.Scalar36 != 0)
        {
            flags |= 0xC0;
        }
        if (fade != 0)
        {
            flags |= 0xC0;
            far = 0;
            cue = fade;
        }
        NativeBattleActionMatrix matrix = Compose656B70(node, position, view, contextScale, effectCamera);
        NativeBattleGfGeometryContext context = new(flags, far ?? 0, cue, null, null, depthBias, 17, 2);
        NativeBattleGfGeometryBatch batch;
        if (kindBytes[index] == 0x30 && textureBytes[index] == 0x31)
        {
            NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Native Diablos windowed geometry requires its current depth averaging controls.");
            NativeBattleEsunaMeshContext windowed = new(flags, far, cue, depthBias, 0, (ushort)(texturePhase & 127), new(0, 0, 256, 256), new(0, 128, 256, 128), 17, 2);
            batch = NativeBattlePandemona291Surface.EmitGeometry701DD0(source.Data.AsSpan().Slice(geometry).ToArray().ToImmutableArray(), matrix, windowed, projection, colors, controls.Zsf4 ?? throw new NotSupportedException("Native Diablos windowed geometry requires its current ZSF4 control."), Vertex, zsf3: controls.Zsf3);
        }
        else
        {
            batch = kindBytes[index] == 0x30
                ? NativeBattleGfGeometryPackets.EmitBatch7043B0(source.Data, geometry, default, matrix, projection, context, colors, Vertex)
                : Alternate(source.Data, geometry, matrix, projection, context, colors, Vertex);
        }
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native Diablos programme requires its current far-color controls.");
        NativeBattleModelRendererRegisters registers = process.ModelRendererRegisters ?? new(null, null, null, null, null);
        registers = far is uint rgb ? registers.PublishModelColor(rgb) : registers with { FarRed = null, FarGreen = null, FarBlue = null };
        uint selectedAddress = morphed ? checked(NativeBattleDiablos325Entry.RequireState(memory).ModelBuffer5208 + 0xD94) : checked(source.Address + (uint)selected);
        memory = memory with { PrimitiveProcess = process with { ModelRendererRegisters = registers, Temporary5082B0Word4 = selectedAddress } };
        return new(memory, batch);

        NativeBattleVector Vertex(int offset)
        {
            if (offset < 0 || (offset & 7) != 0 || offset / 8 >= count)
            {
                throw new InvalidDataException("Native Diablos selected vertex leaves its actual shape bank.");
            }
            if (morphed)
            {
                return morph[offset / 8];
            }
            ReadOnlySpan<byte> bytes = source.Data.AsSpan().Slice(checked(selected + offset), 6);
            return new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes[2..]), BinaryPrimitives.ReadInt16LittleEndian(bytes[4..]));
        }
    
    }
}
