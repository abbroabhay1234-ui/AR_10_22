#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;
using TMPro;
using VirtualStrikeZone.Core;
using VirtualStrikeZone.Interaction;
using VirtualStrikeZone.Rendering;
using VirtualStrikeZone.UI;

namespace VirtualStrikeZone.EditorScripts
{
    public static class StrikeZoneSceneSetup
    {
        private const string MATERIALS_DIR = "Assets/Materials";
        private const string PREFABS_DIR = "Assets/Prefabs";
        private const string SCENES_DIR = "Assets/Scenes";
        private const string SCENE_PATH = "Assets/Scenes/VirtualStrikeZoneScene.unity";

        [MenuItem("Tools/Virtual Strike Zone/Build Complete AR Scene and Prefabs")]
        public static void BuildAll()
        {
            EnsureDirectories();
            Material holoMat = CreateOrGetHoloMaterial();
            Material plateMat = CreateOrGetPlateMaterial();
            Material reticleMat = CreateOrGetReticleMaterial();
            Material lineMat = CreateOrGetLineMaterial();

            GameObject reticlePrefab = CreateReticlePrefab(reticleMat);
            GameObject strikeZonePrefab = CreateStrikeZonePrefab(holoMat, plateMat, lineMat);

            CreateARScene(reticlePrefab, strikeZonePrefab);
            RegisterScene();
            ConfigureProjectSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[StrikeZoneSceneSetup] Completed successfully! Scene built at: " + SCENE_PATH);
        }

        [MenuItem("Tools/Virtual Strike Zone/Configure Project Settings for AR")]
        public static void ConfigureProjectSettings()
        {
            PlayerSettings.productName = "Virtual Strike Zone";
            PlayerSettings.companyName = "BaseballAR";
            PlayerSettings.iOS.cameraUsageDescription = "Virtual Strike Zone requires camera access for AR surface tracking, real-world distance measurement, and pitch tracking.";
            PlayerSettings.iOS.targetOSVersionString = "14.0";
            PlayerSettings.iOS.requiresFullScreen = true;

            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            Debug.Log("[StrikeZoneSceneSetup] PlayerSettings configured for iOS & Android AR!");
        }

        [MenuItem("Tools/Virtual Strike Zone/Export iOS Xcode Project")]
        public static void ExportIOS()
        {
            ConfigureProjectSettings();
            RegisterScene();

            string buildPath = "Builds/iOS";
            if (!Directory.Exists("Builds")) Directory.CreateDirectory("Builds");

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
            buildPlayerOptions.scenes = new[] { SCENE_PATH };
            buildPlayerOptions.locationPathName = buildPath;
            buildPlayerOptions.target = BuildTarget.iOS;
            buildPlayerOptions.options = BuildOptions.None;

            Debug.Log("[StrikeZoneSceneSetup] Starting iOS Xcode project export to: " + buildPath);
            UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            UnityEditor.Build.Reporting.BuildSummary summary = report.summary;

            if (summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log($"[StrikeZoneSceneSetup] iOS export succeeded: {summary.totalSize} bytes in {summary.totalTime.TotalSeconds:F1} seconds!");
            }
            else
            {
                Debug.LogError($"[StrikeZoneSceneSetup] iOS export failed with result {summary.result}: {summary.totalErrors} errors.");
            }
        }

        [MenuItem("Tools/Virtual Strike Zone/Set Scene in Build Settings")]
        public static void RegisterScene()
        {
            EditorBuildSettingsScene[] scenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene(SCENE_PATH, true)
            };
            EditorBuildSettings.scenes = scenes;
            Debug.Log("[StrikeZoneSceneSetup] VirtualStrikeZoneScene set as Scene 0 in Build Settings!");
        }

        private static void EnsureDirectories()
        {
            if (!Directory.Exists(MATERIALS_DIR)) Directory.CreateDirectory(MATERIALS_DIR);
            if (!Directory.Exists(PREFABS_DIR)) Directory.CreateDirectory(PREFABS_DIR);
            if (!Directory.Exists(SCENES_DIR)) Directory.CreateDirectory(SCENES_DIR);
        }

