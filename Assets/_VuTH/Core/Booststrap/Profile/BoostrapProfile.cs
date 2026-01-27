using _VuTH.Common;
using UnityEngine;
using ZLinq;

namespace _VuTH.Core.Booststrap.Profile
{
    public class BootstrapProfile : ScriptableObject
    {
        public GameObject[] boostrapPrefabs;

#if UNITY_EDITOR
        public void OnValidate()
        {
            if (boostrapPrefabs == null) return;

            for (var i = 0; i < boostrapPrefabs.Length; i++)
            {
                var boostrapPrefab = boostrapPrefabs[i];

                // 1. Skip nếu slot trống
                if (!boostrapPrefab) continue;

                // 2. Lấy danh sách component
                var components = boostrapPrefab.GetComponents<MonoBehaviour>();

                // 3. Đếm số component khớp với Generic Type 2 tham số
                var count = components.AsValueEnumerable().Count(c => 
                        c && 
                        IsVBoostrapManager(c.GetType().BaseType)
                );

                // 4. Báo lỗi nếu số lượng không phải là 1
                if (count != 1)
                {
                    Debug.LogError($"Bootstrap prefab at index {i} ('{boostrapPrefab.name}') is invalid. It must contain exactly one VBootstrapManager<,> component but found {count}.");
                }
            }
        }
        
        private static bool IsVBoostrapManager(System.Type type)
        {
            while (type != null && type != typeof(object))
            {
                if (type.IsGenericType && 
                    type.GetGenericTypeDefinition() == typeof(VBootstrapManager<,>))
                    return true;
                type = type.BaseType;
            }
            return false;
        }
#endif
    }
}