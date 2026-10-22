using UnityEngine;
using VirtualStrikeZone.Core;
using TMPro;

namespace VirtualStrikeZone.Rendering
{
    /// <summary>
    /// Procedurally constructs and updates the 3D Strike Zone geometry:
    /// 1. Official 5-sided MLB home plate on the ground plane.
    /// 2. 3D holographic wireframe strike zone box with 17" depth.
    /// 3. 3x3 Statcast pitch grid on the front face facing the pitcher.
    /// 4. Dynamic dimension indicators and Statcast zone cell numbers.
    /// </summary>
    public class StrikeZoneMeshBuilder : MonoBehaviour
    {
        [Header("Data Source")]
        [SerializeField] private StrikeZoneDimensions dimensions;

        [Header("Materials")]
        [SerializeField] private Material homePlateMaterial;
        [SerializeField] private Material holographicZoneMaterial;
        [SerializeField] private Material gridLineMaterial;

        [Header("Rendering Components")]
        [SerializeField] private MeshFilter homePlateMeshFilter;
        [SerializeField] private MeshRenderer homePlateRenderer;
        [SerializeField] private MeshFilter zoneMeshFilter;
        [SerializeField] private MeshRenderer zoneRenderer;

        [Header("Statcast Line Renderers")]
        [SerializeField] private LineRenderer outerBoxLines;
        [SerializeField] private LineRenderer internalGridLines;
        [SerializeField] private LineRenderer moundGuideLine;

        [Header("Statcast Zone Labels")]
        [SerializeField] private GameObject zoneNumbersRoot;
        private TextMeshPro[] zoneNumberTexts = new TextMeshPro[9];

        [Header("Floating Badges")]
        [SerializeField] private Transform widthBadgeTransform;
        [SerializeField] private Transform heightBadgeTransform;
        [SerializeField] private Transform topBadgeTransform;
        [SerializeField] private Transform bottomBadgeTransform;
        [SerializeField] private TextMeshPro widthText;
        [SerializeField] private TextMeshPro heightText;
        [SerializeField] private TextMeshPro topText;
        [SerializeField] private TextMeshPro bottomText;

        public StrikeZoneDimensions Dimensions => dimensions;

        private void Awake()
        {
            if (dimensions == null)
            {
                dimensions = new StrikeZoneDimensions();
            }
            dimensions.OnDimensionsChanged += RebuildAll;
        }

        private void Start()
        {
            InitializeComponents();
            RebuildAll();
        }

        private void OnDestroy()
        {
            if (dimensions != null)
            {
                dimensions.OnDimensionsChanged -= RebuildAll;
            }
        }

        public void SetDimensions(StrikeZoneDimensions newDims)
        {
            if (dimensions != null)
                dimensions.OnDimensionsChanged -= RebuildAll;

            dimensions = newDims;
            if (dimensions != null)
                dimensions.OnDimensionsChanged += RebuildAll;

            RebuildAll();
        }

