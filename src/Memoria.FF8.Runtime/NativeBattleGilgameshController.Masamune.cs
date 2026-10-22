using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGilgameshMasamuneProjectionH(ushort Value) : NativeBattleGilgameshOperation;

public static partial class NativeBattleGilgameshController
{
    private sealed partial class Execution
    {
        private NativeBattleMasamuneResources MasamuneResources => resources.Masamune ?? throw new NotSupportedException("Masamune requires its complete original drawing resources.");

        private int MasamuneFlare(uint address)
        {
            NativeBattleModulePoolTask task = Read(address);
            NativeBattleActionMatrix matrix = NativeBattleGilgameshFlare.Matrix(task);
            Emit(new NativeBattleGilgameshMasamuneProjectionH(0x120));
            DrawMasamuneMesh(MasamuneResources.Flare, matrix, 0x33, null, 0);
            short shadow = inputs.ReadFocalShadow?.Invoke() ?? throw new NotSupportedException("Masamune flare requires its actual projection shadow.");
            Emit(new NativeBattleGilgameshMasamuneProjectionH(unchecked((ushort)shadow)));
            if (Paused)
            {
                return 0;
            }
            if (MasamuneSigned(task, 12) < 6)
            {
                int velocity = MasamuneSigned(task, 0x1E);
                task = task.WriteWord(0x1C, unchecked((ushort)(MasamuneSigned(task, 0x1C) + velocity)));
                task = task.WriteWord(0x1E, unchecked((ushort)(velocity - velocity / 16)));
            }
            task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
            Write(address, task);
            return MasamuneSigned(task, 12) >= 8 ? 2 : 0;
        }

        private int MasamuneBurst(uint address, bool second)
        {
            NativeBattleModulePoolTask task = Read(address);
            int scale = MasamuneSigned(task, 0x1C);
            NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(0, 0, MasamuneSigned(task, 0x18)));
            local = NativeBattlePoseMatrices.Scale56BEF0(local, scale, scale, scale) with { X = MasamuneSigned(task, 0x10), Y = MasamuneSigned(task, 0x12), Z = MasamuneSigned(task, 0x14) };
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(Projection.View ?? throw new NotSupportedException("Masamune bursts require their actual camera matrix."), local);
            int age = MasamuneSigned(task, 12);
            int threshold = second ? 4 : 8;
            int? fade = age >= threshold ? second ? unchecked((age - 4) * 341) : unchecked((age - 8) << 9) : null;
            DrawMasamuneMesh(second ? MasamuneResources.BurstB : MasamuneResources.BurstA, matrix, fade.HasValue ? 0xF3u : 0x33u, fade);
            if (Paused)
            {
                return 0;
            }
            int velocity = MasamuneSigned(task, 0x1E);
            task = task.WriteWord(0x1C, unchecked((ushort)(scale + velocity))).WriteWord(0x1E, unchecked((ushort)(velocity - velocity / 4))).WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
            Write(address, task);
            return MasamuneSigned(task, 12) >= 16 ? 2 : 0;
        }

