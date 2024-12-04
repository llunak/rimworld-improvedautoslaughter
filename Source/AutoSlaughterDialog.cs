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
        [HarmonyPatch(nameof(DoWindowContents))]
        public static IEnumerable<CodeInstruction> DoWindowContents(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            bool found = false;
            for( int i = 0; i < codes.Count; ++i )
            {
                // Log.Message("T:" + i + ":" + codes[i].opcode + "::" + (codes[i].operand != null ? codes[i].operand.ToString() : codes[i].operand));
                if( codes[ i ].opcode == OpCodes.Ldstr && codes[ i ].operand.ToString() == "AutoSlaugtherTip" )
                {
                    codes[ i ] = new CodeInstruction( OpCodes.Ldstr, "ImprovedAutoSlaughter.Tip" );
                    found = true;
                    // There are more instances, do all.
                }
            }
            if(!found)
                Log.Error("ImprovedAutoSlaughter: Failed to patch Dialog_AutoSlaughter.DoWindowContents()");
            return codes;
        }

        [HarmonyTranspiler]
        [HarmonyPatch(nameof(DoAnimalHeader))]
        public static IEnumerable<CodeInstruction> DoAnimalHeader(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            var replace =
            new[] {
                ( source: "AnimalMaleYoung", replace: "ImprovedAutoSlaughter.AnimalMaleTotal", done: false ),
                ( source: "AnimalFemaleYoung", replace: "ImprovedAutoSlaughter.AnimalFemaleTotal", done: false ),
                ( source: "AutoSlaughterHeaderTooltipCurrentMalesYoung", replace: "ImprovedAutoSlaughter.HeaderTooltipCurrentMalesYoung", done: false ),
                ( source: "AutoSlaughterHeaderTooltipMaxMalesYoung", replace: "ImprovedAutoSlaughter.HeaderTooltipMaxMalesYoung", done: false ),
                ( source: "AutoSlaugtherHeaderTooltipCurrentFemalesYoung", replace: "ImprovedAutoSlaughter.HeaderTooltipCurrentFemalesYoung", done: false ),
                ( source: "AutoSlaughterHeaderTooltipMaxFemalesYoung", replace: "ImprovedAutoSlaughter.HeaderTooltipMaxFemalesYoung", done: false )
            };
            for( int i = 0; i < codes.Count; ++i )
            {
                // Log.Message("T:" + i + ":" + codes[i].opcode + "::" + (codes[i].operand != null ? codes[i].operand.ToString() : codes[i].operand));
                if( codes[ i ].opcode == OpCodes.Ldstr )
                {
                    for( int j = 0; j < replace.Length; ++j )
                    {
                        if( !replace[ j ].done && codes[ i ].operand.ToString() == replace[ j ].source )
                        {
                            codes[ i ] = new CodeInstruction( OpCodes.Ldstr, replace[ j ].replace );
                            replace[ j ].done = true;
                            break;
                        }
                    }
                }
            }
            bool found = true;
            for( int j = 0; j < replace.Length; ++j )
                if( !replace[ j ].done )
                    found = false;
            if(!found)
                Log.Error("ImprovedAutoSlaughter: Failed to patch Dialog_AutoSlaughter.DoAnimalHeader()");
            return codes;
        }

    }
}
