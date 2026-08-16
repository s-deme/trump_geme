#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace TrumpLab.Product
{
    public sealed class ProductSettingsScreen : ProductScreen, IPreferredFocusProvider
    {
        private static readonly ProductDisplayMode[] DisplayModes =
            { ProductDisplayMode.Windowed, ProductDisplayMode.Borderless };
        private static readonly ProductPresentationSpeed[] PresentationSpeeds =
        {
            ProductPresentationSpeed.Reduced,
            ProductPresentationSpeed.Normal,
            ProductPresentationSpeed.Fast
        };
        private static readonly ProductInputCommand[] Commands =
        {
            ProductInputCommand.Up,
            ProductInputCommand.Down,
            ProductInputCommand.Left,
            ProductInputCommand.Right,
            ProductInputCommand.Submit,
            ProductInputCommand.Cancel,
            ProductInputCommand.Help
        };

        [SerializeField] private GameObject? generalPanel;
        [SerializeField] private GameObject? bindingsPanel;
        [SerializeField] private Button? generalPageButton;
        [SerializeField] private Button? bindingsPageButton;
        [SerializeField] private Dropdown? displayModeDropdown;
        [SerializeField] private Dropdown? resolutionDropdown;
        [SerializeField] private Toggle? vSyncToggle;
        [SerializeField] private Slider? masterVolumeSlider;
        [SerializeField] private Slider? musicVolumeSlider;
        [SerializeField] private Slider? sfxVolumeSlider;
        [SerializeField] private Text? masterVolumeLabel;
        [SerializeField] private Text? musicVolumeLabel;
        [SerializeField] private Text? sfxVolumeLabel;
        [SerializeField] private Dropdown? presentationSpeedDropdown;
        [SerializeField] private Button[] keyboardBindingButtons = Array.Empty<Button>();
        [SerializeField] private Button[] gamepadBindingButtons = Array.Empty<Button>();
        [SerializeField] private Button? cancelRebindButton;
        [SerializeField] private Text? feedbackLabel;
        [SerializeField] private Button? applyButton;
        [SerializeField] private Button? resetButton;
        [SerializeField] private Button? backButton;

        private ProductSettings? sourceSettings;
        private ProductInputBindings editingBindings = ProductInputBindings.Default;

        public override ScreenId Id => ScreenId.ProductSettings;
        public Selectable? PreferredFocus => generalPageButton;
        public Dropdown DisplayModeDropdown => Require(displayModeDropdown, "Display mode");
        public Dropdown ResolutionDropdown => Require(resolutionDropdown, "Resolution");
        public Toggle VSyncToggle => Require(vSyncToggle, "VSync");
        public Slider MasterVolumeSlider => Require(masterVolumeSlider, "Master volume");
        public Slider MusicVolumeSlider => Require(musicVolumeSlider, "Music volume");
        public Slider SfxVolumeSlider => Require(sfxVolumeSlider, "SFX volume");
        public Dropdown PresentationSpeedDropdown => Require(
            presentationSpeedDropdown, "Presentation speed");
        public IReadOnlyList<Button> KeyboardBindingButtons => keyboardBindingButtons;
        public IReadOnlyList<Button> GamepadBindingButtons => gamepadBindingButtons;
        public Text FeedbackLabel => Require(feedbackLabel, "Settings feedback");

        public event System.Action<ProductSettings>? ApplyRequested;
        public event System.Action? ResetRequested;
        public event System.Action? BackRequested;
        public event System.Action<ProductInputScheme, ProductInputCommand>? RebindRequested;
        public event System.Action? CancelRebindRequested;
        public event System.Action? ValidationRejected;

        public void Configure(GameObject general, GameObject bindings,
            Button generalPage, Button bindingsPage, Dropdown displayMode,
            Dropdown resolution, Toggle vSync, Slider masterVolume, Slider musicVolume,
            Slider sfxVolume, Text masterLabel, Text musicLabel, Text sfxLabel,
            Dropdown presentationSpeed, Button[] keyboardButtons, Button[] gamepadButtons,
            Button cancelRebind, Text feedback, Button apply, Button reset, Button back)
        {
            generalPanel = general;
            bindingsPanel = bindings;
            generalPageButton = generalPage;
            bindingsPageButton = bindingsPage;
            displayModeDropdown = displayMode;
            resolutionDropdown = resolution;
            vSyncToggle = vSync;
            masterVolumeSlider = masterVolume;
            musicVolumeSlider = musicVolume;
            sfxVolumeSlider = sfxVolume;
            masterVolumeLabel = masterLabel;
            musicVolumeLabel = musicLabel;
            sfxVolumeLabel = sfxLabel;
            presentationSpeedDropdown = presentationSpeed;
            keyboardBindingButtons = keyboardButtons?.ToArray() ??
                throw new ArgumentNullException(nameof(keyboardButtons));
            gamepadBindingButtons = gamepadButtons?.ToArray() ??
                throw new ArgumentNullException(nameof(gamepadButtons));
            cancelRebindButton = cancelRebind;
            feedbackLabel = feedback;
            applyButton = apply;
            resetButton = reset;
            backButton = back;
            ConfigureOptions();
            ShowGeneralPage();
            SetRebindState(false, string.Empty);
        }

        private void Awake()
        {
            ValidateConfiguration();
            ConfigureOptions();
            generalPageButton!.onClick.AddListener(ShowGeneralPage);
            bindingsPageButton!.onClick.AddListener(ShowBindingsPage);
            masterVolumeSlider!.onValueChanged.AddListener(HandleVolumeChanged);
            musicVolumeSlider!.onValueChanged.AddListener(HandleVolumeChanged);
            sfxVolumeSlider!.onValueChanged.AddListener(HandleVolumeChanged);
            applyButton!.onClick.AddListener(HandleApply);
            resetButton!.onClick.AddListener(HandleReset);
            backButton!.onClick.AddListener(HandleBack);
            cancelRebindButton!.onClick.AddListener(HandleCancelRebind);
            for (int index = 0; index < Commands.Length; index++)
            {
                int captured = index;
                keyboardBindingButtons[index].onClick.AddListener(
                    () => HandleRebind(ProductInputScheme.Keyboard, Commands[captured]));
                gamepadBindingButtons[index].onClick.AddListener(
                    () => HandleRebind(ProductInputScheme.Gamepad, Commands[captured]));
            }
            ShowGeneralPage();
            SetRebindState(false, string.Empty);
        }

        private void OnDestroy()
        {
            generalPageButton?.onClick.RemoveAllListeners();
            bindingsPageButton?.onClick.RemoveAllListeners();
            masterVolumeSlider?.onValueChanged.RemoveAllListeners();
            musicVolumeSlider?.onValueChanged.RemoveAllListeners();
            sfxVolumeSlider?.onValueChanged.RemoveAllListeners();
            applyButton?.onClick.RemoveAllListeners();
            resetButton?.onClick.RemoveAllListeners();
            backButton?.onClick.RemoveAllListeners();
            cancelRebindButton?.onClick.RemoveAllListeners();
            foreach (Button button in keyboardBindingButtons) button.onClick.RemoveAllListeners();
            foreach (Button button in gamepadBindingButtons) button.onClick.RemoveAllListeners();
        }

        public void SetValues(ProductSettings settings, string feedback = "")
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            sourceSettings = settings;
            editingBindings = settings.InputBindings;
            DisplayModeDropdown.SetValueWithoutNotify(IndexOf(DisplayModes, settings.DisplayMode));
            ResolutionDropdown.SetValueWithoutNotify(IndexOf(
                ProductResolution.Supported, settings.Resolution));
            VSyncToggle.SetIsOnWithoutNotify(settings.VSync);
            MasterVolumeSlider.SetValueWithoutNotify(settings.MasterVolume);
            MusicVolumeSlider.SetValueWithoutNotify(settings.MusicVolume);
            SfxVolumeSlider.SetValueWithoutNotify(settings.SfxVolume);
            PresentationSpeedDropdown.SetValueWithoutNotify(IndexOf(
                PresentationSpeeds, settings.PresentationSpeed));
            RefreshVolumeLabels();
            RefreshBindingLabels();
            SetFeedback(feedback, isError: false);
            ShowGeneralPage();
            SetRebindState(false, string.Empty);
        }

        public bool TrySetBinding(ProductInputScheme scheme, ProductInputCommand command,
            string path, out string error)
        {
            try
            {
                editingBindings = editingBindings.With(scheme, command, path);
                RefreshBindingLabels();
                SetFeedback("Binding updated. Choose Apply to save it.", isError: false);
                error = string.Empty;
                return true;
            }
            catch (ArgumentException exception)
            {
                error = exception.Message;
                SetFeedback(error, isError: true);
                ValidationRejected?.Invoke();
                return false;
            }
        }

        public void SetRebindState(bool active, string message)
        {
            if (cancelRebindButton == null) return;
            cancelRebindButton.gameObject.SetActive(active);
            foreach (Button button in keyboardBindingButtons) button.interactable = !active;
            foreach (Button button in gamepadBindingButtons) button.interactable = !active;
            if (!string.IsNullOrWhiteSpace(message)) SetFeedback(message, isError: false);
        }

        public void SetFeedback(string message, bool isError)
        {
            FeedbackLabel.text = message ?? string.Empty;
            FeedbackLabel.color = isError
                ? new Color(1f, 0.55f, 0.45f, 1f)
                : new Color(0.78f, 0.95f, 0.8f, 1f);
        }

        public bool TryReadSettings(out ProductSettings? settings, out string error)
        {
            settings = null;
            if (sourceSettings == null)
            {
                error = "Product settings are not loaded.";
                return false;
            }
            if (DisplayModeDropdown.value < 0 || DisplayModeDropdown.value >= DisplayModes.Length ||
                ResolutionDropdown.value < 0 ||
                ResolutionDropdown.value >= ProductResolution.Supported.Count ||
                PresentationSpeedDropdown.value < 0 ||
                PresentationSpeedDropdown.value >= PresentationSpeeds.Length)
            {
                error = "Choose a supported display and presentation setting.";
                return false;
            }
            try
            {
                settings = new ProductSettings(
                    ProductSettings.CurrentFormatVersion,
                    DisplayModes[DisplayModeDropdown.value],
                    ProductResolution.Supported[ResolutionDropdown.value],
                    VSyncToggle.isOn,
                    Mathf.RoundToInt(MasterVolumeSlider.value),
                    Mathf.RoundToInt(MusicVolumeSlider.value),
                    Mathf.RoundToInt(SfxVolumeSlider.value),
                    PresentationSpeeds[PresentationSpeedDropdown.value],
                    editingBindings,
                    sourceSettings.Locale,
                    sourceSettings.TextScalePercent,
                    sourceSettings.HighContrast,
                    sourceSettings.ReducedMotion);
                error = string.Empty;
                return true;
            }
            catch (ArgumentException exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private void ConfigureOptions()
        {
            if (displayModeDropdown != null)
            {
                displayModeDropdown.ClearOptions();
                displayModeDropdown.AddOptions(new List<string> { "Windowed", "Borderless" });
            }
            if (resolutionDropdown != null)
            {
                resolutionDropdown.ClearOptions();
                resolutionDropdown.AddOptions(ProductResolution.Supported
                    .Select(resolution => resolution.ToString()).ToList());
            }
            if (presentationSpeedDropdown != null)
            {
                presentationSpeedDropdown.ClearOptions();
                presentationSpeedDropdown.AddOptions(
                    new List<string> { "Reduced", "Normal", "Fast" });
            }
            ConfigureSlider(masterVolumeSlider);
            ConfigureSlider(musicVolumeSlider);
            ConfigureSlider(sfxVolumeSlider);
        }

        private static void ConfigureSlider(Slider? slider)
        {
            if (slider == null) return;
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.wholeNumbers = true;
        }

        private void ValidateConfiguration()
        {
            if (generalPanel == null || bindingsPanel == null || generalPageButton == null ||
                bindingsPageButton == null || displayModeDropdown == null ||
                resolutionDropdown == null || vSyncToggle == null ||
                masterVolumeSlider == null || musicVolumeSlider == null ||
                sfxVolumeSlider == null || masterVolumeLabel == null ||
                musicVolumeLabel == null || sfxVolumeLabel == null ||
                presentationSpeedDropdown == null || cancelRebindButton == null ||
                feedbackLabel == null || applyButton == null || resetButton == null ||
                backButton == null || keyboardBindingButtons.Length != Commands.Length ||
                gamepadBindingButtons.Length != Commands.Length ||
                keyboardBindingButtons.Any(button => button == null) ||
                gamepadBindingButtons.Any(button => button == null))
                throw new InvalidOperationException(
                    "Product settings screen controls are not configured.");
        }

        private void ShowGeneralPage()
        {
            if (generalPanel != null) generalPanel.SetActive(true);
            if (bindingsPanel != null) bindingsPanel.SetActive(false);
        }

        private void ShowBindingsPage()
        {
            if (generalPanel != null) generalPanel.SetActive(false);
            if (bindingsPanel != null) bindingsPanel.SetActive(true);
        }

        private void HandleVolumeChanged(float _) => RefreshVolumeLabels();

        private void RefreshVolumeLabels()
        {
            if (masterVolumeLabel != null && masterVolumeSlider != null)
                masterVolumeLabel.text = "Master " + Mathf.RoundToInt(masterVolumeSlider.value) + "%";
            if (musicVolumeLabel != null && musicVolumeSlider != null)
                musicVolumeLabel.text = "Music " + Mathf.RoundToInt(musicVolumeSlider.value) + "%";
            if (sfxVolumeLabel != null && sfxVolumeSlider != null)
                sfxVolumeLabel.text = "SFX " + Mathf.RoundToInt(sfxVolumeSlider.value) + "%";
        }

        private void RefreshBindingLabels()
        {
            for (int index = 0; index < Commands.Length; index++)
            {
                SetButtonLabel(keyboardBindingButtons[index], Commands[index],
                    editingBindings.Get(ProductInputScheme.Keyboard, Commands[index]));
                SetButtonLabel(gamepadBindingButtons[index], Commands[index],
                    editingBindings.Get(ProductInputScheme.Gamepad, Commands[index]));
            }
        }

        private static void SetButtonLabel(Button button, ProductInputCommand command, string path)
        {
            Text? label = button.GetComponentInChildren<Text>(true);
            if (label == null) throw new InvalidOperationException(
                "A binding button requires a Text label.");
            label.text = CommandLabel(command) + ": " +
                ProductInputController.HumanReadablePath(path);
        }

        private static string CommandLabel(ProductInputCommand command)
        {
            switch (command)
            {
                case ProductInputCommand.Up: return "Up";
                case ProductInputCommand.Down: return "Down";
                case ProductInputCommand.Left: return "Left";
                case ProductInputCommand.Right: return "Right";
                case ProductInputCommand.Submit: return "Submit";
                case ProductInputCommand.Cancel: return "Back";
                case ProductInputCommand.Help: return "Help";
                default: throw new ArgumentOutOfRangeException(nameof(command), command, null);
            }
        }

        private void HandleApply()
        {
            if (!TryReadSettings(out ProductSettings? settings, out string error) || settings == null)
            {
                SetFeedback(error, isError: true);
                ValidationRejected?.Invoke();
                return;
            }
            ApplyRequested?.Invoke(settings);
        }

        private void HandleReset() => ResetRequested?.Invoke();
        private void HandleBack() => BackRequested?.Invoke();
        private void HandleCancelRebind() => CancelRebindRequested?.Invoke();

        private void HandleRebind(ProductInputScheme scheme, ProductInputCommand command) =>
            RebindRequested?.Invoke(scheme, command);

        private static int IndexOf<T>(IReadOnlyList<T> values, T value)
        {
            for (int index = 0; index < values.Count; index++)
                if (EqualityComparer<T>.Default.Equals(values[index], value)) return index;
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported setting value.");
        }

        private static T Require<T>(T? value, string name) where T : class => value ??
            throw new InvalidOperationException(name + " control is not configured.");
    }
}
