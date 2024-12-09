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
        // Postfix for computing totals.
        [HarmonyPostfix]
        [HarmonyPatch(nameof(CountPlayerAnimals))]
        public static void CountPlayerAnimals(int currentMales, ref int currentMalesYoung, int currentFemales, ref int currentFemalesYoung)
        {
            currentMalesYoung += currentMales;
            currentFemalesYoung += currentFemales;
        }

        // Transpiller for disabling filtering out bonded and pregnant animals.
        [HarmonyTranspiler]
        [HarmonyPatch(nameof(CountPlayerAnimals))]
        public static IEnumerable<CodeInstruction> CountPlayerAnimals(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            bool foundBond = false;
            bool foundPregnant = false;
            for( int i = 0; i < codes.Count; ++i )
            {
                // Log.Message("T:" + i + ":" + codes[i].opcode + "::" + (codes[i].operand != null ? codes[i].operand.ToString() : codes[i].operand));
                // The function has code:
                // if (!config.allowSlaughterBonded)
                //     continue;
                // Make the condition always false.
                if( !foundBond
                    && codes[ i ].IsLdarg()
                    && i + 2 < codes.Count
                    && codes[ i + 1 ].opcode == OpCodes.Ldfld && codes[ i + 1 ].operand.ToString() == "System.Boolean allowSlaughterBonded"
                    && codes[ i + 2 ].opcode == OpCodes.Brfalse )
                {
                    codes.RemoveRange( i, 3 );
                    foundBond = true;
                    --i; // Fix 'i' to point after the removed code for the next iteration.
                }
                // The function has code:
                // if (!config.allowSlaughterPregnant)
                //     continue;
                // Make the condition always false.
                if( !foundPregnant
                    && codes[ i ].IsLdarg()
                    && i + 2 < codes.Count
                    && codes[ i + 1 ].opcode == OpCodes.Ldfld && codes[ i + 1 ].operand.ToString() == "System.Boolean allowSlaughterPregnant"
                    && codes[ i + 2 ].opcode == OpCodes.Brfalse_S )
                {
                    codes.RemoveRange( i, 3 );
                    foundPregnant = true;
                    --i; // Fix 'i' to point after the removed code for the next iteration.
                }
            }
            if( !foundBond || !foundPregnant )
                Log.Error("ImprovedAutoSlaughter: Failed to patch Dialog_AutoSlaughter.CountPlayerAnimals()");
            return codes;
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
