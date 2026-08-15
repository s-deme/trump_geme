using UnityEngine;

namespace TrumpLab.Product
{
    public abstract class ProductScreen : MonoBehaviour
    {
        public abstract ScreenId Id { get; }
        public bool IsVisible => gameObject.activeSelf;

        public void SetVisible(bool visible) => gameObject.SetActive(visible);
    }
}
