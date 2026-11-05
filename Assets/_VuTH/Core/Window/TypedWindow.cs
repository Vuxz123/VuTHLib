using System.Threading;
using _VuTH.Common.Log;
using Cysharp.Threading.Tasks;

namespace _VuTH.Core.Window
{
    /// <summary>
    /// A window declaring the type of its data and of its result, so <see cref="WindowManagerExtensions"/> can check
    /// both at compile time. Implemented by <see cref="PopupBase{TData, TResult}"/> and
    /// <see cref="FullScreenBase{TData, TResult}"/>.
    /// </summary>
    public interface ITypedWindow<in TData, out TResult> : ITypedWindowData<TData>
    {
    }

    /// <summary>A window declaring the type of its data (see <see cref="ITypedWindow{TData, TResult}"/>).</summary>
    public interface ITypedWindowData<in TData>
    {
    }

    public static class WindowManagerExtensions
    {
        /// <summary>
        /// <see cref="IWindowManager.Open{TWindow, TResult}(object, CancellationToken)"/> with the data and result
        /// types checked against the window's.
        /// </summary>
        public static UniTask<TResult> Open<TWindow, TData, TResult>(this IWindowManager manager, TData data,
            CancellationToken cancellationToken = default)
            where TWindow : UIViewBase, ITypedWindow<TData, TResult>
        {
            return manager.Open<TWindow, TResult>(data, cancellationToken);
        }

        /// <summary><see cref="IWindowManager.Show{TWindow}(object)"/> with the data type checked against the window's.</summary>
        public static UniTask<TWindow> Show<TWindow, TData>(this IWindowManager manager, TData data)
            where TWindow : UIViewBase, ITypedWindowData<TData>
        {
            return manager.Show<TWindow>(data);
        }
    }

    internal static class TypedWindowData
    {
        /// <summary>The data as <typeparamref name="TData"/>; default (logged) when it is of another type.</summary>
        public static TData Cast<TData>(UIViewBase window, object data)
        {
            if (data is TData typed) return typed;
            if (data == null) return default;

            window.LogError($"{window.GetType().Name} expects {typeof(TData).Name} data, got {data.GetType().Name}.");
            return default;
        }
    }
}
