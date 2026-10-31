using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeKiros262Trail(NativeBattleWaterExecutionFrame frame, NativeBattleGraphicsState graphics)
    {
        NativeBattleKiros262TrailState state = RequireWaterMemory().Kiros262Trail ?? throw new NotSupportedException("Native trail requires its retained original process workspace; migrate the selected older snapshot explicitly.");
        NativeBattlePilotVisualIdentity? trailIdentity = null;
        long sourceGeneration = 0;
        NativeBattleKiros262TrailServices services = new((current, pointer, selector) =>
        {
            int actorIndex = ActorIndex(pointer);
            NativeBattleGraphicsModel actor = graphics.Models[actorIndex];
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actorIndex).PoseSource ?? throw new NotSupportedException("Native trail requires the actual body skeleton.");
            sourceGeneration = unchecked(NativeBattlePilotVisualLifetimes.EpochFor(source) * 397 ^ selector);
            actor = NativeBattleBodyBoneMatrices.Compose5095B0(actor, actor.GeometryWorldMatrix ?? throw new NotSupportedException("Native trail requires the current actor world matrix."));
            ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(actor, source);
            int bone = selector;
            if (selector >= 240)
            {
                int alias = selector - 236;
                if ((uint)alias >= source.Skeleton.Length)
                {
                    throw new NotSupportedException("Native trail alias leaves its actual skeleton source.");
                }
                bone = source.Skeleton[alias];
            }
            if ((uint)bone >= bones.Length)
            {
                throw new NotSupportedException("Native trail selector leaves its current body bone matrices.");
            }
            NativeBattleActionMatrix matrix = bones[bone];
            var rotated = NativeBattleGfMatrices.RotateLong56C600(matrix, 0, 0, -4096);
            var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(rotated.X, rotated.Y, rotated.Z);
            graphics = graphics with { Models = graphics.Models.SetItem(actorIndex, actor) };
            return new(current, new(unchecked((short)matrix.X), unchecked((short)matrix.Y), unchecked((short)matrix.Z)), direction.X, direction.Y, direction.Z);
        }, (current, pointer) =>
        {
            int actorIndex = ActorIndex(pointer);
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actorIndex).PoseSource ?? throw new NotSupportedException("Native trail restore requires its actual body rig.");
            graphics = graphics with { Models = graphics.Models.SetItem(actorIndex, NativeBattleBodyBoneMatrices.Publish508C90(graphics.Models[actorIndex], source)) };
            return current;
        }, (current, trail, color) =>
        {
            NativeBattleEffectArena curve = NativeBattleEffectArena.Clear();
            CopyCurve(NativeBattleKiros262TrailState.CurveA, NativeBattleGilgameshSwordTrailState.CurveA);
            CopyCurve(NativeBattleKiros262TrailState.CurveB, NativeBattleGilgameshSwordTrailState.CurveB);
            byte[] raw = current.Core.Read(0x27979E8, 32);
            NativeBattleActionMatrix view = new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(index * 2))).ToImmutableArray(), BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(20)), BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(24)), BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(28)), 4096);
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattleGilgameshSwordTrailPacketResult packets = NativeBattleGilgameshSwordTrailPackets.Emit(new(curve), view, NativeCamera?.Projection ?? throw new NotSupportedException("Native trail requires its current camera projection."), memory.ScriptedGraphics, definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native trail requires its original color arithmetic."), color, pilotIdentity: trailIdentity);
            packets = packets with { Batch = NativeBattleCinematicActorPresentation.NativeDepths(packets.Batch) };
            uint manager = CoreDword(current, 0x27973EC);
            uint start = CoreDword(current, manager + 0x7C);
            var appended = AppendNativeCinematicPackets(current, start, null, packets.Batch, graphics);
            graphics = appended.Graphics;
            current = WriteCoreDword(appended.Frame, manager + 0x7C, unchecked(start + (uint)packets.PoolCursorAdvance));
            if (!IsNativeEffectArenaAddress(start))
            {
                NativeBattleEffectMemory actual = RequireWaterMemory();
                NativeBattlePrimitiveProcess process = actual.PrimitiveProcess ?? throw new NotSupportedException("Native trail requires its current COMMON packet owner.");
                int next = checked((int)start + packets.PoolCursorAdvance);
                if (next > process.CommonPacketWriteCapacity)
                {
                    throw new InvalidDataException("Native trail final pool publication leaves its allocated COMMON packet bank.");
                }
                NativeEffectMemory = actual with { PrimitiveProcess = process with { ByteCursor = next } };
            }
            return current;

            void CopyCurve(int source, int destination)
            {
                var bytes = trail.Memory.ReadSnapshot(source, 256);
                curve = curve.Write(destination, bytes.Data);
                for (int index = 0; index < bytes.Known.Length; index++)
                {
                    if (!bytes.Known[index])
                    {
                        curve = curve.MarkUnknown(destination + index, 1);
                    }
                }
            }
        })
        {
            ObserveIdentity = identity => trailIdentity = identity with { ResourceGeneration = unchecked(identity.ResourceGeneration * 397 ^ sourceGeneration) }
        };
        NativeBattleWaterSlotBank visualBank = NativeBattleWaterInstructionDispatcher.CurrentBank(frame, NativeBattleEffectInitializerPolicy.Assets(graphics.OriginalEffect!.Binding)[1].Data.Length);
        NativeBattlePilotVisualIdentity identity = NativeBattleCinematicVisualIdentity.Identity(graphics.OriginalEffect.Binding.Effect, frame, visualBank, NativeBattleWaterInstructionDispatcher.CurrentIndex(frame, visualBank));
        NativeBattleKiros262TrailStep drawn = NativeBattleKiros262Trail.Draw(frame, state, services, identity);
        NativeEffectMemory = RequireWaterMemory() with { Kiros262Trail = drawn.State };
        return (drawn.Frame, graphics);

        int ActorIndex(uint pointer)
        {
            if (pointer < 0x1D972C0 || (pointer - 0x1D972C0) % 156 != 0 || (pointer - 0x1D972C0) / 156 >= graphics.Models.Length)
            {
                throw new NotSupportedException("Native trail requires its actual physical actor pointer.");
            }
            return checked((int)((pointer - 0x1D972C0) / 156));
        }
    }
}
