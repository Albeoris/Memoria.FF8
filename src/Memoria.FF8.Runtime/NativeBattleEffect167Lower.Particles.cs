using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect167Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect167Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect167Lower
{
    public static NativeBattleEffect167Step AdvanceParticles(NativeBattleEffect167Frame frame, uint address, NativeBattleEffect167Resources resources, NativeBattleEffect167Inputs inputs)
    {
        (frame, uint spriteAddress) = Allocate(frame, 180);
        (frame, uint scratchAddress) = Allocate(frame, 80);
        int caster = checked((int)frame.State.ReadDword(CasterGlobal));
        NativeBattleEffectArena scratch = Context(frame, scratchAddress, 80);
        NativeBattleEffect167PointStep point = inputs.Point502170(frame, caster, 40, 0, ParticleSlice(scratch, 0, 8));
        frame = point.Frame;
        scratch = CopyBytes(point.Output8, 0, Context(frame, scratchAddress, 80), 0, 8);
        frame = PublishContext(frame, scratchAddress, scratch, 80);
        point = inputs.Point502170(frame, caster, 40, 4096, ParticleSlice(scratch, 8, 8));
        frame = point.Frame;
        scratch = CopyBytes(point.Output8, 0, Context(frame, scratchAddress, 80), 8, 8);
        short x = unchecked((short)(ParticleSigned(scratch, 8) - ParticleSigned(scratch, 0)));
        short y = unchecked((short)(ParticleSigned(scratch, 10) - ParticleSigned(scratch, 2)));
        short z = unchecked((short)(ParticleSigned(scratch, 12) - ParticleSigned(scratch, 4)));
        var normalized = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
        scratch = ParticleWord(ParticleWord(ParticleWord(scratch, 0, normalized.X), 2, normalized.Y), 4, normalized.Z);
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 12);
        int historyIndex = age % 3;
        uint historyAddress = checked(FirstHistoryAddress + (uint)(historyIndex * 20));
        NativeBattleEffectArena history = frame.State.ReadView(historyAddress, 20).Write(0, BitConverter.GetBytes(1u));
        history = CopyBytes(scratch, 8, history, 4, 8);
        history = CopyBytes(scratch, 0, history, 12, 8);
        frame = frame with { State = frame.State.WriteView(historyAddress, history, 20) };
        int count = 0;
        while (count < 3)
        {
            history = frame.State.ReadView(FirstHistoryAddress + (uint)(historyIndex * 20), 20);
            if (ParticleDword(history, 0) == 0)
            {
                break;
            }
            frame = frame with { State = frame.State.WriteView(FirstControlAddress + (uint)(count * 8), ParticleSlice(history, 4, 8), 8).WriteView(SecondControlAddress + (uint)(count * 8), ParticleSlice(history, 12, 8), 8) };
            historyIndex--;
            if (historyIndex < 0)
            {
                historyIndex = 2;
            }
            count++;
        }
        NativeBattlePackedMatrix32 view = inputs.ReadView(frame);
        scratch = CopyBytes(PackedArena(view), 0, scratch, 16, 32);
        frame = PublishContext(frame, scratchAddress, scratch, 80);
        NativeBattleEffectArena sprite = Context(frame, spriteAddress, 180).Write(0, BitConverter.GetBytes(SpriteAddress)).Write(0x1C, BitConverter.GetBytes(0x00483830u)).Write(0x24, BitConverter.GetBytes((ushort)12));
        frame = PublishContext(frame, spriteAddress, sprite, 180);
        frame = frame with { Memory = NativeBattleEffect315Gte.LoadAlternateTranslation64DDA0(NativeBattleEffect315Gte.LoadRotation45DE50(frame.Memory, view), view) };
        int survivors = 0;
        for (int index = 0; index < FirstRecordCount; index++)
        {
            uint rowAddress = FirstRecordsAddress + (uint)(index * FirstRecordStride);
            NativeBattleEffectArena row = frame.State.ReadView(rowAddress, FirstRecordStride);
            if ((ParticleDword(row, 0) & 1) == 0)
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
            frame = frame with { Memory = NativeBattleEffect315Gte.MacToTranslation64DE40(frame.Memory) };
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(frame.Memory, sprite, resources.Sprite, inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame), 17, 2, pilotIdentity: inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(167, frame.State.Storage, FirstRecordsAddress, index) with { ResourceGeneration = SpriteAddress } : null);
            frame = PublishContext(frame with { Memory = drawn.Memory }, spriteAddress, drawn.Temporary180, 180);
            frame = inputs.Execute(frame, new NativeBattleEffect167DrawBatch(drawn.Batch, CursorGlobal));
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
                    row = ParticleWord(row, 6, width + (width >> 5));
                    for (int axis = 0; axis < 3; axis++)
                    {
                        int velocity = ParticleSigned(row, 16 + axis * 2);
                        row = ParticleWord(row, 8 + axis * 2, ParticleSigned(row, 8 + axis * 2) + velocity);
                        row = ParticleWord(row, 16 + axis * 2, velocity - (velocity >> 3));
                    }
                    survivors++;
                }
                frame = frame with { State = frame.State.WriteView(rowAddress, row, FirstRecordStride) };
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
            foreach ((uint controls, uint samples) in new (uint Controls, uint Samples)[] { (FirstControlAddress, FirstSamplesAddress), (SecondControlAddress, SecondSamplesAddress) })
            {
                work = NativeBattleGilgameshSwordTrailCurve.Prepare571620(count, frame.State.ReadView(controls, count * 8), 0, work);
                frame = PublishContext(frame, workAddress, work, 400);
                for (int sample = 0; sample < 6; sample++)
                {
                    uint destination = samples + (uint)(sample * 8);
                    NativeBattleEffectArena output = NativeBattleGilgameshSwordTrailCurve.Evaluate571690(count, work, sample * 4096 / 5, frame.State.ReadView(destination, 8), 0);
                    frame = frame with { State = frame.State.WriteView(destination, output, 8) };
                }
            }
            frame = Release(frame, 400);
            frame = BirthParticles(frame, inputs.CapturePresentation);
        }
        short next = unchecked((short)(age + 1));
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)next)));
        return new(frame, next >= 4 && survivors == 0 ? 2 : 0);
    }

    private static NativeBattleEffect167Frame BirthParticles(NativeBattleEffect167Frame frame, bool capturePresentation = false)
    {
        for (int birth = 0; birth < 5; birth++)
        {
            int index = 0;
            while (index < FirstRecordCount && frame.State.ReadDword(FirstRecordsAddress + (uint)(index * FirstRecordStride)) != 0)
            {
                index++;
            }
            if (index == FirstRecordCount)
            {
                break;
            }
            uint destination = FirstRecordsAddress + (uint)(index * FirstRecordStride);
            if (capturePresentation)
            {
                NativeBattlePilotVisualLifetimes.Allocated(frame.State.Storage, FirstRecordsAddress, index);
            }
            NativeBattleEffectArena row = frame.State.ReadView(destination, FirstRecordStride).Write(0, BitConverter.GetBytes(1u));
            row = ParticleWord(row, 4, 0);
            row = ParticleWord(row, 6, ParticleRoll(ref frame) % 896 + 640);
            row = CopyBytes(frame.State.ReadView(FirstSamplesAddress + (uint)(birth * 8), 8), 0, row, 8, 8);
            for (int axis = 0; axis < 3; axis++)
            {
                row = ParticleWord(row, 8 + axis * 2, ParticleSigned(row, 8 + axis * 2) + ParticleRoll(ref frame) % 10 - 5);
            }
            int factor = ParticleRoll(ref frame) % 15 + 25;
            NativeBattleEffectArena direction = frame.State.ReadView(SecondSamplesAddress + (uint)(birth * 8), 8);
            for (int axis = 0; axis < 3; axis++)
            {
                int random = ParticleRoll(ref frame) % 10 - 5;
                int velocity = unchecked(ParticleSigned(direction, axis * 2) * factor) >> 12;
                row = ParticleWord(row, 16 + axis * 2, velocity + random);
            }
            frame = frame with { State = frame.State.WriteView(destination, row, FirstRecordStride) };
        }
        return frame;
    }

    private static int ParticleRoll(ref NativeBattleEffect167Frame frame)
    {
        NativeCrtRandomState next = frame.Random.Draw(out ushort value);
        frame = frame with { Random = next };
        return value;
    }

    private static NativeBattleEffectArena ParticleSlice(NativeBattleEffectArena source, int offset, int length) => CopyBytes(source, offset, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    private static NativeBattleEffectArena ParticleWord(NativeBattleEffectArena source, int offset, int value) => source.Write(offset, BitConverter.GetBytes(unchecked((ushort)value)));
    private static short ParticleSigned(NativeBattleEffectArena source, int offset) => BinaryPrimitives.ReadInt16LittleEndian(source.Read(offset, 2));
    private static uint ParticleDword(NativeBattleEffectArena source, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(source.Read(offset, 4));
}
