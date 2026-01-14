using Cysharp.Threading.Tasks;

namespace Common.SharedLib.Init
{
    public interface IVInitializable
    {
        UniTask VInitialize();
    }
}