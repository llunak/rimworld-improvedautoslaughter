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
        private static int totalNoSlaughter; // Prevented by SlaughterComponent.noAutoSlaughter.
        private static int totalNoSlaughterMales;
        private static int totalNoSlaughterMalesAdult;
        private static int totalNoSlaughterFemales;
        private static int totalNoSlaughterFemalesAdult;
        private static int totalBonded;
        private static int totalBondedMales;
        private static int totalBondedMalesAdult;
        private static int totalBondedFemales;
        private static int totalBondedFemalesAdult;
        private static int totalPregnant;

        [HarmonyTranspiler]
        [HarmonyPatch(nameof(AnimalsToSlaughter))]
        [HarmonyPatch(MethodType.Getter)]
        public static IEnumerable<CodeInstruction> AnimalsToSlaughter(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            bool foundClear = false;
            bool foundCanSlaughter = false;
            bool foundBond = false;
            bool foundMales = false;
            bool foundFemales = false;
            bool foundPregnant = false;
            bool foundSort = false;
            bool foundTotalPregnant = false;
            bool foundMaxFemales = false;
            bool foundMaxFemalesTotal = false;
            bool foundMaxMales = false;
            bool foundMaxMalesTotal = false;
            bool foundMaxTotal = false;
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
                // tmpAnimals.Clear();
                // Append:
                // AnimalsToSlaughter_HookClear();
                if( !foundClear
                    && codes[ i ].opcode == OpCodes.Ldsfld && codes[ i ].operand.ToString().EndsWith( "tmpAnimalsMale" )
                    && i + 1 < codes.Count
                    && codes[ i + 1 ].opcode == OpCodes.Callvirt && codes[ i + 1 ].operand.ToString() == "Void Clear()" )
                {
                    codes.Insert( i + 2, new CodeInstruction( OpCodes.Call,
                        typeof( AutoSlaughterManager_Patch ).GetMethod( nameof( AnimalsToSlaughter_HookClear ))));
                    foundClear = true;
                }
                // The function has code:
                // CanAutoSlaughterNow(spawnedColonyAnimal)
                // Change to:
                // AnimalsToSlaughter_HookCanSlaughter(spawnedColonyAnimal)
                if( foundClear && !foundCanSlaughter
                    && codes[ i ].opcode == OpCodes.Call && codes[ i ].operand.ToString() == "Boolean CanAutoSlaughterNow(Verse.Pawn)" )
                {
                    codes[ i ] = new CodeInstruction( OpCodes.Call,
                        typeof( AutoSlaughterManager_Patch ).GetMethod( nameof( AnimalsToSlaughter_HookCanSlaughter )));
                    foundCanSlaughter = true;
                }
                // The function has code:
                // (!config.allowSlaughterBonded && spawnedColonyAnimal.relations.GetDirectRelationsCount(PawnRelationDefOf.Bond) > 0)
                // Change to:
                // (!config.allowSlaughterBonded && AnimalsToSlaughter_HookBonded(
                //      spawnedColonyAnimal.relations.GetDirectRelationsCount(PawnRelationDefOf.Bond) > 0, spawnedColonyAnimal))
                // I.e. if bonded animals are not to be slaughtered, count them in order to add the count later to the limit checking.
                if( foundCanSlaughter && !foundBond
                    && codes[ i ].IsLdloc()
                    && i + 6 < codes.Count
                    && codes[ i + 1 ].opcode == OpCodes.Ldfld && codes[ i + 1 ].operand.ToString() == "RimWorld.Pawn_RelationsTracker relations"
                    && codes[ i + 2 ].opcode == OpCodes.Ldsfld && codes[ i + 2 ].operand.ToString() == "RimWorld.PawnRelationDef Bond"
                    && codes[ i + 3 ].opcode == OpCodes.Ldnull
                    && codes[ i + 4 ].opcode == OpCodes.Callvirt && codes[ i + 4 ].operand.ToString().StartsWith( "Int32 GetDirectRelationsCount" )
                    && codes[ i + 5 ].opcode == OpCodes.Ldc_I4_0
                    && codes[ i + 6 ].opcode == OpCodes.Bgt )
                {
                    codes.Insert( i + 6, codes[ i ].Clone()); // load 'spawnedColonyAnimal'
                    codes.Insert( i + 7, new CodeInstruction( OpCodes.Call,
                        typeof( AutoSlaughterManager_Patch ).GetMethod( nameof( AnimalsToSlaughter_HookBond ))));
                    foundBond = true;
                }
                // The function has code:
                // if (spawnedColonyAnimal.gender == Gender.Male)
                // {
                //     if (spawnedColonyAnimal.ageTracker.CurLifeStage.reproductive)
                //         tmpAnimalsMale.Add(spawnedColonyAnimal);
                //     else
                //         tmpAnimalsMaleYoung.Add(spawnedColonyAnimal);
                // }
                // Make the last statement non-conditional (i.e. move it out of the else part).
                if( !foundMales && codes[ i ].opcode == OpCodes.Ldfld && codes[ i ].operand.ToString() == "System.Boolean reproductive"
                    && i + 1 < codes.Count
                    && codes[ i + 1 ].opcode == OpCodes.Brfalse_S
                    && codes[ i + 2 ].opcode == OpCodes.Ldsfld && codes[ i + 2 ].operand.ToString().EndsWith( "tmpAnimalsMale" )
                    && codes[ i + 3 ].IsLdloc()
                    && codes[ i + 4 ].opcode == OpCodes.Callvirt && codes[ i + 4 ].operand.ToString() == "Void Add(Verse.Pawn)"
                    && codes[ i + 5 ].opcode == OpCodes.Br_S )
                {
                    codes[ i + 5 ] = new CodeInstruction( OpCodes.Nop );
                    foundMales = true;
                }
                // Similar for the female part. The code is more complicated, this time insert a jump
                // to the young part.
                if( foundMales && femaleYoungJump == null
                    && codes[ i ].opcode == OpCodes.Ldfld && codes[ i ].operand.ToString() == "System.Boolean reproductive"
                    && i + 2 < codes.Count
                    && codes[ i + 1 ].opcode == OpCodes.Brfalse_S
                    && !( codes[ i + 2 ].opcode == OpCodes.Ldsfld && codes[ i + 2 ].operand.ToString().EndsWith( "tmpAnimalsMale" )))
                {
                    femaleYoungJump = codes[ i + 1 ].operand;
                }
                if( !foundFemales && femaleYoungJump != null
                    && codes[ i ].opcode == OpCodes.Ldsfld && codes[ i ].operand.ToString().EndsWith( "tmpAnimalsFemale" )
                    && i + 2 < codes.Count
                    && codes[ i + 1 ].IsLdloc()
                    && codes[ i + 2 ].opcode == OpCodes.Callvirt && codes[ i + 2 ].operand.ToString() == "Void Add(Verse.Pawn)" )
                {
                    codes.Insert( i + 3, new CodeInstruction( OpCodes.Br_S, femaleYoungJump ));
                    foundFemales = true;
                }
                // The function has code:
                // else if (config.allowSlaughterPregnant)
                // Change to:
                // else if (AnimalsToSlaughter_HookPregnant(config.allowSlaughterPregnant))
                if( !foundPregnant && foundFemales
                    && codes[ i ].IsLdloc()
                    && i + 2 < codes.Count
                    && codes[ i + 1 ].opcode == OpCodes.Ldfld
                    && codes[ i + 1 ].operand.ToString() == "System.Boolean allowSlaughterPregnant"
                    && codes[ i + 2 ].opcode == OpCodes.Brfalse_S )
                {
                    codes.Insert( i + 2, new CodeInstruction( OpCodes.Call,
                        typeof( AutoSlaughterManager_Patch ).GetMethod( nameof( AnimalsToSlaughter_HookPregnant ))));
                    foundPregnant = true;
                }
                // The function has code:
                // tmpAnimals.SortByDescending((Pawn a) => a.ageTracker.AgeBiologicalTicks);
                // tmpAnimalsMale.SortByDescending((Pawn a) => a.ageTracker.AgeBiologicalTicks);
                // tmpAnimalsMaleYoung.SortByDescending((Pawn a) => a.ageTracker.AgeBiologicalTicks);
                // tmpAnimalsFemale.SortByDescending((Pawn a) => a.ageTracker.AgeBiologicalTicks);
                // tmpAnimalsFemaleYoung.SortByDescending((Pawn a) => a.ageTracker.AgeBiologicalTicks);
                // if (config.allowSlaughterPregnant)
                // Replace the entire SortByDescending() part with:
                // AnimalsToSlaughter_HookSort(tmpAnimals, tmpAnimalsMale, tmpAnimalsMaleYoung, tmpAnimalsFemale, tmpAnimalsFemaleYoung );
                if( replaceStart == -1 && codes[ i ].opcode == OpCodes.Ldsfld
                    && codes[ i ].operand.ToString().EndsWith( "tmpAnimals" )
                    && i + 1 < codes.Count
                    && codes[ i + 1 ].opcode == OpCodes.Ldsfld
                    && codes[ i + 1 ].operand.ToString().StartsWith( "System.Func`2[Verse.Pawn,System.Int64]" ))
                {
                    replaceStart = i;
                }
                if( !foundSort && replaceStart != -1 && codes[ i ].IsLdloc()
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
                        typeof( AutoSlaughterManager_Patch ).GetMethod( nameof( AnimalsToSlaughter_HookSort ))));
                    foundSort = true;
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
                if( foundSort && !foundTotalPregnant
                    && codes[ i ].opcode == OpCodes.Ldsfld && codes[ i ].operand.ToString().EndsWith( "tmpAnimals" )
                    && i + 2 < codes.Count
                    && codes[ i + 1 ].opcode == OpCodes.Ldsfld && codes[ i + 1 ].operand.ToString().EndsWith( "tmpAnimalsPregnant" )
                    && codes[ i + 2 ].opcode == OpCodes.Callvirt && codes[ i + 2 ].operand.ToString().StartsWith( "Void AddRange" ))
                {
                    codes.Insert( i + 3, CodeInstruction.LoadField( type, "tmpAnimalsFemaleYoung" ));
                    codes.Insert( i + 4, codes[ i + 1 ].Clone());
                    codes.Insert( i + 5, codes[ i + 2 ].Clone());
                    foundTotalPregnant = true;
                }
                // The function has code:
                // while (tmpAnimalsFemale.Count > config.maxFemales)
                // Change to:
                // while (AnimalsToSlaughter_HookFemales(tmpAnimalsFemale.Count) > config.maxFemales)
                if( foundTotalPregnant && !foundMaxFemales
                    && codes[ i ].opcode == OpCodes.Ldsfld && codes[ i ].operand.ToString().EndsWith( "tmpAnimalsFemale" )
                    && i + 4 < codes.Count
                    && codes[ i + 1 ].opcode == OpCodes.Callvirt && codes[ i + 1 ].operand.ToString() == "Int32 get_Count()"
                    && codes[ i + 2 ].IsLdloc()
                    && codes[ i + 3 ].opcode == OpCodes.Ldfld && codes[ i + 3 ].operand.ToString() == "System.Int32 maxFemales"
                    && codes[ i + 4 ].opcode == OpCodes.Bgt_S )
                {
                    codes.Insert( i + 2, new CodeInstruction( OpCodes.Call,
                        typeof( AutoSlaughterManager_Patch ).GetMethod( nameof( AnimalsToSlaughter_HookFemales ))));
                    foundMaxFemales = true;
                }
                // The same, but for maxFemalesYoung (total).
                if( foundMaxFemales && !foundMaxFemalesTotal
                    && codes[ i ].opcode == OpCodes.Ldsfld && codes[ i ].operand.ToString().EndsWith( "tmpAnimalsFemaleYoung" )
                    && i + 4 < codes.Count
                    && codes[ i + 1 ].opcode == OpCodes.Callvirt && codes[ i + 1 ].operand.ToString() == "Int32 get_Count()"
                    && codes[ i + 2 ].IsLdloc()
                    && codes[ i + 3 ].opcode == OpCodes.Ldfld && codes[ i + 3 ].operand.ToString() == "System.Int32 maxFemalesYoung"
                    && codes[ i + 4 ].opcode == OpCodes.Bgt_S )
                {
                    codes.Insert( i + 2, new CodeInstruction( OpCodes.Call,
                        typeof( AutoSlaughterManager_Patch ).GetMethod( nameof( AnimalsToSlaughter_HookFemalesTotal ))));
                    foundMaxFemalesTotal = true;
                }
                // The same, but for maxMales.
                if( foundMaxFemalesTotal && !foundMaxMales
                    && codes[ i ].opcode == OpCodes.Ldsfld && codes[ i ].operand.ToString().EndsWith( "tmpAnimalsMale" )
                    && i + 4 < codes.Count
                    && codes[ i + 1 ].opcode == OpCodes.Callvirt && codes[ i + 1 ].operand.ToString() == "Int32 get_Count()"
                    && codes[ i + 2 ].IsLdloc()
                    && codes[ i + 3 ].opcode == OpCodes.Ldfld && codes[ i + 3 ].operand.ToString() == "System.Int32 maxMales"
                    && codes[ i + 4 ].opcode == OpCodes.Bgt_S )
                {
                    codes.Insert( i + 2, new CodeInstruction( OpCodes.Call,
                        typeof( AutoSlaughterManager_Patch ).GetMethod( nameof( AnimalsToSlaughter_HookMales ))));
                    foundMaxMales = true;
                }
                // The same, but for maxFemalesYoung (total).
                if( foundMaxMales && !foundMaxMalesTotal
                    && codes[ i ].opcode == OpCodes.Ldsfld && codes[ i ].operand.ToString().EndsWith( "tmpAnimalsMaleYoung" )
                    && i + 4 < codes.Count
                    && codes[ i + 1 ].opcode == OpCodes.Callvirt && codes[ i + 1 ].operand.ToString() == "Int32 get_Count()"
                    && codes[ i + 2 ].IsLdloc()
                    && codes[ i + 3 ].opcode == OpCodes.Ldfld && codes[ i + 3 ].operand.ToString() == "System.Int32 maxMalesYoung"
                    && codes[ i + 4 ].opcode == OpCodes.Bgt_S )
                {
                    codes.Insert( i + 2, new CodeInstruction( OpCodes.Call,
                        typeof( AutoSlaughterManager_Patch ).GetMethod( nameof( AnimalsToSlaughter_HookMalesTotal ))));
                    foundMaxMalesTotal = true;
                }
                // The same, but for maxTotal.
                if( foundMaxMalesTotal && !foundMaxTotal
                    && codes[ i ].opcode == OpCodes.Ldsfld && codes[ i ].operand.ToString().EndsWith( "tmpAnimals" )
                    && i + 4 < codes.Count
                    && codes[ i + 1 ].opcode == OpCodes.Callvirt && codes[ i + 1 ].operand.ToString() == "Int32 get_Count()"
                    && codes[ i + 2 ].IsLdloc()
                    && codes[ i + 3 ].opcode == OpCodes.Ldfld && codes[ i + 3 ].operand.ToString() == "System.Int32 maxTotal"
                    && codes[ i + 4 ].opcode == OpCodes.Bgt ) // No _S here.
                {
                    codes.Insert( i + 2, new CodeInstruction( OpCodes.Call,
                        typeof( AutoSlaughterManager_Patch ).GetMethod( nameof( AnimalsToSlaughter_HookTotal ))));
                    foundMaxTotal = true;
                }
