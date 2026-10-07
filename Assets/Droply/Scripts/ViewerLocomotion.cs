using UnityEngine;
using UnityEngine.XR;

namespace Droply.Landscape
{
    /// <summary>
    /// Walking on the headset. Left thumbstick: walk slowly in the direction the head faces. Right thumbstick left/right: turn in snap steps of 30 degrees,
    /// around the head so the view does not shift. Nothing moves unless a stick is held: no automatic movement, no smooth turning, no head bobbing.
    /// <para>
    /// Sits on the origin of the tracking space (the parent of the tracked camera) and moves that origin; its floor follows the ground under the head, so walking up
    /// the gentle slopes keeps the eye height the person really has. The rules (speed, ramp, soft cushion at the edge and at trunks, snap turns) are in
    /// <see cref="Walker"/>, the area in <see cref="WalkArea"/>; both are plain C# and tested outside Unity (dotnet run --project Tools/CompositionCheck -- selftest).
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(100)]   // after HeadsetPose has placed the head for this frame
    public sealed class ViewerLocomotion : MonoBehaviour
    {
        /// <summary>Time constant (s) with which the floor follows the ground: smooths the facets of the terrain mesh without a noticeable lag on the slopes.</summary>
        const float FloorSmoothing = .12f;

        readonly Walker walker = new Walker();
        Transform head;
        WalkArea area;
        float floor;
        bool floorSet;
        int nextLookup;

        /// <summary>Scenes made before walking existed have the tracked camera rig at the root: give it an origin to move.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AddIfMissing()
        {
            if (FindAnyObjectByType<ViewerLocomotion>() != null) return;
            var pose = FindAnyObjectByType<HeadsetPose>();
            if (pose == null) return;
            Transform origin = pose.transform.parent;
            if (origin == null)
            {
                origin = new GameObject("Tracking space").transform;
                pose.transform.SetParent(origin, false);
            }
            origin.gameObject.AddComponent<ViewerLocomotion>();
        }

        void Update()
        {
            if (!Ready()) return;
            Vector2 move = Stick(XRNode.LeftHand), turn = Stick(XRNode.RightHand);

            float snap = walker.Turn(turn.x);
            if (snap != 0f) transform.RotateAround(head.position, Vector3.up, snap);

            Vector3 h = head.position;
            Vector2 step = walker.Step(area, new Vector2(h.x, h.z), head.eulerAngles.y, move, Time.deltaTime);
            float ground = area.GroundY(new Vector2(h.x + step.x, h.z + step.y));
            if (!floorSet) { floor = ground; floorSet = true; }
            else floor = Mathf.Lerp(floor, ground, 1f - Mathf.Exp(-Mathf.Min(Time.deltaTime, .1f) / FloorSmoothing));
            Vector3 p = transform.position;
            transform.position = new Vector3(p.x + step.x, floor, p.z + step.y);
        }

        /// <summary>Finds the tracked head (a HeadsetPose below this origin) and the walk area of the generated landscape; retries about once a second until both exist.</summary>
        bool Ready()
        {
            if (head != null && area != null) return true;
            if (Time.frameCount < nextLookup) return false;
            nextLookup = Time.frameCount + 60;
            if (head == null)
            {
                var pose = GetComponentInChildren<HeadsetPose>();
                if (pose != null) head = pose.transform;
            }
            if (area == null)
            {
                var generator = FindAnyObjectByType<LandscapeGenerator>();
                if (generator != null) area = generator.WalkArea;
            }
            if (head == null || area == null) return false;
            walker.Halt();
            return true;
        }

        static Vector2 Stick(XRNode hand)
        {
            var device = InputDevices.GetDeviceAtXRNode(hand);
            Vector2 value;
            return device.isValid && device.TryGetFeatureValue(CommonUsages.primary2DAxis, out value) ? value : Vector2.zero;
        }
    }
}
