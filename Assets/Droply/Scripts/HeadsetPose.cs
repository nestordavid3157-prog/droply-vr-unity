using UnityEngine;
using UnityEngine.XR;

namespace Droply.Landscape
{
    public sealed class HeadsetPose : MonoBehaviour
    {
        void Update()
        {
            var device = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (!device.isValid) return;
            if (device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position)) transform.localPosition = position;
            if (device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation)) transform.localRotation = rotation;
        }
    }
}
