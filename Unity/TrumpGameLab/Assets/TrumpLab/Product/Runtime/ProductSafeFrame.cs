#nullable enable

using System;
using UnityEngine;

namespace TrumpLab.Product
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class ProductSafeFrame : MonoBehaviour
    {
        public const float TargetAspectRatio = 16f / 9f;

        [SerializeField] private RectTransform? parentRect;
        private bool applying;

        public RectTransform Frame => (RectTransform)transform;
        public RectTransform ParentRect => parentRect ?? throw new InvalidOperationException(
            "Product safe frame is not configured.");

        public void Configure(RectTransform configuredParent)
        {
            if (configuredParent == null)
                throw new ArgumentNullException(nameof(configuredParent));
            if (transform.parent != configuredParent)
                throw new ArgumentException(
                    "The safe frame must be a direct child of its configured parent.",
                    nameof(configuredParent));
            parentRect = configuredParent;
            ApplyFrame();
        }

        public void ApplyFrame()
        {
            if (applying) return;
            RectTransform parent = parentRect ?? transform.parent as RectTransform ??
                throw new InvalidOperationException(
                    "Product safe frame requires a RectTransform parent.");
            parentRect = parent;
            float width = Mathf.Abs(parent.rect.width);
            float height = Mathf.Abs(parent.rect.height);
            if (width <= Mathf.Epsilon || height <= Mathf.Epsilon) return;

            applying = true;
            try
            {
                float parentAspect = width / height;
                if (parentAspect >= TargetAspectRatio)
                {
                    float normalizedWidth = (height * TargetAspectRatio) / width;
                    Frame.anchorMin = new Vector2((1f - normalizedWidth) * 0.5f, 0f);
                    Frame.anchorMax = new Vector2((1f + normalizedWidth) * 0.5f, 1f);
                }
                else
                {
                    float normalizedHeight = (width / TargetAspectRatio) / height;
                    Frame.anchorMin = new Vector2(0f, (1f - normalizedHeight) * 0.5f);
                    Frame.anchorMax = new Vector2(1f, (1f + normalizedHeight) * 0.5f);
                }
                Frame.pivot = new Vector2(0.5f, 0.5f);
                Frame.anchoredPosition = Vector2.zero;
                Frame.sizeDelta = Vector2.zero;
            }
            finally
            {
                applying = false;
            }
        }

        private void OnEnable()
        {
            if (transform.parent is RectTransform parent)
            {
                parentRect = parentRect ?? parent;
                ApplyFrame();
            }
        }

        private void OnRectTransformDimensionsChange()
        {
            if (isActiveAndEnabled && transform.parent is RectTransform) ApplyFrame();
        }
    }
}
