using System.Buffers.Binary;
using System.Numerics;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Assets.Tests;

public sealed class BattleFormationTests
{
    private const string GameDirectory = @"C:\Steam\steamapps\common\FINAL FANTASY VIII";

    [Fact]
    public void OriginalTowerFormationLoadsSuspendedWedgeAndRetainsElvoretReserve()
    {
        using BattleAssets assets = new(GameDirectory);
        BattleEncounter encounter = assets.LoadEncounter(29);
        Assert.Equal(3, encounter.StageId);
        Assert.Equal(2, encounter.Enemies.Count);
        Assert.Equal(8, encounter.Slots.Count);
        BattleEnemyPlacement biggs = encounter.Enemies[0];
        BattleEnemyPlacement wedge = encounter.Enemies[1];
        BattleEnemyPlacement elvoret = encounter.Slots[2];
        Assert.Equal((0, 74, 110), (biggs.Slot, biggs.ModelId, biggs.Level));
        Assert.Equal((1, 73, 110), (wedge.Slot, wedge.ModelId, wedge.Level));
        Assert.False(biggs.Hidden || biggs.Untargetable || biggs.InitiallySuspended);
        Assert.True(wedge.Enabled && wedge.Hidden && wedge.Untargetable && wedge.InitiallySuspended);
        Assert.Equal((2, 91, 111), (elvoret.Slot, elvoret.ModelId, elvoret.Level));
        Assert.False(elvoret.Enabled);
        Assert.True(elvoret.Hidden && elvoret.Untargetable && elvoret.InitiallySuspended);
        Assert.Equal("Biggs", assets.ReadEnemyName(biggs.ModelId));
        Assert.Equal("Wedge", assets.ReadEnemyName(wedge.ModelId));
        Assert.Equal("Elvoret", assets.ReadEnemyName(elvoret.ModelId));
        Assert.NotEmpty(assets.LoadEnemy(biggs.ModelId).Meshes);
        Assert.NotEmpty(assets.LoadEnemy(wedge.ModelId).Meshes);
        Assert.NotEmpty(assets.LoadEnemy(elvoret.ModelId).Meshes);
        Assert.Equal(new Vector3(0, 0, 13), biggs.Position);
        Assert.Equal(new Vector3(-17, 0, 19), wedge.Position);
    }

    [Fact]
    public void FormationFlagsCoordinatesLevelsAndNativeModelKindsKeepTheSameSlot()
    {
        byte[] record = new byte[128];
        record[4] = 0x55;
        record[5] = 0x33;
        record[6] = 0x0F;
        record[7] = 0xAA;
        for (int slot = 0; slot < 8; slot++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(record.AsSpan(8 + 6 * slot), (short)(100 * slot));
            BinaryPrimitives.WriteInt16LittleEndian(record.AsSpan(10 + 6 * slot), (short)(-200 * slot));
            BinaryPrimitives.WriteInt16LittleEndian(record.AsSpan(12 + 6 * slot), (short)(300 * slot));
            record[56 + slot] = (byte)(slot == 0 ? 0 : 16 + slot);
            record[120 + slot] = (byte)(10 + slot);
        }
        BattleEncounter encounter = BattleFormationReader.Read(0, record);
        Assert.Equal(new[] { 0, 2, 4, 6 }, encounter.Enemies.Select(slot => slot.Slot));
        Assert.False(encounter.Slots[0].IsEnemyModel);
        Assert.Equal(0, encounter.Slots[0].NativeModelId);
        for (int slot = 0; slot < 8; slot++)
        {
            BattleEnemyPlacement placement = encounter.Slots[slot];
            Assert.Equal(new Vector3(slot, 2 * slot, -3 * slot), placement.Position);
            Assert.Equal(10 + slot, placement.Level);
            Assert.Equal(slot % 2 == 1, placement.Hidden);
            Assert.Equal(slot % 4 >= 2, placement.InitiallySuspended);
            Assert.Equal(slot >= 4, placement.Untargetable);
            Assert.Equal(slot % 2 == 0, placement.Enabled);
        }
    }

    [Fact]
    public void EveryInstalledFormationDecodesWithoutConflatingParticipantFlagsAndResources()
    {
        using BattleAssets assets = new(GameDirectory);
        int suspended = 0;
        int reserves = 0;
        for (int id = 0; id < 1024; id++)
        {
            BattleEncounter encounter = assets.LoadEncounter(id);
            Assert.Equal(id, encounter.Id);
            Assert.Equal(Enumerable.Range(0, 8), encounter.Slots.Select(slot => slot.Slot));
            Assert.Equal(encounter.Slots.Where(slot => slot.Enabled), encounter.Enemies);
            suspended += encounter.Enemies.Count(slot => slot.InitiallySuspended);
            reserves += encounter.Slots.Count(slot => !slot.Enabled && slot.NativeModelId != 16);
        }
        Assert.True(suspended > 0);
        Assert.True(reserves > 0);
    }

    [Theory]
    [InlineData(127)]
    [InlineData(129)]
    public void PartialOrOversizedFormationRejectsBeforeDecoding(int length)
    {
        Assert.Throws<InvalidDataException>(() => BattleFormationReader.Read(0, new byte[length]));
    }
}
