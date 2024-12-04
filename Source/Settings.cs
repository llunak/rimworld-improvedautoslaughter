using Verse;
using UnityEngine;

namespace ImprovedAutoSlaughter
{
    public class Settings : ModSettings
    {
        const int defValue = 80; // %

        public int oldLifeExpectancyThreshold = defValue;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look( ref oldLifeExpectancyThreshold, "OldLifeExpectancyThreshold", defValue );
        }
    }

    public class ImprovedAutoSlaughterMod : Mod
    {
        private static Settings _settings;
        public static Settings settings { get { return _settings; }}

        public ImprovedAutoSlaughterMod( ModContentPack content )
            : base( content )
        {
            _settings = GetSettings< Settings >();
        }

        public override string SettingsCategory()
        {
            return "Improved Auto Slaughter";
        }

        public override void DoSettingsWindowContents(Rect rect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin( rect );
            int oldThreshold = settings.oldLifeExpectancyThreshold;
            settings.oldLifeExpectancyThreshold = Mathf.RoundToInt( listing.SliderLabeled(
                "ImprovedAutoSlaughter.OldLifeExpectancyThreshold".Translate( settings.oldLifeExpectancyThreshold ),
                settings.oldLifeExpectancyThreshold, 10f, 150f,
                tooltip: "ImprovedAutoSlaughter.OldLifeExpectancyThresholdTooltip".Translate()));
            listing.End();
            base.DoSettingsWindowContents(rect);
            // Need to dirty the cache in the manager if changed during gameplay.
            if( settings.oldLifeExpectancyThreshold != oldThreshold && Current.Game != null )
                foreach( Map map in Find.Maps )
                    map.autoSlaughterManager?.Notify_ConfigChanged();
        }
    }
}
