namespace _VuTH.Core.Pool
{
    public interface IPoolable
    {
        // Gọi khi lấy ra khỏi pool (thay cho Start/OnEnable)
        void OnSpawn(); 
    
        // Gọi khi trả về pool (thay cho OnDisable/Destroy)
        void OnDespawn();
    }
}