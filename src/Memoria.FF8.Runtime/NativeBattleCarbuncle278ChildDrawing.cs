using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCarbuncle278ChildDraw(NativeBattleEffectMemory Memory, NativeBattleModulePoolTask Task, NativeBattleGfGeometryBatch Packets);

/// <summary>Recipient ray matrices and actor-space shard packets, using the cached physical parent matrix.</summary>
public static class NativeBattleCarbuncle278ChildDrawing
{
    public static NativeBattleActionMatrix Prepare683760(NativeBattleModulePoolTask task, NativeBattleModulePoolTask parent, NativeBattleActionMatrix cached, ImmutableArray<byte> table)
    {
        NativeBattleVector point = NativeBattleCarbuncle278RecipientChildren.Polar6836F0(Vector(parent, 0x54), unchecked((short)task.ReadWord(0x10)), table);
        NativeBattleFullMacVertex translation = NativeBattleGfMatrices.TransformMac460840(cached, point);
        short age = unchecked((short)task.ReadWord(0xC));
        short scaleY = unchecked((short)(age <= 10 ? 4096 : 4096 - (age - 10) * 4096 / 6));
        short angle = unchecked((short)task.ReadWord(0x10));
        short sine = unchecked((short)NativeBattleCameraMath.Sine(angle));
        short cosine = unchecked((short)NativeBattleCameraMath.Cosine(angle));
        NativeBattleActionMatrix z = new([cosine, sine, 0, unchecked((short)-sine), cosine, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleActionMatrix scaled = NativeBattleGfMatrices.Multiply56C090(z, new([4096, 0, 0, 0, scaleY, 0, 0, 0, 4096], 0, 0, 0, 4096));
        scaled = NativeBattleGfMatrices.Multiply56C090(cached, scaled);
        short xAngle = unchecked((short)-task.ReadWord(0xE));
        short sx = unchecked((short)NativeBattleCameraMath.Sine(xAngle));
        short cx = unchecked((short)NativeBattleCameraMath.Cosine(xAngle));
        NativeBattleActionMatrix x = new([4096, 0, 0, 0, cx, sx, 0, unchecked((short)-sx), cx], 0, 0, 0, 4096);
        return NativeBattleGfMatrices.Multiply56C090(scaled, x) with { X = translation.X, Y = translation.Y, Z = translation.Z };
    }

    public static NativeBattleCarbuncle278ChildDraw Draw683760(NativeBattleEffectMemory memory, NativeBattleModulePoolTask task, NativeBattleModulePoolTask parent, NativeBattleCarbuncle278Resources resources, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        NativeBattleStaticMeshResource mesh = resources.RecipientRayMesh ?? throw new NotSupportedException("Native Carbuncle rays require their original mesh.");
        if (mesh.Address != 0x10BF3A8)
        {
            throw new InvalidDataException("Native Carbuncle ray mesh changed its original address.");
        }
        NativeBattleActionMatrix matrix = Prepare683760(task, parent, ReadCached(memory, task.ReadDword(0x18)), resources.RecipientPolarTable);
        short age = unchecked((short)task.ReadWord(0xC));
        int fade = age <= 10 ? 2048 : 2048 + (((age - 10) * 4096 / 6) >> 1);
        NativeBattleGfGeometryBatch packets = NativeBattleModulePackets.EmitBatch572200(mesh.Bytes, 0, mesh.Bytes.Slice(8, mesh.Bytes.Length - 8), matrix, projection, new(0xF3, 0, fade, null, null, null, 17, 2), colors, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(278, task));
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native Carbuncle rays require the current far-color controls.");
        NativeBattleModelRendererRegisters registers = process.ModelRendererRegisters ?? new(null, null, null, null, null);
        memory = memory with { PrimitiveProcess = process with { ModelRendererRegisters = registers.PublishModelColor(0) } };
        return new(memory, task, packets);
    }

    public static NativeBattleCarbuncle278ChildDraw Draw683330(NativeBattleEffectMemory memory, NativeBattleModulePoolTask task, NativeBattleModulePoolTask parent, NativeBattleCarbuncle278Resources resources, NativeBattleCameraProjection projection)
    {
        NativeBattleVector vector = Vector(parent, 0x54);
        if (unchecked((short)parent.ReadWord(0xC)) >= 35)
        {
            short acceleration = unchecked((short)task.ReadWord(0x14));
            if (acceleration == 0)
            {
                memory = memory.DrawSharedEffectRandom7059E0(out ushort random);
                acceleration = (short)((random & 63) + 128);
            }
            else
            {
                acceleration = unchecked((short)Math.Max(1, (int)unchecked((short)(acceleration - 16))));
            }
            task = task.WriteWord(0x14, unchecked((ushort)acceleration)).WriteWord(0x12, unchecked((ushort)(task.ReadWord(0x12) + acceleration)));
            vector = vector with { Z = unchecked((short)task.ReadWord(0x12)) };
        }
        else
        {
            task = task.WriteWord(0x12, unchecked((ushort)vector.Z));
        }
        NativeBattleVector point = NativeBattleCarbuncle278RecipientChildren.Polar6836F0(vector, unchecked((short)task.ReadWord(0xE)), resources.RecipientPolarTable);
        NativeBattleActionMatrix cached = ReadCached(memory, task.ReadDword(0x18));
        NativeBattleFullMacVertex position = NativeBattleGfMatrices.TransformMac460840(cached, point);
        short scale = unchecked((short)task.ReadWord(0x10));
        NativeBattleActionMatrix main = NativeBattleGfMatrices.Multiply56C090(cached, new([scale, 0, 0, 0, scale, 0, 0, 0, scale], 0, 0, 0, 4096)) with { X = position.X, Y = position.Y, Z = position.Z };
        NativeBattleModuleResource sprite = resources.RecipientShardSprite ?? throw new NotSupportedException("Native Carbuncle shards require their original sprite resource.");
        if (sprite.Address != 0x10BCE98)
        {
            throw new InvalidDataException("Native Carbuncle shard sprite changed its original address.");
        }
        NativeBattleGfGeometryBatch packets = NativeBattleCarbuncle278ShardPackets.Emit683490(sprite.Bytes, task.ReadWord(0xC), main, projection, 17, NativeBattlePilotVisualLifetimes.Identity(278, task));
        return new(memory, task, packets);
    }

    public static NativeBattleCarbuncle278ChildDraw Draw6831D0(NativeBattleEffectMemory memory, NativeBattleModulePoolTask task, NativeBattleCarbuncle278Resources resources, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        NativeBattleStaticMeshResource mesh = resources.RecipientRingMesh ?? throw new NotSupportedException("Native Carbuncle rings require their original mesh.");
        if (mesh.Address != 0x10BF480)
        {
            throw new InvalidDataException("Native Carbuncle ring mesh changed its original address.");
        }
        NativeBattleActionMatrix cached = ReadCached(memory, task.ReadDword(0x18));
        NativeBattleFullMacVertex point = NativeBattleGfMatrices.TransformMac460840(cached, new(0, 0, unchecked((short)task.ReadWord(0xE))));
        short scale = unchecked((short)task.ReadWord(0x10));
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Multiply56C090(cached, new([scale, 0, 0, 0, scale, 0, 0, 0, scale], 0, 0, 0, 4096)) with { X = point.X, Y = point.Y, Z = point.Z };
        int angle = (unchecked((short)task.ReadWord(0xC)) << 10) / 20;
        int fade = NativeBattleCameraMath.Sine(angle);
        NativeBattleGfGeometryBatch packets = NativeBattleModulePackets.EmitBatch572200(mesh.Bytes, 0, mesh.Bytes.Slice(8, mesh.Bytes.Length - 8), matrix, projection, new(0xF3, 0, fade, null, null, null, 17, 2), colors, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(278, task));
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native Carbuncle rings require the current far-color controls.");
        NativeBattleModelRendererRegisters registers = process.ModelRendererRegisters ?? new(null, null, null, null, null);
        memory = memory with { PrimitiveProcess = process with { ModelRendererRegisters = registers.PublishModelColor(0) } };
        return new(memory, task, packets);
    }

    public static NativeBattleActionMatrix ReadCached(NativeBattleEffectMemory memory, uint parentAddress)
    {
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native Carbuncle children require their current physical parent cache.");
        int offset = checked((int)(parentAddress - NativeBattleEffectNodeMemory.ArenaAddress));
        byte[] rotation = arena.Read(offset + 0x34, 18);
        byte[] translation = arena.Read(offset + 0x48, 12);
        ImmutableArray<short> words = Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(rotation.AsSpan(index * 2, 2))).ToImmutableArray();
        return new(words, BinaryPrimitives.ReadInt32LittleEndian(translation), BinaryPrimitives.ReadInt32LittleEndian(translation.AsSpan(4)), BinaryPrimitives.ReadInt32LittleEndian(translation.AsSpan(8)), 4096);
    }

    private static NativeBattleVector Vector(NativeBattleModulePoolTask task, int offset) => new(unchecked((short)task.ReadWord(offset)), unchecked((short)task.ReadWord(offset + 2)), unchecked((short)task.ReadWord(offset + 4)));
}
