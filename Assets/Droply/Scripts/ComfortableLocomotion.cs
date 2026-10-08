using UnityEngine;
using UnityEngine.XR;

namespace Droply.Landscape
{
    public sealed class ComfortableLocomotion : MonoBehaviour
    {
        public const float GlideSpeed = .85f;
        const float DeadZone = .16f;
        const float TeleportHorizontalSpeed = 8f;
        const float TeleportGravity = 9.81f;
        const float TeleportDuration = 1.4f;
        const float TeleportStep = .05f;

        Camera trackedCamera;
        bool teleportWasPressed;

        void Awake() { trackedCamera = GetComponentInChildren<Camera>(); }

        void Update()
        {
            if (trackedCamera == null) trackedCamera = GetComponentInChildren<Camera>();
            var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            if (left.isValid && left.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis))
            {
                Quaternion facing = trackedCamera != null ? trackedCamera.transform.rotation : transform.rotation;
                MoveLocal(axis, facing, Time.deltaTime);
            }

            var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            bool pressed = right.isValid && right.TryGetFeatureValue(CommonUsages.primaryButton, out bool button) && button;
            if (pressed && !teleportWasPressed) TryTeleport();
            teleportWasPressed = pressed;
        }

        public void MoveLocal(Vector2 stick, Quaternion viewRotation, float deltaTime)
        {
            if (stick.magnitude < DeadZone || deltaTime <= 0) return;
            Vector3 forward = Vector3.ProjectOnPlane(viewRotation * Vector3.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) return;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 direction = forward * stick.y + right * stick.x;
            if (direction.sqrMagnitude > 1f) direction.Normalize();
            Vector3 translation = direction * (GlideSpeed * deltaTime);
            transform.position += new Vector3(translation.x, 0, translation.z);
        }

        public bool TeleportTo(Vector3 worldPosition)
        {
            Vector3 current = transform.position;
            transform.position = new Vector3(worldPosition.x, current.y, worldPosition.z);
            return true;
        }

        bool TryTeleport()
        {
            if (trackedCamera == null) return false;
            Vector3 origin = trackedCamera.transform.position;
            Vector3 direction = Vector3.ProjectOnPlane(trackedCamera.transform.forward, Vector3.up).normalized;
            if (direction.sqrMagnitude < .01f) return false;
            Vector3 previous = origin;
            for (float time = TeleportStep; time <= TeleportDuration; time += TeleportStep)
            {
                Vector3 current = origin + direction * (TeleportHorizontalSpeed * time) +
                    Vector3.down * (.5f * TeleportGravity * time * time);
                Vector3 segment = current - previous;
                if (Physics.Raycast(previous, segment.normalized, out RaycastHit hit, segment.magnitude,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.GetComponent<MeshCollider>() == null) return false;
                    return TeleportTo(hit.point);
                }
                previous = current;
            }
            return false;
        }
    }
}
