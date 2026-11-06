namespace _VuTH.Core.Pool
{
    public interface IPoolable
    {
        // Called when taken out of the pool (instead of Start / OnEnable)
        void OnSpawn(); 
    
        // Called when returned to the pool (instead of OnDisable / OnDestroy)
        void OnDespawn();
    }
}