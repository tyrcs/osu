// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Utils;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class IndividualStrainEvaluator
    {

        private const double hold_threshold = 30;
        //private const double hold_exponent = 4;
        private const double hold_multiplier = 0.27;
        private const double hold_max_bonus = 0.25;

        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            var maniaCurrent = (ManiaDifficultyHitObject)current;
            double startTime = maniaCurrent.StartTime;
            double endTime = maniaCurrent.EndTime;
            bool isHeld = false;
            double furthestHoldEndTime = 0;
            double furthestHoldStartTime = 0;

            double holdBonus = 0; // Factor to all additional strains in case something else is held

            // We award a bonus if this note starts and ends before the end of another hold note.
            foreach (var maniaPrevious in maniaCurrent.PreviousHitObjects)
            {
                if (maniaPrevious is null)
                    continue;

                if (Precision.DefinitelyBigger(maniaPrevious.EndTime, endTime, 1) &&
                    Precision.DefinitelyBigger(startTime, maniaPrevious.StartTime, 1))
                {
                    isHeld = true;
                    furthestHoldStartTime = Math.Max(furthestHoldStartTime, Math.Abs(startTime - maniaPrevious.StartTime));
                    furthestHoldEndTime = Math.Max(furthestHoldEndTime, Math.Abs(endTime - maniaPrevious.EndTime));
                    //holdReferenceValue = Math.Min(holdReferenceValue, maniaPrevious.HoldBonusReferenced);
                }
            }

            if (isHeld)
            {
                //double holdStartBonus = DifficultyCalculationUtils.Logistic(x: furthestHoldStartTime, multiplier: hold_multiplier, midpointOffset: hold_threshold);
                //double holdEndBonus = DifficultyCalculationUtils.Logistic(x: furthestHoldEndTime, multiplier: hold_multiplier, midpointOffset: hold_threshold);
                //double holdBonusDecay = DifficultyCalculationUtils.Logistic(x: holdReferenceValue, multiplier: -decay_multiplier, midpointOffset: decay_threshold);
                holdBonus = hold_max_bonus; //Math.Min(holdStartBonus, holdEndBonus) * 
            }

            return 2.0 * (1 + holdBonus);
        }
    }
}
