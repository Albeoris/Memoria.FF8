using System.Buffers.Binary;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeFieldRegistrationOverlapTests
{
    [Fact]
    public void OriginalRegistrationReusesConsumedSourceAndPreservesEveryAnimationFrame()
    {
        GameSession session = new(GameSession.Load(TestResources.FixturePath("menu-battle-field-entry")));
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        using GameAssets assets = new(TestResources.GameDirectory);
        FieldAssets field = assets.LoadField(session.State.FieldId);
        NativeFieldRegistrationResources source = field.NativeRegistration!;
        int groupCount = field.Jsm[0] + field.Jsm[1] + field.Jsm[2] + field.Jsm[3];
        ushort[] groups = Enumerable.Range(0, groupCount).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(field.Jsm.AsSpan(8 + index * 2, 2))).OrderBy(group => group >> 7).ToArray();
        NativeFieldRetainedActor[] actors = world.Events.Scene.NativeActorObjectIds.Select(objectId => new NativeFieldRetainedActor(objectId, world.Events.Actors[objectId].ModelId is -1 or ushort.MaxValue ? -1 : world.Events.Actors[objectId].ModelId, groups[objectId] >> 7, (groups[objectId] >> 7) + (groups[objectId] & 127) + 1)).ToArray();
        NativeFieldRegistrationStep step = NativeFieldRegistrationMachine.ReloadKind3(source, actors, NativeBattleRetainedMemory.FromColdProcess());
        Assert.True(step.State.EndAddress > step.State.RelocatedSourceAddress, "This external fixture must exercise native consumed-source arena reuse.");
        Assert.True(step.State.EndAddress < NativeFieldRegistrationMachine.Kind3Limit);
        NativeBattleFileStaging memory = step.Memory.FileStaging!;
        foreach (NativeFieldRegisteredResource resource in step.State.Resources)
        {
            NativeFieldOneModelEntry entry = source.Directory[resource.ModelId];
            int original = entry.Offset;
            if ((entry.Flag >> 28) != 0xD)
            {
                int geometry = entry.Offset + entry.ModelOffset;
                original = geometry + checked((int)BinaryPrimitives.ReadUInt32LittleEndian(source.One.Data.AsSpan(geometry + 56, 4)));
            }
            ushort clips = BinaryPrimitives.ReadUInt16LittleEndian(source.One.Data.AsSpan(original, 2));
            Assert.Equal(clips, BinaryPrimitives.ReadUInt16LittleEndian(memory.Read(resource.Address + 82, 2)));
            uint headers = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(resource.Address + 84, 4));
            uint output = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(resource.Address + 88, 4));
            original += 2;
            for (int clip = 0; clip < clips; clip++)
            {
                ushort frames = BinaryPrimitives.ReadUInt16LittleEndian(source.One.Data.AsSpan(original, 2));
                ushort bones = BinaryPrimitives.ReadUInt16LittleEndian(source.One.Data.AsSpan(original + 2, 2));
                Assert.Equal(frames, BinaryPrimitives.ReadUInt16LittleEndian(memory.Read(headers + (uint)clip * 4, 2)));
                Assert.Equal(bones + 1, BinaryPrimitives.ReadUInt16LittleEndian(memory.Read(headers + (uint)clip * 4 + 2, 2)));
                original += 4;
                for (int frame = 0; frame < frames; frame++)
                {
                    Assert.Equal(source.One.Data.AsSpan(original, 6).ToArray(), memory.Read(output, 6));
                    Assert.Equal(source.One.Data.AsSpan(original + 6, bones * 4).ToArray(), memory.Read(output + 8, bones * 4));
                    original += 6 + bones * 4;
                    output += 8u + (uint)bones * 4;
                }
            }
        }
        step.State.Validate(step.Memory);
        Assert.Throws<InvalidDataException>(() => (step.State with { EndAddress = NativeFieldRegistrationMachine.Kind3Limit + 4 }).Validate(step.Memory));
    }
}