        private void InitializeComponents()
        {
            // Ensure MeshFilters exist
            if (homePlateMeshFilter == null)
            {
                GameObject hpGO = new GameObject("HomePlate_Mesh");
                hpGO.transform.SetParent(transform, false);
                homePlateMeshFilter = hpGO.AddComponent<MeshFilter>();
                homePlateRenderer = hpGO.AddComponent<MeshRenderer>();
                if (homePlateMaterial != null) homePlateRenderer.material = homePlateMaterial;
            }

            if (zoneMeshFilter == null)
            {
                GameObject zoneGO = new GameObject("Zone_Hologram_Mesh");
                zoneGO.transform.SetParent(transform, false);
                zoneMeshFilter = zoneGO.AddComponent<MeshFilter>();
                zoneRenderer = zoneGO.AddComponent<MeshRenderer>();
                if (holographicZoneMaterial != null) zoneRenderer.material = holographicZoneMaterial;
            }

            // Outer box lines (12 edges of the 3D strike zone box)
            if (outerBoxLines == null)
            {
                GameObject linesGO = new GameObject("OuterBox_Lines");
                linesGO.transform.SetParent(transform, false);
                outerBoxLines = linesGO.AddComponent<LineRenderer>();
                ConfigureLineRenderer(outerBoxLines, 0.006f, new Color(0.2f, 0.7f, 1f, 0.9f));
            }

            // Statcast 9-zone internal lines
            if (internalGridLines == null)
            {
                GameObject gridGO = new GameObject("Statcast_Grid_Lines");
                gridGO.transform.SetParent(transform, false);
                internalGridLines = gridGO.AddComponent<LineRenderer>();
                ConfigureLineRenderer(internalGridLines, 0.003f, new Color(0.4f, 0.8f, 1f, 0.5f));
            }

            // 60'6" Pitcher mound laser line guide
            if (moundGuideLine == null)
            {
                GameObject moundGO = new GameObject("Mound_Guide_Line");
                moundGO.transform.SetParent(transform, false);
                moundGuideLine = moundGO.AddComponent<LineRenderer>();
                ConfigureLineRenderer(moundGuideLine, 0.004f, new Color(1f, 0.85f, 0.2f, 0.4f));
            }

            SetupZoneNumberLabels();
        }

        private void ConfigureLineRenderer(LineRenderer lr, float width, Color color)
        {
            lr.useWorldSpace = false;
            lr.startWidth = width;
            lr.endWidth = width;
            if (gridLineMaterial != null)
            {
                lr.material = gridLineMaterial;
            }
            else
            {
                Material unlitMat = new Material(Shader.Find("Sprites/Default"));
                lr.material = unlitMat;
            }
            lr.startColor = color;
            lr.endColor = color;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
        }

        private void SetupZoneNumberLabels()
        {
            if (zoneNumbersRoot == null)
            {
                zoneNumbersRoot = new GameObject("ZoneNumbers_Root");
                zoneNumbersRoot.transform.SetParent(transform, false);
            }

            for (int i = 0; i < 9; i++)
            {
                if (zoneNumberTexts[i] == null)
                {
                    GameObject numGO = new GameObject($"Zone_{i + 1}");
                    numGO.transform.SetParent(zoneNumbersRoot.transform, false);
                    TextMeshPro tmp = numGO.AddComponent<TextMeshPro>();
                    tmp.text = (i + 1).ToString();
                    tmp.fontSize = 2.0f;
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.color = new Color(1f, 1f, 1f, 0.35f);
                    zoneNumberTexts[i] = tmp;
                }
            }
        }

        public void RebuildAll()
        {
            if (dimensions == null) return;

            BuildHomePlateMesh();
            BuildZoneHologramMesh();
            BuildBoxLines();
            BuildInternalGridLines();
            BuildMoundGuideLine();
            UpdateLabelsAndBadges();
        }

        /// <summary>
        /// Builds the official 5-sided MLB home plate pentagon at ground level (Y = 0).
        /// Front edge is 17", depth is 17", beveled corners at 8.5".
        /// </summary>
        private void BuildHomePlateMesh()
        {
            if (homePlateMeshFilter == null) return;

            float w = dimensions.WidthMeters * 0.5f;
            float dFront = 0f;
            float dBevel = -(StrikeZoneDimensions.OFFICIAL_BEVEL_DEPTH_INCHES * StrikeZoneDimensions.INCHES_TO_METERS);
            float dApex = -dimensions.DepthMeters;

            // 5 vertices of standard home plate
            Vector3[] vertices = new Vector3[]
            {
                new Vector3(-w, 0.001f, dFront), // 0: Front Left
                new Vector3( w, 0.001f, dFront), // 1: Front Right
                new Vector3( w, 0.001f, dBevel), // 2: Right Bevel
                new Vector3( 0, 0.001f, dApex),  // 3: Rear Apex (points to catcher)
                new Vector3(-w, 0.001f, dBevel)  // 4: Left Bevel
            };

            // Triangles for 5-sided polygon (3 triangles)
            int[] triangles = new int[]
            {
                0, 1, 2,
                0, 2, 4,
                4, 2, 3
            };

            Vector2[] uvs = new Vector2[]
            {
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0.5f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 0.5f)
            };

