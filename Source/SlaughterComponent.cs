using Verse;
using System.Collections.Generic;

namespace ImprovedAutoSlaughter
{
    public class SlaughterComponent : GameComponent
    {
        public HashSet< Pawn > noAutoSlaughter = new HashSet< Pawn >();

        public bool preventAutoSlaughter( Pawn animal )
        {
            return noAutoSlaughter.Contains( animal );
        }

        public void flipPreventAutoSlaughter( Pawn animal )
        {
            if( !noAutoSlaughter.Remove( animal ))
                noAutoSlaughter.Add( animal );
        }

        public SlaughterComponent( Game game )
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref noAutoSlaughter, "ImprovedAutoSlaughter.NoAutoSlaughter", LookMode.Reference);
        }
    }
}
