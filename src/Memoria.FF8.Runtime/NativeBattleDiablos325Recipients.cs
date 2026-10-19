using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDiablos325RecipientServices(Func<int, NativeBattleGraphicsModel> ReadActor, Action<int, NativeBattleGraphicsModel> WriteActor, Func<int, NativeBattlePoseSource> ReadPose, Func<int, ImmutableArray<NativeBattleRawMesh>> ReadGeometry, Func<NativeBattleDiablos325Frame, int, int, NativeBattleDiablos325Frame> DrawScaled);

public static class NativeBattleDiablos325Recipients
{
    public static NativeBattleDiablos325ProgrammeStep Advance654E7E(NativeBattleDiablos325Frame frame, NativeBattleModulePoolTask task, NativeBattleActionPacketsState packets, NativeBattleDiablos325RecipientServices services)
    {
        int counter = unchecked((short)task.ReadWord(0xC));
        if (counter is < 368 or > 405)
        {
            throw new ArgumentOutOfRangeException(nameof(task));
        }
        NativeBattleDiablos325State state = NativeBattleDiablos325Entry.RequireState(frame.Memory);
        int height = Height654E7E(counter - 368);
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Native Diablos recipient sink requires its retained action header.");
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        int count = header.ReadByte(0x10);
        for (int index = 0; index < count; index++)
        {
            int record = 0x94 + index * 12;
            uint address = task.ReadDword(record);
            int actorIndex = ActorIndex(address);
            NativeBattleGraphicsModel actor = services.ReadActor(actorIndex);
            if (counter == 368)
            {
                (short maximum, short minimum) = Bounds6DFD90(actor, services.ReadPose(actorIndex), services.ReadGeometry(actorIndex));
                task = task.WriteWord(record + 4, unchecked((ushort)maximum)).WriteWord(record + 6, unchecked((ushort)minimum));
                actor = actor with { Flags0 = (ushort)(actor.Flags0 | 8) };
            }
            NativeBattleActionMatrix world = actor.GeometryWorldMatrix ?? throw new NotSupportedException("Native Diablos recipient sink requires its retained actor matrix.");
            int angle = Math.Min(Math.Abs(unchecked((world.X - state.CenterX51D8) >> 2)), 1024);
            int maximumY = unchecked((short)task.ReadWord(record + 4));
            int minimumY = unchecked((short)task.ReadWord(record + 6));
            int extent = unchecked((minimumY - maximumY) * NativeBattleCameraMath.Cosine(angle)) >> 12;
            int scale = extent < height ? unchecked(height << 12) / extent : 4096;
            int offset = extent < height ? unchecked(scale * maximumY) / 4096 : maximumY;
            NativeBattleModelTransform transform = actor.Transform ?? throw new NotSupportedException("Native Diablos recipient sink requires its physical placement WORDs.");
            actor = actor with { GeometryWorldMatrix = world with { Y = unchecked(transform.Position28.Y - offset) } };
            services.WriteActor(actorIndex, actor);
            frame = services.DrawScaled(frame, actorIndex, scale);
            actor = services.ReadActor(actorIndex);
            services.WriteActor(actorIndex, actor with { Flags0 = (ushort)(actor.Flags0 | 4) });
        }
        return new(frame, task);
    }

    public static (short Maximum, short Minimum) Bounds6DFD90(NativeBattleGraphicsModel actor, NativeBattlePoseSource source, ImmutableArray<NativeBattleRawMesh> geometry)
    {
        NativeBattleActionMatrix world = actor.GeometryWorldMatrix ?? throw new NotSupportedException("Native Diablos vertex bounds require the retained actor world matrix.");
        ImmutableArray<NativeBattleActionMatrix> posed = NativeBattleBodyBoneMatrices.Require(actor, source);
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattlePoseMatrices.ComposeStageCamera(posed, world, 0);
        uint mask = actor.SequenceState?.MeshMask124 ?? throw new NotSupportedException("Native Diablos vertex bounds require the current actor object mask.");
        int minimum = 65536;
        int maximum = -65536;
        for (int mesh = 0; mesh < geometry.Length; mesh++)
        {
            if ((mask & (1u << (mesh & 31))) == 0)
            {
                continue;
            }
            foreach (NativeBattleRawVertex vertex in geometry[mesh].Vertices)
            {
                NativeBattleVector point = NativeBattleGfMatrices.Transform460840(bones[vertex.Bone], vertex.Position);
                minimum = Math.Min(minimum, point.Y);
                maximum = Math.Max(maximum, point.Y);
            }
        }
        return (unchecked((short)maximum), unchecked((short)minimum));
    }

    public static int Height654E7E(int age)
    {
        int ratio = age * 4096 / 38;
        return unchecked(Curve(ratio) * 4 - 16640);
    }

    internal static int Curve(int ratio)
    {
        if (ratio >= 1365)
        {
            return unchecked((ratio - 1365) * 426 >> 11) + 3670;
        }
        return NativeBattleCameraMath.Sine(NativeBattleCameraMath.Sine(NativeBattleCameraMath.Sine(ratio >> 2) >> 2) >> 2);
    }

    internal static int ActorIndex(uint address)
    {
        if (address < 0x1D972C0 || (address - 0x1D972C0) % 156 != 0 || (address - 0x1D972C0) / 156 >= 7)
        {
            throw new InvalidDataException("Native Diablos recipient requires its exact physical actor pointer.");
        }
        return checked((int)((address - 0x1D972C0) / 156));
    }
}