        private void DrawMasamuneMesh(NativeBattleStaticMeshResource resource, NativeBattleActionMatrix matrix, uint flags, int? fade, short? matrixPadding18 = null)
        {
            NativeBattleFileStaging staging = MasamuneStaging;
            NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(staging, 88);
            NativeBattleEffectArena context = NativeBattleTemporaryMemory.ReadView(temporary.Staging, temporary.Address, 88);
            context = NativeBattleMasamuneMesh.Put(context, 0, resource.Address);
            context = NativeBattleMasamuneMesh.Put(context, 8, 0);
            context = NativeBattleMasamuneMesh.Put(context, 0x1C, flags);
            if (fade is int cue)
            {
                context = NativeBattleMasamuneMesh.Put(context, 12, unchecked((uint)cue));
            }
            Frame = Frame with { Staging = temporary.Staging };
            NativeBattleMasamuneMeshStep result = NativeBattleMasamuneMesh.Draw(Frame.Memory, resource, matrix, Projection, context, inputs.Colors, matrixPadding18);
            staging = NativeBattleTemporaryMemory.PublishView(MasamuneStaging, temporary.Address, result.Context, 88);
            Frame = Frame with { Memory = result.Memory, Staging = staging };
            Emit(new NativeBattleGilgameshPackets(result.Batch));
            Frame = Frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(MasamuneStaging, 88) };
        }

        private int MasamuneParticles(uint address, bool ring)
        {
            NativeBattleModulePoolTask task = Read(address);
            if (ring && MasamuneSigned(task, 14) > 0)
            {
                if (!Paused)
                {
                    Write(address, task.WriteWord(14, unchecked((ushort)(task.ReadWord(14) - 1))));
                }
                return 0;
            }
            NativeBattleTemporaryAllocation scratch = NativeBattleTemporaryMemory.Allocate5082B0(MasamuneStaging, 72);
            NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(scratch.Staging, scratch.Address, 72);
            NativeBattleMasamuneParticlePreparation births = NativeBattleMasamuneParticles.Prepare(task, Arena, Frame.Random, ring, Paused, direction => temporary = MasamuneVector(temporary, 0x28, direction.X, direction.Y, direction.Z));
            Frame = Frame with { Random = births.Random, Memory = Frame.Memory with { Arena = births.Arena }, Staging = scratch.Staging };
            temporary = NativeBattleMasamuneParticles.PutWord(temporary, 0, 0);
            temporary = NativeBattleMasamuneParticles.PutWord(temporary, 2, 0);
            temporary = NativeBattleMasamuneParticles.PutWord(temporary, 4, 0);
            NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)) with { X = MasamuneSigned(task, 0x10), Y = ring ? 0 : MasamuneSigned(task, 0x12), Z = MasamuneSigned(task, 0x14) };
            NativeBattleActionMatrix view = NativeBattleGfMatrices.Compose56C2F0(Projection.View ?? throw new NotSupportedException("Masamune particles require their actual camera."), local);
            temporary = MasamuneMatrix(temporary, 8, view).MarkUnknown(26, 2);
            (byte[] packed, bool[] known) = temporary.ReadSnapshot(8, 20);
            NativeBattleScriptedLightingMemory lighting = Frame.Memory.ScriptedLighting ?? NativeBattleScriptedLightingMemory.Unknown();
            lighting = lighting with { LightMatrix = new(packed.ToImmutableArray(), known.ToImmutableArray()), BackColor = new(view.X, view.Y, view.Z) };
            NativeBattleFileStaging staging = NativeBattleTemporaryMemory.PublishView(MasamuneStaging, scratch.Address, temporary, 72);
            NativeBattleTemporaryAllocation sprite = NativeBattleTemporaryMemory.Allocate5082B0(staging, 180);
            Frame = Frame with { Memory = Frame.Memory with { ScriptedLighting = lighting }, Staging = sprite.Staging };
            NativeBattleModuleResource resource = ring ? MasamuneResources.RingSprite : MasamuneResources.RotatingSprite;
            staging = NativeBattleModuleSpriteTemporary.Begin571C80(MasamuneStaging, sprite.Address, resource.Address, ring ? (ushort)8 : (ushort)9);
            Frame = Frame with { Staging = staging };
            int survivors = 0;
            int mask = MasamuneSigned(task, 0x16);
            for (int index = 0; index < NativeBattleMasamuneParticles.Capacity; index++)
            {
                int record = NativeBattleMasamuneParticles.Bank(ring) + index * NativeBattleMasamuneParticles.Stride(ring);
                if ((unchecked((int)NativeBattleMasamuneMesh.Dword(Arena, record)) & mask) == 0)
                {
                    continue;
                }
                short size = NativeBattleMasamuneParticles.Word(Arena, record + 6);
                NativeBattleMasamuneParticleHead head = NativeBattleMasamuneSprites.PrepareParticle(Frame.Memory, Arena, record, temporary);
                temporary = head.Scratch72;
                staging = NativeBattleTemporaryMemory.PublishView(MasamuneStaging, scratch.Address, temporary, 72);
                Frame = Frame with { Memory = head.Memory, Staging = staging };
                ushort phase = unchecked((ushort)NativeBattleMasamuneParticles.Word(Arena, record + 4));
                short angle = ring ? (short)0 : NativeBattleMasamuneParticles.Word(Arena, record + 0x1C);
                NativeBattleMasamuneSpriteStep drawn = NativeBattleMasamuneSprites.Draw(Frame.Memory, MasamuneStaging, sprite.Address, resource, phase, angle, ring, size, Projection, inputs.ReadCameraPose().Roll);
                Frame = Frame with { Memory = drawn.Memory, Staging = drawn.Staging };
                Emit(new NativeBattleGilgameshSprites(drawn.Batch));
                if (!Paused)
                {
                    bool dead = resource.ProjectedFrame571C80(phase).NextOffset < 0;
                    Frame = Frame with { Memory = Frame.Memory with { Arena = NativeBattleMasamuneParticles.AdvanceRecord(Arena, record, ring, dead) } };
                    if (!dead)
                    {
                        survivors++;
                    }
                }
            }
            staging = NativeBattleTemporaryMemory.Release5082D0(MasamuneStaging, 180);
            staging = NativeBattleTemporaryMemory.Release5082D0(staging, 72);
            Frame = Frame with { Staging = staging };
            if (Paused)
            {
                return 0;
            }
            Write(address, task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1))));
            return survivors == 0 ? 2 : 0;
        }

        private NativeBattleFileStaging MasamuneStaging => Frame.Staging ?? throw new NotSupportedException("Masamune requires its retained physical temporary memory.");
        private static short MasamuneSigned(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
        private static NativeBattleEffectArena MasamuneVector(NativeBattleEffectArena arena, int offset, int x, int y, int z)
        {
            arena = NativeBattleMasamuneMesh.Put(arena, offset, unchecked((uint)x));
            arena = NativeBattleMasamuneMesh.Put(arena, offset + 4, unchecked((uint)y));
            return NativeBattleMasamuneMesh.Put(arena, offset + 8, unchecked((uint)z));
        }
        private static NativeBattleEffectArena MasamuneMatrix(NativeBattleEffectArena arena, int offset, NativeBattleActionMatrix matrix)
        {
            for (int index = 0; index < 9; index++)
            {
                arena = NativeBattleMasamuneParticles.PutWord(arena, offset + index * 2, matrix.Rotation[index]);
            }
            return MasamuneVector(arena, offset + 20, matrix.X, matrix.Y, matrix.Z);
        }
    }
}
