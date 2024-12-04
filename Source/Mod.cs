using HarmonyLib;
using Verse;
using System.Reflection;

namespace ImprovedAutoSlaughter
{
    [StaticConstructorOnStartup]
    public class HarmonyPatches
    {
        static HarmonyPatches()
        {
            var harmony = new Harmony("llunak.ImprovedAutoSlaughter");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }
}
