using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class BattleAssets
{
    public NativeBattleGraphicsDefinition ReadNativeGraphicsDefinition(IReadOnlyList<BattleCombatantDefinition> actors, NativeBattleGraphicsRules rules)
    {
        rules.Validate();
        ImmutableArray<NativeBattleGraphicsBinding> bindings = actors.Select(actor => new NativeBattleGraphicsBinding(actor.Id, checked((byte)(actor.IsPlayer ? actor.ModelId : actor.ModelId + 16)), ReadNativeGraphicsResource(actor))).ToImmutableArray();
        //47DAC0 queues enemy103 completions before47DD30/47DBA0 player102/103 descriptors.
        ImmutableArray<string> order = actors.Where(actor => !actor.IsPlayer && actor.InitialActive).Concat(actors.Where(actor => actor.IsPlayer)).Select(actor => actor.Id).ToImmutableArray();
        return new(rules, bindings, order);
    }

    public NativeBattleGraphicsResource ReadNativeGraphicsResource(BattleCombatantDefinition actor)
    {
        if (actor.IsPlayer)
        {
            BattleModel body = LoadPlayer(actor.ModelId, actor.Costume);
            BattleModel owner = LoadPlayerSequenceOwner(actor.ModelId, actor.WeaponId, body);
            return (owner.Sequences ?? throw new InvalidDataException("Native player sequence owner lacks its original resource.")).NativeResource with { AnimationFrames = owner.Animations.Select(animation => checked((byte)animation.Frames.Count)).ToImmutableArray() };
        }
        return ReadEnemyNativeGraphicsResource(actor.ModelId);
    }

    public NativeBattleGraphicsResource ReadEnemyNativeGraphicsResource(int model)
    {
        byte[] bytes = Read($"c0m{model:000}.dat");
        int sections = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        if (sections != 11)
        {
            throw new NotSupportedException("A native graphics model requires its original eleven-section DAT.");
        }
        int start = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(16));
        int end = Enumerable.Range(0, sections).Select(index => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4 + 4 * index))).Where(offset => offset > start).DefaultIfEmpty(bytes.Length).Min();
        int animationStart = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12));
        int animationCount = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(animationStart));
        if ((uint)animationCount > 256 || animationStart + 4L + animationCount * 4L > bytes.Length)
        {
            throw new InvalidDataException("Original graphics animation directory exceeds its DAT.");
        }
        ImmutableArray<byte>.Builder frames = ImmutableArray.CreateBuilder<byte>(animationCount);
        for (int index = 0; index < animationCount; index++)
        {
            int pointer = checked(animationStart + BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(animationStart + 4 + index * 4)));
            if ((uint)pointer >= bytes.Length || bytes[pointer] == 0)
            {
                throw new InvalidDataException("Original graphics animation lacks its frame-count byte.");
            }
            frames.Add(bytes[pointer]);
        }
        NativeBattleGraphicsResource resource = new(bytes.AsSpan(start, end - start).ToArray().ToImmutableArray()) { AnimationFrames = frames.MoveToImmutable() };
        resource.Validate();
        return resource;
    }
}
