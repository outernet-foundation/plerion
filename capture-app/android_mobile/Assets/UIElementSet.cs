using Nessle;
using UnityEngine;
using UnityEngine.UI;

namespace PlerionClient.Client
{
    [CreateAssetMenu(fileName = "UIElementSet", menuName = "Scriptable Objects/UIElementSet")]
    public class UIElementSet : ScriptableObject
    {
        public Color foregroundColor => _foregroundColor;
        public Color midgroundColor => _midgroundColor;
        public Color backgroundColor => _backgroundColor;
        public Color deepBackgroundColor => _deepBackgroundColor;
        public Color highlightColor => _highlightColor;
        public Sprite roundedRect => _roundedRect;
        public Sprite moreMenuSprite => _moreMenuSprite;
        public PrimitiveControl<ToggleProps> recordButton => _recordButton;
        public PrimitiveControl<ToggleProps> playButton => _playButton;
        public PrimitiveControl<FoldoutProps> foldout => _foldout;
        public PrimitiveControl<ButtonProps> roundButton => _roundButton;

        [SerializeField]
        private Color _foregroundColor;

        [SerializeField]
        private Color _midgroundColor;

        [SerializeField]
        private Color _backgroundColor;

        [SerializeField]
        private Color _deepBackgroundColor;

        [SerializeField]
        private Color _highlightColor;

        [SerializeField]
        private Sprite _roundedRect;

        [SerializeField]
        private Sprite _moreMenuSprite;

        [SerializeField]
        private PrimitiveControl<ToggleProps> _recordButton;

        [SerializeField]
        private PrimitiveControl<ToggleProps> _playButton;

        [SerializeField]
        private PrimitiveControl<FoldoutProps> _foldout;

        [SerializeField]
        private PrimitiveControl<ButtonProps> _roundButton;
    }
}