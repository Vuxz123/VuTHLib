namespace Core.GameCycle.Screen
{
    /// <summary>
    /// Extra metadata describing the origin/why of a transition.
    /// Keep this lightweight and optional; do not encode lifecycle into TransitionKind.
    /// </summary>
    public readonly struct TransitionContext
    {
        public TransitionContext(string source, string reason = null)
        {
            Source = source;
            Reason = reason;
        }

        /// <summary>
        /// A short identifier of the transition source (e.g. "bootstrap", "screenflow", "ui", "debug").
        /// </summary>
        public string Source { get; }

        /// <summary>
        /// Optional human-readable reason.
        /// </summary>
        public string Reason { get; }

        public override string ToString()
        {
            if (string.IsNullOrWhiteSpace(Reason)) return Source;
            return $"{Source}:{Reason}";
        }

        public static TransitionContext Default => new("default");
    }
}

