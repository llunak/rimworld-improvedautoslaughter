//#define DEBUG

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using RimWorld;
using Verse;

// Change the male/female young columns and values to be male/female total.
// (The names stay the same, including previously configured values for them.)
namespace ImprovedAutoSlaughter
{
    [HarmonyPatch(typeof(AutoSlaughterManager))]
    public static class AutoSlaughterManager_Patch
    {
        [HarmonyTranspiler]
        [HarmonyPatch(nameof(AnimalsToSlaughter))]
        [HarmonyPatch(MethodType.Getter)]
        public static IEnumerable<CodeInstruction> AnimalsToSlaughter(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            bool found1 = false;
            bool found2 = false;
            bool found3 = false;
            bool found4 = false;
            object femaleYoungJump = null;
            int replaceStart = -1;
            Type type = AccessTools.TypeByName( "Verse.AutoSlaughterManager" );
            if( type == null )
            {
                Log.Error( "ImprovedAutoSlaughter: Cannot find Verse.AutoSlaughterManager" );
                return codes;
            }
            for( int i = 0; i < codes.Count; ++i )
            {
                // Log.Message("T:" + i + ":" + codes[i].opcode + "::" + (codes[i].operand != null ? codes[i].operand.ToString() : codes[i].operand));
                // The function has code:
                // if (spawnedColonyAnimal.gender == Gender.Male)
                // {
                //     if (spawnedColonyAnimal.ageTracker.CurLifeStage.reproductive)
                //         tmpAnimalsMale.Add(spawnedColonyAnimal);
                //     else
                //         tmpAnimalsMaleYoung.Add(spawnedColonyAnimal);
                // }
                // Make the last statement non-conditional (i.e. move it out of the else part).
                if( !found1 && codes[ i ].opcode == OpCodes.Ldfld && codes[ i ].operand.ToString() == "System.Boolean reproductive"
                    && i + 1 < codes.Count
                    && codes[ i + 1 ].opcode == OpCodes.Brfalse_S
                    && codes[ i + 2 ].opcode == OpCodes.Ldsfld && codes[ i + 2 ].operand.ToString().EndsWith( "tmpAnimalsMale" )
                    && codes[ i + 3 ].IsLdloc()
                    && codes[ i + 4 ].opcode == OpCodes.Callvirt && codes[ i + 4 ].operand.ToString() == "Void Add(Verse.Pawn)"
                    && codes[ i + 5 ].opcode == OpCodes.Br_S )
                {
                    codes[ i + 5 ] = new CodeInstruction( OpCodes.Nop );
                    found1 = true;
                }
                // Similar for the female part. The code is more complicated, this time insert a jump
                // to the young part.
                if( found1 && femaleYoungJump == null
                    && codes[ i ].opcode == OpCodes.Ldfld && codes[ i ].operand.ToString() == "System.Boolean reproductive"
                    && i + 2 < codes.Count
                    && codes[ i + 1 ].opcode == OpCodes.Brfalse_S
                    && !( codes[ i + 2 ].opcode == OpCodes.Ldsfld && codes[ i + 2 ].operand.ToString().EndsWith( "tmpAnimalsMale" )))
                {
                    femaleYoungJump = codes[ i + 1 ].operand;
                }
                if( !found2 && femaleYoungJump != null
                    && codes[ i ].opcode == OpCodes.Ldsfld && codes[ i ].operand.ToString().EndsWith( "tmpAnimalsFemale" )
                    && i + 2 < codes.Count
                    && codes[ i + 1 ].IsLdloc()
                    && codes[ i + 2 ].opcode == OpCodes.Callvirt && codes[ i + 2 ].operand.ToString() == "Void Add(Verse.Pawn)" )
                {
                    codes.Insert( i + 3, new CodeInstruction( OpCodes.Br_S, femaleYoungJump ));
                    found2 = true;
                }
                // The function has code:
                // tmpAnimals.SortByDescending((Pawn a) => a.ageTracker.AgeBiologicalTicks);
                // tmpAnimalsMale.SortByDescending((Pawn a) => a.ageTracker.AgeBiologicalTicks);
                // tmpAnimalsMaleYoung.SortByDescending((Pawn a) => a.ageTracker.AgeBiologicalTicks);
                // tmpAnimalsFemale.SortByDescending((Pawn a) => a.ageTracker.AgeBiologicalTicks);
                // tmpAnimalsFemaleYoung.SortByDescending((Pawn a) => a.ageTracker.AgeBiologicalTicks);
                // if (config.allowSlaughterPregnant)
                // Replace the entire SortByDescending() part with:
                // AnimalsToSlaughter_Hook(tmpAnimals, tmpAnimalsMale, tmpAnimalsMaleYoung, tmpAnimalsFemale, tmpAnimalsFemaleYoung );
                if( replaceStart == -1 && codes[ i ].opcode == OpCodes.Ldsfld
                    && codes[ i ].operand.ToString().EndsWith( "tmpAnimals" )
                    && i + 1 < codes.Count
                    && codes[ i + 1 ].opcode == OpCodes.Ldsfld
                    && codes[ i + 1 ].operand.ToString().StartsWith( "System.Func`2[Verse.Pawn,System.Int64]" ))
                {
                    replaceStart = i;
                }
                if( !found3 && replaceStart != -1 && codes[ i ].IsLdloc()
                    && i + 1 < codes.Count
                    && codes[ i + 1 ].opcode == OpCodes.Ldfld
                    && codes[ i + 1 ].operand.ToString() == "System.Boolean allowSlaughterPregnant" )
                {
                    var labels = codes[ replaceStart ].labels; // Keep the label on the first instruction.
                    codes[ replaceStart ].labels = null;
                    codes.RemoveRange( replaceStart, i - replaceStart );
                    codes.Insert( replaceStart, CodeInstruction.LoadField( type, "tmpAnimals" ));
                    codes[ replaceStart ].labels = labels;
                    codes.Insert( replaceStart + 1, CodeInstruction.LoadField( type, "tmpAnimalsMale" ));
                    codes.Insert( replaceStart + 2, CodeInstruction.LoadField( type, "tmpAnimalsMaleYoung" ));
                    codes.Insert( replaceStart + 3, CodeInstruction.LoadField( type, "tmpAnimalsFemale" ));
                    codes.Insert( replaceStart + 4, CodeInstruction.LoadField( type, "tmpAnimalsFemaleYoung" ));
                    codes.Insert( replaceStart + 5, new CodeInstruction( OpCodes.Call,
                        typeof( AutoSlaughterManager_Patch ).GetMethod( nameof( AnimalsToSlaughter_Hook ))));
                    found3 = true;
                    i = replaceStart + 5 - 1; // Fix 'i' to be after the inserted code for the next iteration.
                }
                // The function has code:
                // if (config.allowSlaughterPregnant)
                // {
                // tmpAnimalsPregnant.SortByDescending((Pawn a) => a.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Pregnant).S
                // tmpAnimalsFemale.AddRange(tmpAnimalsPregnant);
                // tmpAnimals.AddRange(tmpAnimalsPregnant);
                // }
                // Insert the following in the block:
                // tmpAnimalsFemaleYoung.AddRange(tmpAnimalsPregnant);
                if( found3 && !found4 && codes[ i ].opcode == OpCodes.Ldsfld && codes[ i ].operand.ToString().EndsWith( "tmpAnimals" )
                    && i + 2 < codes.Count
                    && codes[ i + 1 ].opcode == OpCodes.Ldsfld && codes[ i + 1 ].operand.ToString().EndsWith( "tmpAnimalsPregnant" )
                    && codes[ i + 2 ].opcode == OpCodes.Callvirt && codes[ i + 2 ].operand.ToString().StartsWith( "Void AddRange" ))
                {
                    codes.Insert( i + 3, CodeInstruction.LoadField( type, "tmpAnimalsFemaleYoung" ));
                    codes.Insert( i + 4, codes[ i + 1 ].Clone());
                    codes.Insert( i + 5, codes[ i + 2 ].Clone());
                    found4 = true;
                }
#if DEBUG
                if( found4 && codes[ i ].opcode == OpCodes.Stfld && codes[ i ].operand.ToString() == "System.Boolean cacheDirty" )
                {
                    codes.Insert( i + 1, CodeInstruction.LoadField( type, "tmpAnimals" ));
                    codes.Insert( i + 2, CodeInstruction.LoadField( type, "tmpAnimalsMale" ));
                    codes.Insert( i + 3, CodeInstruction.LoadField( type, "tmpAnimalsMaleYoung" ));
                    codes.Insert( i + 4, CodeInstruction.LoadField( type, "tmpAnimalsFemale" ));
                    codes.Insert( i + 5, CodeInstruction.LoadField( type, "tmpAnimalsFemaleYoung" ));
                    codes.Insert( i + 6, CodeInstruction.LoadField( type, "tmpAnimalsPregnant" ));
                    codes.Insert( i + 7, new CodeInstruction( OpCodes.Ldarg_0 ));
                    codes.Insert( i + 8, CodeInstruction.LoadField( type, "animalsToSlaughterCached" ));
                    codes.Insert( i + 9, new CodeInstruction( OpCodes.Call,
                        typeof( AutoSlaughterManager_Patch ).GetMethod( nameof( AnimalsToSlaughter_Hook2 ))));
                }
#endif
            }
            if(!found1 || !found2 || !found3 || !found4)
                Log.Error("ImprovedAutoSlaughter: Failed to patch AutoSlaughterManager.AnimalsToSlaughter()");
            return codes;
        }

