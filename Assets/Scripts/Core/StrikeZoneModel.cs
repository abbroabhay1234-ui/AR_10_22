using System;
using UnityEngine;

namespace VirtualStrikeZone.Core
{
    public enum StrikeZonePreset
    {
        MLB_Adult,
        HighSchool_College,
        LittleLeague,
        YouthTeeBall,
        Custom
    }

    public enum MeasurementUnit
    {
        Inches,
        Centimeters,
        FeetInches
    }

    [System.Serializable]
    public class StrikeZoneDimensions
    {
        // 1 meter = 39.3701 inches
        public const float METERS_TO_INCHES = 39.3700787f;
        public const float INCHES_TO_METERS = 0.0254f;

        // Official MLB Specs: 17 inches wide, 17 inches deep (front to back apex)
        public const float OFFICIAL_WIDTH_INCHES = 17.0f;
        public const float OFFICIAL_DEPTH_INCHES = 17.0f;
        public const float OFFICIAL_BEVEL_DEPTH_INCHES = 8.5f;
        public const float BASEBALL_DIAMETER_INCHES = 2.90f; // Standard MLB baseball: 2.86 - 2.94 in

        [Header("Zone Dimensions (in Inches)")]
        [SerializeField] private float widthInches = OFFICIAL_WIDTH_INCHES;
        [SerializeField] private float depthInches = OFFICIAL_DEPTH_INCHES;
        [SerializeField] private float bottomHeightInches = 18.0f; // Hollow beneath the kneecap
        [SerializeField] private float topHeightInches = 42.0f;    // Midpoint between shoulders and belt
        [SerializeField] private StrikeZonePreset preset = StrikeZonePreset.MLB_Adult;

        public float WidthInches => widthInches;
        public float DepthInches => depthInches;
        public float BottomHeightInches => bottomHeightInches;
        public float TopHeightInches => topHeightInches;
        public float ZoneHeightInches => Mathf.Max(6.0f, topHeightInches - bottomHeightInches);

        public float WidthMeters => widthInches * INCHES_TO_METERS;
        public float DepthMeters => depthInches * INCHES_TO_METERS;
        public float BottomHeightMeters => bottomHeightInches * INCHES_TO_METERS;
        public float TopHeightMeters => topHeightInches * INCHES_TO_METERS;
        public float ZoneHeightMeters => ZoneHeightInches * INCHES_TO_METERS;
        public float CenterHeightMeters => (BottomHeightMeters + TopHeightMeters) * 0.5f;

        public StrikeZonePreset CurrentPreset => preset;

        public event Action OnDimensionsChanged;

        public void SetPreset(StrikeZonePreset newPreset)
        {
            preset = newPreset;
            switch (newPreset)
            {
                case StrikeZonePreset.MLB_Adult:
                    bottomHeightInches = 18.0f;
                    topHeightInches = 42.0f;
                    break;
                case StrikeZonePreset.HighSchool_College:
                    bottomHeightInches = 17.0f;
                    topHeightInches = 40.0f;
                    break;
                case StrikeZonePreset.LittleLeague:
                    bottomHeightInches = 14.0f;
                    topHeightInches = 34.0f;
                    break;
                case StrikeZonePreset.YouthTeeBall:
                    bottomHeightInches = 12.0f;
                    topHeightInches = 28.0f;
                    break;
                case StrikeZonePreset.Custom:
                    break;
            }
            OnDimensionsChanged?.Invoke();
        }

        public void SetCustomHeights(float bottomInches, float topInches)
        {
            bottomHeightInches = Mathf.Clamp(bottomInches, 6.0f, 36.0f);
            topHeightInches = Mathf.Clamp(topInches, bottomHeightInches + 8.0f, 60.0f);
            preset = StrikeZonePreset.Custom;
            OnDimensionsChanged?.Invoke();
        }

        public void SetBottomHeight(float bottomInches)
        {
            bottomHeightInches = Mathf.Clamp(bottomInches, 6.0f, topHeightInches - 6.0f);
            preset = StrikeZonePreset.Custom;
            OnDimensionsChanged?.Invoke();
        }

        public void SetTopHeight(float topInches)
        {
            topHeightInches = Mathf.Clamp(topInches, bottomHeightInches + 6.0f, 60.0f);
            preset = StrikeZonePreset.Custom;
            OnDimensionsChanged?.Invoke();
        }

        /// <summary>
        /// Calculates the Statcast 3x3 zone index (1 to 9) for a local strike zone coordinate.
        /// Returns 1-9 if inside the heart of the zone, or 11-14 for chase/waste shadow zones.
        /// </summary>
        public int GetStatcastZone(Vector3 localHitPos)
        {
            float halfWidth = WidthMeters * 0.5f;
            float colWidth = WidthMeters / 3.0f;
            float rowHeight = ZoneHeightMeters / 3.0f;

            // Normalize X (-halfWidth to +halfWidth -> 0 to 3)
            float normX = (localHitPos.x + halfWidth) / colWidth;
            // Normalize Y (BottomHeightMeters to TopHeightMeters -> 0 to 3, where 2-3 is top row)
            float normY = (localHitPos.y - BottomHeightMeters) / rowHeight;

            int col = Mathf.FloorToInt(normX);
            int row = 2 - Mathf.FloorToInt(normY); // Invert so row 0 is top

            if (col >= 0 && col < 3 && row >= 0 && row < 3)
            {
                // Zones 1-9:
                // 1 2 3 (High)
                // 4 5 6 (Middle)
                // 7 8 9 (Low)
                return row * 3 + col + 1;
            }

            // Outside the 9-box
            return 0; // Ball / Out of zone
        }

        public static string FormatMeasurement(float inches, MeasurementUnit unit)
        {
            switch (unit)
            {
                case MeasurementUnit.Inches:
                    return $"{inches:F1}\"";
                case MeasurementUnit.Centimeters:
                    return $"{inches * 2.54f:F1} cm";
                case MeasurementUnit.FeetInches:
                    int feet = Mathf.FloorToInt(inches / 12f);
                    float rem = inches % 12f;
                    return feet > 0 ? $"{feet}' {rem:F1}\"" : $"{rem:F1}\"";
                default:
                    return $"{inches:F1}\"";
            }
        }
    }
}
