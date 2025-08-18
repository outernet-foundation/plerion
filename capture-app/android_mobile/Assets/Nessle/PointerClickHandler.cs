using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Nessle
{
    public class PointerClickHandler : MonoBehaviour, IPointerClickHandler
    {
        public event Action<PointerEventData> onClick;

        public void OnPointerClick(PointerEventData eventData)
        {
            onClick?.Invoke(eventData);
        }
    }
}