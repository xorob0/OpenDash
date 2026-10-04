// SlipEstimate.cs: whether the driven wheels are spinning or the wheels are locked, estimated from the
// engine against the car's speed, for the sims that do not say (ADR 0018, amended for this).
//
// iRacing publishes no wheel speeds and no traction-control flag, and SimHub's TCActive is a hard 0
// there. SimHub does estimate slip, but only inside ShakeIt: GameData.FeedbackData carries the wheels'
// slip and is [DoNotExpose], and the estimate for iRacing is computed in ShakeIt's wheel slip effect
// and handed to a shaker, never to a property. So no dashboard and no LED profile can read it, and this
// class computes the same thing where a profile can: the plugin publishes it as OpenDash.WheelSpin,
// OpenDash.WheelLock and OpenDash.TCInferred.
//
// The rule is ShakeIt's "RPM vs Speed" (WheelSlipEffect.GetRpmSpeedSlip in SimHub 9.12.6), kept as it
// is rather than improved on, so that a driver who knows what their shaker does knows what this does:
// the ratio of speed to RPM is constant in a gear while the tyres grip, so a frame in which it moves
// is a frame in which the engine and the road disagreed. Pure: no SimHub types, so the net8.0 tests
// compile it.
using System;

namespace OpenDashPlugin
{
    /// <summary>One car's wheelspin and lock-up, frame by frame.</summary>
    /// <remarks>
    /// <para>
    /// What ShakeIt does, step by step: nothing below 1 km/h or with the engine stopped, on either
    /// frame; nothing for half a second after the gear changes, because a shift moves the ratio by
    /// itself; nothing in neutral. Otherwise the change in speed over RPM between the two frames, times
    /// 4000, is the slip. With the brake past 20 % it is a lock, weighted by how far the brake is past
    /// 55 % towards 90 %; with the throttle past 40 % and the clutch out it is a spin, weighted by how
    /// far the throttle is past 70 % towards 100 %. ShakeIt's weighting is
    /// <c>MathExtensions.Offset(value, from, to, clamp: true)</c>, which is not in the assemblies
    /// OpenDash vendors; it is read here as the clamped 0-to-1 position between the two, which is what
    /// its sibling <c>Map</c> does with the same arguments.
    /// </para>
    /// <para>
    /// ShakeIt turns that number into a vibration. A lamp needs a yes or a no, so a frame whose slip is
    /// over <see cref="Threshold"/> lights it, and it stays lit for <see cref="HoldMs"/> after the last
    /// such frame: a spin is a run of frames some of which fall under the line, and a lamp that follows
    /// each of them flickers rather than says "spinning". The threshold and the hold are OpenDash's,
    /// and they are a first guess that a rig should correct.
    /// </para>
    /// <para>
    /// Like ShakeIt's, the number is per frame, so it is larger at a lower update rate. SimHub runs
    /// both at the same rate, so the two agree whatever that rate is.
    /// </para>
    /// </remarks>
    public sealed class SlipEstimate
    {
        /// <summary>Below this, on either frame, nothing is estimated: a car at rest has no ratio.</summary>
        public const double MinimumSpeedKmh = 1.0;

        /// <summary>How long after the gear changes nothing is estimated.</summary>
        public const long GearChangeQuietMs = 500;

        /// <summary>ShakeIt's scale from a change of ratio to a slip.</summary>
        public const double Scale = 4000.0;

        /// <summary>Past this brake, a slip is a lock.</summary>
        public const double BrakeGate = 20.0;

        /// <summary>The brake a lock starts to count from, and the brake it counts in full at.</summary>
        public const double BrakeFrom = 55.0, BrakeFull = 90.0;

        /// <summary>Past this throttle, with the clutch out, a slip is a spin.</summary>
        public const double ThrottleGate = 40.0;

        /// <summary>The throttle a spin starts to count from, and the throttle it counts in full at.</summary>
        public const double ThrottleFrom = 70.0, ThrottleFull = 100.0;

        /// <summary>A clutch pressed further than this is a launch or a shift, not a spin.</summary>
        public const double ClutchOut = 5.0;

        /// <summary>The slip a lamp lights at.</summary>
        public const double Threshold = 0.2;