        private static long ProductiveAgeIndex( Pawn p )
        {
            double f = 0.75;
            // Animals that are above the age threshold go first, oldest first.
            if( p.ageTracker.AgeBiologicalTicks > p.RaceProps.lifeExpectancy * GenDate.TicksPerYear * f )
                return p.ageTracker.AgeBiologicalTicks - ( long )( p.RaceProps.lifeExpectancy * GenDate.TicksPerYear * f );
            // The rest go youngest first.
            return -p.ageTracker.AgeBiologicalTicks;
        }

        public static void AnimalsToSlaughter_Hook( List< Pawn > tmpAnimals, List< Pawn > tmpAnimalsMale,
            List< Pawn > tmpAnimalsMaleYoung, List< Pawn > tmpAnimalsFemale,  List< Pawn > tmpAnimalsFemaleYoung )
        {
            tmpAnimals.SortByDescending( ( Pawn p ) => ProductiveAgeIndex( p ));
            tmpAnimalsMale.SortByDescending( ( Pawn p ) => ProductiveAgeIndex( p ));
            tmpAnimalsMaleYoung.SortByDescending( ( Pawn p ) => ProductiveAgeIndex( p ));
            tmpAnimalsFemale.SortByDescending( ( Pawn p ) => ProductiveAgeIndex( p ));
            tmpAnimalsFemaleYoung.SortByDescending( ( Pawn p ) => ProductiveAgeIndex( p ));
        }

#if DEBUG
        public static void AnimalsToSlaughter_Hook2( List< Pawn > tmpAnimals, List< Pawn > tmpAnimalsMale,
            List< Pawn > tmpAnimalsMaleYoung, List< Pawn > tmpAnimalsFemale,  List< Pawn > tmpAnimalsFemaleYoung,
            List< Pawn > tmpAnimalsPregnant, List< Pawn > result )
        {
            foreach( Pawn p in tmpAnimals )
            {
                Log.Message("Y1:" + p + ":" + p.ageTracker.AgeBiologicalYears );
            }
            foreach( Pawn p in tmpAnimalsMale )
            {
                Log.Message("Y2:" + p + ":" + p.ageTracker.AgeBiologicalYears );
            }
            foreach( Pawn p in tmpAnimalsMaleYoung )
            {
                Log.Message("Y3:" + p + ":" + p.ageTracker.AgeBiologicalYears );
            }
            foreach( Pawn p in tmpAnimalsFemale )
            {
                Log.Message("Y4:" + p + ":" + p.ageTracker.AgeBiologicalYears );
            }
            foreach( Pawn p in tmpAnimalsFemaleYoung )
            {
                Log.Message("Y5:" + p + ":" + p.ageTracker.AgeBiologicalYears );
            }
            foreach( Pawn p in tmpAnimalsPregnant )
            {
                Log.Message("Y6:" + p + ":" + p.ageTracker.AgeBiologicalYears );
            }
            foreach( Pawn p in result )
            {
                Log.Message("Y7:" + p + ":" + p.ageTracker.AgeBiologicalYears );
            }
        }
#endif
    }
}
