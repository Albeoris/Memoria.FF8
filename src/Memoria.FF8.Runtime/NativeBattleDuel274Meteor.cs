using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel274MeteorInputs(NativeBattleDuel274MoveInputs Moves, NativeBattleDuel274ActorServices Actors, Func<int, NativeBattleModuleAnchor> ReadAnchor571400, Func<NativeBattleDuel274Frame, uint, NativeBattleActionMatrix, uint, int?, uint?, NativeBattleDuel274Frame> Draw572200, Func<NativeBattleDuel274Frame, int, NativeBattleVector, NativeBattleVector, NativeBattleVector, NativeBattleDuel274Frame> DrawBody68D1D0);

public static partial class NativeBattleDuel274BasicMoves
{
    public static NativeBattleDuel274Frame Advance68CA60(NativeBattleDuel274Frame frame, int slot, NativeBattleDuel274Resources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattleDuel274MeteorInputs supplied)
    {
        NativeBattleDuel274MoveInputs inputs = supplied.Moves;
        NativeBattleModulePoolTask task = Target(frame, slot, 7);
        int actor = Actor(frame);
        int target = NativeBattleDuel274Controller.Index(task.ReadDword(20));
        if (task.ReadWord(12) == 0)
        {
            NativeBattleGraphicsModel targetModel = inputs.ReadModel(target);
            NativeBattleModelTransform targetTransform = targetModel.Transform ?? throw new NotSupportedException("Native Meteor Strike requires actual target position fields.");
            inputs.WriteModel(target, targetModel with { Transform = targetTransform with { Position28 = new(Signed(task, 40), Signed(task, 42), Signed(task, 44)), Vertical34 = Signed(task, 46) } });
            NativeBattleGraphicsModel actorModel = inputs.ReadModel(actor);
            NativeBattleModelTransform actorTransform = actorModel.Transform ?? throw new NotSupportedException("Native Meteor Strike requires actual actor position fields.");
            int angle = (actorModel.Rotation14 ?? throw new NotSupportedException("Native Meteor Strike requires actual actor yaw.")) + 2048;
            int distance = targetTransform.Scale38 >> 1;
            NativeBattleVector position = actorTransform.Position28 with { X = unchecked((short)(Signed(task, 40) - (distance * NativeBattleCameraMath.Sine(angle) >> 12))), Z = unchecked((short)(Signed(task, 44) - (distance * NativeBattleCameraMath.Cosine(angle) >> 12))) };
            inputs.WriteModel(actor, actorModel with { Transform = actorTransform with { Position28 = position } });
            task = Point(task, 96, inputs.ReadBone502170(actor, 21)).WriteDword(104, 0);
            (short maximum, short minimum) = supplied.Actors.ReadBounds6DFD90(target);
            task = task.WriteWord(92, unchecked((ushort)maximum)).WriteWord(94, unchecked((ushort)minimum));
            uint cursor = Cursor(frame);
            task = task.WriteDword(72, cursor).WriteDword(76, checked(cursor + 296));
            frame = SetCursor(Store(frame, slot, task), checked(cursor + 2828 + 1408));
            frame = NativeBattleDuel274Banks.Initialize508300(frame, 0x2508478, checked(cursor + 2828), 44, 32);
            frame = NativeBattleDuel274Workspaces.Initialize7016B0(frame, resources, cursor, 0x10F73B0, 296);
            frame = NativeBattleDuel274Workspaces.Initialize7016B0(frame, resources, checked(cursor + 296), 0x10F89F4, 2532);
        }
        short counter = Signed(task, 12);
        if (counter is >= 44 and < 62)
        {
            NativeBattleVector bone = inputs.ReadBone502170(actor, 19);
            (int x, int y, int z) = NativeBattleGfMatrices.RotateLong56C600(view, bone.X, 0, bone.Z);
            NativeBattleActionMatrix matrix = view with { X = unchecked(view.X + x), Y = unchecked(view.Y + y), Z = unchecked(view.Z + z) };
            frame = DrawMeteorHierarchy(frame, task.ReadDword(72), matrix, 0);
            if (counter < 53)
            {
                bone = inputs.ReadBone502170(actor, 19);
                NativeBattleActionMatrix local = Yaw(unchecked((short)(counter << 7)), bone.X, 0, bone.Z);
                short angle = unchecked((short)(-2 * bone.Y));
                short[] rotation = local.Rotation.ToArray();
                rotation[4] = angle;
                local = local with { Rotation = rotation.ToImmutableArray() };
                frame = supplied.Draw572200(frame, 0x10E7E18, NativeBattleGfMatrices.Compose56C2F0(view, local), 0xF0, (counter - 44) << 9, 0);
                if (counter < 50)
                {
                    frame = NativeBattleDuel274MeteorParticles.Emit68CA60(frame, bone, 0, 2, false);
                }
            }
        }
        uint elapsed = unchecked((uint)(counter - 60));
        if (elapsed < 16)
        {
            NativeBattleGraphicsModel model = inputs.ReadModel(target);
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Meteor Strike ground wave requires actual target position and scale.");
            short scale = unchecked((short)((transform.Scale38 + 4096) >> 1));
            NativeBattleActionMatrix local = new([scale, 0, 0, 0, scale, 0, 0, 0, scale], transform.Position28.X, 0, transform.Position28.Z, 4096);
            frame = DrawMeteorHierarchy(frame, task.ReadDword(76), NativeBattleGfMatrices.Compose56C2F0(view, local), (transform.Scale38 - 2048) >> 3);
            if (elapsed < 4)
            {
                frame = NativeBattleDuel274MeteorParticles.Emit68CA60(frame, transform.Position28, transform.Vertical34, 4, true);
            }
        }
        if (unchecked((ushort)counter) <= 59)
        {
            NativeBattleVector bone = inputs.ReadBone502170(actor, 21);
            uint curve = checked(0x110E3F0u + (uint)counter * 8);
            NativeBattleVector point = new(unchecked((short)(Signed(task, 40) + ReadWord(curve))), unchecked((short)(Signed(task, 42) + ReadWord(curve + 2) - Signed(task, 92))), unchecked((short)(Signed(task, 44) + ReadWord(curve + 4))));
            NativeBattleGraphicsModel model = inputs.ReadModel(target);
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Meteor Strike requires actual target curve position fields.");
            NativeBattleActionMatrix world = model.GeometryWorldMatrix ?? throw new NotSupportedException("Native Meteor Strike requires target matrix64 before direct translation publication.");
            inputs.WriteModel(target, model with { Transform = transform with { Position28 = point }, GeometryWorldMatrix = world with { X = point.X, Y = point.Y, Z = point.Z }, Flags0 = (ushort)(model.Flags0 | 0x1000) });
            task = Point(task, 96, bone).MarkPayloadUnknown(102, 2);
            frame = Store(frame, slot, task);
            if (counter == 28)
            {
                NativeBattleModuleAnchor anchor = supplied.ReadAnchor571400(target);
                NativeBattleGraphicsModel actorModel = inputs.ReadModel(actor);
                NativeBattleModelTransform actorTransform = actorModel.Transform ?? throw new NotSupportedException("Native Meteor Strike snap requires actual actor position fields.");
                task = task.WriteWord(96, unchecked((ushort)(Signed(task, 96) + anchor.X - actorTransform.Position28.X))).WriteWord(100, unchecked((ushort)(Signed(task, 100) + anchor.Z - actorTransform.Position28.Z)));
                frame = Store(frame, slot, task);
                inputs.WriteModel(actor, actorModel with { Transform = actorTransform with { Position28 = actorTransform.Position28 with { X = anchor.X, Z = anchor.Z } } });
            }
        }
        if (counter is >= 58 and <= 67)
        {
            NativeBattleGraphicsModel model = inputs.ReadModel(actor);
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Meteor Strike withdrawal requires actual actor WORD32.");
            inputs.WriteModel(actor, model with { Transform = transform with { Position28 = transform.Position28 with { Z = unchecked((short)(transform.Position28.Z + ((counter - 57) << 7))) } } });
        }
        if (counter == 61)
        {
            frame = inputs.Flash5712C0(frame, true);
        }
        if (counter == 70)
        {
            frame = inputs.Contact506690(frame, task.ReadByte(14));
        }
        elapsed = unchecked((uint)(counter - 55));
        if (elapsed < 256)
        {
            short center;
            if (elapsed >= 4)
            {
                (short maximum, short minimum) = supplied.Actors.ReadBounds6DFD90(target);
                task = task.WriteWord(92, unchecked((ushort)maximum)).WriteWord(94, unchecked((ushort)minimum));
                center = unchecked((short)((maximum + minimum) >> 1));
            }
            else
            {
                center = unchecked((short)(Signed(task, 92) + (unchecked((int)elapsed * (Signed(task, 94) - Signed(task, 92))) >> 3)));
                int angle = Math.Max(unchecked((int)task.ReadDword(104) - 512), -2048);
                task = task.WriteDword(104, unchecked((uint)angle));
            }
            int vertical = 4096;
            if (elapsed is >= 4 and < 12)
            {
                vertical -= NativeBattleCameraMath.Sine(unchecked((int)(elapsed - 4) << 8)) >> 1;
                center = unchecked((short)(vertical * center >> 12));
            }
            frame = Store(frame, slot, task);
            NativeBattleVector rotation = new(Signed(task, 104), 0, 0);
            NativeBattleVector pivot = new(0, center, 0);
            NativeBattleVector scale = new(4096, unchecked((short)vertical), 4096);
            HashSet<int> visited = [];
            int current = target;
            do
            {
                if (!visited.Add(current))
                {
                    throw new InvalidDataException("Native Meteor Strike linked ring does not return to its selected model.");
                }
                if ((supplied.Actors.ReadFlags(current) & 2) != 0)
                {
                    frame = supplied.DrawBody68D1D0(frame, current, rotation, pivot, scale);
                    supplied.Actors.WriteFlags(current, (ushort)(supplied.Actors.ReadFlags(current) | 0x1004));
                }
                uint linked = supplied.Actors.ReadLinkedModel140(current);
                if (linked == 0)
                {
                    break;
                }
                current = NativeBattleDuel274Controller.Index(linked);
            }
            while (current != target);
        }
        frame = NativeBattleDuel274Banks.Advance508420(frame, 0x2508478, (current, particleSlot) => NativeBattleDuel274MeteorParticles.Advance68D3B0(current, particleSlot, view, supplied.Draw572200));
        return counter == 43 ? inputs.Sound501330(frame, 0x110BE6C) : frame;

        short ReadWord(uint address) => BinaryPrimitives.ReadInt16LittleEndian(resources.Read(address, 2).AsSpan());
        NativeBattleDuel274Frame DrawMeteorHierarchy(NativeBattleDuel274Frame current, uint workspace, NativeBattleActionMatrix matrix, int bias)
        {
            uint morph = checked(BinaryPrimitives.ReadUInt32LittleEndian(current.Staging.Read(0x2508430, 4)) + 116);
            return NativeBattleDuel274Workspaces.Advance701970(current, resources, workspace, new(matrix, null, bias, 0, null, morph), projection, colors, inputs.Submit);
        }
    }
}
