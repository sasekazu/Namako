using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Namako
{
    public static class NamakoMenuCommands
    {
        // Tools
        [MenuItem("Namako/Tools/Namako Mesh Tool")]
        public static void ShowMeshTool()
        {
            NamakoMeshTool window = EditorWindow.GetWindow<NamakoMeshTool>();
            window.minSize = new Vector2(350, 600);
            window.titleContent = new GUIContent("Namako Mesh Tool");
        }

        [MenuItem("Namako/Tools/Namako Haptics Tool")]
        public static void ShowHapticsTool()
        {
            NamakoHapticsTool window = EditorWindow.GetWindow<NamakoHapticsTool>();
            window.titleContent = new GUIContent("Namako Haptics Tool");
        }

        // Quick Actions
        [MenuItem("Namako/Quick Actions/Clean Solver, Meshes and Haptics")]
        public static void CleanAllMeshes()
        {
            if (EditorUtility.DisplayDialog("Confirm Clean All", 
                "Are you sure you want to clean all objects with NamakoSolver, NamakoTetraMesh, and NamakoHaptics components and their generated files? This action cannot be undone.", 
                "Yes", "Cancel"))
            {
                // 各ツールのコンポーネントベースCleanメソッドを呼び出し
                NamakoMeshTool.CleanMeshObjects();
                CleanSolverObjects();
                NamakoHapticsTool.CleanHapticObjects();
                NamakoMeshTool.CleanGeneratedFiles();
                
                // シーンビューを更新
                SceneView.RepaintAll();
                
                Debug.Log("All objects with Namako components and generated files have been cleaned.");
            }
        }

        // Helper methods
        private static void CleanSolverObjects()
        {
            int cleanedCount = 0;

            // NamakoSolverコンポーネントを持つオブジェクトを検索・削除
            System.Type namakoSolverType = System.Type.GetType("Namako.NamakoSolver, Assembly-CSharp");
            if (namakoSolverType != null)
            {
                UnityEngine.Object[] solverComponents = UnityEngine.Object.FindObjectsOfType(namakoSolverType);
                foreach (UnityEngine.Object solver in solverComponents)
                {
                    if (solver != null && solver is Component component)
                    {
                        GameObject solverObj = component.gameObject;
                        Debug.Log($"Removing object with NamakoSolver: {solverObj.name}");
                        UnityEngine.Object.DestroyImmediate(solverObj);
                        cleanedCount++;
                    }
                }
            }

            // フォールバック: 特定の名前のオブジェクトも削除
            GameObject solverObject = GameObject.Find("SolverObject");
            if (solverObject != null)
            {
                Debug.Log($"Removing solver object by name: SolverObject");
                UnityEngine.Object.DestroyImmediate(solverObject);
                cleanedCount++;
            }

            Debug.Log($"Solver cleanup completed. {cleanedCount} solver objects removed.");
        }
    }
}
