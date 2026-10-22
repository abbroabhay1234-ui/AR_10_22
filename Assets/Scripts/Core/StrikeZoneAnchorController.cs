using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using VirtualStrikeZone.Interaction;

namespace VirtualStrikeZone.Core
{
    public enum PlacementState
    {
        ScanningForPlanes,
        ReadyToPlace,
        Placed,
        Locked
    }

    /// <summary>
    /// Manages the rock-solid world placement and AR anchoring of the virtual strike zone.
    /// Utilizes ARFoundation's ARAnchorManager so that the strike zone is anchored to the native
    /// ARKit / ARCore coordinate frame, preventing it from drifting or moving as the user moves around.
    /// </summary>
    [RequireComponent(typeof(ARRaycastManager))]
    public class StrikeZoneAnchorController : MonoBehaviour
    {
        [Header("AR Foundation Components")]
        [SerializeField] private ARRaycastManager raycastManager;
        [SerializeField] private ARAnchorManager anchorManager;
        [SerializeField] private ARPlaneManager planeManager;

        [Header("Target Prefab / Hierarchy")]
        [Tooltip("The Virtual Strike Zone root GameObject to anchor.")]
        [SerializeField] private GameObject strikeZoneRoot;

        [Header("Reticle & Guides")]
        [SerializeField] private GameObject reticleObject;

        [Header("State")]
        [SerializeField] private PlacementState currentState = PlacementState.ScanningForPlanes;

        private ARAnchor currentAnchor;
        private Pose currentRaycastPose;
        private ARPlane currentHitPlane;
        private bool hasValidHit = false;
        private static readonly List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();

        public PlacementState CurrentState => currentState;
        public bool HasValidHit => hasValidHit;
        public Pose CurrentRaycastPose => currentRaycastPose;
        public ARAnchor CurrentAnchor => currentAnchor;

        public event Action<PlacementState> OnStateChanged;
        public event Action<Pose> OnZonePlaced;
        public event Action OnZoneCleared;

        private void Awake()
        {
            if (raycastManager == null)
                raycastManager = GetComponent<ARRaycastManager>();
            if (anchorManager == null)
                anchorManager = GetComponent<ARAnchorManager>();
            if (planeManager == null)
                planeManager = GetComponent<ARPlaneManager>();
        }

        private void Start()
        {
            SetState(PlacementState.ScanningForPlanes);
            if (strikeZoneRoot != null)
            {
                strikeZoneRoot.SetActive(false);
            }
        }

        private void Update()
        {
            if (currentState == PlacementState.Locked)
            {
                if (reticleObject != null && reticleObject.activeSelf)
                    reticleObject.SetActive(false);
                return;
            }

            PerformRaycast();
        }

