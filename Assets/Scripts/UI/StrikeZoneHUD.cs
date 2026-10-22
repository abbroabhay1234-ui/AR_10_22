using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VirtualStrikeZone.Core;
using VirtualStrikeZone.Interaction;
using VirtualStrikeZone.Rendering;

namespace VirtualStrikeZone.UI
{
    /// <summary>
    /// Master UI HUD controller styled after the iOS Apple Measure app.
    /// Provides circular action controls, status prompts, dimension steppers,
    /// and mode switching for placement, calibration, and pitch testing.
    /// </summary>
    public class StrikeZoneHUD : MonoBehaviour
    {
        [Header("Core Systems")]
        [SerializeField] private StrikeZoneAnchorController anchorController;
        [SerializeField] private StrikeZoneMeshBuilder meshBuilder;
        [SerializeField] private BatterCalibrator batterCalibrator;
        [SerializeField] private PitchSimulator pitchSimulator;

        [Header("Status & Prompts")]
        [SerializeField] private TextMeshProUGUI statusPromptText;
        [SerializeField] private TextMeshProUGUI measurementBadgeText;

        [Header("Primary Action Deck")]
        [Tooltip("The iconic large circular action button from Apple Measure")]
        [SerializeField] private Button mainActionButton;
        [SerializeField] private Image mainActionIcon;
        [SerializeField] private Button lockButton;
        [SerializeField] private Image lockIcon;
        [SerializeField] private Button calibrateButton;
        [SerializeField] private Button alignButton;
        [SerializeField] private Button clearButton;
        [SerializeField] private Button pitchTestButton;

        [Header("Height Sliders & Controls")]
        [SerializeField] private GameObject controlsDrawer;
        [SerializeField] private Slider topHeightSlider;
        [SerializeField] private Slider bottomHeightSlider;
        [SerializeField] private Slider rotationSlider;
        [SerializeField] private TextMeshProUGUI topHeightValueText;
        [SerializeField] private TextMeshProUGUI bottomHeightValueText;

        [Header("Preset Dropdown")]
        [SerializeField] private TMP_Dropdown presetDropdown;

        private bool isLocked = false;

        private void Start()
        {
            BindEventListeners();
            UpdateUIState(anchorController != null ? anchorController.CurrentState : PlacementState.ScanningForPlanes);
        }

        private void BindEventListeners()
        {
            if (anchorController != null)
            {
                anchorController.OnStateChanged += UpdateUIState;
                anchorController.OnZonePlaced += (pose) => UpdateUIState(PlacementState.Placed);
                anchorController.OnZoneCleared += () => UpdateUIState(PlacementState.ScanningForPlanes);
            }

            if (batterCalibrator != null)
            {
                batterCalibrator.OnCalibrationStepChanged += HandleCalibrationStepChanged;
            }

            if (mainActionButton != null)
                mainActionButton.onClick.AddListener(OnMainActionButtonClicked);

            if (lockButton != null)
                lockButton.onClick.AddListener(OnLockToggleClicked);

            if (calibrateButton != null)
                calibrateButton.onClick.AddListener(OnCalibrateButtonClicked);

            if (alignButton != null)
                alignButton.onClick.AddListener(OnAlignButtonClicked);

            if (clearButton != null)
                clearButton.onClick.AddListener(OnClearButtonClicked);

            if (pitchTestButton != null)
                pitchTestButton.onClick.AddListener(OnPitchTestButtonClicked);

            if (topHeightSlider != null)
                topHeightSlider.onValueChanged.AddListener(OnTopHeightSliderChanged);

            if (bottomHeightSlider != null)
                bottomHeightSlider.onValueChanged.AddListener(OnBottomHeightSliderChanged);

            if (rotationSlider != null)
                rotationSlider.onValueChanged.AddListener(OnRotationSliderChanged);

            if (presetDropdown != null)
                presetDropdown.onValueChanged.AddListener(OnPresetDropdownChanged);
        }

        private void OnDestroy()
        {
            if (anchorController != null)
            {
                anchorController.OnStateChanged -= UpdateUIState;
            }
            if (batterCalibrator != null)
            {
                batterCalibrator.OnCalibrationStepChanged -= HandleCalibrationStepChanged;
            }
        }

        private void UpdateUIState(PlacementState state)
        {
            switch (state)
            {
                case PlacementState.ScanningForPlanes:
                    SetPrompt("Move phone slowly to detect ground surface");
                    SetMainActionButtonInteractable(false);
                    SetSecondaryButtonsVisible(false);
                    break;

                case PlacementState.ReadyToPlace:
                    SetPrompt("Surface detected. Tap + to anchor strike zone");
                    SetMainActionButtonInteractable(true);
                    SetSecondaryButtonsVisible(false);
                    break;

                case PlacementState.Placed:
                    SetPrompt("Strike Zone Anchored! Tap Lock to freeze position");
                    SetMainActionButtonInteractable(true);
                    SetSecondaryButtonsVisible(true);
                    UpdateLockVisual(false);
                    UpdateDimensionLabels();
                    break;

                case PlacementState.Locked:
                    SetPrompt("Strike Zone Position Locked (Zero Drift)");
                    SetSecondaryButtonsVisible(true);
                    UpdateLockVisual(true);
                    break;
            }
        }

