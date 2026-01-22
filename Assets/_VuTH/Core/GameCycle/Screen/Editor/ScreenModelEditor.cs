using Common.Scene;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core.GameCycle.Screen.Editor
{
    [CustomEditor(typeof(ScreenModel))]
    public class ScreenModelEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // 1. Vẽ Inspector mặc định của ScriptableObject trước
            base.OnInspectorGUI();

            var screenModel = (ScreenModel)target;

            GUILayout.Space(20);
            EditorGUILayout.LabelField("Validation Status", EditorStyles.boldLabel);

            // 2. Kiểm tra xem ScriptableObject có main scene không
            if (screenModel.mainSceneRef == null)
            {
                EditorGUILayout.HelpBox("Screen Model does NOT have a Main Scene assigned.", MessageType.Warning);
                return;
            }
            
            if (screenModel.mainSceneRef.editorAsset == null)
            {
                EditorGUILayout.HelpBox("Screen Model's Main Scene reference is broken or missing.", MessageType.Error);
                return;
            }
            
            // 3. Kiểm tra xem scene có đang mở không
            if (EditorSceneUtil.IsSceneOpen(screenModel.mainSceneRef))
            {
                EditorGUILayout.HelpBox($"Main Scene '{screenModel.mainSceneRef.editorAsset.name}' is currently OPEN in the Editor.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox($"Main Scene '{screenModel.mainSceneRef.editorAsset.name}' is NOT open. Please open it to validate MetaData.", MessageType.Warning);
                if (GUILayout.Button("Open Main Scene"))
                {
                    EditorSceneUtil.OpenSceneInEditor(screenModel.mainSceneRef, OpenSceneMode.Single);
                }
                return;
            }
            
            // So we only validate the *currently open* scene.
            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || string.IsNullOrEmpty(activeScene.path))
            {
                EditorGUILayout.HelpBox("Cannot validate MetaData because no scene is currently open.", MessageType.Info);
                return;
            }

            // 4. Scene đã mở -> Tiến hành Validate logic
            ValidateActiveScene(screenModel);
        }

        private void ValidateActiveScene(ScreenModel currentModel)
        {
            // Tìm ScreenMetaData trong scene (bao gồm cả object đang bị disable)
            // FindFirstObjectByType là API mới của Unity, thay thế FindObjectOfType
            var metaData = FindFirstObjectByType<ScreenMetaData>(FindObjectsInactive.Include);

            if (!metaData)
            {
                // TRƯỜNG HỢP 1: Chưa có object ScreenMetaData -> BÁO LỖI ĐỎ
                EditorGUILayout.HelpBox($"[Missing] Scene '{SceneManager.GetActiveScene().name}' does NOT have a [ScreenMetaData] object!", MessageType.Error);

                GUI.backgroundColor = new Color(1f, 0.5f, 0.5f); // Màu đỏ nhạt
                if (GUILayout.Button("Auto Create ScreenMetaData"))
                {
                    CreateMetaData(currentModel);
                }
                GUI.backgroundColor = Color.white;
            }
            else
            {
                // Kiểm tra xem nó có nằm ở root không (Optional - nhưng khuyên dùng)
                if (metaData.transform.parent)
                {
                    EditorGUILayout.HelpBox("ScreenMetaData should be a Root GameObject (not a child).", MessageType.Warning);
                }

                // TRƯỜNG HỢP 2: Có object nhưng Model bị sai/trống -> BÁO LỖI VÀNG
                if (metaData.model != currentModel)
                {
                    EditorGUILayout.HelpBox($"[Mismatch] MetaData found but referencing wrong Model:\n" +
                                            $"Found: {(metaData.model ? metaData.model.name : "None")}\n" +
                                            $"Expected: {currentModel.name}", MessageType.Error);

                    GUI.backgroundColor = Color.yellow;
                    if (GUILayout.Button("Fix: Assign Current Model to MetaData"))
                    {
                        Undo.RecordObject(metaData, "Fix Screen Model Ref");
                        metaData.model = currentModel;
                        // Đánh dấu object đã thay đổi để Unity cho phép Save Scene
                        EditorUtility.SetDirty(metaData);
                    }
                    GUI.backgroundColor = Color.white;
                }
                else
                {
                    // TRƯỜNG HỢP 3: Mọi thứ OK -> BÁO XANH
                    GUI.backgroundColor = Color.green;
                    EditorGUILayout.HelpBox("OK: ScreenMetaData is correctly setup.", MessageType.Info);
                    GUI.backgroundColor = Color.white;
                    
                    if (GUILayout.Button("Select MetaData Object"))
                    {
                        Selection.activeGameObject = metaData.gameObject;
                        EditorGUIUtility.PingObject(metaData.gameObject);
                    }
                }
            }
        }

        private void CreateMetaData(ScreenModel model)
        {
            // Tạo GameObject mới
            var go = new GameObject("ScreenMetaData");
            
            // Thêm component
            var meta = go.AddComponent<ScreenMetaData>();
            
            // Gán Model hiện tại vào
            meta.model = model;
            
            // Đưa lên đầu Hierarchy (Sibling Index 0) cho dễ nhìn
            go.transform.SetSiblingIndex(0);
            
            // Đăng ký Undo để lỡ user bấm nhầm có thể Ctrl+Z
            Undo.RegisterCreatedObjectUndo(go, "Create ScreenMetaData");
            
            // Chọn object vừa tạo
            Selection.activeGameObject = go;
            
            Debug.Log($"[ScreenModelEditor] Created ScreenMetaData for {model.name}");
        }
    }
}