using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace Nessle
{
    [CreateAssetMenu(fileName = "UIResources", menuName = "Scriptable Objects/UIResources")]
    public class UIResources : ScriptableObject
    {
        public static Sprite RoundedBox => _instance._roundedBox;

        private static UIResources _instance => Resources.Load<UIResources>("UIResources");

        [SerializeField] private Sprite _roundedBox;
    }
}