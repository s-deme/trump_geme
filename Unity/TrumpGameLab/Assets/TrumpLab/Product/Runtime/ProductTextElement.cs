#nullable enable

using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace TrumpLab.Product
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Text))]
    public sealed class ProductTextElement : MonoBehaviour
    {
        [SerializeField] private Text? target;
        [SerializeField] private ProductTextContentMode contentMode;
        [SerializeField] private string stableKey = string.Empty;
        [SerializeField] private int baseFontSize;

        public Text Target => target != null ? target : target = GetComponent<Text>();
        public ProductTextContentMode ContentMode => contentMode;
        public string StableKey => stableKey;
        public int BaseFontSize => baseFontSize;

        public void Configure(Text configuredTarget, ProductTextContentMode mode,
            string key, int configuredBaseFontSize = 0)
        {
            if (configuredTarget == null) throw new ArgumentNullException(nameof(configuredTarget));
            if (configuredTarget.gameObject != gameObject)
                throw new ArgumentException(
                    "Product text element must target Text on the same object.",
                    nameof(configuredTarget));
            if (!Enum.IsDefined(typeof(ProductTextContentMode), mode))
                throw new ArgumentOutOfRangeException(nameof(mode));
            string requiredKey = ProductTextCatalog.RequireStableKey(key, nameof(key));
            if (mode == ProductTextContentMode.Static && !ProductTextCatalog.Contains(requiredKey))
                throw new ArgumentException(
                    "Static product text key is not in the catalog: " + requiredKey,
                    nameof(key));
            if (!string.IsNullOrEmpty(stableKey) &&
                (contentMode != mode ||
                    !string.Equals(stableKey, requiredKey, StringComparison.Ordinal)))
                throw new InvalidOperationException(
                    "A product text element's content mode and stable key are immutable once configured.");
            int requestedSize = configuredBaseFontSize > 0
                ? configuredBaseFontSize
                : configuredTarget.fontSize;
            if (requestedSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(configuredBaseFontSize));
            if (baseFontSize > 0 && baseFontSize != requestedSize)
                throw new InvalidOperationException(
                    "A product text element's base font size is immutable once configured.");
            if (target != null && target != configuredTarget)
                throw new InvalidOperationException(
                    "A product text element's Text target is immutable once configured.");

            target = configuredTarget;
            contentMode = mode;
            stableKey = requiredKey;
            baseFontSize = requestedSize;
            if (mode == ProductTextContentMode.Static)
                configuredTarget.text = ProductTextCatalog.English.Get(requiredKey);
        }

        public void Configure(ProductTextContentMode mode, string key,
            int configuredBaseFontSize = 0) =>
            Configure(Target, mode, key, configuredBaseFontSize);

        public void Apply(IProductText text, Font font, int textScalePercent)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (font == null) throw new ArgumentNullException(nameof(font));
            if (!ProductSettings.SupportedTextScalePercents.Contains(textScalePercent))
                throw new ArgumentOutOfRangeException(nameof(textScalePercent));
            EnsureConfigured();
            Target.font = font;
            Target.fontSize = Mathf.Max(1,
                Mathf.RoundToInt(baseFontSize * textScalePercent / 100f));
            if (contentMode == ProductTextContentMode.Static)
                Target.text = text.Get(stableKey);
        }

        private void Awake()
        {
            if (target == null) target = GetComponent<Text>();
            if (baseFontSize <= 0 && target != null) baseFontSize = target.fontSize;
        }

        private void EnsureConfigured()
        {
            if (target == null) target = GetComponent<Text>();
            if (target == null || baseFontSize <= 0 || string.IsNullOrWhiteSpace(stableKey) ||
                !Enum.IsDefined(typeof(ProductTextContentMode), contentMode) ||
                contentMode == ProductTextContentMode.Static &&
                !ProductTextCatalog.Contains(stableKey))
                throw new InvalidOperationException(
                    "Product text element is not configured with a stable key and base size.");
        }
    }
}
