using UnityEngine;

namespace Common.SharedLib
{
    /// <summary>
    /// Singleton base class cho MonoBehaviour với interface
    /// </summary>
    /// <typeparam name="T"> class kế thừa Singleton </typeparam>
    /// <typeparam name="TI"> interface mà Singleton sẽ triển khai </typeparam>
    public abstract class VSingleton<T, TI> : MonoBehaviour where T : MonoBehaviour, TI
    {
        private static T _instance;
        
        public static TI Instance => _instance;
        
        public static bool HasInstance => _instance != null;
        
        protected virtual void Awake()
        {
            var self = this as T;
            
            if (_instance == null)
            {
                _instance = self; 
                DontDestroyOnLoad(gameObject);
            }
            else if (!ReferenceEquals(_instance, self))
            {
                // A second instance was created; destroy it
                Destroy(gameObject);
            }
        }
        
        protected virtual void OnDestroy()
        {
            if (ReferenceEquals(_instance, this))
            {
                _instance = null;
            }
        }
    }
}