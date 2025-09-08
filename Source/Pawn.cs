using HarmonyLib;
using UnityEngine;
using Verse;
using RimWorld;
using System.Collections.Generic;

namespace ImprovedAutoSlaughter
{
    [HarmonyPatch(typeof(Pawn))]
    public static class Pawn_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(GetGizmos))]
        public static IEnumerable<Gizmo> GetGizmos(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach( Gizmo gizmo in __result )
                yield return gizmo;
            Pawn pawn = __instance;
            if( !pawn.RaceProps.Animal || pawn.Faction != Faction.OfPlayer )
                yield break;
            // Do not show the gizmos if no auto slaughter is configured for this animal kind.
            if( pawn.Map?.autoSlaughterManager == null )
                yield break;
            AutoSlaughterConfig config = pawn.Map.autoSlaughterManager.configs.Find(
                c => c.animal == pawn.def );
            if( config == null )
                yield break;
            // Do not use AnyLimit, it also checks pregnant and bonded, and returns true by default.
            if( config.maxTotal == -1 && config.maxMales == -1 && config.maxFemales == -1
                && config.maxMalesYoung == -1 && config.maxFemalesYoung == -1 )
            {
                yield break;
            }
            Command_Toggle action = new Command_Toggle();
            action.defaultLabel = "ImprovedAutoSlaughter.NoAutoSlaughterLabel".Translate();
            action.defaultDesc = "ImprovedAutoSlaughter.NoAutoSlaughterDesc".Translate();
            action.icon = ContentFinder<Texture2D>.Get("UI/Designators/NoAutoSlaughter");
            action.hotKey = KeyBindingDefOf.Misc2;
            action.isActive = () => Current.Game.GetComponent< SlaughterComponent >().preventAutoSlaughter( pawn );
            action.toggleAction = delegate
            {
                Current.Game.GetComponent< SlaughterComponent >().flipPreventAutoSlaughter( pawn );
                pawn.Map?.autoSlaughterManager?.Notify_ConfigChanged();
            };
            yield return action;

            action = new Command_Toggle();
            action.defaultLabel = "ImprovedAutoSlaughter.PriorityAutoSlaughterLabel".Translate();
            action.defaultDesc = "ImprovedAutoSlaughter.PriorityAutoSlaughterDesc".Translate();
            action.icon = ContentFinder<Texture2D>.Get("UI/Designators/PriorityAutoSlaughter");
            action.hotKey = KeyBindingDefOf.Misc12;
            action.isActive = () => Current.Game.GetComponent< SlaughterComponent >().preferAutoSlaughter( pawn );
            action.toggleAction = delegate
            {
                Current.Game.GetComponent< SlaughterComponent >().flipPreferAutoSlaughter( pawn );
                pawn.Map?.autoSlaughterManager?.Notify_ConfigChanged();
            };
            yield return action;
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(Destroy))]
        public static void Destroy( Pawn __instance )
        {
            Current.Game.GetComponent< SlaughterComponent >().Remove( __instance );
        }
    }
}
