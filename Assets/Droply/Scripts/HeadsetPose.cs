using UnityEngine;
using UnityEngine.XR;

namespace Droply.Landscape
{
    /// <summary>
    /// Follows the tracked head. The rig imposes no height: the eye height is whatever the headset reports. To make that the real height above the floor
    /// (and not zero at eye level), the first valid frame asks the tracking system for the floor origin; the terrain is level at the origin.
    /// </summary>
    public sealed class HeadsetPose : MonoBehaviour
    {
        bool originRequested;

        void Update()
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
