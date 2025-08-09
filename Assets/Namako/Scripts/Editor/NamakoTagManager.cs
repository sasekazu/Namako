#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace Namako.Editor
{
    /// <summary>
    /// NamakoRigidBodyタグを自動作成するエディタ拡張
    /// </summary>
    [InitializeOnLoad]
    public static class NamakoTagManager
    {
        private const string NAMAKO_RIGID_BODY_TAG = "NamakoRigidBody";

        // Unity起動時に自動実行
        static NamakoTagManager()
        {
            EditorApplication.delayCall += AutoCreateTag;
        }

        /// <summary>
        /// 自動でタグを作成（起動時用）
        /// </summary>
        private static void AutoCreateTag()
        {
            if (!IsNamakoRigidBodyTagExists())
            {
                CreateNamakoRigidBodyTag();
            }
        }

        /// <summary>
        /// NamakoRigidBodyタグが存在するかチェック
        /// </summary>
        /// <returns>タグが存在する場合はtrue</returns>
        public static bool IsNamakoRigidBodyTagExists()
        {
            string[] tags = UnityEditorInternal.InternalEditorUtility.tags;
            for (int i = 0; i < tags.Length; i++)
            {
                if (tags[i] == NAMAKO_RIGID_BODY_TAG)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// NamakoRigidBodyタグを作成
        /// </summary>
        /// <returns>作成に成功した場合はtrue</returns>
        public static bool CreateNamakoRigidBodyTag()
        {
            if (IsNamakoRigidBodyTagExists())
            {
                return true;
            }

            try
            {
                // TagManagerアセットを取得
                SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
                SerializedProperty tagsProp = tagManager.FindProperty("tags");

                // 空きスロットを探す
                for (int i = 0; i < tagsProp.arraySize; i++)
                {
                    SerializedProperty t = tagsProp.GetArrayElementAtIndex(i);
                    if (string.IsNullOrEmpty(t.stringValue))
                    {
                        t.stringValue = NAMAKO_RIGID_BODY_TAG;
                        tagManager.ApplyModifiedProperties();
                        Debug.Log($"[Namako] Tag '{NAMAKO_RIGID_BODY_TAG}' を自動作成しました。");
                        return true;
                    }
                }

                // 空きスロットがない場合は新しく追加
                tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
                SerializedProperty newTag = tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1);
                newTag.stringValue = NAMAKO_RIGID_BODY_TAG;
                tagManager.ApplyModifiedProperties();

                Debug.Log($"[Namako] Tag '{NAMAKO_RIGID_BODY_TAG}' を自動作成しました。");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Namako] Tag '{NAMAKO_RIGID_BODY_TAG}' の作成に失敗しました: {e.Message}");
                return false;
            }
        }
    }
}
#endif
