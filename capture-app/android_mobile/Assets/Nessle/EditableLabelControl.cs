using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Nessle
{
    public class EditableLabelControl : MonoBehaviour, IPointerClickHandler
    {
        public TextMeshProUGUI label => _label;
        public TMP_InputField inputField => _inputField;
        public Image background => _background;
        public TextMeshProUGUI placeholder => _placeholder;

        [SerializeField]
        private TextMeshProUGUI _label;

        [SerializeField]
        private TMP_InputField _inputField;

        [SerializeField]
        private Image _background;

        [SerializeField]
        private TextMeshProUGUI _placeholder;

        private void Awake()
        {
            _inputField.onDeselect.AddListener(_ =>
            {
                _inputField.gameObject.SetActive(false);
                _label.gameObject.SetActive(true);
            });

            _inputField.onEndEdit.AddListener(_ =>
            {
                _inputField.gameObject.SetActive(false);
                _label.gameObject.SetActive(true);
            });

            _inputField.onValueChanged.AddListener(x => label.text = x);
            _inputField.gameObject.SetActive(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.clickCount > 1)
            {
                _inputField.gameObject.SetActive(true);
                _label.gameObject.SetActive(false);

                EventSystem.current.SetSelectedGameObject(_inputField.gameObject);
            }
        }
    }
}