using UnityEditor;
using UnityEngine;

namespace Namako
{
    public class NamakoHapticsTool : EditorWindow
    {
        private GameObject meshObj;
        private GameObject proxyObj;
        private GameObject inputObj;
        private string proxyObjName = "Proxy";
        private string inputObjName = "Input";
        private string meshObjName = "TetMesh";
        private Vector2 scrollPosition = Vector2.zero;

        [MenuItem("Window/NamakoHapticsTool")]
        public static void ShowWindow()
        {
            NamakoHapticsTool window = EditorWindow.GetWindow(typeof(NamakoHapticsTool)) as NamakoHapticsTool;
            window.minSize = new Vector2(300, 400);
            window.titleContent = new GUIContent("Namako Haptics Tool");
        }

        void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            GUILayout.Space(10);
            EditorGUILayout.LabelField("Namako Haptics Tool", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Haptic Interface Generation & Management", EditorStyles.miniLabel);
            
            DrawSeparator();

            // Haptic Interface Section
            DrawSectionHeader("Haptic Interface", "Generate proxy and input objects for haptic interaction");
            EditorGUI.indentLevel++;
            
            meshObj = EditorGUILayout.ObjectField("Tetrahedral Mesh Object", meshObj, typeof(GameObject), true) as GameObject;
            
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Generate Haptic Interface", GUILayout.Height(30)))
            {
                GenerateHapticInterface();
            }
            GUI.backgroundColor = Color.white;
            EditorGUI.indentLevel--;

            DrawSeparator();

            // Haptic Objects Info Section
            DrawSectionHeader("Current Objects", "Status of haptic interface objects");
            EditorGUI.indentLevel++;
            DrawHapticObjectsInfo();
            EditorGUI.indentLevel--;

            DrawSeparator();

            // Utility Section
            DrawSectionHeader("Utilities", "Clean up haptic objects");
            EditorGUI.indentLevel++;
            GUI.backgroundColor = new Color(1.0f, 0.6f, 0.6f);
            if (GUILayout.Button("Clean Haptic Objects", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Confirm Clean", 
                    "Are you sure you want to clean all haptic objects? This action cannot be undone.", 
                    "Yes", "Cancel"))
                {
                    CleanHapticObjects();
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUI.indentLevel--;
            
            GUILayout.Space(10);
            
            EditorGUILayout.EndScrollView();
        }

        void GenerateHapticInterface()
        {
            if (meshObj == null)
            {
                Debug.LogError("Tetrahedral Mesh Object not found. Please assign a mesh object with NamakoSolver component.");
                return;
            }

            NamakoSolver solver = meshObj.GetComponent<NamakoSolver>();
            if (solver == null)
            {
                Debug.LogError("NamakoSolver component not found on the mesh object. Please generate the mesh first using NamakoMeshTool.");
                return;
            }

            // Clean existing haptic objects first
            CleanHapticObjects();

            // Generate Input object
            inputObj = new GameObject(inputObjName);
            inputObj.transform.SetPositionAndRotation(new Vector3(0.0f, 0.2f, 0.0f), Quaternion.identity);

            // Generate Proxy object
            proxyObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            proxyObj.name = proxyObjName;
            proxyObj.transform.localScale = Vector3.one * solver.HIPRad * 2.0f;
            proxyObj.transform.SetPositionAndRotation(inputObj.transform.position, Quaternion.identity);

            // Setup solver references
            solver.proxyObj = proxyObj;
            solver.inputObj = inputObj;

            Debug.Log("Haptic interface generated successfully.");
        }

        void CleanHapticObjects()
        {
            // Find and destroy haptic objects
            GameObject existingInput = GameObject.Find(inputObjName);
            GameObject existingProxy = GameObject.Find(proxyObjName);

            if (existingInput != null)
            {
                DestroyImmediate(existingInput);
                Debug.Log($"Cleaned {inputObjName} object.");
            }

            if (existingProxy != null)
            {
                DestroyImmediate(existingProxy);
                Debug.Log($"Cleaned {proxyObjName} object.");
            }

            // Clear references
            inputObj = null;
            proxyObj = null;

            // Update solver references if mesh object exists
            if (meshObj != null)
            {
                NamakoSolver solver = meshObj.GetComponent<NamakoSolver>();
                if (solver != null)
                {
                    solver.proxyObj = null;
                    solver.inputObj = null;
                }
            }
        }

        void DrawHapticObjectsInfo()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Check current status
            GameObject currentInput = GameObject.Find(inputObjName);
            GameObject currentProxy = GameObject.Find(proxyObjName);
            GameObject currentMesh = GameObject.Find(meshObjName);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Input Object:", GUILayout.Width(100));
            EditorGUILayout.LabelField(currentInput != null ? "✓ Present" : "✗ Missing", 
                currentInput != null ? EditorStyles.miniLabel : EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Proxy Object:", GUILayout.Width(100));
            EditorGUILayout.LabelField(currentProxy != null ? "✓ Present" : "✗ Missing",
                currentProxy != null ? EditorStyles.miniLabel : EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Mesh Object:", GUILayout.Width(100));
            EditorGUILayout.LabelField(currentMesh != null ? "✓ Present" : "✗ Missing",
                currentMesh != null ? EditorStyles.miniLabel : EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndHorizontal();

            if (currentMesh != null)
            {
                NamakoSolver solver = currentMesh.GetComponent<NamakoSolver>();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Solver Link:", GUILayout.Width(100));
                bool solverLinked = solver != null && solver.proxyObj != null && solver.inputObj != null;
                EditorGUILayout.LabelField(solverLinked ? "✓ Linked" : "✗ Not Linked",
                    solverLinked ? EditorStyles.miniLabel : EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        // UI Helper Methods
        void DrawSeparator()
        {
            GUILayout.Space(8);
            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
            GUILayout.Space(8);
        }

        void DrawSectionHeader(string title, string description)
        {
            GUILayout.Space(5);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (!string.IsNullOrEmpty(description))
            {
                EditorGUILayout.LabelField(description, EditorStyles.miniLabel);
            }
            GUILayout.Space(5);
        }
    }
}
