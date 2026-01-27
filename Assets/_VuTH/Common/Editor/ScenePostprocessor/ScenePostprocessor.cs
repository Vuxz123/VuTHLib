using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace _VuTH.Common.Editor.ScenePostprocessor
{
    public class ScenePostprocessor : AssetPostprocessor
    {
        // Cache danh sách các Task để không phải Reflection nhiều lần
        private static List<ISceneImportTask> _tasks;

        private static void LoadTasks()
        {
            if (_tasks != null) return;
            
            // Dùng Reflection tìm tất cả class implement IScreenImportTask
            var types = TypeCache.GetTypesDerivedFrom<ISceneImportTask>()
                .Where(t => !t.IsAbstract && !t.IsInterface)
                .OrderBy(t => t.Name);

            _tasks = new List<ISceneImportTask>();
            foreach (var type in types)
            {
                _tasks.Add((ISceneImportTask)Activator.CreateInstance(type));
            }
            
            // Sắp xếp theo Order
            _tasks.Sort((a, b) => a.Order.CompareTo(b.Order));
        }

        // Hàm này được Unity gọi tự động mỗi khi có file thay đổi/thêm mới/xóa
        static void OnPostprocessAllAssets(
            string[] importedAssets, 
            string[] deletedAssets, 
            string[] movedAssets, 
            string[] movedFromAssetPaths)
        {
            // 1. Lọc ra các file Scene (.unity) vừa được import
            var changedScenePaths = importedAssets.Where(p => p.EndsWith(".unity")).ToList();
            
            if (changedScenePaths.Count == 0) return;

            LoadTasks();

            // 2. Tìm tất cả ScreenModel trong project để đối chiếu
            // (Cách này nhanh hơn LoadAllAssets)
            
            foreach (string scene in changedScenePaths)
            {
                foreach (var task in _tasks)
                {
                    task.OnSceneImported(scene);
                }
            }
        }
    }
}