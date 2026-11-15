using TerrariaModder.Core.Logging;

namespace ShiftBatch
{
    // A per-mod switch must also open this logger's threshold: Core defaults
    // to Info, which would otherwise discard every ILogger.Debug call.
    public static class DebugLog
    {
        private static ILogger logger;
        private static BatchConfig config;
        private static LogLevel originalLevel;

        public static bool Enabled { get { return logger != null && config != null && config.DebugLogging; } }

        public static void Initialize(ILogger target, BatchConfig settings)
        {
            Unload();
            logger = target;
            config = settings;
            if (logger != null) originalLevel = logger.MinLevel;
            ApplySettings();
        }

        public static void ApplySettings()
        {
            if (logger != null) logger.MinLevel = Enabled ? LogLevel.Debug : originalLevel;
        }

        public static void Write(string message)
        {
            if (Enabled) logger.Debug(message);
        }

        public static void Unload()
        {
            if (logger != null) logger.MinLevel = originalLevel;
            logger = null;
            config = null;
        }
    }
}
