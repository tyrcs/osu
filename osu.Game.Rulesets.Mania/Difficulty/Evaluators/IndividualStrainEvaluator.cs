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
    public class IndividualStrainEvaluator
    {

        // All constants are subject to change

        private const double press_bonus_threshold_1 = 31; // 1/8 at 240BPM
        private const double press_bonus_multiplier_1 = 0.27;
        private const double press_bonus_threshold_2 = 250; // 1/1 at 240BPM
        private const double press_bonus_multiplier_2 = 0.036;
        private const double press_bonus_max_value = 0.25;
        private const double press_bonus_min_portion = 0.05;

        private const double shield_bonus_threshold = 500; // 1/1 at 120BPM; this is the threshold interval to get shield bonus
        private const double shield_bonus_exponent = 2;
        private const double shield_bonus_overlapped_tail_threshold = 63; // 1/4 at 240BPM
        private const double shield_bonus_overlapped_threshold = 31; // 1/8 at 240BPM
        private const double shield_bonus_overlapped_multiplier = 0.27;
        private const double shield_bonus_length_threshold = 125; // 1/2 at 240BPM
        private const double shield_bonus_length_multiplier = 0.075;
        private const double shield_bonus_max_value = 1.5; // Very high
        private const double shield_bonus_length_bonus = 1.5;

        private const double column_distance_decay = 0.333333;

        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            var maniaCurrent = (ManiaDifficultyHitObject)current;
            double startTime = maniaCurrent.StartTime;
            double endTime = maniaCurrent.EndTime;
            int column = maniaCurrent.Column;
            int totalColumns = maniaCurrent.PreviousHitObjects.Length;

            double pressBonus = 0;
            double shieldBonus = 0;

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

                // calculate press bonus
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

                // IV. Shield bonus
                // similar to press bonus, this is just an additional buff for shields

                //   Overlap Multiplier (sigmoid function)
                //             <<-- v   V -->>
                // cur.  :          |   [-----------
                // adja. :  [-------------------]
                //              _____________
                // Bonus :  ___/             \___
                //           0        1        0

                //   Length Multiplier (sigmoid function)
                //               <<-- V -->>
                // cur.  :   |  [-----]
                //                    ___________
                // Bonus2:  _______///
                //             0             1

                //   Interval Multiplier (exponential)
                //           <<-- V -->>
                // cur.  :    |   [-----------
                //             _
                // Bonus3:      \\\\\\\\\\\______
                //             1             0

                if (adjacentImpactness > 0 &&
                    Precision.DefinitelyBigger(maniaPrevious.EndTime + shield_bonus_overlapped_tail_threshold, startTime, 1) &&
                    Precision.DefinitelyBigger(startTime, maniaPrevious.StartTime, 1))
                {
                    var currentColumnPrevious = maniaCurrent.PrevInColumn(0);
                    if (currentColumnPrevious is null)
                        continue;
                    double shieldInterval = Math.Max(Math.Abs(currentColumnPrevious.StartTime - startTime), 1);
                    if (shieldInterval <= shield_bonus_threshold)
                    {
                        double shieldHeadMultiplier = Precision.DefinitelyBigger(currentColumnPrevious.StartTime, maniaPrevious.StartTime, 1) ? 1 :
                                                      (startTime - maniaPrevious.StartTime) / shieldInterval; // smoothing WIP
                        double shieldTailMultiplier = DifficultyCalculationUtils.Logistic(x: Math.Abs(maniaPrevious.EndTime + shield_bonus_overlapped_tail_threshold - startTime),
                                                                                          multiplier: shield_bonus_overlapped_multiplier,
                                                                                          midpointOffset: shield_bonus_overlapped_threshold);
                        double shieldIntervalBonus = Math.Pow(1 - shieldInterval / shield_bonus_threshold, shield_bonus_exponent);
                        shieldBonus += adjacentImpactness * shieldHeadMultiplier * shieldTailMultiplier * shieldIntervalBonus;
                    }

                }

            }

            // sqrt for press bonus that > 1
            pressBonus = (pressBonus > 1 ? Math.Sqrt(pressBonus) : pressBonus) * press_bonus_max_value;

            double shieldLengthBonus = Precision.DefinitelyBigger(endTime, startTime, 1) ? 0 :
                                       DifficultyCalculationUtils.Logistic(x: Math.Abs(endTime - startTime), // extra bonus for shield bonus if the note is an LN
                                                                           multiplier: shield_bonus_length_multiplier,
                                                                           midpointOffset: shield_bonus_length_threshold) * shield_bonus_length_bonus;
            // sqrt for shield bonus that > 1
            shieldBonus = (shieldBonus > 1 ? Math.Sqrt(shieldBonus) : shieldBonus) * (shield_bonus_max_value + shieldLengthBonus);

            return 2.0 * (1 + pressBonus + shieldBonus);
        }
    }
}
