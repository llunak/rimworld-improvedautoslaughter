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
        }
    }
}
