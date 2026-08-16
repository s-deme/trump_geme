using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TrumpLab.Product
{
    public abstract class ProductScreen : MonoBehaviour, ICancelHandler
    {
        public abstract ScreenId Id { get; }
        public bool IsVisible => gameObject.activeSelf;
        public event System.Action? CancelRequested;

        public void SetVisible(bool visible) => gameObject.SetActive(visible);

        public virtual void OnCancel(BaseEventData eventData)
        {
            if (eventData == null) throw new ArgumentNullException(nameof(eventData));
            if (IsVisible) CancelRequested?.Invoke();
        }
    }
}