            Mesh mesh = new Mesh { name = "MLB_HomePlate" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.RecalculateNormals();

            homePlateMeshFilter.sharedMesh = mesh;
        }

        /// <summary>
        /// Creates the semi-transparent holographic faces for the 3D Strike Zone box.
        /// </summary>
        private void BuildZoneHologramMesh()
        {
            if (zoneMeshFilter == null) return;

            float w = dimensions.WidthMeters * 0.5f;
            float d = dimensions.DepthMeters;
            float yBottom = dimensions.BottomHeightMeters;
            float yTop = dimensions.TopHeightMeters;

            // 8 corners of the 3D strike zone box:
            // Front face (Z = 0):
            // 0: Bottom-Left, 1: Bottom-Right, 2: Top-Right, 3: Top-Left
            // Back face (Z = -d):
            // 4: Bottom-Left, 5: Bottom-Right, 6: Top-Right, 7: Top-Left

            Vector3[] vertices = new Vector3[]
            {
                // Front face
                new Vector3(-w, yBottom, 0),
                new Vector3( w, yBottom, 0),
                new Vector3( w, yTop,    0),
                new Vector3(-w, yTop,    0),
                // Back face
                new Vector3(-w, yBottom, -d),
                new Vector3( w, yBottom, -d),
                new Vector3( w, yTop,    -d),
                new Vector3(-w, yTop,    -d),
            };

            // Double-sided quad triangles for front face and sides
            int[] triangles = new int[]
            {
                // Front face (facing pitcher)
                0, 2, 1, 0, 3, 2,
                0, 1, 2, 0, 2, 3,
                // Top face
                3, 6, 2, 3, 7, 6,
                // Bottom face
                0, 1, 5, 0, 5, 4,
                // Left face
                0, 4, 7, 0, 7, 3,
                // Right face
                1, 2, 6, 1, 6, 5
            };

            Mesh mesh = new Mesh { name = "StrikeZone_Hologram" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();

            zoneMeshFilter.sharedMesh = mesh;
        }

        /// <summary>
        /// Draws the crisp neon outer frame edges of the 3D strike zone box.
        /// </summary>
        private void BuildBoxLines()
        {
            if (outerBoxLines == null) return;

            float w = dimensions.WidthMeters * 0.5f;
            float d = dimensions.DepthMeters;
            float yB = dimensions.BottomHeightMeters;
            float yT = dimensions.TopHeightMeters;

            // Continuous path drawing the 3D box wireframe
            Vector3[] path = new Vector3[]
            {
                // Front rectangle
                new Vector3(-w, yB, 0),
                new Vector3( w, yB, 0),
                new Vector3( w, yT, 0),
                new Vector3(-w, yT, 0),
                new Vector3(-w, yB, 0),
                // To back bottom-left
                new Vector3(-w, yB, -d),
                // Back rectangle
                new Vector3( w, yB, -d),
                new Vector3( w, yT, -d),
                new Vector3(-w, yT, -d),
                new Vector3(-w, yB, -d),
                // Connect remaining 3 corners
                new Vector3(-w, yT, -d),
                new Vector3(-w, yT, 0),
                new Vector3( w, yT, 0),
                new Vector3( w, yT, -d),
                new Vector3( w, yB, -d),
                new Vector3( w, yB, 0)
            };

            outerBoxLines.positionCount = path.Length;
            outerBoxLines.SetPositions(path);
        }

        /// <summary>
        /// Draws the 4 dividing lines creating the 3x3 Statcast grid on the front face.
        /// </summary>
        private void BuildInternalGridLines()
        {
            if (internalGridLines == null) return;

            float w = dimensions.WidthMeters * 0.5f;
            float colW = dimensions.WidthMeters / 3.0f;
            float rowH = dimensions.ZoneHeightMeters / 3.0f;
            float yB = dimensions.BottomHeightMeters;
            float yT = dimensions.TopHeightMeters;

            // 2 vertical lines + 2 horizontal lines
            // Using a connected segment sequence:
            float col1X = -w + colW;
            float col2X = -w + colW * 2f;
            float row1Y = yB + rowH;
            float row2Y = yB + rowH * 2f;

            Vector3[] gridPath = new Vector3[]
            {
                // Vertical 1
                new Vector3(col1X, yB, 0.001f),
                new Vector3(col1X, yT, 0.001f),
                // Move across top to Vertical 2
                new Vector3(col2X, yT, 0.001f),
                new Vector3(col2X, yB, 0.001f),
                // Move to Horizontal 1
                new Vector3(-w, row1Y, 0.001f),
                new Vector3( w, row1Y, 0.001f),
                // Move up to Horizontal 2
                new Vector3( w, row2Y, 0.001f),
                new Vector3(-w, row2Y, 0.001f)
            };

            internalGridLines.positionCount = gridPath.Length;
            internalGridLines.SetPositions(gridPath);
        }

        /// <summary>
        /// Draws a laser guide extending towards the pitcher's mound (60 ft 6 in = 18.44 meters).
        /// </summary>
        private void BuildMoundGuideLine()
        {
            if (moundGuideLine == null) return;

            float moundDistanceMeters = 18.4404f; // Official 60 ft 6 inches
            Vector3[] path = new Vector3[]
            {
                new Vector3(0, 0.01f, 0),
                new Vector3(0, 0.01f, moundDistanceMeters)
            };

            moundGuideLine.positionCount = 2;
            moundGuideLine.SetPositions(path);
        }

        private void UpdateLabelsAndBadges()
        {
            float w = dimensions.WidthMeters * 0.5f;
            float colW = dimensions.WidthMeters / 3.0f;
            float rowH = dimensions.ZoneHeightMeters / 3.0f;
            float yB = dimensions.BottomHeightMeters;

            // Position the 9 Statcast zone numbers
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    int index = row * 3 + col;
                    if (index < zoneNumberTexts.Length && zoneNumberTexts[index] != null)
                    {
                        float x = -w + (col + 0.5f) * colW;
                        float y = yB + (2.5f - row) * rowH; // Row 0 is top
                        zoneNumberTexts[index].transform.localPosition = new Vector3(x, y, 0.002f);
                    }
                }
            }

