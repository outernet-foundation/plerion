using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Nessle
{
    public class SelectionHelper : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        public event Action onWasSelected;
        public event Action onWasDeselected;

        public void OnSelect(BaseEventData eventData)
        {
            onWasSelected?.Invoke();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            onWasDeselected?.Invoke();
        }
    }
}