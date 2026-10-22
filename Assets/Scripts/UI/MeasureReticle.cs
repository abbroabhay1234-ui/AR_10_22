using UnityEngine;
using TMPro;

namespace VirtualStrikeZone.UI
{
    /// <summary>
    /// Implements the iOS Measure-app style circular reticle:
    /// - Smooth position/rotation interpolation to the ground surface.
    /// - Subtle pulsing animation when locked on a valid plane.
    /// - Real-time distance measurement readout from camera to target point.
    /// </summary>
    public class MeasureReticle : MonoBehaviour
    {
        [Header("Visual Elements")]
        [SerializeField] private Transform outerRing;
        [SerializeField] private Transform centerDot;
        [SerializeField] private TextMeshPro distanceLabel;

        [Header("Animation Settings")]
        [SerializeField] private float smoothSpeed = 15f;
        [SerializeField] private float pulseFrequency = 2.5f;
        [SerializeField] private float pulseScaleDelta = 0.08f;

        private Vector3 targetPosition;
        private Quaternion targetRotation;
        private Vector3 initialRingScale;
        private Camera arCamera;

        private void Awake()
        {
            if (outerRing != null)
                initialRingScale = outerRing.localScale;
            else
                initialRingScale = Vector3.one * 0.15f;
        }

        private void Start()
        {
            arCamera = Camera.main;
        }

        private void Update()
        {
            // Smoothly move towards the raycast hit pose
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * smoothSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * smoothSpeed);

            // Subtle Apple Measure breathing pulse effect
            if (outerRing != null)
            {
                float pulse = 1.0f + Mathf.Sin(Time.time * pulseFrequency * Mathf.PI) * pulseScaleDelta;
                outerRing.localScale = initialRingScale * pulse;
            }

            // Update live distance readout to user
            UpdateDistanceReadout();
        }

        public void SetTargetPose(Pose pose)
        {
            targetPosition = pose.position;
            targetRotation = pose.rotation;
        }

        private void UpdateDistanceReadout()
        {
            if (distanceLabel == null) return;

            if (arCamera == null)
            {
                arCamera = Camera.main;
                if (arCamera == null) return;
            }

            float distMeters = Vector3.Distance(arCamera.transform.position, transform.position);
            float distFeet = distMeters * 3.28084f;

            if (distFeet < 3.0f)
            {
                float distInches = distMeters * 39.3701f;
                distanceLabel.text = $"{distInches:F0}\"";
            }
            else
            {
                int feet = Mathf.FloorToInt(distFeet);
                float remInches = (distFeet - feet) * 12f;
                distanceLabel.text = $"{feet}' {remInches:F0}\"";
            }
        }
    }
}
