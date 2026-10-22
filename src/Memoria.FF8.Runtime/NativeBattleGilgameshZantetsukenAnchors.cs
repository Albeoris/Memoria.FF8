using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGilgameshZantetsukenAnchorStep(NativeBattleEffectMemory Memory, NativeBattleFileStaging Staging, NativeBattleVector Position);

public static class NativeBattleGilgameshZantetsukenAnchors
{
    public static NativeBattleBoneAnchorProducer Resolve58E2E0(NativeBattleGraphicsModel model, NativeBattlePoseSource? source, byte selector, short fraction)
    {
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Zantetsuken requires its actual model transform.");
        if ((model.Flags0 & 2) == 0)
        {
            return new(null, 0, transform.Position28);
        }
        source = source ?? throw new NotSupportedException("Zantetsuken requires the current body's original skeleton.");
        var matrices = NativeBattleBodyBoneMatrices.Require(model, source);
        int bone = selector;
        if (selector >= 0xF0)
        {
            int offset = selector - 0xEC;
            if (offset >= 16)
            {
                throw new InvalidDataException("Zantetsuken bone alias leaves its actual skeleton header.");
            }
            bone = offset == 4 ? model.SequenceState?.Geometry4 ?? source.Skeleton[offset] : offset == 5 ? model.SequenceState?.Geometry5 ?? source.Skeleton[offset] : source.Skeleton[offset];
            if ((bone & 0x80) != 0)
            {
                fraction = 4096;
                bone &= 0x7F;
            }
        }
        if ((uint)bone >= matrices.Length)
        {
            throw new InvalidDataException("Zantetsuken bone alias leaves its current matrix publication.");
        }
        int record = 16 + bone * 48;
        short parent = BinaryPrimitives.ReadInt16LittleEndian(source.Skeleton.AsSpan().Slice(record, 2));
        short length = BinaryPrimitives.ReadInt16LittleEndian(source.Skeleton.AsSpan().Slice(record + 2, 2));
        short distance = parent < 0 ? (short)0 : unchecked((short)(fraction * length >> 12));
        return new(matrices[bone], distance, transform.Position28);
    }

    public static NativeBattleGilgameshZantetsukenAnchorStep Project58E2E0(NativeBattleEffectMemory memory, NativeBattleFileStaging staging, NativeBattleBoneAnchorProducer producer, short? matrixPadding)
    {
        if (!producer.Enabled)
        {
            return new(memory, staging, producer.DisabledPosition);
        }
        NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(staging, 52);
        NativeBattleGeometryRegisters geometry = NativeBattleGeometry.LoadMatrix(memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown(), producer.Matrix!, true, true, matrixPadding);
        NativeBattleGeometryRegisterStep step = NativeBattleGeometry.LoadVector(new(geometry, memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown()), 0, new(0, 0, producer.Distance), null);
        step = NativeBattleGeometry.Transform(step);
        int[] mac = step.Colors.Mac.Select(value => value ?? throw new InvalidDataException("Zantetsuken anchor did not publish its full MAC output.")).ToArray();
        byte[] outputs = new byte[12];
        for (int lane = 0; lane < 3; lane++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(outputs.AsSpan(lane * 4, 4), mac[lane]);
        }
        staging = temporary.Staging.Write(temporary.Address + 0x20, outputs);
        byte[] flags = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(flags, step.Colors.Flags ?? throw new InvalidDataException("Zantetsuken anchor did not publish its GTE flags."));
        staging = staging.Write(temporary.Address + 0x30, flags);
        staging = NativeBattleTemporaryMemory.Release5082D0(staging, 52);
        return new(memory with { GeometryRegisters = step.Geometry, ScriptedColorRegisters = step.Colors }, staging, new(unchecked((short)mac[0]), unchecked((short)mac[1]), unchecked((short)mac[2])));
    }

    public static NativeBattleModuleAnchor Average58E260(NativeBattleVector first, NativeBattleVector second, short modelWord24)
    {
        return new(unchecked((short)((first.X + second.X) / 2)), unchecked((short)((first.Y + second.Y) / 2)), unchecked((short)((first.Z + second.Z) / 2)), modelWord24);
    }
}