#if DEBUG
                if( codes[ i ].opcode == OpCodes.Stfld && codes[ i ].operand.ToString() == "System.Boolean cacheDirty" )
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
                        typeof( AutoSlaughterManager_Patch ).GetMethod( nameof( AnimalsToSlaughter_Hook_Debug ))));
                }
#endif
            }
            if( !foundClear || !foundCanSlaughter || !foundBond || !foundMales || !foundFemales || !foundPregnant || !foundSort || !foundTotalPregnant
                || !foundMaxFemales || !foundMaxFemalesTotal || !foundMaxMales || !foundMaxMalesTotal || !foundMaxTotal )
            {
                Log.Error("ImprovedAutoSlaughter: Failed to patch AutoSlaughterManager.AnimalsToSlaughter()");
            }
            return codes;
        }

        public static void AnimalsToSlaughter_HookClear()
        {
            totalNoSlaughter = 0;
            totalNoSlaughterMales = 0;
            totalNoSlaughterMalesAdult = 0;
            totalNoSlaughterFemales = 0;
            totalNoSlaughterFemalesAdult = 0;
            totalBonded = 0;
            totalBondedMales = 0;
            totalBondedMalesAdult = 0;
            totalBondedFemales = 0;
            totalBondedFemalesAdult = 0;
            totalPregnant = 0;
        }

        // Intentionally patch the place that calls this function rather than
        // adding a postfix to the function. This ensures that our code will be called last,
        // making it possible to other mods to modify the function without affecting
        // this functionality, regardless of mod order.
        // That will prevent animals skipped by those mods to be included in the counts though,
        // so this is mainly for things like alien animals or whatever, not for duplicating
        // functionality of this mod.
        public static bool AnimalsToSlaughter_HookCanSlaughter( Pawn animal )
        {
            if( !AutoSlaughterManager.CanAutoSlaughterNow( animal ))
                return false;
            if( !Current.Game.GetComponent< SlaughterComponent >().preventAutoSlaughter( animal ))
                return true;
            ++totalNoSlaughter;
            if ( animal.gender == Gender.Male )
            {
                ++totalNoSlaughterMales;
                if ( animal.ageTracker.CurLifeStage.reproductive )
                    ++totalNoSlaughterMalesAdult;
            }
            else
            {
                ++totalNoSlaughterFemales;
                if ( animal.ageTracker.CurLifeStage.reproductive )
                    ++totalNoSlaughterFemalesAdult;
            }
            return false;
        }

        public static bool AnimalsToSlaughter_HookBond( bool bonded, Pawn animal )
        {
            // This function is only called if slaughtering of bonded is not allowed.
            if( !bonded )
                return false;
            ++totalBonded; // Bonded still need to be included in counts.
            if ( animal.gender == Gender.Male )
            {
                ++totalBondedMales;
                if ( animal.ageTracker.CurLifeStage.reproductive )
                    ++totalBondedMalesAdult;
            }
            else
            {
                ++totalBondedFemales;
                if ( animal.ageTracker.CurLifeStage.reproductive )
                    ++totalBondedFemalesAdult;
            }
            return true;
        }

        public static bool AnimalsToSlaughter_HookPregnant( bool allowSlaughterPregnant )
        {
            // This function is always called if pregnant.
            if( allowSlaughterPregnant )
                return true;
            ++totalPregnant; // If slaughtering of pregnant is not allowed, they still need to be included in counts.
            return false;
        }

        private static long ProductiveAgeIndex( Pawn p )
        {
            double f = ImprovedAutoSlaughterMod.settings.oldLifeExpectancyThreshold / 100f;
            // Animals that are above the age threshold go first, oldest first.
            if( p.ageTracker.AgeBiologicalTicks > p.RaceProps.lifeExpectancy * GenDate.TicksPerYear * f )
                return p.ageTracker.AgeBiologicalTicks - ( long )( p.RaceProps.lifeExpectancy * GenDate.TicksPerYear * f );
            // The rest go youngest first.
            return -p.ageTracker.AgeBiologicalTicks;
        }

        public static void AnimalsToSlaughter_HookSort( List< Pawn > tmpAnimals, List< Pawn > tmpAnimalsMale,
            List< Pawn > tmpAnimalsMaleYoung, List< Pawn > tmpAnimalsFemale, List< Pawn > tmpAnimalsFemaleYoung )
        {
            tmpAnimals.SortByDescending( ( Pawn p ) => ProductiveAgeIndex( p ));
            tmpAnimalsMale.SortByDescending( ( Pawn p ) => ProductiveAgeIndex( p ));
            tmpAnimalsMaleYoung.SortByDescending( ( Pawn p ) => ProductiveAgeIndex( p ));
            tmpAnimalsFemale.SortByDescending( ( Pawn p ) => ProductiveAgeIndex( p ));
            tmpAnimalsFemaleYoung.SortByDescending( ( Pawn p ) => ProductiveAgeIndex( p ));
        }

        public static int AnimalsToSlaughter_HookFemales( int count )
        {
            if( count == 0 )
                return 0; // The list is empty, nothing to iterate over.
            return count + totalBondedFemalesAdult + totalPregnant;
        }

        public static int AnimalsToSlaughter_HookFemalesTotal( int count )
        {
            if( count == 0 )
                return 0; // The list is empty, nothing to iterate over.
            return count + totalBondedFemales + totalPregnant;
        }

        public static int AnimalsToSlaughter_HookMales( int count )
        {
            if( count == 0 )
                return 0; // The list is empty, nothing to iterate over.
            return count + totalBondedMalesAdult;
        }

        public static int AnimalsToSlaughter_HookMalesTotal( int count )
        {
            if( count == 0 )
                return 0; // The list is empty, nothing to iterate over.
            return count + totalBondedMales;
        }

        public static int AnimalsToSlaughter_HookTotal( int count )
        {
            if( count == 0 )
                return 0; // The list is empty, nothing to iterate over.
            return count + totalBonded + totalPregnant;
        }

#if DEBUG
        public static void AnimalsToSlaughter_Hook_Debug( List< Pawn > tmpAnimals, List< Pawn > tmpAnimalsMale,
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