        private void PerformRaycast()
        {
            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            // Raycast against horizontal planes (surfaces suitable for home plate)
            if (raycastManager.Raycast(screenCenter, s_Hits, TrackableType.PlaneWithinPolygon | TrackableType.Planes))
            {
                hasValidHit = true;
                ARRaycastHit hit = s_Hits[0];
                currentRaycastPose = hit.pose;
                currentHitPlane = planeManager.GetPlane(hit.trackableId);

                if (currentState == PlacementState.ScanningForPlanes)
                {
                    SetState(PlacementState.ReadyToPlace);
                }

                if (reticleObject != null)
                {
                    reticleObject.SetActive(true);
                    reticleObject.transform.position = currentRaycastPose.position;
                    // Keep reticle parallel to ground plane
                    reticleObject.transform.rotation = currentRaycastPose.rotation;
                }
            }
            else
            {
                hasValidHit = false;
                if (currentState == PlacementState.ReadyToPlace)
                {
                    SetState(PlacementState.ScanningForPlanes);
                }

                if (reticleObject != null && currentState != PlacementState.Placed)
                {
                    reticleObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Places the strike zone at the current raycast location and locks it into an ARAnchor
        /// so it remains permanently fixed in real-world space.
        /// </summary>
        public bool PlaceStrikeZone()
        {
            if (!hasValidHit || strikeZoneRoot == null)
                return false;

            // Remove any previous anchor to prevent ghost anchors
            ClearExistingAnchor();

            Pose targetPose = currentRaycastPose;

            // Orient the strike zone so the front face points towards the camera (where the pitcher is)
            Camera arCam = Camera.main;
            if (arCam != null)
            {
                Vector3 forwardToCam = arCam.transform.position - targetPose.position;
                forwardToCam.y = 0; // Keep horizontal
                if (forwardToCam.sqrMagnitude > 0.001f)
                {
                    targetPose.rotation = Quaternion.LookRotation(forwardToCam.normalized, Vector3.up);
                }
            }

            // Create native ARKit / ARCore Anchor for zero drift
            if (anchorManager != null && currentHitPlane != null)
            {
                // Attach directly to the ARPlane trackable
                currentAnchor = anchorManager.AttachAnchor(currentHitPlane, targetPose);
            }

            // Fallback: Create standalone ARAnchor GameObject if plane attachment is unavailable
            if (currentAnchor == null)
            {
                GameObject anchorGO = new GameObject("StrikeZone_ARAnchor");
                anchorGO.transform.position = targetPose.position;
                anchorGO.transform.rotation = targetPose.rotation;
                currentAnchor = anchorGO.AddComponent<ARAnchor>();
            }

            // Parent the strike zone to the anchor so it updates precisely with AR subsystem loop closures
            strikeZoneRoot.transform.SetParent(currentAnchor.transform, false);
            strikeZoneRoot.transform.localPosition = Vector3.zero;
            strikeZoneRoot.transform.localRotation = Quaternion.identity;
            strikeZoneRoot.SetActive(true);

            HapticManager.TriggerPlacementImpact();
            SetState(PlacementState.Placed);
            OnZonePlaced?.Invoke(targetPose);

            return true;
        }

        /// <summary>
        /// Locks the strike zone position permanently. Hides reticles and disables further plane adjustment.
        /// </summary>
        public void ToggleLock(bool lockZone)
        {
            if (currentState == PlacementState.ScanningForPlanes && lockZone)
                return;

            SetState(lockZone ? PlacementState.Locked : PlacementState.Placed);

            if (reticleObject != null)
            {
                reticleObject.SetActive(!lockZone);
            }

            HapticManager.TriggerSelectionTick();
        }

        /// <summary>
        /// Rotates the strike zone around its vertical axis to fine-tune facing towards the pitcher's mound.
        /// </summary>
        public void RotateZone(float angleDelta)
        {
            if (strikeZoneRoot != null && currentState != PlacementState.Locked)
            {
                strikeZoneRoot.transform.Rotate(Vector3.up, angleDelta, Space.World);
            }
        }

        /// <summary>
        /// Automatically re-aligns the strike zone to face directly towards the user's camera.
        /// </summary>
        public void AlignTowardsCamera()
        {
            if (strikeZoneRoot == null || Camera.main == null) return;

            Vector3 direction = Camera.main.transform.position - strikeZoneRoot.transform.position;
            direction.y = 0;
            if (direction.sqrMagnitude > 0.01f)
            {
                strikeZoneRoot.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                HapticManager.TriggerSelectionTick();
            }
        }

        /// <summary>
        /// Clears the placed strike zone and resumes scanning for a new location.
        /// </summary>
        public void ClearStrikeZone()
        {
            ClearExistingAnchor();

            if (strikeZoneRoot != null)
            {
                strikeZoneRoot.transform.SetParent(null);
                strikeZoneRoot.SetActive(false);
            }

            SetState(PlacementState.ScanningForPlanes);
            OnZoneCleared?.Invoke();
            HapticManager.TriggerSelectionTick();
        }

        private void ClearExistingAnchor()
        {
            if (currentAnchor != null)
            {
                Destroy(currentAnchor.gameObject);
                currentAnchor = null;
            }
        }

        private void SetState(PlacementState newState)
        {
            if (currentState != newState)
            {
                currentState = newState;
                OnStateChanged?.Invoke(currentState);
            }
        }
    }
}
