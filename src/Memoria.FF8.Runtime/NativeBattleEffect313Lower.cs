using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect313Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect313Lower
{
    public static NativeBattleEffect313Step Advance(NativeBattleEffect313Frame frame, uint address, NativeBattleEffect313Resources resources, NativeBattleEffect313Inputs inputs) => Read(frame, address).Callback switch
    {
        0x59B9F0 => AdvanceDebris(frame, address, resources, inputs),
        0x59BB60 => AdvanceRing(frame, address, resources, inputs),
        0x59BCA0 => AdvanceSparkle(frame, address, resources, inputs),
        0x59BDF0 => AdvanceBeam(frame, address, resources, inputs),
        0x59C250 => AdvanceSmoke(frame, address, resources, inputs),
        0x59C2F0 => AdvanceExpansion(frame, address, resources, inputs),
        0x59C3E0 => AdvanceRotatingSprite(frame, address, resources, inputs),
        0x59C4F0 => AdvanceActor(frame, address, inputs),
        0x59C9E0 => AdvanceCleanup(frame, address, inputs),
        _ => throw new NotSupportedException("Effect313 requires its selected native lower callback.")
    };

    public static NativeBattleEffect313Step AdvanceCleanup(NativeBattleEffect313Frame frame, uint address, NativeBattleEffect313Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (Signed(task, 12) == 0)
        {
            frame = Write(frame, address, task.WriteWord(14, 0));
            frame = inputs.Execute(frame, new NativeBattleEffect313AppendTextureLoad508630(checked(frame.State.ReadDword(0x223CC98) + 0x10000), address + 14));
        }
        task = Read(frame, address);
        return new(Write(frame, address, task.WriteWord(12, unchecked((ushort)(Signed(task, 12) + 1)))), task.ReadWord(14) != 0 ? 2 : 0);
    }

    public static bool Paused313(NativeBattleEffect313Frame frame, NativeBattleEffect313Inputs inputs) => (inputs.ReadFlags(frame) & 0x201) != 0;
    public static short Word313(NativeBattleEffectArena bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.Read(offset, 2));
    public static NativeBattleEffectArena Put313(NativeBattleEffectArena bytes, int offset, uint value) => bytes.Write(offset, BitConverter.GetBytes(value));
    public static NativeBattleEffectArena PutWord313(NativeBattleEffectArena bytes, int offset, int value) => bytes.Write(offset, BitConverter.GetBytes(unchecked((short)value)));
    public static int Random313(ref NativeBattleEffect313Frame frame)
    {
        NativeCrtRandomState random = frame.Random.Draw(out ushort value);
        frame = frame with { Random = random };
        return value;
    }

    public static uint Mandatory313(ref NativeBattleEffect313Frame frame, uint callback)
    {
        (frame, uint address) = Append(frame, 0x223BE18, callback);
        return address != 0 ? address : throw new InvalidDataException("Effect313 mandatory child allocation exhausted its actual native task bank.");
    }

    private static NativeBattleEffect313Frame DrawMesh313(NativeBattleEffect313Frame frame, NativeBattleActionMatrix local, uint source, uint flags, int? cue, NativeBattleEffect313Resources resources, NativeBattleEffect313Inputs inputs)
    {
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local), null));
        (frame, uint temporary) = Allocate(frame, 88);
        NativeBattleEffectArena context = Put313(Put313(Context(frame, temporary, 88), 0, source), 28, flags);
        if (cue is { } value)
        {
            context = context.Write(8, new byte[3]);
            context = Put313(context, 12, unchecked((uint)value));
        }
        frame = PublishContext(frame, temporary, context, 88);
        NativeBattleStaticMeshContext88Step draw = NativeBattleStaticMeshContext88.DrawLoaded(frame.Memory, resources.ReadMesh(source), inputs.ReadProjection(frame), context, inputs.Colors, 17, 2);
        frame = PublishContext(frame with { Memory = draw.Memory }, temporary, draw.Context, 88);
        frame = inputs.Execute(frame, new NativeBattleEffect313DrawBatch(draw.Batch, 0x223CC9C));
        return Release(frame, 88);
    }

    private static NativeBattleActionMatrix AtTask313(NativeBattleActionMatrix rotation, NativeBattleModulePoolTask task) => rotation with { X = Signed(task, 16), Y = Signed(task, 18), Z = Signed(task, 20) };
    private static NativeBattleEffect313Frame DrawSprite313(NativeBattleEffect313Frame frame, uint temporary, NativeBattleEffectArena context, NativeBattleEffect313Resources resources, NativeBattleEffect313Inputs inputs)
    {
        frame = PublishContext(frame, temporary, context, 180);
        NativeBattleModuleSpriteDraw draw = NativeBattleModuleSpriteDrawing.Draw571C80(frame.Memory, context, resources.Sprite, inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame), 17, 2);
        frame = PublishContext(frame with { Memory = draw.Memory }, temporary, draw.Temporary180, 180);
        frame = inputs.Execute(frame, new NativeBattleEffect313DrawBatch(draw.Batch, 0x223CC9C));
        return Release(frame, 180);
    }

    private static NativeBattleEffect313Frame SetupSprite313(NativeBattleEffect313Frame frame, NativeBattleEffectArena point, int scale, int depth, NativeBattleEffect313Inputs inputs)
    {
        (byte[] packed, bool[] known) = point.ReadSnapshot(0, 8);
        short? w = known[6] && known[7] ? BinaryPrimitives.ReadInt16LittleEndian(packed.AsSpan(6)) : null;
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), Vector(point), w, unchecked((short)scale), unchecked((short)depth)) };
        NativeBattleGeometryRegisterStep registers = Registers(frame);
        registers = registers with { Colors = registers.Colors with { NormalInput = System.Collections.Immutable.ImmutableArray.Create(packed), NormalInputKnown = System.Collections.Immutable.ImmutableArray.Create(known) } };
        return Publish(frame, registers);
    }
}
