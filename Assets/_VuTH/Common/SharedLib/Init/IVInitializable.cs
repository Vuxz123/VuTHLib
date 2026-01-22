using Cysharp.Threading.Tasks;

namespace Common.Init
{
    public interface IVInitializable
    {
        UniTask VInitialize();
    }
}