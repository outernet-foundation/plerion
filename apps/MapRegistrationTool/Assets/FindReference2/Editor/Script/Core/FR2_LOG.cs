using UnityEngine;

namespace vietlabs.fr2
{
    internal static class FR2_LOG
    {
        [HideInCallstack]
        public static void Log(object message)
        {
#if FR2_DEBUG || FR2_DEV
            UnityEngine.Debug.Log(message);
#endif
        }

        [HideInCallstack]
        public static void Log(object message, UnityEngine.Object context)
        {
#if FR2_DEBUG || FR2_DEV
            UnityEngine.Debug.Log(message, context);
#endif
        }

        [HideInCallstack]
        public static void LogWarning(object message)
        {
#if FR2_DEBUG || FR2_DEV
            UnityEngine.Debug.LogWarning(message);
#endif
        }

        [HideInCallstack]
        public static void LogWarning(object message, UnityEngine.Object context)
        {
#if FR2_DEBUG || FR2_DEV
            UnityEngine.Debug.LogWarning(message, context);
#endif
        }

        
        [HideInCallstack]
        public static void LogError(object message)
        {
#if FR2_DEBUG || FR2_DEV
            UnityEngine.Debug.LogError(message);
#endif
        }

        [HideInCallstack]
        public static void LogError(object message, UnityEngine.Object context)
        {
#if FR2_DEBUG || FR2_DEV
            UnityEngine.Debug.LogError(message, context);
#endif
        }
    }
}