using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Reflection.Emit;
using Verse;

namespace ImprovedAutoSlaughter
{
    [HarmonyPatch(typeof(Dialog_AutoSlaughter))]
    public static class Dialog_AutoSlaughter_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(CountPlayerAnimals))]
        public static void CountPlayerAnimals(int currentMales, ref int currentMalesYoung, int currentFemales, ref int currentFemalesYoung)
        {
            currentMalesYoung += currentMales;
            currentFemalesYoung += currentFemales;
        }

        [HarmonyTranspiler]
        [HarmonyPatch(nameof(DoAnimalHeader))]
        public static IEnumerable<CodeInstruction> DoAnimalHeader(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            bool found1 = false;
            bool found2 = false;
            for( int i = 0; i < codes.Count; ++i )
            {
                // Log.Message("T:" + i + ":" + codes[i].opcode + "::" + (codes[i].operand != null ? codes[i].operand.ToString() : codes[i].operand));
                if( codes[ i ].opcode == OpCodes.Ldstr && codes[ i ].operand.ToString() == "AnimalMaleYoung" )
                {
                    codes[ i ] = new CodeInstruction( OpCodes.Ldstr, "ImprovedAutoSlaughter.AnimalMaleTotal" );
                    found1 = true;
                }
                if( codes[ i ].opcode == OpCodes.Ldstr && codes[ i ].operand.ToString() == "AnimalFemaleYoung" )
                {
                    codes[ i ] = new CodeInstruction( OpCodes.Ldstr, "ImprovedAutoSlaughter.AnimalFemaleTotal" );
                    found2 = true;
                }
            }
            if(!found1 || !found2)
                Log.Error("ImprovedAutoSlaughter: Failed to patch Dialog_AutoSlaughter.DoAnimalHeader()");
            return codes;
        }

    }
}
