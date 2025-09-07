using Verse;
using System.Collections.Generic;

namespace ImprovedAutoSlaughter
{
    public class SlaughterComponent : GameComponent
    {
        public HashSet< Pawn > noAutoSlaughter = new HashSet< Pawn >();
        public HashSet< Pawn > priorityAutoSlaughter = new HashSet< Pawn >();

        public bool preventAutoSlaughter( Pawn animal )
        {
            return noAutoSlaughter.Contains( animal );
        }

        public void flipPreventAutoSlaughter( Pawn animal )
        {
            if( !noAutoSlaughter.Remove( animal ))
                noAutoSlaughter.Add( animal );
        }

        public bool preferAutoSlaughter( Pawn animal )
        {
            return priorityAutoSlaughter.Contains( animal );
        }

        public void flipPreferAutoSlaughter( Pawn animal )
        {
            if( !priorityAutoSlaughter.Remove( animal ))
                priorityAutoSlaughter.Add( animal );
        }

        public SlaughterComponent( Game game )
        {
        }

        public void Remove( Pawn animal )
        {
            noAutoSlaughter.Remove( animal );
            priorityAutoSlaughter.Remove( animal );
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref noAutoSlaughter, "ImprovedAutoSlaughter.NoAutoSlaughter", LookMode.Reference);
            Scribe_Collections.Look(ref priorityAutoSlaughter, "ImprovedAutoSlaughter.PriorityAutoSlaughter", LookMode.Reference);
        }
    }
}
