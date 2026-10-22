using System;
using System.Collections;
using UnityEngine;
using VirtualStrikeZone.Core;
using VirtualStrikeZone.Rendering;
using TMPro;

namespace VirtualStrikeZone.Interaction
{
    public struct PitchResult
    {
        public bool isStrike;
        public int statcastZone; // 1-9 for strikes, 0 for ball
        public Vector3 localImpactPoint;
        public float pitchSpeedMph;
        public string calloutText;
    }

    /// <summary>
    /// Simulates realistic baseball pitches towards the virtual strike zone.
    /// Provides real-time collision detection against the 3D strike zone prism,
    /// identifies the exact Statcast sub-zone (1-9), and produces MLB-style callouts.
    /// </summary>
    public class PitchSimulator : MonoBehaviour
    {
        [Header("Target References")]
        [SerializeField] private StrikeZoneAnchorController anchorController;
        [SerializeField] private StrikeZoneMeshBuilder meshBuilder;

        [Header("Pitch Settings")]
        [Range(40f, 105f)]
        [SerializeField] private float pitchSpeedMph = 85.0f;
        [SerializeField] private bool throwFromCamera = true;

        [Header("Visual Feedback")]
        [SerializeField] private GameObject pitchMarkerPrefab;
        [SerializeField] private TextMeshProUGUI calloutTextUI;

        private GameObject activeBall;
        public event Action<PitchResult> OnPitchResolved;

        /// <summary>
        /// Fires a test baseball pitch towards the virtual strike zone.
        /// </summary>
        public void ThrowTestPitch()
        {
            if (meshBuilder == null || anchorController == null || anchorController.CurrentState == PlacementState.ScanningForPlanes)
            {
                Debug.LogWarning("[PitchSimulator] Strike zone is not yet anchored in the real world.");
                return;
            }

            StartCoroutine(PitchRoutine());
        }

        public void SetPitchSpeed(float speedMph)
        {
            pitchSpeedMph = Mathf.Clamp(speedMph, 40f, 105f);
        }

        private IEnumerator PitchRoutine()
        {
            StrikeZoneDimensions dims = meshBuilder.Dimensions;
            Transform zoneRoot = meshBuilder.transform;

            // Target is the center of the front face of the strike zone
            Vector3 targetCenter = zoneRoot.TransformPoint(new Vector3(
                UnityEngine.Random.Range(-dims.WidthMeters * 0.45f, dims.WidthMeters * 0.45f),
                UnityEngine.Random.Range(dims.BottomHeightMeters, dims.TopHeightMeters),
                0f
            ));

            Vector3 startPos;
            if (throwFromCamera && Camera.main != null)
            {
                startPos = Camera.main.transform.position + Camera.main.transform.forward * 0.3f;
            }
            else
            {
                // Official pitcher's mound: 60 ft 6 in (18.44m) away from plate along zone forward
                startPos = zoneRoot.position + zoneRoot.forward * 18.44f + Vector3.up * 1.8f;
            }

            // Create temporary baseball visual
            if (activeBall != null) Destroy(activeBall);

            activeBall = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            float ballDiameterMeters = StrikeZoneDimensions.BASEBALL_DIAMETER_INCHES * StrikeZoneDimensions.INCHES_TO_METERS;
            activeBall.transform.localScale = Vector3.one * ballDiameterMeters;
            activeBall.name = "AR_Baseball";

            // Add simple white baseball material with red seams or bright trail
            Renderer ballRenderer = activeBall.GetComponent<Renderer>();
            ballRenderer.material = new Material(Shader.Find("Standard"));
            ballRenderer.material.color = Color.white;

            TrailRenderer trail = activeBall.AddComponent<TrailRenderer>();
            trail.time = 0.4f;
            trail.startWidth = ballDiameterMeters * 0.6f;
            trail.endWidth = 0.005f;
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.startColor = new Color(1f, 0.3f, 0.1f, 0.8f);
            trail.endColor = new Color(1f, 0.8f, 0.2f, 0f);

            // Calculate flight parameters
            float speedMetersPerSec = pitchSpeedMph * 0.44704f;
            float totalDistance = Vector3.Distance(startPos, targetCenter);
            float flightDuration = totalDistance / speedMetersPerSec;

            float elapsed = 0f;
            Vector3 lastPos = startPos;
            bool hitResolved = false;

            while (elapsed < flightDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / flightDuration);

                // Parabolic trajectory (gravity drop ~ 0.5 * g * t^2)
                Vector3 currentPos = Vector3.Lerp(startPos, targetCenter, t);
                float gravityDrop = 0.5f * 9.81f * Mathf.Pow(t * flightDuration, 2f) * (1f - t);
                currentPos.y -= gravityDrop * 0.3f;

                activeBall.transform.position = currentPos;

                // Check collision plane crossing
                Vector3 localPos = zoneRoot.InverseTransformPoint(currentPos);
                Vector3 prevLocalPos = zoneRoot.InverseTransformPoint(lastPos);

                // Crossing front strike zone plane (Z crossing 0 from positive to negative)
                if (prevLocalPos.z >= 0f && localPos.z <= 0f && !hitResolved)
                {
                    hitResolved = true;
                    ResolveImpact(localPos, dims, zoneRoot);
                }

                lastPos = currentPos;
                yield return null;
            }

