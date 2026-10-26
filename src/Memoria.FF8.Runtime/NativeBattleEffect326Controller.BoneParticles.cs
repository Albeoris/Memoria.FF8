using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect326Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect326Controller
{
    public static NativeBattleEffect326Step AdvanceBoneParticles(NativeBattleEffect326Frame frame, uint address, NativeBattleEffect326Resources resources, NativeBattleEffect326Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        bool paired = task.Callback == 0x62FC90;
        if (!paired && task.Callback != 0x62F740)
        {
            throw new InvalidDataException("Effect326 bone particles require their actual native callback.");
        }
        uint historyBase = paired ? BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(checked(0xE196A4u + (uint)(Signed(task, 0x18) * 4)), 4)) : 0x24F0C88;
        uint spriteSource = paired ? 0xE10E04u : 0xE108E8u;
        int bone = paired ? Signed(task, 14) : 40;
        (frame, uint spriteAddress) = Allocate(frame, 180);
        (frame, uint scratchAddress) = Allocate(frame, 80);
        NativeBattleEffectArena scratch = Context(frame, scratchAddress, 80);
        NativeBattleEffect326PointStep point = ReadOwnedBone502170(frame, resources, 0x24F1268, bone, 0, ParticleSlice(scratch, 0, 8));
        frame = point.Frame;
        scratch = CopyBytes(point.Point8, 0, Context(frame, scratchAddress, 80), 0, 8);
        frame = PublishContext(frame, scratchAddress, scratch, 80);
        point = ReadOwnedBone502170(frame, resources, 0x24F1268, bone, 4096, ParticleSlice(scratch, 8, 8));
        frame = point.Frame;
        scratch = CopyBytes(point.Point8, 0, Context(frame, scratchAddress, 80), 8, 8);
        short x = unchecked((short)(ParticleSigned(scratch, 8) - ParticleSigned(scratch, 0)));
        short y = unchecked((short)(ParticleSigned(scratch, 10) - ParticleSigned(scratch, 2)));
        short z = unchecked((short)(ParticleSigned(scratch, 12) - ParticleSigned(scratch, 4)));
        var normalized = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
        NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(ReadPacked(frame, BaseMatrix).ReadMatrix(), new(unchecked((short)normalized.X), unchecked((short)normalized.Y), unchecked((short)normalized.Z)));
        scratch = ParticleWord(ParticleWord(ParticleWord(scratch, 0, rotated.X), 2, rotated.Y), 4, rotated.Z);
        int age = Signed(task, 12);
        int historyIndex = age % 3;
        uint historyAddress = checked(historyBase + (uint)(historyIndex * 20));
        NativeBattleEffectArena history = frame.State.ReadView(historyAddress, 20).Write(0, BitConverter.GetBytes(1u));
        history = CopyBytes(scratch, 8, history, 4, 8);
        history = CopyBytes(scratch, 0, history, 12, 8);
        frame = frame with { State = frame.State.WriteView(historyAddress, history, 20) };
        int count = 0;
        while (count < 3)
        {
            history = frame.State.ReadView(historyBase + (uint)(historyIndex * 20), 20);
            if (ParticleDword(history, 0) == 0)
            {
                break;
            }
            frame = frame with { State = frame.State.WriteView(0x24F0BD0u + (uint)(count * 8), ParticleSlice(history, 4, 8), 8).WriteView(0x24F0C28u + (uint)(count * 8), ParticleSlice(history, 12, 8), 8) };
            historyIndex--;
            if (historyIndex < 0)
            {
                historyIndex = 2;
            }
            count++;
        }
        NativeBattlePackedMatrix32 view = ReadPacked(frame, ViewMatrix);
        scratch = CopyBytes(PackedArena(view), 0, scratch, 16, 32);
        frame = PublishContext(frame, scratchAddress, scratch, 80);
        NativeBattleEffectArena sprite = Context(frame, spriteAddress, 180).Write(0, BitConverter.GetBytes(spriteSource)).Write(0x1C, BitConverter.GetBytes(paired ? 0x202020u : 0x404040u)).Write(0x24, BitConverter.GetBytes((ushort)12));
        frame = PublishContext(frame, spriteAddress, sprite, 180);
        frame = frame with { Memory = NativeBattleEffect315Gte.LoadAlternateTranslation64DDA0(NativeBattleEffect315Gte.LoadRotation45DE50(frame.Memory, view), view) };
        int survivors = 0;
        for (int index = 0; index < 150; index++)
        {
            uint rowAddress = frame.State.ReadDword(0xE19670) + (uint)(index * 24);
            NativeBattleEffectArena row = ReadBytes(frame, rowAddress, 24);
            if ((ParticleDword(row, 0) & (paired ? 2u : 1u)) == 0)
            {
                continue;
            }
            (byte[] packedW, bool[] knownW) = row.ReadSnapshot(14, 2);
            short? w = knownW.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(packedW) : null;
            NativeBattleVector position = new(ParticleSigned(row, 8), ParticleSigned(row, 10), ParticleSigned(row, 12));
            frame = Publish(frame, NativeBattleGeometry.LoadVector(Registers(frame), 0, position, w));
            frame = frame with { Memory = NativeBattleEffect315Gte.SetDiagonal64DE00(NativeBattleEffect315Gte.TransformAlternate4607E0(frame.Memory), unchecked((ushort)ParticleSigned(row, 6))) };
            sprite = ParticleWord(Context(frame, spriteAddress, 180), 4, ParticleSigned(row, 4));
            frame = PublishContext(frame, spriteAddress, sprite, 180);
            frame = frame with { Memory = IrToTranslation64DE90(frame.Memory) };
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(frame.Memory, sprite, resources.Sprite, inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame), 17, 2);
            frame = PublishContext(frame with { Memory = drawn.Memory }, spriteAddress, drawn.Temporary180, 180);
            frame = inputs.Execute(frame, new NativeBattleEffect326DeliverBatch(drawn.Batch, 0x1D8E054));
            if ((inputs.ReadFlags(frame) & 0x201) == 0)
            {
                row = ParticleWord(row, 4, ParticleSigned(row, 4) + 1);
                if (ParticleSigned(Context(frame, spriteAddress, 180), 0x28) < 0)
                {
                    row = row.Write(0, BitConverter.GetBytes(0u));
                }
                else
                {
                    int width = ParticleSigned(row, 6);
                    row = ParticleWord(row, 6, width + (width >> (paired ? 6 : 5)));
                    for (int axis = 0; axis < 3; axis++)
                    {
                        int velocity = ParticleSigned(row, 16 + axis * 2);
                        row = ParticleWord(row, 8 + axis * 2, ParticleSigned(row, 8 + axis * 2) + velocity);
                        row = ParticleWord(row, 16 + axis * 2, velocity - (velocity >> 3));
                    }
                    survivors++;
                }
                frame = WriteBytes(frame, rowAddress, row, 24);
            }
        }
        frame = Release(Release(frame, 80), 180);
        if ((inputs.ReadFlags(frame) & 0x201) != 0)
        {
            return new(frame, 0);
        }
        if (age is >= 0 and <= 11 && count > 1)
        {
            (frame, uint workAddress) = Allocate(frame, 400);
            NativeBattleEffectArena work = Context(frame, workAddress, 400);
            foreach ((uint controls, uint samples) in new (uint Controls, uint Samples)[] { (0x24F0BD0u, 0x24F1068u), (0x24F0C28u, 0x24F1168u) })
            {
                work = NativeBattleGilgameshSwordTrailCurve.Prepare571620(count, frame.State.ReadView(controls, count * 8), 0, work);
                frame = PublishContext(frame, workAddress, work, 400);
                for (int sample = 0; sample < (paired ? 4 : 6); sample++)
                {
                    uint destination = samples + (uint)(sample * 8);
                    NativeBattleEffectArena output = NativeBattleGilgameshSwordTrailCurve.Evaluate571690(count, work, sample * 4096 / (paired ? 3 : 5), frame.State.ReadView(destination, 8), 0);
                    frame = frame with { State = frame.State.WriteView(destination, output, 8) };
                }
            }
            frame = Release(frame, 400);
            frame = BirthBoneParticles(frame, paired);
        }
        short next = unchecked((short)(age + 1));
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)next)));
        return new(frame, next >= 4 && survivors == 0 ? 2 : 0);
    }

    private static NativeBattleEffect326Frame BirthBoneParticles(NativeBattleEffect326Frame frame, bool paired)
    {
        for (int birth = 0; birth < (paired ? 3 : 5); birth++)
        {
            int index = 0;
            while (index < 150 && ParticleDword(ReadBytes(frame, frame.State.ReadDword(0xE19670) + (uint)(index * 24), 4), 0) != 0)
            {
                index++;
            }
            if (index == 150)
            {
                break;
            }
            uint destination = frame.State.ReadDword(0xE19670) + (uint)(index * 24);
            NativeBattleEffectArena row = ReadBytes(frame, destination, 24).Write(0, BitConverter.GetBytes(paired ? 2u : 1u));
            row = ParticleWord(row, 4, 0);
            row = ParticleWord(row, 6, paired ? ParticleRoll(ref frame) % 1024 + 256 : ParticleRoll(ref frame) % 1152 + 896);
            row = CopyBytes(frame.State.ReadView(0x24F1068u + (uint)(birth * 8), 8), 0, row, 8, 8);
            for (int axis = 0; axis < 3; axis++)
            {
                row = ParticleWord(row, 8 + axis * 2, ParticleSigned(row, 8 + axis * 2) + ParticleRoll(ref frame) % (paired ? 20 : 10) - (paired ? 10 : 5));
            }
            int factor = ParticleRoll(ref frame) % 15 + 25;
            NativeBattleEffectArena direction = frame.State.ReadView(0x24F1168u + (uint)(birth * 8), 8);
            for (int axis = 0; axis < 3; axis++)
            {
                int random = ParticleRoll(ref frame) % (paired ? 20 : 10) - (paired ? 10 : 5);
                int velocity = unchecked(ParticleSigned(direction, axis * 2) * factor) >> 12;
                row = ParticleWord(row, 16 + axis * 2, velocity + random);
            }
            frame = WriteBytes(frame, destination, row, 24);
        }
        return frame;
    }

}
