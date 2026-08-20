#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TrumpLab.Product.Tests
{
    public sealed class ProductAccessibilityTests
    {
        private readonly List<GameObject> created = new List<GameObject>();
        private EventSystem? registeredEventSystem;

        [TearDown]
        public void TearDown()
        {
            if (registeredEventSystem != null)
            {
                InvokeEventSystemLifecycle(registeredEventSystem, "OnDisable");
                registeredEventSystem = null;
            }
            for (int index = created.Count - 1; index >= 0; index--)
            {
                if (created[index] != null) UnityEngine.Object.DestroyImmediate(created[index]);
            }
            created.Clear();
        }

        [Test]
        public void PalettesMeetEverySemanticContrastRequirement()
        {
            Assert.That(ProductWcagContrast.ContrastRatio(Color.black, Color.white),
                Is.EqualTo(21d).Within(0.001d));
            Assert.That(ProductWcagContrast.ContrastRatio(Color.gray, Color.gray),
                Is.EqualTo(1d).Within(0.001d));

            foreach (ProductUiPalette palette in new[]
                { ProductUiPalette.Normal, ProductUiPalette.HighContrast })
            {
                AssertRatio(palette.NormalText, palette.Background,
                    ProductWcagContrast.NormalTextMinimum, palette, "normal/background");
                AssertRatio(palette.NormalText, palette.Surface,
                    ProductWcagContrast.NormalTextMinimum, palette, "normal/surface");
                AssertRatio(palette.MutedText, palette.Background,
                    ProductWcagContrast.NormalTextMinimum, palette, "muted/background");
                AssertRatio(palette.MutedText, palette.Surface,
                    ProductWcagContrast.NormalTextMinimum, palette, "muted/surface");
                AssertRatio(palette.LargeText, palette.Background,
                    ProductWcagContrast.LargeTextMinimum, palette, "large/background");
                AssertRatio(palette.LargeText, palette.Surface,
                    ProductWcagContrast.LargeTextMinimum, palette, "large/surface");
                AssertRatio(palette.ControlText, palette.ControlBackground,
                    ProductWcagContrast.NormalTextMinimum, palette, "control");
                AssertRatio(palette.ControlText, palette.ActiveControlBackground,
                    ProductWcagContrast.ActiveControlMinimum, palette, "active control");
                AssertRatio(palette.DisabledControlText,
                    palette.DisabledControlBackground,
                    ProductWcagContrast.NormalTextMinimum, palette, "disabled control");
                AssertRatio(palette.PositiveText, palette.PositiveBackground,
                    ProductWcagContrast.NormalTextMinimum, palette, "positive");
                AssertRatio(palette.ErrorText, palette.ErrorBackground,
                    ProductWcagContrast.NormalTextMinimum, palette, "error");
                AssertRatio(palette.FocusIndicator, palette.Background,
                    ProductWcagContrast.FocusIndicatorMinimum, palette,
                    "focus/background");
                AssertRatio(palette.FocusIndicator, palette.Surface,
                    ProductWcagContrast.FocusIndicatorMinimum, palette, "focus/surface");
                AssertRatio(palette.FocusIndicator, palette.ControlBackground,
                    ProductWcagContrast.FocusIndicatorMinimum, palette, "focus/control");
                AssertRatio(palette.FocusIndicator, palette.ActiveControlBackground,
                    ProductWcagContrast.FocusIndicatorMinimum, palette,
                    "focus/active control");

                foreach (ProductGraphicRole role in
                    Enum.GetValues(typeof(ProductGraphicRole)).Cast<ProductGraphicRole>())
                    Assert.That(palette.ColorFor(role).a, Is.EqualTo(1f), role.ToString());
            }

            Assert.That(ProductWcagContrast.MeetsNormalText(
                ProductUiPalette.Normal.NormalText, ProductUiPalette.Normal.Background), Is.True);
            Assert.That(ProductWcagContrast.MeetsLargeText(
                ProductUiPalette.Normal.LargeText, ProductUiPalette.Normal.Surface), Is.True);
            Assert.That(ProductWcagContrast.MeetsFocusIndicator(
                ProductUiPalette.Normal.FocusIndicator, ProductUiPalette.Normal.Background),
                Is.True);
            Assert.That(ProductWcagContrast.MeetsActiveControl(
                ProductUiPalette.Normal.ControlText,
                ProductUiPalette.Normal.ActiveControlBackground), Is.True);
        }

        [Test]
        public void GraphicElementKeepsItsBaseRoleImmutableAndAppliesPalette()
        {
            GameObject root = Create("Graphic", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            Image image = root.GetComponent<Image>();
            image.color = new Color(0.1f, 0.2f, 0.3f, 0.25f);
            ProductGraphicElement element = root.AddComponent<ProductGraphicElement>();

            element.Configure(image, ProductGraphicRole.Surface, preserveAlpha: true);
            element.Configure(image, ProductGraphicRole.Surface, preserveAlpha: true);
            element.Apply(ProductUiPalette.HighContrast);

            Assert.That(element.BaseRole, Is.EqualTo(ProductGraphicRole.Surface));
            Assert.That(element.PreserveAlpha, Is.True);
            Assert.That(element.TargetGraphic, Is.SameAs(image));
            Color expected = ProductUiPalette.HighContrast.Surface;
            expected.a = 0.25f;
            Assert.That(image.color, Is.EqualTo(expected));
            Assert.Throws<InvalidOperationException>(() =>
                element.Configure(image, ProductGraphicRole.Background,
                    preserveAlpha: true));
            Assert.Throws<InvalidOperationException>(() =>
                element.Configure(image, ProductGraphicRole.Surface,
                    preserveAlpha: false));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ProductUiPalette.Normal.ColorFor((ProductGraphicRole)999));
        }

        [Test]
        public void AccessibleControlUsesLocalizedLabelOutlineAndHitTargetPolicy()
        {
            EventSystem eventSystem = CreateEventSystem();
            GameObject root = Create("AccessibleControl", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(44f, 44f);
            Button button = root.GetComponent<Button>();
            Image image = root.GetComponent<Image>();
            ProductGraphicElement graphic = root.AddComponent<ProductGraphicElement>();
            graphic.Configure(image, ProductGraphicRole.ControlBackground);
            graphic.Apply(ProductUiPalette.HighContrast);
            ProductAccessibleControl accessible =
                root.AddComponent<ProductAccessibleControl>();
            accessible.Configure(button, "control.play");

            var text = new RecordingText("ja-JP");
            accessible.Apply(text, ProductUiPalette.HighContrast);

            Assert.That(accessible.LabelKey, Is.EqualTo("control.play"));
            Assert.That(accessible.ParticipatesInNavigation, Is.True);
            Assert.That(accessible.ResolvedLabel, Is.EqualTo("ja-JP:control.play"));
            Assert.That(accessible.ReferenceHitSize, Is.EqualTo(new Vector2(44f, 44f)));
            Assert.That(accessible.HasMinimumReferenceHitTarget, Is.True);
            Assert.That(accessible.IsFocusVisible, Is.False);
            Assert.That(accessible.FocusOutline.effectColor,
                Is.EqualTo(ProductUiPalette.HighContrast.FocusIndicator));
            Assert.That(button.targetGraphic.color, Is.EqualTo(Color.white),
                "ColorTint controls must not multiply the semantic palette twice.");
            Assert.That(button.colors.normalColor,
                Is.EqualTo(ProductUiPalette.HighContrast.ControlBackground));
            Assert.That(button.colors.selectedColor,
                Is.EqualTo(ProductUiPalette.HighContrast.ActiveControlBackground));

            ExecuteEvents.Execute(root, new BaseEventData(eventSystem),
                ExecuteEvents.selectHandler);
            Assert.That(accessible.IsFocusVisible, Is.True,
                "Focus requires a non-color outline.");
            ExecuteEvents.Execute(root, new BaseEventData(eventSystem),
                ExecuteEvents.deselectHandler);
            Assert.That(accessible.IsFocusVisible, Is.False);

            rect.sizeDelta = new Vector2(43f, 44f);
            Assert.That(accessible.HasMinimumReferenceHitTarget, Is.False);
            Assert.Throws<InvalidOperationException>(() =>
                accessible.Configure(button, "control.changed"));
            Assert.Throws<InvalidOperationException>(() =>
                accessible.Configure(button, "control.play",
                    participatesInNavigation: false));
        }

        [Test]
        public void AccessibleControlCanOutlineAChildTargetGraphic()
        {
            EventSystem eventSystem = CreateEventSystem();
            GameObject root = Create("Slider", typeof(RectTransform), typeof(Slider));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(440f, 52f);
            GameObject handle = Create("Handle", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            handle.transform.SetParent(root.transform, worldPositionStays: false);
            Slider slider = root.GetComponent<Slider>();
            slider.targetGraphic = handle.GetComponent<Image>();
            ProductAccessibleControl accessible =
                root.AddComponent<ProductAccessibleControl>();
            accessible.Configure(slider, "control.master_volume");
            accessible.Apply(new RecordingText("en-US"), ProductUiPalette.Normal);

            Assert.That(accessible.FocusOutline.gameObject, Is.SameAs(handle));
            ExecuteEvents.Execute(root, new BaseEventData(eventSystem),
                ExecuteEvents.selectHandler);
            Assert.That(accessible.IsFocusVisible, Is.True);
            Assert.That(slider.targetGraphic.color, Is.EqualTo(Color.white));
        }

        [Test]
        public void AccessibleControlRuntimeLabelUsesCopiedArgumentsAndKeepsStableBaseKey()
        {
            GameObject root = Create("RuntimeLabel", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(Button));
            ProductAccessibleControl accessible =
                root.AddComponent<ProductAccessibleControl>();
            accessible.Configure(root.GetComponent<Button>(), "control.base");
            var arguments = new object[] { "Visible option" };

            accessible.SetRuntimeLabel("accessibility.dropdown_option", arguments);
            arguments[0] = "Mutated after assignment";
            accessible.Apply(new RecordingText("en-US"), ProductUiPalette.Normal);

            Assert.That(accessible.LabelKey, Is.EqualTo("control.base"),
                "Runtime labels must not mutate the stable configured contract.");
            Assert.That(accessible.ResolvedLabel,
                Is.EqualTo("en-US:accessibility.dropdown_option:Visible option"));

            accessible.ClearRuntimeLabel();
            accessible.Apply(new RecordingText("en-US"), ProductUiPalette.Normal);
            Assert.That(accessible.ResolvedLabel, Is.EqualTo("en-US:control.base"));
        }

        [Test]
        public void SafeFrameRecalculatesCenteredSixteenByNineWithoutAccumulation()
        {
            GameObject parentObject = Create("Parent", typeof(RectTransform));
            RectTransform parent = parentObject.GetComponent<RectTransform>();
            parent.sizeDelta = new Vector2(2100f, 900f);
            GameObject frameObject = Create("SafeFrame", typeof(RectTransform));
            frameObject.transform.SetParent(parent, worldPositionStays: false);
            ProductSafeFrame safeFrame = frameObject.AddComponent<ProductSafeFrame>();

            safeFrame.Configure(parent);

            AssertFrame(safeFrame.Frame, 1600f, 900f);
            Assert.That(safeFrame.Frame.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(safeFrame.Frame.anchorMin.x,
                Is.EqualTo((1f - (1600f / 2100f)) * 0.5f).Within(0.0001f));
            Assert.That(safeFrame.Frame.anchorMax.x,
                Is.EqualTo((1f + (1600f / 2100f)) * 0.5f).Within(0.0001f));

            parent.sizeDelta = new Vector2(1600f, 1200f);
            safeFrame.ApplyFrame();
            Vector2 firstMinimum = safeFrame.Frame.anchorMin;
            Vector2 firstMaximum = safeFrame.Frame.anchorMax;
            safeFrame.ApplyFrame();

            AssertFrame(safeFrame.Frame, 1600f, 900f);
            Assert.That(safeFrame.Frame.anchorMin, Is.EqualTo(firstMinimum));
            Assert.That(safeFrame.Frame.anchorMax, Is.EqualTo(firstMaximum));
            Assert.That(firstMinimum.y, Is.EqualTo(0.125f).Within(0.0001f));
            Assert.That(firstMaximum.y, Is.EqualTo(0.875f).Within(0.0001f));
        }

        [Test]
        public void NavigationUsesVisualGeometryAndExcludesHiddenOrDisabledControls()
        {
            EventSystem eventSystem = CreateEventSystem();
            GameObject rootObject = Create("UiRoot", typeof(RectTransform));
            RectTransform root = rootObject.GetComponent<RectTransform>();
            root.sizeDelta = new Vector2(800f, 600f);
            ProductSafeFrame safeFrame = CreateSafeFrame(root);

            // Deliberately use a hierarchy order that differs from the visual order.
            ProductAccessibleControl bottomRight = Control(root, "BottomRight",
                new Vector2(100f, -100f), interactable: true, active: true);
            ProductAccessibleControl topLeft = Control(root, "TopLeft",
                new Vector2(-100f, 100f), interactable: true, active: true);
            ProductAccessibleControl hidden = Control(root, "Hidden",
                new Vector2(-100f, 0f), interactable: true, active: false);
            ProductAccessibleControl topRight = Control(root, "TopRight",
                new Vector2(100f, 100f), interactable: true, active: true);
            ProductAccessibleControl disabled = Control(root, "Disabled",
                new Vector2(100f, 0f), interactable: false, active: true);
            ProductAccessibleControl pointerOnly = Control(root, "PointerOnly",
                Vector2.zero, interactable: true, active: true,
                participatesInNavigation: false);
            ProductAccessibleControl bottomLeft = Control(root, "BottomLeft",
                new Vector2(-100f, -100f), interactable: true, active: true);

            GameObject controllerObject = Create("AccessibilityController");
            ProductAccessibilityController controller =
                controllerObject.AddComponent<ProductAccessibilityController>();
            controller.Configure(root, safeFrame, new RecordingText("en-US"));
            controller.Apply(ProductSettings.CreateDefaults("en-US")
                .WithHighContrast(true));

            Assert.That(controller.CurrentPalette,
                Is.SameAs(ProductUiPalette.HighContrast));
            Assert.That(topLeft.Control.navigation.selectOnRight,
                Is.SameAs(topRight.Control));
            Assert.That(topLeft.Control.navigation.selectOnDown,
                Is.SameAs(bottomLeft.Control));
            Assert.That(topRight.Control.navigation.selectOnDown,
                Is.SameAs(bottomRight.Control));
            Assert.That(bottomLeft.Control.navigation.selectOnRight,
                Is.SameAs(bottomRight.Control));
            Assert.That(hidden.Control.navigation.mode, Is.EqualTo(Navigation.Mode.None));
            Assert.That(disabled.Control.navigation.mode, Is.EqualTo(Navigation.Mode.None));
            Assert.That(pointerOnly.Control.navigation.mode, Is.EqualTo(Navigation.Mode.None));
            Assert.That(pointerOnly.ResolvedLabel,
                Is.EqualTo("en-US:control.pointeronly"),
                "Pointer-only controls still receive locale and palette updates.");
            Assert.That(hidden.ResolvedLabel, Is.EqualTo("en-US:control.hidden"),
                "Inactive controls still receive locale and palette updates.");
            Assert.That(eventSystem.currentSelectedGameObject,
                Is.SameAs(topLeft.gameObject));
            Assert.That(topLeft.IsFocusVisible, Is.True);
            Assert.That(new[] { topRight, bottomLeft, bottomRight },
                Has.All.Matches<ProductAccessibleControl>(control =>
                    !control.IsFocusVisible));

            topLeft.gameObject.SetActive(false);
            controller.RefreshNavigation();

            Assert.That(eventSystem.currentSelectedGameObject,
                Is.SameAs(topRight.gameObject));
            Assert.That(topRight.IsFocusVisible, Is.True);
            Assert.That(topLeft.IsFocusVisible, Is.False);
        }

        [Test]
        public void RefreshNavigationAppliesSemanticsToRuntimeCreatedControls()
        {
            EventSystem eventSystem = CreateEventSystem();
            GameObject rootObject = Create("UiRoot", typeof(RectTransform));
            RectTransform root = rootObject.GetComponent<RectTransform>();
            root.sizeDelta = new Vector2(800f, 600f);
            ProductSafeFrame safeFrame = CreateSafeFrame(root);
            ProductAccessibleControl original = Control(root, "Original",
                Vector2.zero, interactable: true, active: true);

            GameObject controllerObject = Create("AccessibilityController");
            ProductAccessibilityController controller =
                controllerObject.AddComponent<ProductAccessibilityController>();
            controller.Configure(root, safeFrame, new RecordingText("ja-JP"));
            controller.Apply(ProductSettings.CreateDefaults("ja-JP")
                .WithHighContrast(true));

            ProductAccessibleControl runtimeControl = Control(root, "Runtime",
                new Vector2(0f, -100f), interactable: true, active: true);
            Assert.That(runtimeControl.ResolvedLabel, Is.Empty);

            controller.RefreshNavigation();

            Assert.That(runtimeControl.ResolvedLabel,
                Is.EqualTo("ja-JP:control.runtime"));
            Assert.That(runtimeControl.Control.targetGraphic.color, Is.EqualTo(Color.white));
            Assert.That(runtimeControl.Control.colors.normalColor,
                Is.EqualTo(ProductUiPalette.HighContrast.ControlBackground));
            Assert.That(runtimeControl.Control.navigation.mode,
                Is.EqualTo(Navigation.Mode.Explicit));
            Assert.That(original.Control.navigation.selectOnDown,
                Is.SameAs(runtimeControl.Control));
            Assert.That(eventSystem.currentSelectedGameObject,
                Is.SameAs(original.gameObject), "A valid visible focus must be preserved.");
        }

        private static void AssertRatio(Color foreground, Color background, double minimum,
            ProductUiPalette palette, string name)
        {
            double ratio = ProductWcagContrast.ContrastRatio(foreground, background);
            Assert.That(ratio, Is.GreaterThanOrEqualTo(minimum),
                (palette.IsHighContrast ? "high contrast " : "normal ") + name);
        }

        private static void AssertFrame(RectTransform frame, float width, float height)
        {
            Assert.That(frame.rect.width, Is.EqualTo(width).Within(0.01f));
            Assert.That(frame.rect.height, Is.EqualTo(height).Within(0.01f));
            Assert.That(frame.rect.width / frame.rect.height,
                Is.EqualTo(ProductSafeFrame.TargetAspectRatio).Within(0.0001f));
        }

        private ProductSafeFrame CreateSafeFrame(RectTransform parent)
        {
            GameObject frameObject = Create("SafeFrame", typeof(RectTransform));
            frameObject.transform.SetParent(parent, worldPositionStays: false);
            ProductSafeFrame frame = frameObject.AddComponent<ProductSafeFrame>();
            frame.Configure(parent);
            return frame;
        }

        private ProductAccessibleControl Control(RectTransform parent, string name,
            Vector2 position, bool interactable, bool active,
            bool participatesInNavigation = true)
        {
            GameObject root = Create(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Button));
            root.transform.SetParent(parent, worldPositionStays: false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(120f, 60f);
            rect.anchoredPosition = position;
            Button button = root.GetComponent<Button>();
            button.interactable = interactable;
            ProductAccessibleControl accessible =
                root.AddComponent<ProductAccessibleControl>();
            accessible.Configure(button, "control." + name.ToLowerInvariant(),
                participatesInNavigation);
            root.SetActive(active);
            return accessible;
        }

        private EventSystem CreateEventSystem()
        {
            GameObject root = Create("EventSystem", typeof(EventSystem));
            EventSystem eventSystem = root.GetComponent<EventSystem>();
            InvokeEventSystemLifecycle(eventSystem, "OnEnable");
            EventSystem.current = eventSystem;
            registeredEventSystem = eventSystem;
            return eventSystem;
        }

        private static void InvokeEventSystemLifecycle(EventSystem eventSystem,
            string methodName)
        {
            MethodInfo? method = typeof(EventSystem).GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
                throw new InvalidOperationException(
                    "EventSystem lifecycle method is unavailable: " + methodName);
            method.Invoke(eventSystem, null);
        }

        private GameObject Create(string name, params Type[] components)
        {
            var root = new GameObject(name, components);
            created.Add(root);
            return root;
        }

        private sealed class RecordingText : IProductText
        {
            public string RequestedLocale { get; }
            public string EffectiveLocale { get; }

            public RecordingText(string locale)
            {
                RequestedLocale = locale;
                EffectiveLocale = locale;
            }

            public string Get(string key, params object[] arguments) =>
                EffectiveLocale + ":" + key + (arguments.Length == 0
                    ? string.Empty
                    : ":" + string.Join(",", arguments.Select(argument =>
                        argument?.ToString() ?? string.Empty)));
        }
    }
}
