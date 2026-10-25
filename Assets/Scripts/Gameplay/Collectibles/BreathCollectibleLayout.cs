using System;
using System.Collections.Generic;

namespace WingSinger {
    public readonly struct PaperRoutePoint {
        public readonly double Time;
        public readonly BreathLane Lane;
        public PaperRoutePoint(double time, BreathLane lane) { Time = time; Lane = lane; }
    }

    public static class BreathCollectibleLayout {
        public static PaperRoutePoint[] Create(BreathPattern pattern, double interval) {
            if (double.IsNaN(interval) || double.IsInfinity(interval) || interval < 0.05)
                throw new ArgumentOutOfRangeException(nameof(interval));
            var points = new List<PaperRoutePoint>();
            Add(points, 0, BreathLane.Middle);
            AddStraight(points, pattern, 0, pattern.Preparation, BreathLane.Middle, interval);
            double start = pattern.Preparation + pattern.GuideTransition;
            while (start <= pattern.Duration) {
                AddStraight(points, pattern, start, start + pattern.Inhale, BreathLane.Upper, interval);
                AddMiddleCrossing(points, pattern, start + pattern.Inhale);
                double lower = start + pattern.Inhale + pattern.GuideTransition;
                AddStraight(points, pattern, lower, lower + pattern.Exhale, BreathLane.Lower, interval);
                AddMiddleCrossing(points, pattern, lower + pattern.Exhale);
                start += pattern.Cycle;
            }
            return points.ToArray();
        }

        static void AddMiddleCrossing(List<PaperRoutePoint> points, BreathPattern p, double start) {
            double middle = start + p.GuideTransition / 2;
            if (middle <= p.Duration && p.GuideTransition > 0) Add(points, middle, BreathLane.Middle);
        }

        static void AddStraight(List<PaperRoutePoint> points, BreathPattern p, double start, double end, BreathLane lane, double interval) {
            if (start > p.Duration) return;
            end = Math.Min(end, p.Duration);
            int gaps = Math.Max(1, (int)Math.Ceiling((end - start) / interval));
            for (int i = 0; i <= gaps; i++) Add(points, start + (end - start) * i / gaps, lane);
        }

        static void Add(List<PaperRoutePoint> points, double time, BreathLane lane) {
            // Adjacent route sections can share an endpoint. It must award only once.
            if (points.Count > 0 && Math.Abs(points[points.Count - 1].Time - time) < 0.000001
                && points[points.Count - 1].Lane == lane) return;
            points.Add(new PaperRoutePoint(time, lane));
        }
    }
}
