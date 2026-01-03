// utils for asdfjkljkl

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public class DifficultyUtils
    {
        // Calculate the impactness that depends on if two notes are in the same hand or not
        // same: 1   special: 0.5   different: 0
        // special is for the middle column of odd column counts
        // We assume the two objects are not in the same column
        // Todo: assign a custom value for every column of every keymode

        // public static double SameHandImpactness(DifficultyHitObject object1, DifficultyHitObject object2)
        public static double SameHandImpactness(int objectColumn1, int objectColumn2, int totalColumns)
        {
            // var hitObject1 = (ManiaDifficultyHitObject)object1;
            // var hitObject2 = (ManiaDifficultyHitObject)object2;
            // int totalColumns = hitObject1.PreviousHitObjects.Length;
            // int objectColumn1 = hitObject1.Column;
            // int objectColumn2 = hitObject2.Column;

            // left: 0   middle: 0.5   right: 1
            double objectHand1 = (double)objectColumn1 / totalColumns == 0.5 ? 0.5 : Math.Round((double)objectColumn1 / totalColumns);
            double objectHand2 = (double)objectColumn2 / totalColumns == 0.5 ? 0.5 : Math.Round((double)objectColumn2 / totalColumns);

            return objectHand1 == objectHand2 ? 1 : (objectHand1 + objectHand2) % 1; // should return 0 if it's left + right
        }

        // public static bool IsAdjacentColumn(DifficultyHitObject object1, DifficultyHitObject object2)
        public static bool IsAdjacentColumn(int objectColumn1, int objectColumn2)
        {
            // var hitObject1 = (ManiaDifficultyHitObject)object1;
            // var hitObject2 = (ManiaDifficultyHitObject)object2;
            // int objectColumn1 = hitObject1.Column;
            // int objectColumn2 = hitObject2.Column;

            return Math.Abs(objectColumn1 - objectColumn2) == 1;
        }
        public static int ColumnDistance(int objectColumn1, int objectColumn2)
        {
            return Math.Abs(objectColumn1 - objectColumn2);
        }

        // true: 1   special: 0.5   false: 0

        // public static double IsSameHandAdjacentColumn(DifficultyHitObject object1, DifficultyHitObject object2)
        public static double SameHandAdjacentColumnImpactness(int objectColumn1, int objectColumn2, int totalColumns, double decay)
        {
            int columnDistance = ColumnDistance(objectColumn1, objectColumn2);
            double sameHandImpactness = SameHandImpactness(objectColumn1, objectColumn2, totalColumns);

            return sameHandImpactness * Math.Min(Math.Pow(decay, columnDistance - 1), 1);
        }
    }
}