        private static Material CreateOrGetHoloMaterial()
        {
            string path = $"{MATERIALS_DIR}/M_StrikeZone_Holo.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("VirtualStrikeZone/HolographicZone");
                if (shader == null) shader = Shader.Find("Standard");
                mat = new Material(shader);
                mat.SetColor("_MainColor", new Color(0.1f, 0.6f, 1f, 0.25f));
                mat.SetColor("_RimColor", new Color(0.4f, 0.85f, 1f, 0.9f));
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        private static Material CreateOrGetPlateMaterial()
        {
            string path = $"{MATERIALS_DIR}/M_HomePlate.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
                mat.color = new Color(0.95f, 0.95f, 0.95f, 1f);
                mat.SetFloat("_Glossiness", 0.3f);
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        private static Material CreateOrGetReticleMaterial()
        {
            string path = $"{MATERIALS_DIR}/M_MeasureReticle.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("VirtualStrikeZone/MeasureReticle");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                mat = new Material(shader);
                mat.SetColor("_Color", new Color(1f, 1f, 1f, 0.95f));
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        private static Material CreateOrGetLineMaterial()
        {
            string path = $"{MATERIALS_DIR}/M_GridLine.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Sprites/Default"));
                mat.color = new Color(0.3f, 0.85f, 1f, 0.9f);
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        private static GameObject CreateReticlePrefab(Material reticleMat)
        {
            string prefabPath = $"{PREFABS_DIR}/MeasureReticle.prefab";
            GameObject reticleGO = new GameObject("MeasureReticle");

            // Circular reticle quad flat against plane (rotate 90 deg around X)
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "ReticleRing";
            quad.transform.SetParent(reticleGO.transform, false);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = Vector3.one * 0.18f; // 18cm diameter
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.GetComponent<Renderer>().material = reticleMat;

            // Distance label
            GameObject labelGO = new GameObject("DistanceLabel");
            labelGO.transform.SetParent(reticleGO.transform, false);
            labelGO.transform.localPosition = new Vector3(0f, 0.05f, 0.12f);
            TextMeshPro tmp = labelGO.AddComponent<TextMeshPro>();
            tmp.fontSize = 2.0f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.text = "0.0\"";
            labelGO.AddComponent<FloatingBadge>();

            // MeasureReticle script
            MeasureReticle mr = reticleGO.AddComponent<MeasureReticle>();
            SerializedObject so = new SerializedObject(mr);
            so.FindProperty("outerRing").objectReferenceValue = quad.transform;
            so.FindProperty("distanceLabel").objectReferenceValue = tmp;
            so.ApplyModifiedProperties();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(reticleGO, prefabPath);
            Object.DestroyImmediate(reticleGO);
            return saved;
        }

        private static GameObject CreateStrikeZonePrefab(Material holoMat, Material plateMat, Material lineMat)
        {
            string prefabPath = $"{PREFABS_DIR}/VirtualStrikeZone.prefab";
            GameObject zoneGO = new GameObject("VirtualStrikeZone");

            StrikeZoneMeshBuilder builder = zoneGO.AddComponent<StrikeZoneMeshBuilder>();

            // Setup Badge prefabs
            GameObject widthBadge = CreateBadge(zoneGO.transform, "WidthBadge", "17.0\"");
            GameObject heightBadge = CreateBadge(zoneGO.transform, "HeightBadge", "24.0\"");
            GameObject topBadge = CreateBadge(zoneGO.transform, "TopBadge", "Top: 42.0\"");
            GameObject bottomBadge = CreateBadge(zoneGO.transform, "BottomBadge", "Knee: 18.0\"");

            SerializedObject so = new SerializedObject(builder);
            so.FindProperty("homePlateMaterial").objectReferenceValue = plateMat;
            so.FindProperty("holographicZoneMaterial").objectReferenceValue = holoMat;
            so.FindProperty("gridLineMaterial").objectReferenceValue = lineMat;

            so.FindProperty("widthBadgeTransform").objectReferenceValue = widthBadge.transform;
            so.FindProperty("heightBadgeTransform").objectReferenceValue = heightBadge.transform;
            so.FindProperty("topBadgeTransform").objectReferenceValue = topBadge.transform;
            so.FindProperty("bottomBadgeTransform").objectReferenceValue = bottomBadge.transform;

            so.FindProperty("widthText").objectReferenceValue = widthBadge.GetComponentInChildren<TextMeshPro>();
            so.FindProperty("heightText").objectReferenceValue = heightBadge.GetComponentInChildren<TextMeshPro>();
            so.FindProperty("topText").objectReferenceValue = topBadge.GetComponentInChildren<TextMeshPro>();
            so.FindProperty("bottomText").objectReferenceValue = bottomBadge.GetComponentInChildren<TextMeshPro>();

            so.ApplyModifiedProperties();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(zoneGO, prefabPath);
            Object.DestroyImmediate(zoneGO);
            return saved;
        }

        private static GameObject CreateBadge(Transform parent, string name, string defaultText)
        {
            GameObject badge = new GameObject(name);
            badge.transform.SetParent(parent, false);
            badge.AddComponent<FloatingBadge>();

            // Text
            GameObject textGO = new GameObject("Text");
            textGO.transform.SetParent(badge.transform, false);
            TextMeshPro tmp = textGO.AddComponent<TextMeshPro>();
            tmp.text = defaultText;
            tmp.fontSize = 2.4f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 1f, 1f, 0.95f);

            return badge;
        }

        private static void CreateARScene(GameObject reticlePrefab, GameObject strikeZonePrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Directional Light
            GameObject lightGO = new GameObject("Directional Light");
            Light light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 1.0f;
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // 2. AR Session
            GameObject sessionGO = new GameObject("AR Session");
            sessionGO.AddComponent<ARSession>();
            sessionGO.AddComponent<ARInputManager>();

            // 3. XR Origin (AR Session Origin)
            GameObject xrOriginGO = new GameObject("XR Origin");
            XROrigin xrOrigin = xrOriginGO.AddComponent<XROrigin>();

            ARRaycastManager raycastManager = xrOriginGO.AddComponent<ARRaycastManager>();
            ARAnchorManager anchorManager = xrOriginGO.AddComponent<ARAnchorManager>();
            ARPlaneManager planeManager = xrOriginGO.AddComponent<ARPlaneManager>();

            // AR Camera inside Camera Offset
            GameObject offsetGO = new GameObject("Camera Offset");
            offsetGO.transform.SetParent(xrOriginGO.transform, false);
            xrOrigin.CameraFloorOffsetObject = offsetGO;

            GameObject camGO = new GameObject("Main Camera");
            camGO.transform.SetParent(offsetGO.transform, false);
            camGO.tag = "MainCamera";
            Camera cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;

            camGO.AddComponent<ARCameraManager>();
            camGO.AddComponent<ARCameraBackground>();
            xrOrigin.Camera = cam;

            // 4. Instantiate Reticle & StrikeZone
            GameObject reticleInstance = (GameObject)PrefabUtility.InstantiatePrefab(reticlePrefab);
            reticleInstance.name = "MeasureReticle";
            reticleInstance.SetActive(false);

            GameObject zoneInstance = (GameObject)PrefabUtility.InstantiatePrefab(strikeZonePrefab);
            zoneInstance.name = "VirtualStrikeZone";
            zoneInstance.SetActive(false);

            // 5. Add Controllers on XR Origin
            StrikeZoneAnchorController anchorCtrl = xrOriginGO.AddComponent<StrikeZoneAnchorController>();
            BatterCalibrator calibrator = xrOriginGO.AddComponent<BatterCalibrator>();
            PitchSimulator pitchSim = xrOriginGO.AddComponent<PitchSimulator>();

            SerializedObject soAnchor = new SerializedObject(anchorCtrl);
            soAnchor.FindProperty("raycastManager").objectReferenceValue = raycastManager;
            soAnchor.FindProperty("anchorManager").objectReferenceValue = anchorManager;
            soAnchor.FindProperty("planeManager").objectReferenceValue = planeManager;
            soAnchor.FindProperty("strikeZoneRoot").objectReferenceValue = zoneInstance;
            soAnchor.FindProperty("reticleObject").objectReferenceValue = reticleInstance;
            soAnchor.ApplyModifiedProperties();

            SerializedObject soCalib = new SerializedObject(calibrator);
            soCalib.FindProperty("anchorController").objectReferenceValue = anchorCtrl;
            soCalib.FindProperty("meshBuilder").objectReferenceValue = zoneInstance.GetComponent<StrikeZoneMeshBuilder>();
            soCalib.ApplyModifiedProperties();

            SerializedObject soPitch = new SerializedObject(pitchSim);
            soPitch.FindProperty("anchorController").objectReferenceValue = anchorCtrl;
            soPitch.FindProperty("meshBuilder").objectReferenceValue = zoneInstance.GetComponent<StrikeZoneMeshBuilder>();
            soPitch.ApplyModifiedProperties();

            // 6. Setup Canvas and Apple Measure UI
            CreateMeasureHUD(anchorCtrl, zoneInstance.GetComponent<StrikeZoneMeshBuilder>(), calibrator, pitchSim);

            EditorSceneManager.SaveScene(scene, SCENE_PATH);
        }

        private static void CreateMeasureHUD(StrikeZoneAnchorController anchorCtrl, StrikeZoneMeshBuilder meshBuilder, BatterCalibrator calibrator, PitchSimulator pitchSim)
        {
            // Event System
            GameObject esGO = new GameObject("EventSystem");
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            // Canvas
            GameObject canvasGO = new GameObject("MeasureHUD_Canvas");
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            // HUD Controller
            StrikeZoneHUD hud = canvasGO.AddComponent<StrikeZoneHUD>();

            // Top Status Prompt Pill
            GameObject promptPill = CreateUIPanel(canvasGO.transform, "PromptPill", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -120), new Vector2(700, 70), new Color(0.1f, 0.1f, 0.12f, 0.85f));
            TextMeshProUGUI promptText = CreateUIText(promptPill.transform, "PromptText", "Move phone slowly to detect ground surface", 28, Color.white);

            // Measurement Live Badge Pill
            GameObject badgePill = CreateUIPanel(canvasGO.transform, "BadgePill", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -210), new Vector2(620, 60), new Color(0.15f, 0.15f, 0.18f, 0.8f));
            TextMeshProUGUI badgeText = CreateUIText(badgePill.transform, "BadgeText", "17\"W x 24\"H (18\" - 42\")", 26, new Color(0.4f, 0.85f, 1f));