            // Update badge text values
            if (widthText != null)
                widthText.text = StrikeZoneDimensions.FormatMeasurement(dimensions.WidthInches, MeasurementUnit.Inches);

            if (heightText != null)
                heightText.text = StrikeZoneDimensions.FormatMeasurement(dimensions.ZoneHeightInches, MeasurementUnit.Inches);

            if (topText != null)
                topText.text = $"Top: {StrikeZoneDimensions.FormatMeasurement(dimensions.TopHeightInches, MeasurementUnit.Inches)}";

            if (bottomText != null)
                bottomText.text = $"Knee: {StrikeZoneDimensions.FormatMeasurement(dimensions.BottomHeightInches, MeasurementUnit.Inches)}";

            // Reposition badge transforms to match current zone bounds
            if (widthBadgeTransform != null)
                widthBadgeTransform.localPosition = new Vector3(0, dimensions.TopHeightMeters + 0.05f, 0);

            if (heightBadgeTransform != null)
                heightBadgeTransform.localPosition = new Vector3(-w - 0.08f, dimensions.CenterHeightMeters, 0);

            if (topBadgeTransform != null)
                topBadgeTransform.localPosition = new Vector3(w + 0.12f, dimensions.TopHeightMeters, 0);

            if (bottomBadgeTransform != null)
                bottomBadgeTransform.localPosition = new Vector3(w + 0.12f, dimensions.BottomHeightMeters, 0);
        }
    }
}
