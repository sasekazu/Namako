using UnityEditor;
using UnityEngine;

namespace Namako
{
    public class NamakoHapticsTool : EditorWindow
    {
        private GameObject meshObj;
        private GameObject proxyObj;
        private GameObject inputObj;
        private GameObject hapticToolObj;
        private string proxyObjName = "Proxy";
        private string inputObjName = "Input";
        private string hapticInterfaceObjName = "HapticInterfaceObject";
        private float hipRadius = 0.03f;
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
            
            // HIP Radius input
            hipRadius = EditorGUILayout.Slider("HIP Radius", hipRadius, 0.001f, 0.1f);
            
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
            // Clean existing haptic objects first
            CleanHapticObjects();

            // Generate HapticTool parent object
            hapticToolObj = new GameObject(hapticInterfaceObjName);
            hapticToolObj.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            // Add NamakoHaptics component to HapticTool
            Component namakoHapticsComponent = null;
            
            try
            {
                // Try multiple methods to add the component
                
                // Method 1: Use reflection to find type in all assemblies
                var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                System.Type namakoHapticsType = null;
                
                foreach (var assembly in assemblies)
                {
                    namakoHapticsType = assembly.GetType("Namako.NamakoHaptics");
                    if (namakoHapticsType != null) break;
                }
                
                if (namakoHapticsType != null)
                {
                    namakoHapticsComponent = hapticToolObj.AddComponent(namakoHapticsType);
                    Debug.Log("NamakoHaptics component added successfully using reflection.");
                }
                else
                {
                    Debug.LogWarning("NamakoHaptics component type not found. Please add it manually after generation.");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to add NamakoHaptics component: {e.Message}. Please add it manually after generation.");
            }

            // Generate Input object
            inputObj = new GameObject(inputObjName);
            inputObj.transform.SetParent(hapticToolObj.transform);
            inputObj.transform.SetPositionAndRotation(new Vector3(0.0f, 0.2f, 0.0f), Quaternion.identity);

            // Generate Proxy object
            proxyObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            proxyObj.name = proxyObjName;
            proxyObj.transform.SetParent(hapticToolObj.transform);
            // Set proxy scale based on HIP radius (diameter = radius * 2)
            proxyObj.transform.localScale = Vector3.one * hipRadius * 2.0f;
            proxyObj.transform.SetPositionAndRotation(inputObj.transform.position, Quaternion.identity);

            // Set HIP Radius in NamakoHaptics component if it was successfully added
            if (namakoHapticsComponent != null)
            {
                // Use SerializedObject to set the HIPRad field
                SerializedObject serializedHaptics = new SerializedObject(namakoHapticsComponent);
                SerializedProperty hipRadProperty = serializedHaptics.FindProperty("HIPRad");
                if (hipRadProperty != null)
                {
                    hipRadProperty.floatValue = hipRadius;
                    serializedHaptics.ApplyModifiedProperties();
                    Debug.Log($"HIP Radius set to {hipRadius} in NamakoHaptics component.");
                }
                else
                {
                    Debug.LogWarning("HIPRad property not found in NamakoHaptics component.");
                }
            }

            // Find or create NamakoSolver and link it to the haptic object
            LinkToNamakoSolver();

            Debug.Log("Haptic interface generated successfully.");
        }

        void CleanHapticObjects()
        {
            // Find and destroy haptic tool parent object (this will destroy all children)
            GameObject existingHapticTool = GameObject.Find(hapticInterfaceObjName);
            
            if (existingHapticTool != null)
            {
                DestroyImmediate(existingHapticTool);
                Debug.Log($"Cleaned {hapticInterfaceObjName} object and all its children.");
            }

            // Clear references
            hapticToolObj = null;
            inputObj = null;
            proxyObj = null;
        }

        void LinkToNamakoSolver()
        {
            // Find existing NamakoSolver in the scene
            GameObject solverObj = GameObject.Find("SolverObject");
            Component namakoSolver = null;

            if (solverObj != null)
            {
                // Try to get NamakoSolver component
                namakoSolver = solverObj.GetComponent(System.Type.GetType("Namako.NamakoSolver, Assembly-CSharp"));
            }

            // If no SolverObject or NamakoSolver found, create one
            if (solverObj == null || namakoSolver == null)
            {
                if (solverObj == null)
                {
                    solverObj = new GameObject("SolverObject");
                    Debug.Log("Created new SolverObject.");
                }

                // Try to add NamakoSolver component
                var solverType = System.Type.GetType("Namako.NamakoSolver, Assembly-CSharp");
                if (solverType != null)
                {
                    // Use CreateFromTool method if available
                    var createFromToolMethod = solverType.GetMethod("CreateFromTool", 
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                    
                    if (createFromToolMethod != null)
                    {
                        namakoSolver = createFromToolMethod.Invoke(null, new object[] { solverObj }) as Component;
                        if (namakoSolver != null)
                        {
                            Debug.Log("NamakoSolver created using CreateFromTool method.");
                        }
                    }
                    else
                    {
                        // Fallback: direct component addition
                        namakoSolver = solverObj.AddComponent(solverType);
                        Debug.Log("NamakoSolver added directly as component.");
                    }
                }
                else
                {
                    Debug.LogError("NamakoSolver type not found. Cannot create solver.");
                    return;
                }
            }

            // Link haptic object to NamakoSolver
            if (namakoSolver != null && hapticToolObj != null)
            {
                SerializedObject serializedSolver = new SerializedObject(namakoSolver);
                SerializedProperty hapticsObjectProperty = serializedSolver.FindProperty("hapticsObject");
                
                if (hapticsObjectProperty != null)
                {
                    hapticsObjectProperty.objectReferenceValue = hapticToolObj;
                    serializedSolver.ApplyModifiedProperties();
                    Debug.Log($"Linked {hapticInterfaceObjName} to NamakoSolver's hapticsObject field.");
                }
                else
                {
                    Debug.LogWarning("hapticsObject property not found in NamakoSolver.");
                }
            }
        }

        void DrawHapticObjectsInfo()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Check current status
            GameObject currentHapticTool = GameObject.Find(hapticInterfaceObjName);
            GameObject currentInput = null;
            GameObject currentProxy = null;

            if (currentHapticTool != null)
            {
                Transform inputTransform = currentHapticTool.transform.Find(inputObjName);
                Transform proxyTransform = currentHapticTool.transform.Find(proxyObjName);
                currentInput = inputTransform != null ? inputTransform.gameObject : null;
                currentProxy = proxyTransform != null ? proxyTransform.gameObject : null;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("HapticInterfaceObject:", GUILayout.Width(140));
            EditorGUILayout.LabelField(currentHapticTool != null ? "✓ Present" : "✗ Missing", 
                currentHapticTool != null ? EditorStyles.miniLabel : EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Input Object:", GUILayout.Width(140));
            EditorGUILayout.LabelField(currentInput != null ? "✓ Present" : "✗ Missing", 
                currentInput != null ? EditorStyles.miniLabel : EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Proxy Object:", GUILayout.Width(140));
            EditorGUILayout.LabelField(currentProxy != null ? "✓ Present" : "✗ Missing",
                currentProxy != null ? EditorStyles.miniLabel : EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndHorizontal();

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
