// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Utils;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class OverallStrainEvaluator
    {
        private const double release_threshold = 30;
        //private const double release_exponent = 3;
        private const double release_multiplier = 0.27;
        private const double overlap_max_bonus = 1;
        private const double hold_threshold = 30;
        //private const double hold_exponent = 4;
        private const double hold_multiplier = 0.27;
        private const double hold_max_bonus = 0.25;
        //private const double length_threshold = 30; // RC value
        //private const double length_mantissa = 10;
        //private const double length_max_bonus = 1;
        //private const double length_min_bonus = 0.01;
        //private const double decay_threshold = 25;
        //private const double decay_multiplier = 0.3;

        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            var maniaCurrent = (ManiaDifficultyHitObject)current;
            double startTime = maniaCurrent.StartTime;
            double endTime = maniaCurrent.EndTime;
            bool isOverlapping = false;
            bool isHeld = false;

            double closestOverlapEndTime = Math.Abs(endTime - startTime); // Lowest value we can assume with the current information
            double closestOverlapStartTime = Math.Abs(endTime - startTime);
            double overlapBonus = 0; // Addition to the current note in case it's a hold and has to be released awkwardly
            double furthestHoldEndTime = 0;
            double furthestHoldStartTime = 0;
            double holdBonus = 0; // Factor to all additional strains in case something else is held
            double lengthBonus = 0; // Bonus for long notes especially for super short ones
            //double holdReferenceValue = 10000;


            foreach (var maniaPrevious in maniaCurrent.PreviousHitObjects)
            {
                if (maniaPrevious is null)
                    continue;

                // The current note is overlapped if a previous note or end is overlapping the current note body
                // isOverlapping |= Precision.DefinitelyBigger(maniaPrevious.EndTime, startTime, 1) &&
                //                 Precision.DefinitelyBigger(endTime, maniaPrevious.EndTime, 1) &&
                //                 Precision.DefinitelyBigger(startTime, maniaPrevious.StartTime, 1);

                // For overlap time interval detection we only account for overlapped notes
                if (Precision.DefinitelyBigger(maniaPrevious.EndTime, startTime, 1) &&
                    Precision.DefinitelyBigger(endTime, maniaPrevious.EndTime, 1) &&
                    Precision.DefinitelyBigger(startTime, maniaPrevious.StartTime, 1))
                {
                    isOverlapping = true;
                    closestOverlapStartTime = Math.Min(closestOverlapStartTime, Math.Abs(startTime - maniaPrevious.EndTime));
                    closestOverlapEndTime = Math.Min(closestOverlapEndTime, Math.Abs(endTime - maniaPrevious.EndTime));
                }


                // We give a slight bonus to everything if something is held meanwhile
                // This is mutually exclusive to the overlap bonus
                if (Precision.DefinitelyBigger(maniaPrevious.EndTime, endTime, 1) &&
                    Precision.DefinitelyBigger(startTime, maniaPrevious.StartTime, 1))
                {
                    isHeld = true;
                    furthestHoldStartTime = Math.Max(furthestHoldStartTime, Math.Abs(startTime - maniaPrevious.StartTime));
                    furthestHoldEndTime = Math.Max(furthestHoldEndTime, Math.Abs(endTime - maniaPrevious.EndTime));
                    //holdReferenceValue = Math.Min(holdReferenceValue, maniaPrevious.HoldBonusReferenced);
                }
            }

            if (isOverlapping)
            {
                //double overlapStartBonus = Math.Min(Math.Pow(closestOverlapStartTime / release_threshold, Math.Log2(release_exponent)), 1);
                //double overlapEndBonus = Math.Min(Math.Pow(closestOverlapEndTime / release_threshold, Math.Log2(release_exponent)), 1);
                //double overlapStartBonus = DifficultyCalculationUtils.Logistic(x: closestOverlapStartTime, multiplier: release_multiplier, midpointOffset: release_threshold);
                double overlapStartBonus = 1;
                double overlapEndBonus = DifficultyCalculationUtils.Logistic(x: closestOverlapEndTime, multiplier: release_multiplier, midpointOffset: release_threshold);
                overlapBonus = Math.Min(overlapStartBonus, overlapEndBonus) * overlap_max_bonus;
            }

            // proposal function
            // (x / threshold) ^ log2(exponent)
            // at x = threshold / 2, the bonus equals to 1/exponent
            // at x = threshold, the bonus reaches its maximum of 1.0x

            // hold bonus decay uses sigmoid function
            // Math.Min(Math.Pow(furthestHoldStartTime / hold_threshold, Math.Log2(hold_exponent)), 1);

            if (isHeld)
            {
                //double holdStartBonus = DifficultyCalculationUtils.Logistic(x: furthestHoldStartTime, multiplier: hold_multiplier, midpointOffset: hold_threshold);
                //double holdEndBonus = DifficultyCalculationUtils.Logistic(x: furthestHoldEndTime, multiplier: hold_multiplier, midpointOffset: hold_threshold);
                //double holdBonusDecay = DifficultyCalculationUtils.Logistic(x: holdReferenceValue, multiplier: -decay_multiplier, midpointOffset: decay_threshold);
                holdBonus = hold_max_bonus; //Math.Min(holdStartBonus, holdEndBonus) * 
            }

            // ln length bonus
            // mantissa ^ -(x / threshold)
            // the shorter the LN is, the higher the bonus is, cuz it's generally hard to get
            // rainbow 300s on very short lns while the ratio is a big part of pp algorithm
            // at x = threshold, the bonus equals to 1 / mantissa
            // note: the RC ensures that the vast majority of LNs in ranked maps will be longer than 30ms
            // note2: the ln detection below is a provisional method

            //if (Precision.DefinitelyBigger(endTime, startTime, 1))
            //{
            //    double noteLength = Math.Abs(endTime - startTime);
            //    lengthBonus = Math.Max(Math.Pow(length_mantissa, -1 * noteLength / length_threshold), length_min_bonus) * length_max_bonus;
            //}

            return (1 + overlapBonus) * (1 + holdBonus) * (1 + lengthBonus);
        }
    }
}