            // Pitch Callout Banner
            GameObject calloutPill = CreateUIPanel(canvasGO.transform, "CalloutPill", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 300), new Vector2(650, 100), new Color(0.05f, 0.05f, 0.08f, 0.9f));
            TextMeshProUGUI calloutText = CreateUIText(calloutPill.transform, "CalloutText", "READY FOR PITCH", 36, Color.yellow);

            // Bottom Action Deck
            GameObject deck = CreateUIPanel(canvasGO.transform, "ActionDeck", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 150), new Vector2(1000, 220), new Color(0f, 0f, 0f, 0f));

            // Large White Circular Action Button (Apple Measure "+")
            GameObject mainBtnGO = CreateCircularButton(deck.transform, "MainActionButton", new Vector2(0, 0), 140, Color.white, "+", 60, Color.black);
            Button mainBtn = mainBtnGO.GetComponent<Button>();

            // Circular Lock Button
            GameObject lockBtnGO = CreateCircularButton(deck.transform, "LockButton", new Vector2(-220, 0), 100, new Color(0.2f, 0.2f, 0.25f, 0.85f), "LOCK", 20, Color.white);
            Button lockBtn = lockBtnGO.GetComponent<Button>();

            // Circular Calibrate Button
            GameObject calibBtnGO = CreateCircularButton(deck.transform, "CalibrateButton", new Vector2(-370, 0), 90, new Color(0.2f, 0.2f, 0.25f, 0.85f), "BATTER", 18, Color.white);
            Button calibBtn = calibBtnGO.GetComponent<Button>();

            // Circular Align Button
            GameObject alignBtnGO = CreateCircularButton(deck.transform, "AlignButton", new Vector2(220, 0), 100, new Color(0.2f, 0.2f, 0.25f, 0.85f), "ALIGN", 20, Color.white);
            Button alignBtn = alignBtnGO.GetComponent<Button>();

            // Circular Clear / Reset Button
            GameObject clearBtnGO = CreateCircularButton(deck.transform, "ClearButton", new Vector2(370, 0), 90, new Color(0.35f, 0.15f, 0.15f, 0.85f), "CLEAR", 18, Color.white);
            Button clearBtn = clearBtnGO.GetComponent<Button>();

            // Link to HUD SerializedObject
            SerializedObject soHUD = new SerializedObject(hud);
            soHUD.FindProperty("anchorController").objectReferenceValue = anchorCtrl;
            soHUD.FindProperty("meshBuilder").objectReferenceValue = meshBuilder;
            soHUD.FindProperty("batterCalibrator").objectReferenceValue = calibrator;
            soHUD.FindProperty("pitchSimulator").objectReferenceValue = pitchSim;

            soHUD.FindProperty("statusPromptText").objectReferenceValue = promptText;
            soHUD.FindProperty("measurementBadgeText").objectReferenceValue = badgeText;
            soHUD.FindProperty("mainActionButton").objectReferenceValue = mainBtn;
            soHUD.FindProperty("lockButton").objectReferenceValue = lockBtn;
            soHUD.FindProperty("calibrateButton").objectReferenceValue = calibBtn;
            soHUD.FindProperty("alignButton").objectReferenceValue = alignBtn;
            soHUD.FindProperty("clearButton").objectReferenceValue = clearBtn;

            soHUD.ApplyModifiedProperties();

            // Link Callout to PitchSimulator
            SerializedObject soPitch = new SerializedObject(pitchSim);
            soPitch.FindProperty("calloutTextUI").objectReferenceValue = calloutText;
            soPitch.ApplyModifiedProperties();
        }

        private static GameObject CreateUIPanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            Image img = go.AddComponent<Image>();
            img.color = color;
            return go;
        }

        private static TextMeshProUGUI CreateUIText(Transform parent, string name, string text, float fontSize, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            return tmp;
        }

        private static GameObject CreateCircularButton(Transform parent, string name, Vector2 anchoredPos, float diameter, Color btnColor, string label, float fontSize, Color textColor)
        {
            GameObject btnGO = new GameObject(name);
            btnGO.transform.SetParent(parent, false);
            RectTransform rt = btnGO.AddComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(diameter, diameter);

            Image img = btnGO.AddComponent<Image>();
            img.color = btnColor;
            Button btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = img;

            GameObject textGO = new GameObject("Label");
            textGO.transform.SetParent(btnGO.transform, false);
            RectTransform textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = textColor;

            return btnGO;
        }
    }
}
#endif
