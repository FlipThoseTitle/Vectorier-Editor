using UnityEngine;

namespace Vectorier.Debug
{
    public static class DebugLog
    {
        // Global toggle for enabling/disabling logs (Except Error and Warn)
        public static bool Enable = true;

        // Info-level logging (normal messages)
        public static void Info(string message)
        {
            if (!Enable) return;
            UnityEngine.Debug.Log($"{message}");
        }

        // Warning messages
        public static void Warn(string message)
        {
            UnityEngine.Debug.LogWarning($"{message}");
        }

        // Error messages
        public static void Error(string message)
        {
            UnityEngine.Debug.LogError($"{message}");
        }

        // Optional: contextual logging (e.x include object reference)
        public static void Info(object context, string message)
        {
            if (!Enable) return;
            UnityEngine.Debug.Log($"{message}", context as Object);
        }

        public static void Error(object context, string message)
        {
            UnityEngine.Debug.LogError($"{message}", context as Object);
        }
    }
}
