using System;
using UnityEngine;

namespace vietlabs.fr2
{
    [Serializable] internal class FR2_IDRef
    {
        [SerializeField] internal FR2_ID fromId;
        [SerializeField] internal FR2_ID toId;
        
        // public string type; // The class that reference this asset
        // public string path; // The property path that the class used to reference to asset
        // public bool isWeak; // Weak: Addressable / Atlas

        public override string ToString()
        {
            return $"{fromId.ToString()} -> {toId.ToString()}";
        }
    }
}
