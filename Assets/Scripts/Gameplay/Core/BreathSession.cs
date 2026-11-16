using System;
using System.Collections.Generic;

namespace WingSinger {
    public enum BreathLane { Lower = -1, Middle = 0, Upper = 1 }
    public enum BreathPhase { Preview, Running, Finished }

    // Pure time/score model: the guide and player sample the same route at the same world X.
    public readonly struct BreathPattern {
        public readonly double Inhale, Exhale, Duration, PointsPerCollectible, Lead, Transition, Preparation, GuideTransition;
        public readonly double LoopSeconds, MiddleTail;
        public readonly int LoopCount;
        public bool Segmented => LoopCount > 1;
        public double ReturnStart => Preparation + GuideTransition + Math.Floor((LoopSeconds - Preparation - GuideTransition - MiddleTail) / Cycle) * Cycle - GuideTransition;
        public double LocalTime(double time) => Segmented ? Math.Min(LoopSeconds, Math.Max(0, time) - Math.Min(LoopCount - 1, (int)(Math.Max(0, time) / LoopSeconds)) * LoopSeconds) : Math.Max(0, time);
        public double Cycle => Inhale + Exhale + 2 * GuideTransition;
        double FirstArrival => Preparation + GuideTransition;

        public BreathPattern(double inhale, double exhale, double duration, double pointsPerCollectible,
            double lead, double transition, double preparation = 0, double guideTransition = 0,
            int loopCount = 1, double loopSeconds = 0, double middleTail = 2) {
            if (!Positive(inhale) || !Positive(exhale) || !Positive(duration)
                || !Nonnegative(pointsPerCollectible) || !Nonnegative(lead) || !Nonnegative(transition)
                || !Nonnegative(preparation) || !Nonnegative(guideTransition))
                throw new ArgumentException("Breathing settings must be finite; durations must be positive.");
            Inhale = inhale; Exhale = exhale; Duration = duration;
            PointsPerCollectible = pointsPerCollectible; Lead = lead; Transition = transition;
            Preparation = preparation; GuideTransition = guideTransition;
            LoopCount = loopCount; LoopSeconds = loopCount > 1 ? loopSeconds : duration; MiddleTail = middleTail;
            if (loopCount < 1 || !Positive(LoopSeconds) || !Nonnegative(middleTail)
                || (Segmented && (Math.Abs(duration - loopCount * loopSeconds) > .001 || ReturnStart <= FirstArrival)))
                throw new ArgumentException("Invalid segmented breathing route.");
        }

        static bool Nonnegative(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;
        static bool Positive(double value) => Nonnegative(value) && value > 0;

        public bool IsPreparing(double routeTime) => Segmented
            ? LocalTime(routeTime) < Preparation || LocalTime(routeTime) >= ReturnStart
            : routeTime < Preparation;
        public bool IsReturning(double routeTime) => Segmented && LocalTime(routeTime) >= ReturnStart
            && LocalTime(routeTime) < ReturnStart + GuideTransition;

        // During a curve the requested lane is the destination, so the prompt can guide input early.
        public BreathLane LaneAt(double routeTime) {
            routeTime = LocalTime(routeTime);
            if (routeTime < Preparation || (Segmented && routeTime >= ReturnStart)) return BreathLane.Middle;
            if (routeTime < FirstArrival) return BreathLane.Upper;
            double cycleTime = (routeTime - FirstArrival) % Cycle;
            if (cycleTime < Inhale) return BreathLane.Upper;
            if (cycleTime < Inhale + GuideTransition + Exhale) return BreathLane.Lower;
            return BreathLane.Upper;
        }

        public bool IsGuideTransition(double routeTime) {
            routeTime = LocalTime(routeTime);
            if (routeTime < Preparation || GuideTransition == 0) return false;
            if (Segmented && routeTime >= ReturnStart) return routeTime < ReturnStart + GuideTransition;
            if (routeTime < FirstArrival) return true;
            double cycleTime = (routeTime - FirstArrival) % Cycle;
            return (cycleTime >= Inhale && cycleTime < Inhale + GuideTransition)
                || cycleTime >= Inhale + GuideTransition + Exhale;
        }

        // Continuous route height in units of lane spacing; shared by the guide and its trail.
        public double HeightAt(double routeTime) {
            routeTime = LocalTime(routeTime);
            if (routeTime < Preparation) return 0;
            if (Segmented && routeTime >= ReturnStart) return routeTime < ReturnStart + GuideTransition
                ? -1 + Smooth((routeTime - ReturnStart) / GuideTransition) : 0;
            if (routeTime < FirstArrival) return Smooth((routeTime - Preparation) / GuideTransition);
            double cycleTime = (routeTime - FirstArrival) % Cycle;
            if (cycleTime < Inhale) return 1;
            if (cycleTime < Inhale + GuideTransition)
                return 1 - 2 * Smooth((cycleTime - Inhale) / GuideTransition);
            if (cycleTime < Inhale + GuideTransition + Exhale) return -1;
            return -1 + 2 * Smooth((cycleTime - Inhale - GuideTransition - Exhale) / GuideTransition);
        }

        static double Smooth(double t) => t * t * (3 - 2 * t);

        double DwellTime(double time, double start, double duration) {
            time = Math.Max(0, time - FirstArrival);
            return Math.Floor(time / Cycle) * duration
                + Math.Min(Math.Max(0, time % Cycle - start), duration);
        }

        // Integrates all crossed boundaries, including long frames and partial final seconds.
        public double MatchingSeconds(double from, double to, BreathLane lane) {
            from = Math.Max(0, from); to = Math.Max(from, to);
            if (lane == BreathLane.Middle) return 0;
            double start = lane == BreathLane.Upper ? 0 : Inhale + GuideTransition;
            double duration = lane == BreathLane.Upper ? Inhale : Exhale;
            if (!Segmented) return DwellTime(to, start, duration) - DwellTime(from, start, duration);
            double total = 0;
            for (int i = 0; i < LoopCount; i++) {
                double a = Math.Max(0, Math.Min(ReturnStart, from - i * LoopSeconds));
                double b = Math.Max(0, Math.Min(ReturnStart, to - i * LoopSeconds));
                total += DwellTime(b, start, duration) - DwellTime(a, start, duration);
            }
            return total;
        }
    }

    public sealed class BreathSession {
        public BreathPattern Pattern { get; }
        public double Elapsed { get; private set; }
        public double PlayTime => Math.Max(0, Elapsed - Pattern.Lead);
        public double Remaining => Math.Max(0, Pattern.Duration - PlayTime);
        public double CorrectSeconds { get; private set; }
        readonly HashSet<int> collected = new HashSet<int>();
        public int CollectedCount => collected.Count;
        public double Score => CollectedCount * Pattern.PointsPerCollectible;
        public bool CanCollectThisFrame { get; private set; }
        public BreathLane PlayerLane { get; private set; } = BreathLane.Middle;
        public BreathLane RequiredLane => Pattern.LaneAt(PlayTime);
        public double TransitionRemaining { get; private set; }
        public BreathPhase Phase => Elapsed >= Pattern.Lead + Pattern.Duration ? BreathPhase.Finished
            : Elapsed < Pattern.Lead ? BreathPhase.Preview : BreathPhase.Running;
        public bool IsMatching => Phase == BreathPhase.Running && TransitionRemaining <= 0
            && !Pattern.IsPreparing(PlayTime) && !Pattern.IsGuideTransition(PlayTime) && PlayerLane == RequiredLane;

        public BreathSession(BreathPattern pattern) { Pattern = pattern; }

        public bool TryCollect(int id) => id >= 0 && CanCollectThisFrame && collected.Add(id);

        public void Advance(double deltaSeconds, BreathLane requestedLane) {
            if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            if ((int)requestedLane < -1 || (int)requestedLane > 1)
                throw new ArgumentOutOfRangeException(nameof(requestedLane));
            CanCollectThisFrame = false;
            if (Phase == BreathPhase.Finished) return;
            if (PlayerLane != requestedLane) {
                PlayerLane = requestedLane;
                TransitionRemaining = Pattern.Transition;
            }
            double end = Math.Min(Elapsed + deltaSeconds, Pattern.Lead + Pattern.Duration);
            double scoreStart = Math.Max(Pattern.Lead, Elapsed + TransitionRemaining);
            if (end > scoreStart)
                CorrectSeconds += Pattern.MatchingSeconds(scoreStart - Pattern.Lead, end - Pattern.Lead, PlayerLane);
            TransitionRemaining = Math.Max(0, TransitionRemaining - (end - Elapsed));
            Elapsed = end;
            // Keep the final movement frame eligible, then close on the next Advance.
            CanCollectThisFrame = Phase != BreathPhase.Preview;
        }
    }
}
