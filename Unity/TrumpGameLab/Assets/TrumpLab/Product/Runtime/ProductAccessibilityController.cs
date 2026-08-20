#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TrumpLab.Product
{
    [DisallowMultipleComponent]
    public sealed class ProductAccessibilityController : MonoBehaviour,
        IProductSettingsApplier
    {
        private const ulong SignatureOffset = 14695981039346656037UL;
        private const ulong SignaturePrime = 1099511628211UL;
        private const float DropdownItemHeight = 60f;
        private const float DropdownFallbackWidth = 620f;

        [SerializeField] private RectTransform? uiRoot;
        [SerializeField] private ProductSafeFrame? safeFrame;
        [SerializeField] private MonoBehaviour? textBehaviour;

        private IProductText? configuredText;
        private readonly List<Selectable> signatureSelectables =
            new List<Selectable>(256);
        private readonly List<Transform> transientTransforms =
            new List<Transform>(256);
        private readonly List<Toggle> transientDropdownItems =
            new List<Toggle>(32);
        private readonly Vector3[] worldCorners = new Vector3[4];
        private HierarchySignature hierarchySignature;
        private bool hasHierarchySignature;
        private bool refreshingNavigation;

        public RectTransform UiRoot => uiRoot ?? throw Missing(nameof(uiRoot));
        public ProductSafeFrame SafeFrame => safeFrame ?? throw Missing(nameof(safeFrame));
        public IProductText Text => configuredText ??
            (textBehaviour as IProductText) ?? throw Missing(nameof(textBehaviour));
        public ProductUiPalette CurrentPalette { get; private set; } = ProductUiPalette.Normal;

        public void Configure(RectTransform configuredRoot, ProductSafeFrame configuredSafeFrame,
            IProductText text)
        {
            uiRoot = configuredRoot ?? throw new ArgumentNullException(nameof(configuredRoot));
            safeFrame = configuredSafeFrame ??
                throw new ArgumentNullException(nameof(configuredSafeFrame));
            configuredText = text ?? throw new ArgumentNullException(nameof(text));
            textBehaviour = text as MonoBehaviour;
            NormalizeTransientDropdowns();
            SynchronizeHierarchySignature();
        }

        public void Apply(ProductSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            ValidateConfiguration();
            CurrentPalette = settings.HighContrast
                ? ProductUiPalette.HighContrast
                : ProductUiPalette.Normal;
            SafeFrame.ApplyFrame();
            RefreshNavigation();
        }

        public void RefreshNavigation()
        {
            if (uiRoot == null) throw Missing(nameof(uiRoot));
            if (refreshingNavigation) return;
            refreshingNavigation = true;
            try
            {
                NormalizeTransientDropdowns();
                RefreshSemanticElements();
                RebuildActiveLayouts();
                Selectable[] all =
                    UiRoot.GetComponentsInChildren<Selectable>(includeInactive: true);
                Transform? navigationScope = ActiveNavigationScope();
                var nodes = all.Where(selectable => IsEligible(selectable) &&
                        IsInsideNavigationScope(selectable.transform, navigationScope))
                    .Select(selectable => new NavigationNode(
                        selectable, CenterInRoot(selectable)))
                    .OrderByDescending(node => node.Center.y)
                    .ThenBy(node => node.Center.x)
                    .ThenBy(node => node.Selectable.GetInstanceID())
                    .ToArray();

                var eligible = new HashSet<Selectable>(
                    nodes.Select(node => node.Selectable));
                foreach (Selectable selectable in all)
                {
                    if (eligible.Contains(selectable)) continue;
                    Navigation excluded = selectable.navigation;
                    excluded.mode = Navigation.Mode.None;
                    excluded.selectOnUp = null;
                    excluded.selectOnDown = null;
                    excluded.selectOnLeft = null;
                    excluded.selectOnRight = null;
                    selectable.navigation = excluded;
                }

                foreach (NavigationNode node in nodes)
                {
                    Navigation navigation = node.Selectable.navigation;
                    navigation.mode = nodes.Length == 1
                        ? Navigation.Mode.None
                        : Navigation.Mode.Explicit;
                    navigation.wrapAround = false;
                    navigation.selectOnUp = nodes.Length == 1
                        ? null : FindNeighbor(node, nodes, Vector2.up);
                    navigation.selectOnDown = nodes.Length == 1
                        ? null : FindNeighbor(node, nodes, Vector2.down);
                    navigation.selectOnLeft = nodes.Length == 1
                        ? null : FindNeighbor(node, nodes, Vector2.left);
                    navigation.selectOnRight = nodes.Length == 1
                        ? null : FindNeighbor(node, nodes, Vector2.right);
                    node.Selectable.navigation = navigation;
                }

                RestoreVisibleFocus(nodes);
                SynchronizeHierarchySignature();
            }
            finally
            {
                refreshingNavigation = false;
            }
        }

        private void LateUpdate()
        {
            if (uiRoot == null || safeFrame == null || refreshingNavigation ||
                configuredText == null && !(textBehaviour is IProductText)) return;
            HierarchySignature current = CaptureHierarchySignature();
            if (hasHierarchySignature && hierarchySignature.Equals(current)) return;
            RefreshNavigation();
        }

        private HierarchySignature CaptureHierarchySignature()
        {
            signatureSelectables.Clear();
            UiRoot.GetComponentsInChildren(includeInactive: true, signatureSelectables);
            ulong hash = SignatureOffset;
            Mix(ref hash, signatureSelectables.Count);
            foreach (Selectable selectable in signatureSelectables)
            {
                Transform controlTransform = selectable.transform;
                ProductAccessibleControl? semantic =
                    selectable.GetComponent<ProductAccessibleControl>();
                Mix(ref hash, selectable.GetInstanceID());
                Mix(ref hash, selectable.enabled);
                Mix(ref hash, selectable.gameObject.activeSelf);
                Mix(ref hash, selectable.gameObject.activeInHierarchy);
                Mix(ref hash, selectable.interactable);
                Mix(ref hash, selectable.IsActive());
                Mix(ref hash, selectable.IsInteractable());
                Mix(ref hash, semantic != null);
                if (semantic != null)
                {
                    Mix(ref hash, semantic.GetInstanceID());
                    Mix(ref hash, semantic.enabled);
                    Mix(ref hash, semantic.IsConfigured);
                    Mix(ref hash, semantic.ParticipatesInNavigation);
                    Mix(ref hash, semantic.LabelRevision);
                }
                MixHierarchy(ref hash, controlTransform);
                MixGeometry(ref hash, controlTransform);
            }
            return new HierarchySignature(signatureSelectables.Count, hash);
        }

        private void MixHierarchy(ref ulong hash, Transform controlTransform)
        {
            Transform? current = controlTransform;
            while (current != null)
            {
                Mix(ref hash, current.GetInstanceID());
                Mix(ref hash, current.GetSiblingIndex());
                Mix(ref hash, current.childCount);
                if (current == UiRoot) break;
                current = current.parent;
            }
        }

        private void MixGeometry(ref ulong hash, Transform controlTransform)
        {
            Vector3 position = controlTransform.position;
            Quaternion rotation = controlTransform.rotation;
            Vector3 scale = controlTransform.lossyScale;
            Mix(ref hash, position.x);
            Mix(ref hash, position.y);
            Mix(ref hash, position.z);
            Mix(ref hash, rotation.x);
            Mix(ref hash, rotation.y);
            Mix(ref hash, rotation.z);
            Mix(ref hash, rotation.w);
            Mix(ref hash, scale.x);
            Mix(ref hash, scale.y);
            Mix(ref hash, scale.z);
            if (!(controlTransform is RectTransform rect)) return;

            Rect localRect = rect.rect;
            Mix(ref hash, localRect.x);
            Mix(ref hash, localRect.y);
            Mix(ref hash, localRect.width);
            Mix(ref hash, localRect.height);
            Mix(ref hash, rect.anchorMin.x);
            Mix(ref hash, rect.anchorMin.y);
            Mix(ref hash, rect.anchorMax.x);
            Mix(ref hash, rect.anchorMax.y);
            Mix(ref hash, rect.pivot.x);
            Mix(ref hash, rect.pivot.y);
            rect.GetWorldCorners(worldCorners);
            for (int index = 0; index < worldCorners.Length; index++)
            {
                Mix(ref hash, worldCorners[index].x);
                Mix(ref hash, worldCorners[index].y);
                Mix(ref hash, worldCorners[index].z);
            }
        }

        private void SynchronizeHierarchySignature()
        {
            hierarchySignature = CaptureHierarchySignature();
            hasHierarchySignature = true;
        }

        private void NormalizeTransientDropdowns()
        {
            if (uiRoot == null || safeFrame == null) return;
            transientTransforms.Clear();
            UiRoot.GetComponentsInChildren(includeInactive: true, transientTransforms);

            foreach (Transform candidate in transientTransforms)
            {
                if (string.Equals(candidate.name, "Blocker", StringComparison.Ordinal))
                    NormalizeDropdownBlocker(candidate);
            }
            foreach (Transform candidate in transientTransforms)
            {
                if (string.Equals(candidate.name, "Dropdown List", StringComparison.Ordinal))
                    NormalizeDropdownList(candidate);
            }
        }

        private void NormalizeDropdownList(Transform candidate)
        {
            if (!(candidate is RectTransform rect)) return;
            if (!rect.IsChildOf(SafeFrame.Frame))
                rect.SetParent(SafeFrame.Frame, worldPositionStays: true);
            rect.SetAsLastSibling();
            float listWidth = Mathf.Max(DropdownFallbackWidth, rect.rect.width);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, listWidth);

            ScrollRect? scroll = rect.GetComponent<ScrollRect>();
            if (scroll?.content != null)
            {
                if (scroll.viewport != null)
                    scroll.viewport.SetSizeWithCurrentAnchors(
                        RectTransform.Axis.Horizontal, listWidth);
                RectTransform content = scroll.content;
                content.anchorMin = new Vector2(0.5f, content.anchorMin.y);
                content.anchorMax = new Vector2(0.5f, content.anchorMax.y);
                content.anchoredPosition = new Vector2(0f, content.anchoredPosition.y);
                content.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Horizontal, listWidth);
                VerticalLayoutGroup? group = content.GetComponent<VerticalLayoutGroup>();
                if (group != null)
                {
                    group.childControlWidth = false;
                    group.childForceExpandWidth = false;
                }
            }

            transientDropdownItems.Clear();
            rect.GetComponentsInChildren(includeInactive: false, transientDropdownItems);
            foreach (Toggle item in transientDropdownItems)
            {
                var itemRect = (RectTransform)item.transform;
                LayoutElement layout = item.GetComponent<LayoutElement>() ??
                    item.gameObject.AddComponent<LayoutElement>();
                layout.minHeight = DropdownItemHeight;
                layout.preferredHeight = DropdownItemHeight;
                layout.flexibleHeight = 0f;
                itemRect.anchorMin = new Vector2(0.5f, itemRect.anchorMin.y);
                itemRect.anchorMax = new Vector2(0.5f, itemRect.anchorMax.y);
                itemRect.anchoredPosition = new Vector2(0f, itemRect.anchoredPosition.y);
                itemRect.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Horizontal, listWidth);
                itemRect.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Vertical, DropdownItemHeight);
                ProductAccessibleControl? accessible =
                    item.GetComponent<ProductAccessibleControl>();
                Text? visibleLabel = item.GetComponentInChildren<Text>(includeInactive: false);
                if (accessible == null || !accessible.IsConfigured || visibleLabel == null)
                    continue;
                accessible.SetRuntimeLabel(
                    "accessibility.dropdown_option", visibleLabel.text);
            }
            if (scroll?.content != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
                foreach (Toggle item in transientDropdownItems)
                    ((RectTransform)item.transform).SetSizeWithCurrentAnchors(
                        RectTransform.Axis.Horizontal, listWidth);
            }
        }

        private void NormalizeDropdownBlocker(Transform candidate)
        {
            if (!(candidate is RectTransform rect)) return;
            if (!rect.IsChildOf(SafeFrame.Frame))
                rect.SetParent(SafeFrame.Frame, worldPositionStays: false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            rect.SetAsLastSibling();

            Image image = rect.GetComponent<Image>() ??
                rect.gameObject.AddComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;
            ProductGraphicElement graphic =
                rect.GetComponent<ProductGraphicElement>() ??
                rect.gameObject.AddComponent<ProductGraphicElement>();
            graphic.Configure(image, ProductGraphicRole.Surface, preserveAlpha: true);

            Button button = rect.GetComponent<Button>() ??
                rect.gameObject.AddComponent<Button>();
            if (button.targetGraphic == null) button.targetGraphic = image;
            ProductAccessibleControl accessible =
                rect.GetComponent<ProductAccessibleControl>() ??
                rect.gameObject.AddComponent<ProductAccessibleControl>();
            accessible.Configure(button, "common.cancel", participatesInNavigation: false);
        }

        private void RefreshSemanticElements()
        {
            IProductText text = Text;
            foreach (ProductGraphicElement element in
                UiRoot.GetComponentsInChildren<ProductGraphicElement>(includeInactive: true))
                element.Apply(CurrentPalette);
            foreach (ProductAccessibleControl control in
                UiRoot.GetComponentsInChildren<ProductAccessibleControl>(includeInactive: true))
                control.Apply(text, CurrentPalette);
        }

        private void RebuildActiveLayouts()
        {
            Canvas.ForceUpdateCanvases();
            foreach (LayoutGroup group in
                UiRoot.GetComponentsInChildren<LayoutGroup>(includeInactive: false))
            {
                if (group.transform is RectTransform rect)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            }
            Canvas.ForceUpdateCanvases();
        }

        private void Awake()
        {
            if (textBehaviour is IProductText persistedText) configuredText = persistedText;
        }

        private void RestoreVisibleFocus(IReadOnlyList<NavigationNode> nodes)
        {
            EventSystem? eventSystem = EventSystem.current;
            GameObject? selected = eventSystem?.currentSelectedGameObject;
            NavigationNode? selectedNode = nodes.FirstOrDefault(node =>
                node.Selectable.gameObject == selected);
            if (eventSystem != null && selectedNode == null)
            {
                eventSystem.SetSelectedGameObject(null);
                selected = null;
                if (nodes.Count > 0)
                {
                    eventSystem.SetSelectedGameObject(nodes[0].Selectable.gameObject);
                    selected = nodes[0].Selectable.gameObject;
                }
            }

            foreach (ProductAccessibleControl control in
                UiRoot.GetComponentsInChildren<ProductAccessibleControl>(includeInactive: true))
                control.SetFocusVisible(control.gameObject == selected);
        }

        private bool IsEligible(Selectable selectable)
        {
            if (selectable == null || !selectable.transform.IsChildOf(UiRoot) ||
                !selectable.gameObject.activeInHierarchy || !selectable.IsActive() ||
                !selectable.IsInteractable()) return false;
            ProductAccessibleControl? accessible =
                selectable.GetComponent<ProductAccessibleControl>();
            return accessible != null && accessible.IsConfigured &&
                accessible.isActiveAndEnabled && accessible.ParticipatesInNavigation &&
                accessible.Control == selectable;
        }

        private Transform? ActiveNavigationScope()
        {
            ProductErrorPanel? error =
                UiRoot.GetComponentInChildren<ProductErrorPanel>(includeInactive: false);
            if (error != null && error.gameObject.activeInHierarchy) return error.transform;

            Transform? dropdownList = UiRoot.GetComponentsInChildren<Transform>(
                    includeInactive: false)
                .LastOrDefault(candidate => string.Equals(candidate.name,
                    "Dropdown List", StringComparison.Ordinal));
            if (dropdownList != null) return dropdownList;

            MatchScreen? match = UiRoot.GetComponentsInChildren<MatchScreen>(
                    includeInactive: false)
                .FirstOrDefault(candidate => candidate.IsContextHelpVisible);
            return match?.ContextHelpPanel.transform;
        }

        private static bool IsInsideNavigationScope(Transform candidate, Transform? scope) =>
            scope == null || candidate == scope || candidate.IsChildOf(scope);

        private Vector2 CenterInRoot(Selectable selectable)
        {
            RectTransform rect = selectable.transform as RectTransform ??
                throw new InvalidOperationException(
                    "Product navigation controls require RectTransform components.");
            Vector3 worldCenter = rect.TransformPoint(rect.rect.center);
            Vector3 localCenter = UiRoot.InverseTransformPoint(worldCenter);
            return new Vector2(localCenter.x, localCenter.y);
        }

        private static Selectable? FindNeighbor(NavigationNode source,
            IReadOnlyList<NavigationNode> nodes, Vector2 direction)
        {
            NavigationNode? best = null;
            float bestScore = float.PositiveInfinity;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            foreach (NavigationNode candidate in nodes)
            {
                if (candidate.Selectable == source.Selectable) continue;
                Vector2 offset = candidate.Center - source.Center;
                float forward = Vector2.Dot(offset, direction);
                if (forward <= 0.01f) continue;
                float sideways = Mathf.Abs(Vector2.Dot(offset, perpendicular));
                float angularPenalty = sideways / forward;
                float score = (angularPenalty * 100000f) + offset.sqrMagnitude;
                if (score < bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }
            return best?.Selectable;
        }

        private void ValidateConfiguration()
        {
            _ = UiRoot;
            _ = SafeFrame;
            _ = Text;
        }

        private static InvalidOperationException Missing(string name) =>
            new InvalidOperationException(
                "Product accessibility controller is not configured: " + name);

        private static void Mix(ref ulong hash, bool value) =>
            Mix(ref hash, value ? 1 : 0);

        private static void Mix(ref ulong hash, float value) =>
            Mix(ref hash, value.GetHashCode());

        private static void Mix(ref ulong hash, int value)
        {
            unchecked
            {
                hash = (hash ^ (uint)value) * SignaturePrime;
            }
        }

        private readonly struct HierarchySignature : IEquatable<HierarchySignature>
        {
            public HierarchySignature(int count, ulong hash)
            {
                Count = count;
                Hash = hash;
            }

            public int Count { get; }
            public ulong Hash { get; }

            public bool Equals(HierarchySignature other) =>
                Count == other.Count && Hash == other.Hash;
        }

        private sealed class NavigationNode
        {
            public Selectable Selectable { get; }
            public Vector2 Center { get; }

            public NavigationNode(Selectable selectable, Vector2 center)
            {
                Selectable = selectable;
                Center = center;
            }
        }
    }
}
