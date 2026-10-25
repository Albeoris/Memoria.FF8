using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState AdvanceNativeOriginalEffect(NativeBattleGraphicsState graphics)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native500900 requires its retained returned-task pointer.");
        if (memory.ReadReturnedTask5044B0() == 0)
        {
            return graphics;
        }
        NativeBattleOriginalEffectState selected = graphics.OriginalEffect ?? throw new InvalidDataException("Native returned task has no current module selection.");
        NativeBattleOriginalEffectState task = memory.DetachedReturnedTask ?? selected;
        if (task.ReturnedDescriptor != memory.ReturnedTaskPointer1D96AAC)
        {
            throw new InvalidDataException("Native500900 returned-task pointer disagrees with its actual pool owner.");
        }
        NativeBattleGraphicsState advanced = AdvanceNativeOriginalEffectTask(graphics with { OriginalEffect = task });
        NativeBattleOriginalEffectState current = advanced.OriginalEffect ?? throw new InvalidDataException("Native500900 lost its polled returned-task owner.");
        memory = NativeEffectMemory!;
        if (current.ReturnedDescriptor == 0)
        {
            NativeEffectMemory = memory with { ReturnedTaskPointer1D96AAC = 0, DetachedReturnedTask = null };
        }
        else if (memory.DetachedReturnedTask is not null)
        {
            NativeEffectMemory = memory with { DetachedReturnedTask = current };
        }
        return memory.DetachedReturnedTask is null ? advanced : advanced with { OriginalEffect = selected };
    }

    private NativeBattleGraphicsState AdvanceNativeOriginalEffectTask(NativeBattleGraphicsState graphics)
    {
        if (graphics.OriginalEffect is not { ReturnedDescriptor: > 0 } effect)
        {
            return graphics;
        }
        if (effect.Binding.Effect is >= 292 and <= 296)
        {
            return AdvanceNativeStatusCureItem(graphics);
        }
        if (effect.Binding.Entry is 0x6BD8E0 or 0x6BE960)
        {
            return AdvanceNativeStatusMagic(graphics);
        }
        if (effect.Binding.Entry == 0x6EA500)
        {
            return AdvanceNativeStorm11(graphics);
        }
        if (effect.Binding.Entry == 0x6D2AE0)
        {
            return AdvanceNativeReflect(graphics);
        }
        if (effect.Binding.Entry == 0x86B9A0)
        {
            return AdvanceNativeProtect(graphics);
        }
        if (effect.Binding.Entry == 0x866010)
        {
            return AdvanceNativeShell(graphics);
        }
        if (effect.Binding.Entry == 0x694520)
        {
            return AdvanceNativeWhiteWind226(graphics);
        }
        if (effect.Binding.Entry == 0x8885D0)
        {
            return AdvanceNativeClash26(graphics);
        }
        if (effect.Binding.Entry == 0x6185D0)
        {
            return AdvanceNativeSleep145(graphics);
        }
        if (effect.Binding.Entry == NativeBattleShockwavePulsar260Profile.Entry)
        {
            return AdvanceNativeShockwavePulsar260(graphics);
        }
        if (effect.Binding.Entry == NativeBattleRayBomb307Profile.Entry)
        {
            return AdvanceNativeRayBomb307(graphics);
        }
        if (effect.Binding.Entry == NativeBattleUltraWaves184Profile.Entry)
        {
            return AdvanceNativeUltraWaves184(graphics);
        }
        if (effect.Binding.Entry == NativeBattleElectrocute275Profile.Entry)
        {
            return AdvanceNativeElectrocute275(graphics);
        }
        if (effect.Binding.Entry == NativeBattleHypnotize133Profile.Entry)
        {
            return RunNativeHypnotize133(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleBeamLaser134Profile.Entry)
        {
            return RunNativeBeamLaser134(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect101Profile.Entry)
        {
            return RunNativeEffect101(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEnergyBomber217Profile.Entry)
        {
            return RunNativeEnergyBomber217(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleElectricDischarge126Profile.Entry)
        {
            return RunNativeElectricDischarge126(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleReflectBeam135Profile.Entry)
        {
            return RunNativeReflectBeam135(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleSaliva138Profile.Entry)
        {
            return RunNativeSaliva138(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect283Profile.Entry)
        {
            return RunNativeEffect283(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect69Profile.Entry)
        {
            return RunNativeEffect69(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect344Profile.Entry)
        {
            return RunNativeEffect344(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect70Profile.Entry)
        {
            return RunNativeEffect70(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect79Profile.Entry)
        {
            return RunNativeEffect79(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect64Profile.Entry)
        {
            return RunNativeEffect64(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect65Profile.Entry)
        {
            return RunNativeEffect65(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect66Profile.Entry)
        {
            return RunNativeEffect66(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect68Profile.Entry)
        {
            return RunNativeEffect68(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect166Profile.Entry)
        {
            return RunNativeEffect166(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect210Profile.Entry)
        {
            return RunNativeEffect210(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect285Profile.Entry)
        {
            return RunNativeEffect285(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect324Profile.Entry)
        {
            return RunNativeEffect324(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect290Profile.Entry)
        {
            return RunNativeEffect290(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect308Profile.Entry)
        {
            return RunNativeEffect308(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect251Profile.Entry)
        {
            return RunNativeEffect251(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect320Profile.Entry)
        {
            return RunNativeEffect320(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect345Profile.Entry)
        {
            return RunNativeEffect345(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect316Profile.Entry)
        {
            return RunNativeEffect316(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect63Profile.Entry)
        {
            return RunNativeEffect63(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect54Profile.Entry)
        {
            return RunNativeEffect54(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect313Profile.Entry)
        {
            return RunNativeEffect313(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect287Profile.Entry)
        {
            return RunNativeEffect287(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect301Profile.Entry)
        {
            return RunNativeEffect301(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect321Profile.Entry)
        {
            return RunNativeEffect321(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect318Profile.Entry)
        {
            return RunNativeEffect318(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect323Profile.Entry)
        {
            return RunNativeEffect323(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect322Profile.Entry)
        {
            return RunNativeEffect322(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect319Profile.Entry)
        {
            return RunNativeEffect319(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect227Profile.Entry)
        {
            return RunNativeEffect227(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect213Profile.Entry)
        {
            return RunNativeEffect213(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect209Profile.Entry)
        {
            return RunNativeEffect209(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect207Profile.Entry)
        {
            return RunNativeEffect207(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect174Profile.Entry)
        {
            return RunNativeEffect174(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect173Profile.Entry)
        {
            return RunNativeEffect173(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect167Profile.Entry)
        {
            return RunNativeEffect167(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect140Profile.Entry)
        {
            return RunNativeEffect140(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect76Profile.Entry)
        {
            return RunNativeEffect76(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect80Profile.Entry)
        {
            return RunNativeEffect80(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect83Profile.Entry)
        {
            return RunNativeEffect83(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect289Profile.Entry)
        {
            return RunNativeEffect289(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect315Profile.Entry)
        {
            return RunNativeEffect315(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect303Profile.Entry)
        {
            return RunNativeEffect303(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect298Profile.Entry)
        {
            return RunNativeEffect298(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleMegaSpark279Profile.Entry)
        {
            return RunNativeMegaSpark279(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleBoomerangSword304Profile.Entry)
        {
            return RunNativeBoomerangSword304(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleSnipeLaser302Profile.Entry)
        {
            return RunNativeSnipeLaser302(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEnemyMicroMissiles284Profile.Entry)
        {
            return RunNativeEnemyMicroMissiles284(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleMiniPulseCannon286Profile.Entry)
        {
            return RunNativeMiniPulseCannon286(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEvilEye282Profile.Entry)
        {
            return RunNativeEvilEye282(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleShotgun281Profile.Entry)
        {
            return RunNativeShotgun281(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleGravija214Profile.Entry)
        {
            return RunNativeGravija214(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleJunk44Profile.Entry)
        {
            return RunNativeJunk44(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleStare45Profile.Entry)
        {
            return RunNativeStare45(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleSigh46Profile.Entry)
        {
            return RunNativeSigh46(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleMagmaBreath48Profile.Entry)
        {
            return RunNativeMagmaBreath48(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleResonance49Profile.Entry)
        {
            return RunNativeResonance49(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleBladeShot12Profile.Entry)
        {
            return RunNativeBladeShot12(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleAcid170Profile.Entry)
        {
            return RunNativeAcid170(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleMorph172Profile.Entry)
        {
            return RunNativeMorph172(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattlePoisonGas171Profile.Entry)
        {
            return RunNativePoisonGas171(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleAdditional181Profile.Entry)
        {
            return RunNativeAdditional181(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleChainGun190Profile.Entry)
        {
            return RunNativeChainGun190(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleAdditional182Profile.Entry)
        {
            return RunNativeAdditional182(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleBlaster186Profile.Entry)
        {
            return RunNativeBlaster186(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleCannonBlow151Profile.Entry)
        {
            return RunNativeCannonBlow151(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleGatlingGun150Profile.Entry)
        {
            return RunNativeGatlingGun150(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleBreath129Profile.Entry)
        {
            return RunNativeBreath129(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleSonic139Profile.Entry)
        {
            return RunNativeSonic139(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect183Profile.Entry)
        {
            return RunNativeEffect183(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleBreath157Profile.Entry)
        {
            return RunNativeBreath157(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleWildCannonBlow156Profile.Entry)
        {
            return RunNativeWildCannonBlow156(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleSandStorm155Profile.Entry)
        {
            return RunNativeSandStorm155(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleGastricJuice128Profile.Entry)
        {
            return RunNativeGastricJuice128(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleDisolvingAcid132Profile.Entry)
        {
            return RunNativeDisolvingAcid132(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleOilShot136Profile.Entry)
        {
            return RunNativeOilShot136(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleBadBreath131Profile.Entry)
        {
            return RunNativeBadBreath131(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleOilBlast137Profile.Entry)
        {
            return RunNativeOilBlast137(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleMeltEye158Profile.Entry)
        {
            return RunNativeMeltEye158(graphics, false);
        }
        if (effect.Binding.Entry is 0x5CF760 or 0x5CEDD0)
        {
            return RunNativeNeedlesFamily(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleLaserEye71Profile.Entry)
        {
            return RunNativeLaserEye71(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEerieSoundWave130Profile.Entry)
        {
            return AdvanceNativeEerieSoundWave130(graphics);
        }
        if (effect.Binding.Entry == NativeBattlePetrifyStare127Profile.Entry)
        {
            return AdvanceNativePetrifyStare127(graphics);
        }
        if (effect.Binding.Entry == NativeBattleAcid317Profile.Entry)
        {
            return AdvanceNativeAcid317(graphics);
        }
        if (effect.Binding.Entry == NativeBattleGatlingGun305Profile.Entry)
        {
            return AdvanceNativeGatlingGun305(graphics);
        }
        if (effect.Binding.Entry == NativeBattleDegenerator306Profile.Entry)
        {
            return AdvanceNativeDegenerator306(graphics);
        }
        if (effect.Binding.Entry == NativeBattleBadBreath300Profile.Entry)
        {
            return AdvanceNativeBadBreath300(graphics);
        }
        if (effect.Binding.Entry == NativeBattleMicroMissiles299Profile.Entry)
        {
            return AdvanceNativeMicroMissiles299(graphics);
        }
        if (effect.Binding.Entry == NativeBattleWhiteWind297Profile.Entry)
        {
            return AdvanceNativeWhiteWind297(graphics);
        }
        if (effect.Binding.Entry == NativeBattleCurse47Controller.Entry)
        {
            return AdvanceNativeCurse47(graphics);
        }
        if (effect.Binding.Entry == NativeBattleDoom8Controller.Entry)
        {
            return AdvanceNativeDoom8(graphics);
        }
        if (effect.Binding.Entry is 0x8B68C0 or 0x7C4460)
        {
            return AdvanceNativeDeath(graphics);
        }
        if (effect.Binding.Entry == 0x60AFB0)
        {
            return AdvanceNativeUltrasonic152(graphics);
        }
        if (effect.Binding.Entry == 0x5CC840)
        {
            return AdvanceNativeSuicide180(graphics);
        }
        if (effect.Binding.Entry == 0x85F4E0)
        {
            return AdvanceNativeConfuse36(graphics);
        }
        if (effect.Binding.Entry == 0x6BD1C0)
        {
            return AdvanceNativeSlow123(graphics);
        }
        if (effect.Binding.Entry == 0x6BB6B0)
        {
            return AdvanceNativeHaste125(graphics);
        }
        if (effect.Binding.Entry == 0x6C9380)
        {
            return AdvanceNativeFloat115(graphics);
        }
        if (effect.Binding.Entry == 0x6BDE80)
        {
            return AdvanceNativeBlind(graphics);
        }
        if (effect.Binding.Entry == 0x614290)
        {
            return AdvanceNativeRegen147(graphics);
        }
        if (effect.Binding.Entry == 0x67E730)
        {
            return AdvanceNativeFullCure280(graphics);
        }
        if (effect.Binding.Entry == 0x6BBDA0)
        {
            return AdvanceNativeFlare124(graphics);
        }
        if (effect.Binding.Entry == 0x6C99C0)
        {
            return AdvanceNativeZombie(graphics);
        }
        if (effect.Binding.Entry == 0x6BEFD0)
        {
            return AdvanceNativeStop119(graphics);
        }
        if (effect.Binding.Entry == 0x6791F0)
        {
            return AdvanceNativeRapture288(graphics);
        }
        if (effect.Binding.Entry == 0x6C16A0)
        {
            return AdvanceNativeBreak117(graphics);
        }
        if (effect.Binding.Entry == 0x60F690)
        {
            return AdvanceNativeUltima149(graphics);
        }
        if (effect.Binding.Entry == 0x5BC960)
        {
            return AdvanceNativeWall189(graphics);
        }
        if (effect.Binding.Entry == 0x8D8510)
        {
            return AdvanceNativeQuake38(graphics);
        }
        if (effect.Binding.Entry == 0x7844A0)
        {
            return RunNativeTheEnd85(graphics, false);
        }
        if (effect.Binding.Entry == 0x865600)
        {
            return AdvanceNativePain34(graphics);
        }
        if (effect.Binding.Entry == 0x6CF4A0)
        {
            return AdvanceNativeDispel109(graphics, ReadNativeCurrentStackOperand, PublishNativeCurrentStackOperand);
        }
        if (effect.Binding.Entry == 0x614D70)
        {
            return AdvanceNativeTornado146(graphics);
        }
        if (effect.Binding.Entry is 0x8513D0 or 0x803200)
        {
            return AdvanceNativeDrain39(graphics);
        }
        if (effect.Binding.Entry == 0x6CFCD0)
        {
            return AdvanceNativeBerserk108(graphics);
        }
        if (effect.Binding.Entry == 0x8465C0)
        {
            return AdvanceNativeDribble41(graphics);
        }
        if (effect.Binding.Entry == 0x83ECC0)
        {
            return AdvanceNativeShoot42(graphics);
        }
        if (effect.Binding.Entry == 0x7003C0)
        {
            return AdvanceNativeLanding17(graphics);
        }
        if (effect.Binding.Entry == 0x6CE210)
        {
            return AdvanceNativeControl110(graphics);
        }
        if (effect.Binding.Entry == 0x5CDF40)
        {
            return AdvanceNativeBombGlow179(graphics);
        }
        if (effect.Binding.Entry == 0x684EE0)
        {
            return RunNativeXatmDeath276(graphics, false);
        }
        if (effect.Binding.Entry == 0x627E60)
        {
            return RunNativeRayBomb10(graphics, false);
        }
        if (effect.Binding.Entry == 0x683F10)
        {
            return AdvanceNativeFunguarLaser277(graphics);
        }
        if (effect.Binding.Entry == 0x5D00F0)
        {
            return AdvanceNativeSandStorm176(graphics);
        }
        if (effect.Binding.Entry == 0x607C10)
        {
            return AdvanceNativeUltraWaves154(graphics);
        }
        if (effect.Binding.Entry == 0x609640)
        {
            return AdvanceNativeStickyWeb153(graphics);
        }
        if (effect.Binding.Entry == 0x8BE1F0)
        {
            return AdvanceNativeDarkMist13(graphics);
        }
        if (effect.Binding.Entry == 0x6CAC20)
        {
            return AdvanceNativeBadBreath113(graphics);
        }
        if (effect.Binding.Entry == 0x6120D0)
        {
            return AdvanceNativeMeltdown(graphics);
        }
        if (effect.Binding.Entry == 0x6CD920)
        {
            return AdvanceNativeAura(graphics);
        }
        if (effect.Binding.Entry == 0x5E1750)
        {
            return AdvanceNativeSleepingGas168(graphics);
        }
        if (effect.Binding.Entry == 0x5DEE60)
        {
            return AdvanceNativeGastricJuice169(graphics);
        }
        if (effect.Binding.Entry == 0x5A6C20)
        {
            return AdvanceNativeRenzokukenVariant(graphics, NativeBattleRenzokukenVariantProfile.Renzokuken259);
        }
        if (effect.Binding.Entry == 0x58B930)
        {
            return AdvanceNativeRenzokukenVariant(graphics, NativeBattleRenzokukenVariantProfile.Renzokuken332);
        }
        if (effect.Binding.Entry == 0x589BD0)
        {
            return AdvanceNativeRenzokukenVariant(graphics, NativeBattleRenzokukenVariantProfile.Renzokuken333);
        }
        if (effect.Binding.Entry == 0x61DE70)
        {
            return AdvanceNativeRenzokuken141(graphics);
        }
        if (effect.Binding.Effect is >= 334 and <= 337 or >= 339 and <= 343)
        {
            return AdvanceNativeRenzokukenSpecial(graphics);
        }
        if (effect.Binding.Effect == 161)
        {
            return AdvanceNativeRenzokukenSpecial161(graphics);
        }
        if (effect.Binding.Entry == 0x600BC0)
        {
            return AdvanceNativeRenzokuken159(graphics);
        }
        if (effect.Binding.Entry == 0x5FF080)
        {
            return AdvanceNativeRenzokuken160(graphics);
        }
        if (effect.Binding.Entry == 0x5F90F0)
        {
            return AdvanceNativeFatedCircle162(graphics);
        }
        if (effect.Binding.Entry == 0x5B7190)
        {
            return AdvanceNativeShot193(graphics);
        }
        if (effect.Binding.Entry == 0x6CBBE0)
        {
            return AdvanceNativeDuel112(graphics);
        }
        if (effect.Binding.Entry == 0x6897E0)
        {
            return AdvanceNativeDuel274(graphics);
        }
        if (effect.Binding.Entry == 0x6A0D20)
        {
            return AdvanceNativeDuel212(graphics);
        }
        if (effect.Binding.Entry == 0x6959E0)
        {
            return AdvanceNativeDuel216(graphics);
        }
        if (effect.Binding.Entry == 0x69AB60)
        {
            return AdvanceNativeDuel215(graphics);
        }
        if (effect.Binding.Entry == 0x6391D0)
        {
            return AdvanceNativeNoMercy200(graphics);
        }
        if (effect.Binding.Entry == 0x5E9470)
        {
            return AdvanceNativeLionHeart165(graphics);
        }
        if (effect.Binding.Entry == 0x5F0260)
        {
            return AdvanceNativeBlastingZone164(graphics);
        }
        if (effect.Binding.Entry == 0x5F5B80)
        {
            return AdvanceNativeRoughDivide163(graphics);
        }
        if (effect.Binding.Entry == 0x78D7B0)
        {
            return AdvanceNativeAngelWing84(graphics);
        }
        if (effect.Binding.Entry == 0x77F250)
        {
            return AdvanceNativeRinoa86(graphics);
        }
        if (effect.Binding.Entry == 0x773870)
        {
            return AdvanceNativeRinoa87(graphics);
        }
        if (effect.Binding.Entry == 0x7684F0)
        {
            return AdvanceNativeRinoa88(graphics);
        }
        if (effect.Binding.Entry == 0x6F9CF0)
        {
            return AdvanceNativeRinoa89(graphics);
        }
        if (effect.Binding.Entry == 0x5AA3E0)
        {
            return AdvanceNativeShot198(graphics);
        }
        if (effect.Binding.Entry == 0x5AED30)
        {
            return AdvanceNativeShot197(graphics);
        }
        if (effect.Binding.Entry == 0x5B2A40)
        {
            return AdvanceNativeShot195(graphics);
        }
        if (effect.Binding.Entry == 0x5B4C60)
        {
            return AdvanceNativeShot194(graphics);
        }
        if (effect.Binding.Entry == 0x5B96F0)
        {
            return AdvanceNativeShot192(graphics);
        }
        if (effect.Binding.Entry == 0x5B1900)
        {
            return AdvanceNativeShot196(graphics);
        }
        if (effect.Binding.Entry == 0x5BE340)
        {
            return AdvanceNativeShot188(graphics);
        }
        if (effect.Binding.Entry == 0x84D030)
        {
            return RunNativeScan40(graphics, false);
        }
        if (effect.Binding.Entry == 0x5D1AD0)
        {
            return RunNativeHoly175(graphics, false);
        }
        if (effect.Binding.Entry == 0x6D3DB0)
        {
            return RunNativeThundaga105(graphics, false);
        }
        if (effect.Binding.Entry is 0x58D760 or 0x58D930 or 0x58DB10 or 0x58DCF0)
        {
            return RunNativeGilgamesh(graphics, false);
        }
        if (effect.Binding.Entry == 0x5A8750)
        {
            return RunNativeCactuar199(graphics, false);
        }
        if (effect.Binding.Entry == 0x5C0D50)
        {
            return RunNativeShiva185(graphics, false);
        }
        if (effect.Binding.Entry == 0xA9B200)
        {
            return AdvanceNativeWaterA9B870(graphics);
        }
        if (NativeBattleCinematicProfile.IsSupported(effect.Binding.Effect))
        {
            return AdvanceNativeCinematic(graphics);
        }
        if (effect.Binding.Effect == 291)
        {
            return RunNativePandemona291(graphics, false);
        }
        if (effect.Binding.Effect == 191)
        {
            return RunNativeDoomtrain191(graphics, false);
        }
        if (effect.Binding.Effect == 95)
        {
            return RunNativeSiren95(graphics, false);
        }
        if (effect.Binding.Effect == 90)
        {
            return RunNativeTonberry90(graphics, false);
        }
        if (effect.Binding.Effect == 278)
        {
            return RunNativeCarbuncle278(graphics, false);
        }
        if (effect.Binding.Effect == 325)
        {
            return RunNativeDiablos325(graphics, false);
        }
        if (effect.Binding.Entry == 0x70D390)
        {
            return AdvanceNativeChoco70EB00(graphics);
        }
        if (effect.Binding.Entry == 0x717D30)
        {
            return AdvanceNativeChoco717ED0(graphics);
        }
        if (effect.Binding.Entry == 0x721860)
        {
            return AdvanceNativeChoco7219D0(graphics);
        }
        if (effect.Binding.Entry == 0x729A60)
        {
            return AdvanceNativeChoco729BD0(graphics);
        }
        if (effect.Binding.Entry == 0x731DE0)
        {
            return AdvanceNativeMoogle731F70(graphics);
        }
        if (effect.Binding.Entry == 0x759370)
        {
            return AdvanceNativeRush759510(graphics);
        }
        if (effect.Binding.Entry is 0x74E390 or 0x7475F0)
        {
            return AdvanceNativeAngeloRestoration(graphics);
        }
        if (effect.Binding.Entry == 0x755130)
        {
            return AdvanceNativeSearch7552B0(graphics);
        }
        if (effect.Binding.Entry == 0x6652C0)
        {
            return AdvanceNativeEffect311(graphics);
        }
        if (effect.Binding.Entry == 0x6D1310)
        {
            return AdvanceNativeEffect107(graphics);
        }
        if (effect.Binding.Entry == 0x6C0C80)
        {
            return AdvanceNativeAero6C0D20(graphics);
        }
        if (effect.Binding.Entry == 0x6D6310)
        {
            return AdvanceNativeBlizzaga6D6400(graphics);
        }
        if (effect.Binding.Entry == 0x6DA300)
        {
            return AdvanceNativeBlizzara6DA3A0(graphics);
        }
        if (effect.Binding.Entry == 0x6DC2D0)
        {
            return AdvanceNativeThundara6DC390(graphics);
        }
        if (effect.Binding.Entry == 0x61BFE0)
        {
            return AdvanceNativeFiraga61CDA0(graphics);
        }
        if (effect.Binding.Entry == 0x61CE10)
        {
            return AdvanceNativeFira61DD90(graphics);
        }
        if (effect.Binding.Entry == 0x61ABA0)
        {
            return AdvanceNativeBlizzard61BF70(graphics);
        }
        if (effect.Binding.Entry is 0x8D6A00 or 0x81FA10)
        {
            return AdvanceNativeCure8D6B30(graphics);
        }
        if (effect.Binding.Entry is 0x8649C0 or 0x8D4450)
        {
            return AdvanceNativeLife(graphics);
        }
        if (effect.Binding.Entry == 0x8D5080)
        {
            return AdvanceNativeDouble8D5180(graphics);
        }
        if (effect.Binding.Entry == 0x89DEF0)
        {
            return AdvanceNativeTriple89DFF0(graphics);
        }
        if (effect.Binding.Entry == 0x89A6B0)
        {
            return AdvanceNativeBio22_89A7B0(graphics);
        }
        if (effect.Binding.Entry is 0x889D60 or 0x81C800)
        {
            return AdvanceNativeCura889E40(graphics);
        }
        if (effect.Binding.Entry is 0x8AB140 or 0x7E58C0 or 0x7A51B0 or 0x79EE30)
        {
            return RunNativeActorDirector(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect20Profile.Entry)
        {
            return RunNativeEffect20(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleDevour62Profile.Entry)
        {
            return RunNativeDevour62(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleMeltingBubble43Profile.Entry)
        {
            return RunNativeMeltingBubble43(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleWindBlast29Profile.Entry)
        {
            return RunNativeWindBlast29(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleEffect60Profile.Entry)
        {
            return RunNativeEffect60(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattlePsychoBlast23Profile.Entry)
        {
            return RunNativePsychoBlast23(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleRaldo19Profile.Entry)
        {
            return RunNativeRaldo19(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleDrinkMagic37Profile.Entry)
        {
            return RunNativeDrinkMagic37(graphics, false);
        }
        if (effect.Binding.Entry is 0x7BE2D0 or 0x7CBDB0)
        {
            return RunNativeMightyGuard(graphics, false);
        }
        if (effect.Binding.Entry is 0x666DD0 or 0x6664A0)
        {
            return RunNativeHero(graphics, false);
        }
        if (effect.Binding.Entry == NativeBattleDrawStock15Profile.Entry)
        {
            return RunNativeDrawStock15(graphics, false);
        }
        if (effect.Binding.Entry is 0x808820 or 0x7D1E80)
        {
            return RunNativeAqua(graphics, false);
        }
        if (effect.Binding.Entry is 0x87DB10 or 0x817410)
        {
            return AdvanceNativeCuraga87DC50(graphics);
        }
        if (effect.Binding.Entry is 0x88CF70 or 0x883110 or 0x7FD150 or 0x7DEA20)
        {
            return AdvanceNativeRestoration(graphics);
        }
        if (effect.Binding.Entry is 0x8CE3D0 or 0x811F70 or 0x7F7CB0 or 0x7F2810)
        {
            return AdvanceNativeRecovery(graphics);
        }
        if (effect.Binding.Entry == 0x6C3550)
        {
            NativeBattleQuezacotlPreparation prepared = NativeBattleQuezacotl.Prepare6C3760(NativeEffectMemory ?? throw new NotSupportedException("Native Quezacotl callbacks require retained effect memory."), effect, NativeVibrationProcess, NativeCamera?.Projection?.View);
            NativeEffectMemory = prepared.Memory;
            NativeVibrationProcess = prepared.Vibration;
            graphics = graphics with { OriginalEffect = prepared.Effect };
            if (prepared.ChildBoundary)
            {
                NativeGraphics = graphics;
                NativeBattleEffectTaskPool childPool = prepared.Effect.Pools.Single(pool => pool.DescriptorAddress == 0x2521738);
                if (childPool.Head is null)
                {
                    return AdvanceNativeQuezacotlAfterChild6C3760(graphics);
                }
                if (childPool.Slots[0].ReadWord(12) is >= 1 and <= 355)
                {
                    graphics = AdvanceNativeQuezacotlFirstWindow(graphics);
                    NativeBattleStageState childStage = NativeStage ?? throw new NotSupportedException("Native5713E0 requires its retained stage rows.");
                    NativeStage = childStage with { Groups = childStage.Groups.Select(group => group with { ColorDepth = 2400, ColorFlags = 0 }).ToImmutableArray() };
                    return graphics;
                }
                NativeBattleQuezacotlChildStep child = NativeBattleQuezacotlChild.AdvanceFirst6C3940(prepared.Memory, prepared.Effect);
                NativeEffectMemory = child.Memory;
                graphics = graphics with { OriginalEffect = child.Effect };
                if (child.FileRequest is { } request)
                {
                    QueueNativeQuezacotlFile(request, child.Effect);
                }
                if (child.Entered)
                {
                    graphics = SetNativeQuezacotlTargets6C5300(graphics, child.Effect, true);
                    uint sound = child.Effect.Binding.NativeQuezacotlResources?.InitialSound ?? throw new NotSupportedException("Native501330 requires the actual12AE4F0 Quezacotl sound DWORD.");
                    PublishNativeModuleSound(new(sound, 1, null) { Pan = 128 });
                    NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native6C3940 requires its retained camera globals.")).WriteEffectGlobals(new(new(0, -3827, -1983), new(0, -5150, -723), 288, 0));
                }
            }
            NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native6C3760 requires its actual5713E0 stage context rows.");
            short intensity = checked((short)graphics.OriginalEffect!.Quezacotl!.Intensity17A4);
            NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = intensity, ColorFlags = 0 }).ToImmutableArray() };
            return graphics;
        }
        if (effect.Binding.Entry == 0x700C10)
        {
            NativeBattleEffect3Step module = NativeBattleEffect3.Step(effect, new(nativeActionPackets, index => ReadNativeGraphicsBone(graphics, index, 241, 0), index => graphics.Models[index].Transform ?? throw new NotSupportedException("Native effect3 requires its original model transform.")) { ApplyContact = ApplyContact });
            foreach (NativeBattleEffect3Sound sound in module.Sounds)
            {
                PublishNativeModuleSound(sound.Sound, sound.Flags, sound.Position);
            }
            return graphics with { OriginalEffect = module.Effect, ModuleDraws = graphics.ModuleDraws.AddRange(module.Draws) };
        }
        if (effect.Binding.Entry != 0x6298A0)
        {
            throw new NotSupportedException($"Native effect{effect.Binding.Effect} requires its original callback scheduler.");
        }
        NativeBattleEffectProgramResources resources = effect.Binding.ProgramResources ?? throw new NotSupportedException("Native Fire callbacks require their original executable resource bank.");
        NativeBattleFireStep step = NativeBattleFireEffect.Step(effect, new(graphics.Flags, nativeActionPackets, nativeCrtRandom ?? throw new NotSupportedException("Native Fire requires the retained55CBD2 CRT random owner."), index => ReadNativeModelAnchor(graphics, index), index => graphics.Models[index].Transform ?? throw new NotSupportedException("Native Fire requires its original model transform."), resources.Sprites.AddRange(definition.NativeModuleResources)) { ApplyContact = ApplyContact });
        nativeCrtRandom = step.CrtRandomAfter;
        foreach (NativeBattleFireSound sound in step.Sounds)
        {
            if (sound.DescriptorAddress != 0xDEE130 || sound.Volume != 256)
            {
                throw new InvalidDataException("Native Fire sound does not identify its original501330/5013A0 descriptor.");
            }
            if (sound.Position is { } position)
            {
                PublishNativeModuleSound(resources.Sound, 0x100, position);
            }
            else
            {
                byte pan = checked((byte)(sound.Pan ?? throw new InvalidDataException("Native501330 Fire sound lacks its original pan.")));
                PublishNativeModuleSound(new(resources.Sound, 0x100, null) { Pan = pan });
            }
        }
        return graphics with { OriginalEffect = step.Effect, ModuleDraws = graphics.ModuleDraws.AddRange(step.Draws) };

        void ApplyContact(int index)
        {
            ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
            graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(index)] });
            graphics = graphics with { PendingContacts = pending };
        }
    }
}
