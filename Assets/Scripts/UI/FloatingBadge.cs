using UnityEngine;

namespace VirtualStrikeZone.UI
{
    /// <summary>
    /// Billboarding behavior for dimension badges and measurement tags,
    /// ensuring they always face the AR camera smoothly.
    /// </summary>
    public class FloatingBadge : MonoBehaviour
    {
        [SerializeField] private bool lockYRotation = false;

        private Transform mainCameraTransform;

        private void Start()
        {
            if (Camera.main != null)
            {
                mainCameraTransform = Camera.main.transform;
            }
        }

        private void LateUpdate()
        {
            if (mainCameraTransform == null)
            {
                if (Camera.main != null) mainCameraTransform = Camera.main.transform;
                else return;
            }

            Vector3 lookDirection = transform.position - mainCameraTransform.position;
            if (lockYRotation)
            {
                lookDirection.y = 0;
            }

            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(lookDirection);
            }
        }
    }
}
