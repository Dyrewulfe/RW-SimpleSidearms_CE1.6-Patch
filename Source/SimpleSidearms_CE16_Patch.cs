using Harmony;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using CombatExtended;
using Verse;

namespace SimpleSidearms_CE16_Patch
{
    public class SimpleSidearms_CE16_Patch
    {
        [StaticConstructorOnStartup]
        static class HarmonyPatches
        {
            static HarmonyPatches()
            {
                HarmonyInstance harmony = HarmonyInstance.Create("rimworld.dyrewulfe.ss_ce16_compat");

                harmony.Patch(AccessTools.Method(typeof(SimpleSidearms.utilities.GettersFilters), "findBestMeleeWeapon"),
                    new HarmonyMethod(typeof(HarmonyPatches), nameof(findBestMeleeWeapon_Prefix)));
            }

            private static bool findBestMeleeWeapon_Prefix(ref ThingWithComps __result, ref Pawn pawn, bool skipDangerous)
            {
                List<Thing> weapons = new List<Thing> { null };
                Thing wasEquipped = null;
                Thing best = null;
                float bestSoFar = float.MinValue;

                // Need to temporarily remove the equipped weapon so it doesn't gain damage variation bonuses
                // when calling GetStatValue later.
                if (pawn.equipment != null && pawn.equipment.Primary != null)
                {
                    wasEquipped = pawn.equipment.Primary;
                    pawn.equipment.TryTransferEquipmentToContainer(pawn.equipment.Primary, pawn.inventory.innerContainer);
                }

                foreach (Thing thing in pawn.inventory.innerContainer)
                {
                    if ((thing.def.IsMeleeWeapon || (thing.def.tools != null && thing.def.tools.Any((Tool x) => (x.VerbsProperties != null && x.VerbsProperties.Any((VerbProperties y) => y.IsMeleeAttack))))))
                    {
                        weapons.Add(thing);
                    }
                }

                foreach (Thing weapon in weapons)
                {
                    float dpsAvg = -1f;

                    if (skipDangerous)
                        if (isDangerousWeapon(weapon as ThingWithComps))
                            continue;

                    if (weapon == null)
                    {
                        dpsAvg = GetUnarmedDPS(pawn);
                        //Log.Message("DPS for unarmed is " + dpsAvg);
                    }
                    else
                    {
                        dpsAvg = weapon.GetStatValue(StatDefOf.MeleeWeapon_AverageDPS);
                        //Log.Message("DPS for " + weapon.Label + " is " + dpsAvg);
                    }

                    if (dpsAvg > bestSoFar)
                    {
                        bestSoFar = dpsAvg;
                        best = weapon;
                    }

                }

                /*if (best == null)
                    Log.Message("best: unarmed");
                else
                    Log.Message("best: " + best.def.defName);*/

                // We have to manually re-equip in this case, because Simple Sidearms won't. And I'm too lazy for a better solution.
                if (wasEquipped != null && wasEquipped == best)
                {
                    pawn.inventory.innerContainer.Remove(wasEquipped);
                    pawn.equipment.AddEquipment(best as ThingWithComps);
                }

                __result = best as ThingWithComps;
                return false;
            }

        }

        private static float GetUnarmedDPS(Pawn pawn)
        {
            var tools = pawn.Tools;
            var totalDPS = 0f;
            if (tools.NullOrEmpty())
            {
                return 0;
            }
            if (tools.Any(x => !(x is ToolCE)))
            {
                Log.Error($"SimpleSidearms_CE1.6_Patch: Trying to get stat MeleeDamageAverage from {pawn.Name} which has no support for Combat Extended..?");
                return 0;
            }

            var unarmedBonus = pawn.GetStatValue(CE_StatDefOf.UnarmedDamage);
            var totalSelectionWeight = 0f;
            foreach (var tool in tools)
            {
                totalSelectionWeight += tool.chanceFactor;
            }
            foreach (var tool in tools)
            {
                var toolDamage = tool.AdjustedBaseMeleeDamageAmount(null, tool.capacities?.First()?.VerbsProperties?.First()?.meleeDamageDef);
                // We're skipping damage variation here and using the defaults, since we can't 
                // apply the damage variation to every possible weapon without making a mess of things.
                // This way we get a fair comparison to the available melee weapons.
                var minDPS = (toolDamage + unarmedBonus) / tool.cooldownTime * 0.5f;// * StatWorker_MeleeDamageBase.GetDamageVariationMin(pawn);
                var maxDPS = (toolDamage + unarmedBonus) / tool.cooldownTime * 1.5f;// * StatWorker_MeleeDamageBase.GetDamageVariationMax(pawn);
                var weightFactor = tool.chanceFactor / totalSelectionWeight;
                totalDPS += weightFactor * ((minDPS + maxDPS) / 2f);
            }
            return totalDPS;
        }

        private static bool isDangerousWeapon(ThingWithComps weapon)
        {
            if (weapon == null)
                return false;
            CompEquippable equip = weapon.TryGetComp<CompEquippable>();
            if (equip == null)
                return false;
            if (equip.PrimaryVerb.IsIncendiary() || equip.PrimaryVerb.verbProps.onlyManualCast || equip.PrimaryVerb.verbProps.ai_IsBuildingDestroyer)
                return true;
            else
                return false;
        }
    }
}