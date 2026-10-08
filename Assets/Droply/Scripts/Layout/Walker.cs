using UnityEngine;

namespace Droply.Landscape
{
    /// <summary>
    /// The viewer's artificial locomotion as plain maths (the headset side, which reads the thumbsticks and moves the tracking space, is <c>ViewerLocomotion</c>).
    /// Comfort rules: slow walking only while the stick is held, in the direction the head faces; a short ramp instead of a jolt when starting and stopping;
    /// a soft cushion before the edge of the walk area and before trunks (the outward part of the speed fades to zero, the part along the edge stays, so one glides
    /// along it instead of hitting a wall); snap turns instead of smooth turning. No automatic movement, no head bobbing, nothing moves the view on its own.
    /// </summary>
    public sealed class Walker
    {
        /// <summary>Walking speed at full stick, m/s: a slow walk.</summary>
        public const float MaxSpeed = 1.2f;
        /// <summary>From standing to full speed (and back) in this time: no jolt, and no long drawn-out acceleration either.</summary>
        public const float RampSeconds = .3f;
        public const float MaxAcceleration = MaxSpeed / RampSeconds;
        /// <summary>Metres before the edge (or a trunk) within which the speed towards it fades to zero.</summary>
        public const float Cushion = 1.2f;
        /// <summary>The cushion brings the speed towards the edge to zero this far before it, so the hard limit behind it (<see cref="Contained"/>) never has to stop anyone abruptly.</summary>
        public const float EdgeMargin = .15f;
        public const float DeadZone = .15f;
        public const float SnapDegrees = 30f;
        const float TurnPress = .7f, TurnRelease = .35f;

        Vector2 velocity;
        bool turnArmed = true;

        public Vector2 Velocity { get { return velocity; } }

        /// <summary>
        /// One frame of walking. <paramref name="head"/> is the head's position on the ground (x, z), <paramref name="yawDegrees"/> where the head faces
        /// (0 = +z, 90 = +x), <paramref name="stick"/> the move stick (x right, y forward). Returns how far to move the tracking space (x, z).
        /// </summary>
        public Vector2 Step(WalkArea area, Vector2 head, float yawDegrees, Vector2 stick, float dt)
        {
            dt = Mathf.Clamp(dt, 0f, .1f);   // a long frame (loading) must not become a long jump
            Vector2 target = Target(stick, yawDegrees);
            if (area != null) target = Cushioned(area, head, target);
            Vector2 change = target - velocity;
            float m = change.magnitude, limit = MaxAcceleration * dt;
            velocity = m > limit ? velocity + change * (limit / m) : target;
            Vector2 step = velocity * dt;
            return area != null ? Contained(area, head, step) : step;
        }

        /// <summary>The velocity the stick asks for: proportional to the deflection beyond the dead zone, turned into the direction the head faces.</summary>
        public static Vector2 Target(Vector2 stick, float yawDegrees)
        {
            float m = stick.magnitude;
            if (m <= DeadZone) return new Vector2(0f, 0f);
            float speed = MaxSpeed * Mathf.Clamp01((m - DeadZone) / (1f - DeadZone));
            float sx = stick.x / m, sy = stick.y / m, yaw = yawDegrees * Mathf.PI / 180f;
            float s = Mathf.Sin(yaw), c = Mathf.Cos(yaw);
            // forward = (sin, cos), right = (cos, -sin)
            return new Vector2(c * sx + s * sy, -s * sx + c * sy) * speed;
        }

        /// <summary>Within <see cref="Cushion"/> of the edge the speed towards it is limited, down to zero just before the edge; the speed along the edge stays.</summary>
        static Vector2 Cushioned(WalkArea area, Vector2 head, Vector2 target)
        {
            float d = area.Distance(head);
            if (d < -(Cushion + EdgeMargin)) return target;
            Vector2 n = area.Normal(head);
            float outward = Vector2.Dot(target, n), allowed = MaxSpeed * Mathf.Clamp01((-d - EdgeMargin) / Cushion);
            return outward > allowed ? target - n * (outward - allowed) : target;
        }

        /// <summary>Safety net behind the cushion: a step may not lead further out of the area (or deeper into an obstacle) than where it starts.</summary>
        Vector2 Contained(WalkArea area, Vector2 head, Vector2 step)
        {
            float from = Mathf.Max(area.Distance(head), 0f);
            Vector2 next = head + step;
            if (area.Distance(next) <= from) return step;
            Vector2 n = area.Normal(next);
            float outward = Vector2.Dot(step, n);
            if (outward > 0f)
            {
                Vector2 slide = step - n * outward;
                if (area.Distance(head + slide) <= from) { velocity -= n * Mathf.Max(0f, Vector2.Dot(velocity, n)); return slide; }
            }
            velocity = new Vector2(0f, 0f);
            return new Vector2(0f, 0f);
        }

        /// <summary>
        /// Snap turning from the turn stick's x axis: one turn of <see cref="SnapDegrees"/> per push (positive = to the right), and the stick has to come back
        /// towards the middle before the next one, so holding it never spins the view.
        /// </summary>
        public float Turn(float stickX)
        {
            float a = Mathf.Abs(stickX);
            if (turnArmed && a >= TurnPress) { turnArmed = false; return stickX > 0f ? SnapDegrees : -SnapDegrees; }
            if (!turnArmed && a <= TurnRelease) turnArmed = true;
            return 0f;
        }

        /// <summary>Stops at once (for example after the tracking space was reset).</summary>
        public void Halt() { velocity = new Vector2(0f, 0f); }
    }
}
