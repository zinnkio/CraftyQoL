using CraftyQoL.Core;

namespace CraftyQoL
{
    internal static class MeadMetadata
    {
        internal static MeadFlags Effects(ItemDrop.ItemData.SharedData shared)
        {
            // Nutrition and stamina savings are not restorative potion effects.
            if(!(shared.m_consumeStatusEffect is SE_Stats effect))return MeadFlags.Other;
            bool resistance=false;
            if(effect.m_mods!=null)foreach(var mod in effect.m_mods)
                if(mod.m_type!=0 && MeadEffects.Resistant((int)mod.m_modifier))resistance=true;
            bool utility=MeadEffects.Reduction(effect.m_staminaDrainPerSec,effect.m_runStaminaDrainModifier,effect.m_jumpStaminaUseModifier,
                effect.m_attackStaminaUseModifier,effect.m_blockStaminaUseModifier,effect.m_blockStaminaUseFlatValue,effect.m_dodgeStaminaUseModifier,
                effect.m_swimStaminaUseModifier,effect.m_homeItemStaminaUseModifier,effect.m_sneakStaminaUseModifier,effect.m_runStaminaUseModifier,effect.m_windRunStaminaModifier)<0
                || MeadEffects.Positive(effect.m_addMaxCarryWeight,effect.m_speedModifier,effect.m_swimSpeedModifier,effect.m_addArmor,effect.m_armorMultiplier,
                    effect.m_raiseSkillModifier,effect.m_skillLevelModifier,effect.m_skillLevelModifier2)>0;
            return MeadEffects.Classify(MeadEffects.Positive(effect.m_healthUpFront,effect.m_healthOverTime,effect.m_healthPerTick),effect.m_healthRegenMultiplier,
                MeadEffects.Positive(effect.m_staminaUpFront,effect.m_staminaOverTime),effect.m_staminaRegenMultiplier,
                MeadEffects.Positive(effect.m_eitrUpFront,effect.m_eitrOverTime),effect.m_eitrRegenMultiplier,resistance,utility);
        }
    }
}
