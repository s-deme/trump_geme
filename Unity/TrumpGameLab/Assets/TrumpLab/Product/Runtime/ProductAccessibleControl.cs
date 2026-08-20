#nullable enable

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TrumpLab.Product
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Selectable))]
    public sealed class ProductAccessibleControl : MonoBehaviour, ISelectHandler,
        IDeselectHandler
    {
        public const float MinimumReferenceHitSize = 44f;
        private static readonly Vector2 FocusOutlineDistance = new Vector2(3f, -3f);

        [SerializeField] private Selectable? selectable;
        [SerializeField] private Outline? focusOutline;
        [SerializeField] private string labelKey = string.Empty;
        [SerializeField] private bool participatesInNavigation = true;
        [SerializeField] private bool configured;

        private string? runtimeLabelKey;
        private object[] runtimeLabelArguments = Array.Empty<object>();
        private int labelRevision;

        public Selectable Control => selectable ?? throw Missing(nameof(selectable));
        public Outline FocusOutline => focusOutline ?? throw Missing(nameof(focusOutline));
        public string LabelKey => configured ? labelKey : throw Missing(nameof(labelKey));
        public bool ParticipatesInNavigation => participatesInNavigation;
        public string ResolvedLabel { get; private set; } = string.Empty;
        public bool IsFocusVisible => focusOutline != null && focusOutline.enabled;
        internal bool IsConfigured => configured;
        internal int LabelRevision => labelRevision;

        public Vector2 ReferenceHitSize
        {
            get
            {
                RectTransform rect = Control.transform as RectTransform ??
                    throw new InvalidOperationException(
                        "An accessible control requires a RectTransform.");
                return new Vector2(Mathf.Abs(rect.rect.width), Mathf.Abs(rect.rect.height));
            }
        }

        public bool HasMinimumReferenceHitTarget
        {
            get
            {
                Vector2 size = ReferenceHitSize;
                return size.x >= MinimumReferenceHitSize &&
                    size.y >= MinimumReferenceHitSize;
            }
        }

        public void Configure(Selectable configuredControl, string stableLabelKey,
            bool participatesInNavigation = true)
        {
            if (configuredControl == null)
                throw new ArgumentNullException(nameof(configuredControl));
            if (configuredControl.gameObject != gameObject)
                throw new ArgumentException(
                    "The accessible control must configure the Selectable on its own object.",
                    nameof(configuredControl));
            string key = RequireLabelKey(stableLabelKey);
            if (configured && (selectable != configuredControl ||
                !string.Equals(labelKey, key, StringComparison.Ordinal) ||
                this.participatesInNavigation != participatesInNavigation))
                throw new InvalidOperationException(
                    "An accessible control's selectable, label, and navigation participation " +
                    "are immutable.");
            selectable = configuredControl;
            labelKey = key;
            this.participatesInNavigation = participatesInNavigation;
            configured = true;
            Outline outline = EnsureOutline();
            if (!participatesInNavigation) outline.enabled = false;
        }

        public void SetRuntimeLabel(string key, params object[] args)
        {
            string validatedKey = RequireLabelKey(key);
            if (args == null) throw new ArgumentNullException(nameof(args));
            int expectedArgumentCount =
                ProductTextCatalog.Entry(validatedKey).ArgumentCount;
            if (args.Length != expectedArgumentCount)
                throw new ArgumentException(
                    "Text key '" + validatedKey + "' requires " +
                    expectedArgumentCount + " arguments, but received " +
                    args.Length + ".", nameof(args));
            if (string.Equals(runtimeLabelKey, validatedKey, StringComparison.Ordinal) &&
                ArgumentsEqual(runtimeLabelArguments, args)) return;

            object[] copiedArguments = args.Length == 0
                ? Array.Empty<object>()
                : new object[args.Length];
            if (args.Length > 0)
                Array.Copy(args, copiedArguments, args.Length);
            runtimeLabelKey = validatedKey;
            runtimeLabelArguments = copiedArguments;
            unchecked { labelRevision++; }
        }

        public void ClearRuntimeLabel()
        {
            if (runtimeLabelKey == null && runtimeLabelArguments.Length == 0) return;
            runtimeLabelKey = null;
            runtimeLabelArguments = Array.Empty<object>();
            unchecked { labelRevision++; }
        }

        public void Apply(IProductText text, ProductUiPalette palette)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (palette == null) throw new ArgumentNullException(nameof(palette));
            string resolvedKey = runtimeLabelKey ?? LabelKey;
            ResolvedLabel = text.Get(resolvedKey, runtimeLabelArguments);
            if (string.IsNullOrWhiteSpace(ResolvedLabel))
                throw new InvalidOperationException(
                    "An accessible control resolved to an empty label: " + resolvedKey);

            if (!participatesInNavigation)
            {
                if (focusOutline != null) focusOutline.enabled = false;
                return;
            }

            Outline outline = EnsureOutline();
            outline.effectColor = palette.FocusIndicator;
            outline.effectDistance = FocusOutlineDistance;
            outline.useGraphicAlpha = false;
            ApplyControlColors(palette);
            SetFocusVisible(EventSystem.current?.currentSelectedGameObject == gameObject);
        }

        public void OnSelect(BaseEventData eventData)
        {
            SetFocusVisible(true);
            EnsureVisibleInAncestorScrollRect();
        }
        public void OnDeselect(BaseEventData eventData) => SetFocusVisible(false);

        public void SetFocusVisible(bool visible)
        {
            if (!visible || !configured || !participatesInNavigation ||
                !isActiveAndEnabled || !Control.IsActive() || !Control.IsInteractable())
            {
                if (focusOutline != null) focusOutline.enabled = false;
                return;
            }
            Outline outline = EnsureOutline();
            outline.enabled = true;
        }

        private void Awake()
        {
            if (selectable == null) selectable = GetComponent<Selectable>();
            if (configured) EnsureOutline();
        }

        private void OnDisable()
        {
            if (focusOutline != null) focusOutline.enabled = false;
        }

        private void EnsureVisibleInAncestorScrollRect()
        {
            if (!(transform is RectTransform item)) return;
            ScrollRect? scroll = GetComponentInParent<ScrollRect>(includeInactive: false);
            RectTransform? content = scroll?.content;
            RectTransform? viewport = scroll?.viewport;
            if (scroll == null || content == null || viewport == null) return;

            Canvas.ForceUpdateCanvases();
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                viewport, item);
            Rect visible = viewport.rect;
            Vector2 delta = Vector2.zero;
            if (scroll.horizontal)
            {
                if (bounds.min.x < visible.xMin) delta.x = bounds.min.x - visible.xMin;
                else if (bounds.max.x > visible.xMax) delta.x = bounds.max.x - visible.xMax;
            }
            if (scroll.vertical)
            {
                if (bounds.min.y < visible.yMin) delta.y = bounds.min.y - visible.yMin;
                else if (bounds.max.y > visible.yMax) delta.y = bounds.max.y - visible.yMax;
            }
            if (delta.sqrMagnitude <= 0.0001f) return;

            scroll.StopMovement();
            content.anchoredPosition -= delta;
            Canvas.ForceUpdateCanvases();
        }

        private Outline EnsureOutline()
        {
            if (focusOutline == null)
            {
                Graphic graphic = Control.targetGraphic ?? GetComponent<Graphic>() ??
                    throw new InvalidOperationException(
                        "An accessible control requires a target Graphic for its focus outline.");
                focusOutline = graphic.GetComponent<Outline>() ??
                    graphic.gameObject.AddComponent<Outline>();
            }
            focusOutline.effectDistance = FocusOutlineDistance;
            focusOutline.useGraphicAlpha = false;
            return focusOutline;
        }

        private void ApplyControlColors(ProductUiPalette palette)
        {
            // uGUI multiplies the Graphic base color by ColorBlock tint. Accessible
            // controls therefore keep a neutral base and make ColorBlock the sole
            // semantic color source; otherwise the palette would be darkened twice.
            if (Control.transition == Selectable.Transition.ColorTint &&
                Control.targetGraphic != null)
                Control.targetGraphic.color = Color.white;
            ColorBlock colors = Control.colors;
            colors.normalColor = palette.ControlBackground;
            colors.highlightedColor = palette.ActiveControlBackground;
            colors.pressedColor = palette.ActiveControlBackground;
            colors.selectedColor = palette.ActiveControlBackground;
            colors.disabledColor = palette.DisabledControlBackground;
            colors.colorMultiplier = 1f;
            Control.colors = colors;
        }

        private static string RequireLabelKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException(
                    "An accessible label key cannot be empty.", nameof(key));
            return key;
        }

        private static bool ArgumentsEqual(object[] left, object[] right)
        {
            if (left.Length != right.Length) return false;
            for (int index = 0; index < left.Length; index++)
            {
                if (!Equals(left[index], right[index])) return false;
            }
            return true;
        }

        private static InvalidOperationException Missing(string name) =>
            new InvalidOperationException(
                "Product accessible control is not configured: " + name);
    }
}