        /// <summary>How long a lamp stays lit after the last frame over <see cref="Threshold"/>.</summary>
        public const long HoldMs = 200;

        private bool hasLast;
        private double lastSpeed, lastRpm;
        private string lastGear;
        private long? gearChangedAt;
        private long? spinUntil, lockUntil;

        /// <summary>The driven wheels are spinning, or were a moment ago.</summary>
        public bool Spin { get; private set; }

        /// <summary>The wheels are locked under braking, or were a moment ago.</summary>
        public bool Lock { get; private set; }

        /// <summary>
        /// A spin on a car whose traction control is switched on, which is when the system is cutting in.
        /// </summary>
        /// <remarks>
        /// Inferred and not measured: no sim OpenDash runs on says when its traction control acts, so this
        /// says the wheels spun while the dial was above zero. On a car with no traction control, or with
        /// it off, it stays false and <see cref="Spin"/> alone says what happened.
        /// </remarks>
        public bool TcInferred { get; private set; }

        /// <summary>The last frame's slip, before the threshold: what ShakeIt would have shaken.</summary>
        public double Slip { get; private set; }

        /// <summary>Forgets the last frame and turns everything off, as a car leaving the track should.</summary>
        public void Reset()
        {
            hasLast = false;
            lastGear = null;
            gearChangedAt = null;
            spinUntil = lockUntil = null;
            Spin = Lock = TcInferred = false;
            Slip = 0;
        }

        /// <summary>One frame.</summary>
        /// <param name="running">Whether a game is running and has a car on the road. False resets.</param>
        /// <param name="speedKmh">The car's speed.</param>
        /// <param name="rpm">The engine's.</param>
        /// <param name="gear">SimHub's gear: "N", "R", or a number.</param>
        /// <param name="throttle">0 to 100.</param>
        /// <param name="brake">0 to 100.</param>
        /// <param name="clutch">0 to 100, pressed.</param>
        /// <param name="tcLevel">SimHub's TCLevel: above zero when the car's traction control is on.</param>
        /// <param name="nowMs">A monotonic clock.</param>
        public void Update(bool running, double speedKmh, double rpm, string gear, double throttle, double brake, double clutch, double tcLevel, long nowMs)
        {
            if (!running)
            {
                Reset();
                return;
            }

            Slip = 0;
            var spinning = false;
            var locking = false;
            if (hasLast && speedKmh > MinimumSpeedKmh && rpm > 0 && lastSpeed > MinimumSpeedKmh && lastRpm > 0)
            {
                if (!string.Equals(lastGear, gear, StringComparison.Ordinal)) gearChangedAt = nowMs;
                var shifting = gearChangedAt.HasValue && nowMs - gearChangedAt.Value < GearChangeQuietMs;
                if (!shifting && !string.Equals(gear, "N", StringComparison.Ordinal))
                {
                    var change = Math.Abs(lastSpeed / lastRpm - speedKmh / rpm);
                    if (brake > BrakeGate)
                    {
                        Slip = change * Scale * Offset(brake, BrakeFrom, BrakeFull);
                        locking = Slip > Threshold;
                    }
                    else if (throttle > ThrottleGate && clutch < ClutchOut)
                    {
                        Slip = change * Scale * Offset(throttle, ThrottleFrom, ThrottleFull);
                        spinning = Slip > Threshold;
                    }
                }
            }

            if (spinning) spinUntil = nowMs + HoldMs;
            if (locking) lockUntil = nowMs + HoldMs;
            Spin = spinUntil.HasValue && nowMs < spinUntil.Value;
            Lock = lockUntil.HasValue && nowMs < lockUntil.Value;
            TcInferred = Spin && tcLevel > 0;

            hasLast = true;
            lastSpeed = speedKmh;
            lastRpm = rpm;
            lastGear = gear;
        }

        /// <summary>Where <paramref name="value"/> sits between <paramref name="from"/> and
        /// <paramref name="to"/>, as 0 to 1.</summary>
        public static double Offset(double value, double from, double to)
        {
            if (to <= from) return value >= to ? 1 : 0;
            var position = (value - from) / (to - from);
            return position < 0 ? 0 : position > 1 ? 1 : position;
        }
    }
}
