using UnityEngine;
using UnityEngine.XR;

namespace Droply.Landscape
{
    /// <summary>
    /// Follows the tracked head, relative to its parent (the tracking space, which <see cref="ViewerLocomotion"/> moves). The rig imposes no height: the eye height is
    /// whatever the headset reports. To make that the real height above the floor (and not zero at eye level), the first valid frame asks the tracking system for the
    /// floor origin; the terrain is level at the start. The pose is read in Update and again just before rendering, so the picture uses the latest prediction.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class HeadsetPose : MonoBehaviour
    {
        bool originRequested;

        void OnEnable() { Application.onBeforeRender += Track; }
        void OnDisable() { Application.onBeforeRender -= Track; }
        void Update() { Track(); }

        void Track()
        {
            var device = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (!device.isValid) return;
            if (!originRequested && device.subsystem != null)
            {
                originRequested = true;
                if (!device.subsystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor))
                    Debug.LogWarning("The floor tracking origin is not available: the eye height may be reported relative to the headset instead of the floor.");
            }
            if (device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position)) transform.localPosition = position;
            if (device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation)) transform.localRotation = rotation;
        }
    }
}