            yield return new WaitForSeconds(1.5f);
            if (activeBall != null) Destroy(activeBall);
        }

        private void ResolveImpact(Vector3 localHit, StrikeZoneDimensions dims, Transform zoneRoot)
        {
            float ballRadius = (StrikeZoneDimensions.BASEBALL_DIAMETER_INCHES * 0.5f) * StrikeZoneDimensions.INCHES_TO_METERS;
            float halfWidthWithBall = (dims.WidthMeters * 0.5f) + ballRadius;
            float bottomWithBall = dims.BottomHeightMeters - ballRadius;
            float topWithBall = dims.TopHeightMeters + ballRadius;

            bool isStrike = (Mathf.Abs(localHit.x) <= halfWidthWithBall) &&
                            (localHit.y >= bottomWithBall && localHit.y <= topWithBall);

            int statcastZone = dims.GetStatcastZone(localHit);

            string callout;
            if (isStrike)
            {
                callout = statcastZone > 0 ? $"STRIKE! (Zone {statcastZone})" : "STRIKE! (Edge)";
                HapticManager.TriggerStrikeCallout();
            }
            else
            {
                callout = "BALL";
                HapticManager.TriggerSelectionTick();
            }

            PitchResult result = new PitchResult
            {
                isStrike = isStrike,
                statcastZone = statcastZone,
                localImpactPoint = localHit,
                pitchSpeedMph = pitchSpeedMph,
                calloutText = $"{callout} - {pitchSpeedMph:F0} MPH"
            };

            // Spawn visual impact marker on the strike zone
            SpawnImpactMarker(localHit, isStrike, zoneRoot);

            if (calloutTextUI != null)
            {
                calloutTextUI.text = result.calloutText;
                calloutTextUI.color = isStrike ? new Color(0.2f, 1f, 0.4f) : new Color(1f, 0.3f, 0.3f);
            }

            OnPitchResolved?.Invoke(result);
        }

        private void SpawnImpactMarker(Vector3 localHit, bool isStrike, Transform zoneRoot)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.transform.SetParent(zoneRoot, false);
            marker.transform.localPosition = new Vector3(localHit.x, localHit.y, 0f);
            float markerSize = StrikeZoneDimensions.BASEBALL_DIAMETER_INCHES * StrikeZoneDimensions.INCHES_TO_METERS * 0.8f;
            marker.transform.localScale = Vector3.one * markerSize;

            Renderer r = marker.GetComponent<Renderer>();
            r.material = new Material(Shader.Find("Standard"));
            r.material.color = isStrike ? new Color(0.1f, 1f, 0.3f, 0.9f) : new Color(1f, 0.2f, 0.2f, 0.9f);

            Destroy(marker, 5.0f); // Auto clean up after 5 seconds
        }
    }
}
