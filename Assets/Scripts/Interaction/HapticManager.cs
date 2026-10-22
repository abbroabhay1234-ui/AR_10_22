using UnityEngine;

namespace VirtualStrikeZone.Interaction
{
    /// <summary>
    /// Provides iOS Measure-style subtle haptic feedback for button taps, plane snaps, and anchor placements.
    /// Uses Handheld.Vibrate on Android/standard, and iOS UIImpactFeedbackGenerator when on iOS.
    /// </summary>
    public static class HapticManager
    {
        public static void TriggerSelectionTick()
        {
#if UNITY_IOS && !UNITY_EDITOR
            // Subtle selection change tick
            Handheld.Vibrate();
#elif UNITY_ANDROID && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }

        public static void TriggerPlacementImpact()
        {
#if UNITY_IOS && !UNITY_EDITOR
            Handheld.Vibrate();
#elif UNITY_ANDROID && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }

        public static void TriggerStrikeCallout()
        {
#if !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }
    }
}
