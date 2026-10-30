using static Memoria.FF8.Runtime.NativeBattleEffect218Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect218Controller
{
    public static (NativeBattleEffect218Frame Frame, int Result) AdvanceMeshParticles636EB0(NativeBattleEffect218Frame frame, uint address, uint graphicsFlags, NativeBattleStaticMeshResource mesh, NativeBattlePackedMatrix32 currentView, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, Func<NativeBattleEffect218Frame, NativeBattleGfGeometryBatch, NativeBattleEffect218Frame> deliver)
    {
        if (mesh.Address != 0xE1F5F0)
        {
            throw new InvalidDataException("Effect218 mesh particles require their actual selected mesh.");
        }
        uint bank = frame.State.ReadDword(0xE257E4);
        (frame, uint scratchAddress) = Allocate(frame, 104);
        bool paused = (graphicsFlags & 0x201) != 0;
        if (!paused && Signed(Read(frame, address), 12) <= 6)
        {
            frame = SpawnMeshParticles636EF1(frame, bank, scratchAddress);
        }
        (frame, uint contextAddress) = Allocate(frame, 88);
        NativeBattleEffectArena scratch = NativeBattleEffectNodeContextMemory.Copy(PackedArena(currentView), 0, Context(frame, scratchAddress, 104), 8, 32).Write(2, new byte[2]);
        NativeBattleEffectArena context = Context(frame, contextAddress, 88).Write(0, BitConverter.GetBytes(mesh.Address)).Write(8, new byte[4]).Write(28, new byte[4]);
        frame = PublishContext(PublishContext(frame, scratchAddress, scratch, 104), contextAddress, context, 88);
        for (int index = 0; index < 150; index++)
        {
            uint location = checked(bank + (uint)(index * 32));
            NativeBattleEffectArena row = ReadBytes(frame, location, 32);
            if ((ParticleDword218(row, 0) & 1) == 0)
            {
                continue;
            }
            NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CE30(new(ParticleWord218(row, 24), 0, ParticleWord218(row, 28)));
            short scale = ParticleWord218(row, 6);
            NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(rotation, scale, scale, scale) with { X = ParticleWord218(row, 8), Y = ParticleWord218(row, 10), Z = ParticleWord218(row, 12) };
            NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(currentView.ReadMatrix(), local), null);
            scratch = Context(frame, scratchAddress, 104).Write(0, row.Read(24, 2)).Write(4, row.Read(28, 2)).Write(0x48, BitConverter.GetBytes((int)scale)).Write(0x4C, BitConverter.GetBytes((int)scale)).Write(0x50, BitConverter.GetBytes((int)scale));
            scratch = NativeBattleEffectOwnedModelRendering.StoreMatrix(scratch, 0x28, packed);
            frame = PublishContext(frame, scratchAddress, scratch, 104);
            frame = LoadMatrix(frame, packed);
            NativeBattleStaticMeshContext88Step drawn = NativeBattleStaticMeshContext88.DrawLoaded(frame.Memory, mesh, projection, Context(frame, contextAddress, 88), colors, pilotIdentity: NativeBattleEffect218Presentation.Row(frame, bank, index, mesh.Address));
            frame = PublishContext(frame with { Memory = drawn.Memory }, contextAddress, drawn.Context, 88);
            frame = deliver(frame, drawn.Batch);
            if (!paused)
            {
                row = ParticleWord218(row, 4, ParticleWord218(row, 4) + 1);
                for (int axis = 0; axis < 3; axis++)
                {
                    short velocity = ParticleWord218(row, 16 + axis * 2);
                    row = ParticleWord218(row, 8 + axis * 2, ParticleWord218(row, 8 + axis * 2) + velocity);
                    row = ParticleWord218(row, 16 + axis * 2, velocity - (velocity >> 7));
                }
                row = ParticleWord218(row, 24, ParticleWord218(row, 24) + ParticleWord218(row, 26));
                row = ParticleWord218(row, 28, ParticleWord218(row, 28) + ParticleWord218(row, 30));
                frame = WriteBytes(frame, location, row, 32);
            }
        }
        frame = Release(Release(frame, 88), 104);
        if (paused)
        {
            return (frame, 0);
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        frame = Write(frame, address, task);
        if (Signed(task, 12) < 45)
        {
            return (frame, 0);
        }
        for (int index = 0; index < 150; index++)
        {
            uint location = checked(bank + (uint)(index * 32));
            if ((ParticleDword218(ReadBytes(frame, location, 4), 0) & 1) != 0)
            {
                frame = WriteBytes(frame, location, NativeBattleEffectArena.Clear().Write(0, new byte[4]), 4);
            }
        }
        return (frame, 2);
    }

    public static NativeBattleEffect218Frame SpawnMeshParticles636EF1(NativeBattleEffect218Frame frame, uint bank, uint scratchAddress)
    {
        for (int selected = 0; selected < 24; selected++)
        {
            int slot = 0;
            while (slot < 150 && ParticleDword218(ReadBytes(frame, checked(bank + (uint)(slot * 32)), 4), 0) != 0)
            {
                slot++;
            }
            if (slot == 150)
            {
                break;
            }
            uint location = checked(bank + (uint)(slot * 32));
            if (frame.CapturePresentation)
            {
                NativeBattlePilotVisualLifetimes.Allocated(frame.State.ParticleGlobals, bank, slot);
            }
            NativeBattleEffectArena row = ReadBytes(frame, location, 32).Write(0, BitConverter.GetBytes(1u)).Write(4, new byte[2]);
            frame = Roll(frame, out ushort scale);
            row = ParticleWord218(row, 6, (scale & 1023) + 128);
            uint originAddress = checked(0x24F3378u + (uint)(selected * 16));
            row = NativeBattleEffectNodeContextMemory.Copy(ReadBytes(frame, originAddress, 8), 0, row, 8, 8);
            for (int axis = 0; axis < 3; axis++)
            {
                frame = Roll(frame, out ushort jitter);
                row = ParticleWord218(row, 8 + axis * 2, ParticleWord218(row, 8 + axis * 2) + jitter % 1400 - 700);
            }
            NativeBattleEffectArena scratch = NativeBattleEffectNodeContextMemory.Copy(ReadBytes(frame, originAddress + 8, 8), 0, Context(frame, scratchAddress, 104), 0, 8);
            var normalized = NativeBattleActionGeometryRules.NormalizeVector56BC50(ParticleWord218(scratch, 0), ParticleWord218(scratch, 2), ParticleWord218(scratch, 4));
            scratch = ParticleWord218(ParticleWord218(ParticleWord218(scratch, 0, normalized.X), 2, normalized.Y), 4, normalized.Z);
            frame = PublishContext(frame, scratchAddress, scratch, 104);
            frame = Roll(frame, out ushort velocity);
            int speed = velocity % 950 + 100;
            for (int axis = 0; axis < 3; axis++)
            {
                row = ParticleWord218(row, 16 + axis * 2, unchecked(ParticleWord218(scratch, axis * 2) * speed) >> 12);
            }
            frame = Roll(frame, out ushort xAngle);
            row = ParticleWord218(row, 24, xAngle & 1023);
            frame = Roll(frame, out ushort xSpin);
            int spin = xSpin % 67 + 30;
            row = ParticleWord218(row, 26, (spin & 1) == 0 ? spin : -spin);
            frame = Roll(frame, out ushort zAngle);
            row = ParticleWord218(row, 28, (zAngle & 1023) - 512);
            frame = Roll(frame, out ushort zSpin);
            row = ParticleWord218(row, 30, zSpin % 40 + 15);
            frame = WriteBytes(frame, location, row, 32);
        }
        return frame;
    }
}
