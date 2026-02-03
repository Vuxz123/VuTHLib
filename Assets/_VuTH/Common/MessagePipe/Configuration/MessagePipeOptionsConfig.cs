using UnityEngine;
using UnityEngine.Serialization;

namespace _VuTH.Common.MessagePipe.Configuration
{
    /// <summary>
    /// Configuration asset for MessagePipeOptions.
    /// Allows runtime configuration of MessagePipe behavior without code changes.
    /// </summary>
    [CreateAssetMenu(fileName = "MessagePipeOptionsConfig", menuName = "VuTH/MessagePipe/Options Config")]
    public class MessagePipeOptionsConfig : ScriptableObject
    {
        [Header("MessagePipe Options")]
        [Tooltip("Enable capturing stack traces for debugging. May have performance impact.")]
        [SerializeField] public bool enableCaptureStackTrace = false;

        [Tooltip("Preserve the generated MessagePipeRegistrar class from code stripping.")]
        [SerializeField] public bool preserveRegistrar = true;
    }
}
