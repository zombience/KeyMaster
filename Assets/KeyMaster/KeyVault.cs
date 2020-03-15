using UnityEngine;

namespace KeyMaster
{
    /// <summary>
    /// store <see cref="KeyToken"/> objects in a serialized way so that
    /// viewing key map doesn't need to scan all assemblies to build map each time which is slow
    /// </summary>
    public class KeyVault : ScriptableObject
    {
        [HideInInspector]
        public KeyToken[] tokens;

        [SerializeField, HideInInspector]
        bool allowRemote;

        [SerializeField, HideInInspector]
        KeyMasterRemoteConfig remoteConfig = new KeyMasterRemoteConfig()
        {
            port = 9001,
        };

        public KeyMasterRemoteConfig GetConfig()
        {
            return remoteConfig;
        }
    }
}
