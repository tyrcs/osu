// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Utils;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class OverallStrainEvaluator
    {

        // All constants are subject to change

        private const double press_bonus_threshold_1 = 31; // 1/8 at 240BPM
        private const double press_bonus_multiplier_1 = 0.27;
        private const double press_bonus_threshold_2 = 250; // 1/1 at 240BPM
        private const double press_bonus_multiplier_2 = 0.036;
        private const double press_bonus_max_value = 0.25;
        private const double press_bonus_min_portion = 0.05;

        private const double hold_bonus_release_time_addition = 31; // 1/8 at 240BPM
        private const double hold_bonus_threshold = 31; // 1/8 at 240BPM
        private const double hold_bonus_multiplier = 0.27;
        private const double hold_bonus_max_value = 0.75; // relatively high because there are not too many LNs with many same hand notes
        private const double hold_intensity_steepness = 5; // amount of Intensity for hold_bonus_max_value - holdBonus to cut in half
        private const double hold_intensity_threshold = 250; // 1/1 at 240BPM; this is the threshold interval to get extra intensity
        private const double hold_intensity_exponent = 4;
        private const double hold_intensity_max_bonus = 4;

        private const double release_bonus_threshold = 62; // 1/4 at 240BPM
        private const double release_bonus_multiplier = 0.075;
        private const double release_bonus_max_value = 0.5; // the release could be considerably tricky

        private const double column_distance_decay = 0.333333;

        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            var maniaCurrent = (ManiaDifficultyHitObject)current;
            double startTime = maniaCurrent.StartTime;
            double endTime = maniaCurrent.EndTime;
            int column = maniaCurrent.Column;
            int totalColumns = maniaCurrent.PreviousHitObjects.Length;

            double pressBonus = 0;
            double holdBonus = 0;
            double releaseBonus = 0;

            foreach (var maniaPreviousLoop in maniaCurrent.PreviousHitObjects)
            {
                if (maniaPreviousLoop is null)
                    continue;

                // solution for the chord order issue
                // the same chord contains either current or the NextInColumn of each note in PreviousHitObjects
                var maniaPrevious = maniaPreviousLoop;
                var maniaPreviousNext = maniaPrevious.NextInColumn(0);
                if (maniaPreviousNext != null &&
                    maniaPreviousNext.StartTime == startTime)
                {
                    maniaPrevious = maniaPreviousNext;
                }

                // initialzation for various calculation

                int previousColumn = maniaPrevious.Column;
                if (previousColumn == column) // All of the calculation is based on cross-column notes
                    continue;

                double sameHandImpactness = DifficultyUtils.SameHandImpactness(previousColumn, column, totalColumns);
                double adjacentImpactness = DifficultyUtils.SameHandAdjacentColumnImpactness(previousColumn, column, totalColumns, column_distance_decay);

                // I. Press bonus
                // measure how hard for a note to be pressed correctly
                // similar to old holdBonus
                // if current note is pressed while an adjacent same hand LN is held we award a bonus for current note

                //               <<-- V -->>
                // cur.  :            |
                // adja. :  [-------------------]
                //            ____         ____
                // Bonus :  _/    \_______/    \_ * max_value
                //          0 1  min_portion  1 0

                if (adjacentImpactness > 0 &&
                    Precision.DefinitelyBigger(maniaPrevious.EndTime, startTime, 1) &&
                    Precision.DefinitelyBigger(startTime, maniaPrevious.StartTime, 1))
                {
                    double closestActionTime = Math.Min(Math.Abs(maniaPrevious.EndTime - startTime), Math.Abs(startTime - maniaPrevious.StartTime));
                    double pressHeadMultiplier = DifficultyCalculationUtils.Logistic(x: closestActionTime, multiplier: press_bonus_multiplier_1, midpointOffset: press_bonus_threshold_1);
                    double pressBodyMultiplier = DifficultyCalculationUtils.Logistic(x: closestActionTime, multiplier: -press_bonus_multiplier_2, midpointOffset: press_bonus_threshold_2)
                                               * (1 - press_bonus_min_portion) + press_bonus_min_portion;
                    pressBonus += adjacentImpactness * pressHeadMultiplier * pressBodyMultiplier;
                }

                // Bonuses below are only for long notes

                if (Precision.DefinitelyBigger(endTime, startTime, 1))
                {

                    // II. Hold bonus
                    // measure how hard for a long note to be held correctly
                    // this is not the same as old holdBonus
                    // basically more intense the same hand notes are, more likely the hold will be interrupted
                    // for the sake of this we need to get all notes that is pressed during the LN

                    if (sameHandImpactness > 0)
                    {
                        double holdIntensity = 0;
                        var heldNote = maniaPrevious.NextInColumn(0);
                        // loop every note until the LN ends
                        // we assume current maniaPrevious.StartTime is at most startTime

                        for (int i = 0; i < endTime + hold_bonus_release_time_addition - startTime; i++)
                        {
                            if (heldNote is null ||
                                Precision.DefinitelyBigger(heldNote.StartTime, endTime + hold_bonus_release_time_addition, 1))
                                break;

                            double heldStartTime = heldNote.StartTime;
                            // calculate the interval between heldNote and previous note of heldNote
                            double intensityBonus = 1;
                            var previousHeldNote = heldNote.PrevInColumn(0);
                            if (previousHeldNote != null)
                            {
                                double heldInterval = Math.Abs(previousHeldNote.StartTime - heldStartTime);
                                intensityBonus = 1 + Math.Pow(1 - heldInterval / hold_intensity_threshold, hold_intensity_exponent) * hold_intensity_max_bonus;
                            }

                            double closestActionTime = Math.Min(Math.Abs(endTime + hold_bonus_release_time_addition - heldNote.StartTime), Math.Abs(heldNote.StartTime - startTime));
                            holdIntensity += DifficultyCalculationUtils.Logistic(x: closestActionTime, multiplier: hold_bonus_multiplier, midpointOffset: hold_bonus_threshold)
                                             * sameHandImpactness * intensityBonus;
                            heldNote = heldNote.NextInColumn(0);
                        }

                        holdBonus += holdIntensity;
                    }

                    // III. Release bonus
                    // measure how hard for a long note to be released correctly
                    // similar to old overlapBonus
                    // we award a bonus for current note if the note is released while an adjacent same hand LN is held
                    // due to it being trickier to release

                    //              <<--  V  -->>
                    // cur.  :------------]
                    // adja. :  [-------------------]
                    //              _____________
                    // Bonus :  ___/             \___ * max_value
                    //           0        1        0

                    // for the sake of this we need to refer to the last note in the column before current LN ends
                    // Todo: could make this code incorporated into hold bonus section due to adjacentImpactness overlapping sameHandImpactness

                    if (adjacentImpactness > 0)
                    {
                        var heldNote = maniaPrevious;
                        var overlappingNote = heldNote;
                        bool isOverlapping = false;
                        // loop every note until the LN ends to get the overlapped LN (if theres one)

                        for (int i = 0; i < endTime - startTime; i++)
                        {
                            if (heldNote is null ||
                                Precision.DefinitelyBigger(heldNote.StartTime, endTime, 1))
                                break;

                            if (Precision.DefinitelyBigger(heldNote.EndTime, endTime, 1))
                            {
                                isOverlapping = true;
                                overlappingNote = heldNote;
                                break;
                            }

                            heldNote = heldNote.NextInColumn(0);
                        }

                        // calculate the release bonus if theres an overlapping LN
                        if (isOverlapping)
                        {
                            double closestActionTime = Math.Min(Math.Abs(overlappingNote.EndTime - endTime), Math.Abs(endTime - overlappingNote.StartTime));
                            releaseBonus += adjacentImpactness *
                                            DifficultyCalculationUtils.Logistic(x: closestActionTime, multiplier: release_bonus_multiplier, midpointOffset: release_bonus_threshold);
                        }
                    }
                }

                // WIP: overallReleaseIntensity
                // idea taken from sunny rework

            }

            // sqrt for press bonus that > 1
            pressBonus = (pressBonus > 1 ? Math.Sqrt(pressBonus) : pressBonus) * press_bonus_max_value;

            // holdBonus held the total intensity before
            // and we calculate the actual holdBonus
            holdBonus = (1 - Math.Pow(2, -holdBonus / hold_intensity_steepness)) * hold_bonus_max_value;

            // sqrt for release bonus that > 1
            releaseBonus = (releaseBonus > 1 ? Math.Sqrt(releaseBonus) : releaseBonus) * release_bonus_max_value;

            return 1 + pressBonus + holdBonus + releaseBonus;
            // return (1 + pressBonus) * (1 + holdBonus) * (1 + releaseBonus);
        }
    }
}
