// #define FR2_DEBUG


using System;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;


namespace vietlabs.fr2
{
    internal class FR2_SmartLock
    {
        public enum PingLockState
        {
            None,
            Scene,  // Ping/highlight action triggered from scene context
            Asset   // Ping/highlight action triggered from asset context
        }
        
        private PingLockState pingLockState = PingLockState.None;
        public void SetPingLockState(PingLockState state)
        {
            pingLockState = state;
            #if FR2_DEBUG
            if (state != PingLockState.None)
            {
                FR2_LOG.Log($"SmartLock: Set ping lock state to {state}");
            }
            #endif
        }
        
        public bool ConsumePingLockState()
        {
            bool hadPingLock = pingLockState != PingLockState.None;
            if (hadPingLock)
            {
                // FR2_LOG.Log($"SmartLock: Consuming ping lock state {pingLockState}");
                pingLockState = PingLockState.None;
            }
            return hadPingLock;
        }
        
        public bool ShouldRefreshWithSmartLogic(EditorWindow window, UnityObject[] panelSelection = null)
        {
            if (!FR2_SelectionManager.Instance.HasSelection) return false;
            if (ConsumePingLockState()) return false;
            if (panelSelection == null || panelSelection.Length == 0) return true;
            return window != EditorWindow.focusedWindow;
        }
    }
} 