using System;
using UnityEngine;
using VirtualStrikeZone.Core;
using VirtualStrikeZone.Rendering;
using TMPro;

namespace VirtualStrikeZone.Interaction
{
    public enum CalibrationStep
    {
        Idle,
        AimingKnee,
        AimingChest,
        Complete
    }

    /// <summary>
    /// Implements iOS Measure-app style point-to-point measurement to calibrate
    /// the virtual strike zone heights directly against any batter's physical body.
    /// Point 1: Hollow beneath the kneecap (Zone Bottom).
    /// Point 2: Midpoint between shoulders and belt (Zone Top).
    /// </summary>
    public class BatterCalibrator : MonoBehaviour
    {
        [Header("Target References")]
        [SerializeField] private StrikeZoneAnchorController anchorController;
        [SerializeField] private StrikeZoneMeshBuilder meshBuilder;

        [Header("Measure Tape Visuals")]
        [SerializeField] private LineRenderer tapeLineRenderer;
        [SerializeField] private GameObject bottomPointMarker;
        [SerializeField] private GameObject topPointMarker;
        [SerializeField] private TextMeshPro tapeDistanceText;

        [Header("Current State")]
        [SerializeField] private CalibrationStep currentStep = CalibrationStep.Idle;

        private Vector3 bottomPoint;
        private Vector3 topPoint;
        private float calibratedBottomInches;
        private float calibratedTopInches;

        public CalibrationStep CurrentStep => currentStep;
        public event Action<CalibrationStep> OnCalibrationStepChanged;

        private void Start()
        {
            if (tapeLineRenderer != null)
                tapeLineRenderer.enabled = false;
            if (bottomPointMarker != null)
                bottomPointMarker.SetActive(false);
            if (topPointMarker != null)
                topPointMarker.SetActive(false);
        }

        private void Update()
        {
            if (currentStep == CalibrationStep.AimingChest && anchorController != null)
            {
                // Live tape line tracking between bottom point and current aiming crosshair
                Vector3 currentAimPos = anchorController.CurrentRaycastPose.position;
                UpdateTapeLine(bottomPoint, currentAimPos);
            }
        }

        /// <summary>
        /// Begins the 2-point measurement calibration workflow.
        /// </summary>
        public void StartCalibration()
        {
            currentStep = CalibrationStep.AimingKnee;
            if (tapeLineRenderer != null) tapeLineRenderer.enabled = false;
            if (bottomPointMarker != null) bottomPointMarker.SetActive(false);
            if (topPointMarker != null) topPointMarker.SetActive(false);

            HapticManager.TriggerSelectionTick();
            OnCalibrationStepChanged?.Invoke(currentStep);
        }

        /// <summary>
        /// Handles the action button press during calibration (Measure App + button).
        /// </summary>
        public void RecordCurrentPoint()
        {
            if (anchorController == null) return;
            Vector3 aimPos = anchorController.CurrentRaycastPose.position;

            if (currentStep == CalibrationStep.AimingKnee)
            {
                bottomPoint = aimPos;
                if (bottomPointMarker != null)
                {
                    bottomPointMarker.transform.position = bottomPoint;
                    bottomPointMarker.SetActive(true);
                }

                // Reference strike zone ground plane height
                float groundY = anchorController.CurrentAnchor != null
                    ? anchorController.CurrentAnchor.transform.position.y
                    : bottomPoint.y;

                float heightAboveGround = Mathf.Max(0.1f, bottomPoint.y - groundY);
                calibratedBottomInches = heightAboveGround * StrikeZoneDimensions.METERS_TO_INCHES;

                currentStep = CalibrationStep.AimingChest;
                if (tapeLineRenderer != null) tapeLineRenderer.enabled = true;

                HapticManager.TriggerPlacementImpact();
                OnCalibrationStepChanged?.Invoke(currentStep);
            }
            else if (currentStep == CalibrationStep.AimingChest)
            {
                topPoint = aimPos;
                if (topPointMarker != null)
                {
                    topPointMarker.transform.position = topPoint;
                    topPointMarker.SetActive(true);
                }

                float groundY = anchorController.CurrentAnchor != null
                    ? anchorController.CurrentAnchor.transform.position.y
                    : bottomPoint.y;

                float heightAboveGround = Mathf.Max(bottomPoint.y - groundY + 0.2f, topPoint.y - groundY);
                calibratedTopInches = heightAboveGround * StrikeZoneDimensions.METERS_TO_INCHES;

                // Apply calibrated heights to strike zone
                if (meshBuilder != null && meshBuilder.Dimensions != null)
                {
                    meshBuilder.Dimensions.SetCustomHeights(calibratedBottomInches, calibratedTopInches);
                }

                currentStep = CalibrationStep.Complete;
                HapticManager.TriggerPlacementImpact();
                OnCalibrationStepChanged?.Invoke(currentStep);
            }
        }

        public void CancelCalibration()
        {
            currentStep = CalibrationStep.Idle;
            if (tapeLineRenderer != null) tapeLineRenderer.enabled = false;
            if (bottomPointMarker != null) bottomPointMarker.SetActive(false);
            if (topPointMarker != null) topPointMarker.SetActive(false);

            OnCalibrationStepChanged?.Invoke(currentStep);
        }

        private void UpdateTapeLine(Vector3 start, Vector3 end)
        {
            if (tapeLineRenderer == null) return;

            tapeLineRenderer.positionCount = 2;
            tapeLineRenderer.SetPosition(0, start);
            tapeLineRenderer.SetPosition(1, end);

            if (tapeDistanceText != null)
            {
                float distMeters = Vector3.Distance(start, end);
                float distInches = distMeters * StrikeZoneDimensions.METERS_TO_INCHES;
                tapeDistanceText.text = $"{distInches:F1}\"";
                tapeDistanceText.transform.position = (start + end) * 0.5f + Vector3.up * 0.05f;
            }
        }
    }
}
