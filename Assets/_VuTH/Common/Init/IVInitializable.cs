using Cysharp.Threading.Tasks;

namespace _VuTH.Common.Init
{
    public interface IVInitializable
    {
        UniTask VInitialize();
    }
}