        private void HandleCalibrationStepChanged(CalibrationStep step)
        {
            switch (step)
            {
                case CalibrationStep.AimingKnee:
                    SetPrompt("Aim at Batter's KNEE and tap +");
                    break;
                case CalibrationStep.AimingChest:
                    SetPrompt("Aim at Batter's CHEST / BELT and tap +");
                    break;
                case CalibrationStep.Complete:
                    SetPrompt("Batter Calibrated Successfully!");
                    UpdateDimensionLabels();
                    break;
                case CalibrationStep.Idle:
                    UpdateUIState(anchorController.CurrentState);
                    break;
            }
        }

        private void OnMainActionButtonClicked()
        {
            if (batterCalibrator != null && batterCalibrator.CurrentStep != CalibrationStep.Idle)
            {
                batterCalibrator.RecordCurrentPoint();
                return;
            }

            if (anchorController != null)
            {
                if (anchorController.CurrentState == PlacementState.ReadyToPlace ||
                    anchorController.CurrentState == PlacementState.ScanningForPlanes)
                {
                    anchorController.PlaceStrikeZone();
                }
                else if (anchorController.CurrentState == PlacementState.Placed ||
                         anchorController.CurrentState == PlacementState.Locked)
                {
                    // If already placed, main button triggers a test pitch
                    if (pitchSimulator != null)
                    {
                        pitchSimulator.ThrowTestPitch();
                    }
                }
            }
        }

        private void OnLockToggleClicked()
        {
            isLocked = !isLocked;
            if (anchorController != null)
            {
                anchorController.ToggleLock(isLocked);
            }
            UpdateLockVisual(isLocked);
        }

        private void OnCalibrateButtonClicked()
        {
            if (batterCalibrator != null)
            {
                batterCalibrator.StartCalibration();
            }
        }

        private void OnAlignButtonClicked()
        {
            if (anchorController != null)
            {
                anchorController.AlignTowardsCamera();
            }
        }

        private void OnClearButtonClicked()
        {
            isLocked = false;
            if (anchorController != null)
            {
                anchorController.ClearStrikeZone();
            }
            if (batterCalibrator != null)
            {
                batterCalibrator.CancelCalibration();
            }
        }

        private void OnPitchTestButtonClicked()
        {
            if (pitchSimulator != null)
            {
                pitchSimulator.ThrowTestPitch();
            }
        }

        private void OnTopHeightSliderChanged(float val)
        {
            if (meshBuilder != null && meshBuilder.Dimensions != null)
            {
                meshBuilder.Dimensions.SetTopHeight(val);
                UpdateDimensionLabels();
            }
        }

        private void OnBottomHeightSliderChanged(float val)
        {
            if (meshBuilder != null && meshBuilder.Dimensions != null)
            {
                meshBuilder.Dimensions.SetBottomHeight(val);
                UpdateDimensionLabels();
            }
        }

        private void OnRotationSliderChanged(float val)
        {
            if (anchorController != null)
            {
                anchorController.RotateZone(val);
            }
        }

        private void OnPresetDropdownChanged(int index)
        {
            if (meshBuilder != null && meshBuilder.Dimensions != null)
            {
                StrikeZonePreset preset = (StrikeZonePreset)index;
                meshBuilder.Dimensions.SetPreset(preset);
                UpdateDimensionLabels();
            }
        }

        private void UpdateDimensionLabels()
        {
            if (meshBuilder == null || meshBuilder.Dimensions == null) return;
            StrikeZoneDimensions d = meshBuilder.Dimensions;

            if (measurementBadgeText != null)
            {
                measurementBadgeText.text = $"{d.WidthInches:F0}\"W x {d.ZoneHeightInches:F0}\"H ({d.BottomHeightInches:F0}\" - {d.TopHeightInches:F0}\")";
            }

            if (topHeightValueText != null)
                topHeightValueText.text = $"{d.TopHeightInches:F1}\"";

            if (bottomHeightValueText != null)
                bottomHeightValueText.text = $"{d.BottomHeightInches:F1}\"";
        }

        private void SetPrompt(string message)
        {
            if (statusPromptText != null)
                statusPromptText.text = message;
        }

        private void SetMainActionButtonInteractable(bool interactable)
        {
            if (mainActionButton != null)
                mainActionButton.interactable = interactable;
        }

        private void SetSecondaryButtonsVisible(bool visible)
        {
            if (lockButton != null) lockButton.gameObject.SetActive(visible);
            if (calibrateButton != null) calibrateButton.gameObject.SetActive(visible);
            if (alignButton != null) alignButton.gameObject.SetActive(visible);
            if (clearButton != null) clearButton.gameObject.SetActive(visible);
            if (pitchTestButton != null) pitchTestButton.gameObject.SetActive(visible);
            if (controlsDrawer != null) controlsDrawer.SetActive(visible);
        }

        private void UpdateLockVisual(bool locked)
        {
            if (lockIcon != null)
            {
                lockIcon.color = locked ? new Color(1f, 0.8f, 0.1f) : Color.white;
            }
        }
    }
}
