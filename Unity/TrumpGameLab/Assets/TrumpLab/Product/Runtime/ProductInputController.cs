#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;

namespace TrumpLab.Product
{
    public sealed class ProductInputController : MonoBehaviour, IProductSettingsValidator
    {
        private const string KeyboardGroup = "KeyboardMouse";
        private const string GamepadGroup = "Gamepad";
        private const string KeyboardSpacePath = "<Keyboard>/space";

        [SerializeField] private InputSystemUIInputModule? uiInputModule;

        private readonly Dictionary<(ProductInputScheme, ProductInputCommand), BindingTarget>
            bindingTargets = new Dictionary<(ProductInputScheme, ProductInputCommand), BindingTarget>();
        private readonly List<InputActionReference> actionReferences =
            new List<InputActionReference>();
        private InputActionAsset? actions;
        private InputAction? helpAction;
        private InputAction? submitAction;
        private int keyboardSpaceSubmitBindingIndex = -1;
        private InputActionRebindingExtensions.RebindingOperation? rebindOperation;
        private ProductInputScheme? rebindScheme;
        private string? pendingRebindPath;
        private bool initialized;
        private bool actionsWereEnabledForRebind;

        public event System.Action? HelpRequested;
        public event System.Action? GamepadDisconnected;
        public event System.Action? GamepadReconnected;

        public bool IsRebinding => rebindOperation != null;
        public ProductInputBindings CurrentBindings { get; private set; } =
            ProductInputBindings.Default;

        public void Configure(InputSystemUIInputModule configuredModule) =>
            uiInputModule = configuredModule ?? throw new ArgumentNullException(
                nameof(configuredModule));

        private void Awake() => Initialize();

        public void Initialize()
        {
            if (initialized) return;
            if (uiInputModule == null)
                throw new InvalidOperationException("Product input module is not configured.");
            BuildActions();
            initialized = true;
            ApplyBindings(ProductInputBindings.Default);
            InputSystem.onDeviceChange += HandleDeviceChange;
        }

        private void OnDestroy()
        {
            InputSystem.onDeviceChange -= HandleDeviceChange;
            CancelRebind();
            if (helpAction != null) helpAction.performed -= HandleHelpPerformed;
            actions?.Disable();
            foreach (InputActionReference reference in actionReferences)
                if (reference != null) Destroy(reference);
            actionReferences.Clear();
            if (actions != null) Destroy(actions);
            actions = null;
        }

        public void ApplyBindings(ProductInputBindings bindings)
        {
            if (bindings == null) throw new ArgumentNullException(nameof(bindings));
            if (!TryValidate(bindings, out string error))
                throw new ArgumentException(error, nameof(bindings));
            if (!initialized) Initialize();
            if (actions == null) throw new InvalidOperationException(
                "Product input actions are not initialized.");

            bool wasEnabled = actions.enabled;
            if (wasEnabled) actions.Disable();
            foreach (KeyValuePair<(ProductInputScheme, ProductInputCommand), BindingTarget> item
                in bindingTargets)
            {
                string path = bindings.Get(item.Key.Item1, item.Key.Item2);
                item.Value.Action.ApplyBindingOverride(item.Value.BindingIndex, path);
            }
            ApplyKeyboardSpaceFallback(bindings);
            CurrentBindings = bindings;
            if (wasEnabled) actions.Enable();
        }

        public string EffectivePath(ProductInputScheme scheme, ProductInputCommand command)
        {
            BindingTarget target = RequireTarget(scheme, command);
            InputBinding binding = target.Action.bindings[target.BindingIndex];
            return binding.effectivePath;
        }

        public string BindingLabel(ProductInputScheme scheme, ProductInputCommand command) =>
            InputControlPath.ToHumanReadableString(EffectivePath(scheme, command));

        public void RequestHelp() => HelpRequested?.Invoke();

        public static string HumanReadablePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Input control path cannot be empty.", nameof(path));
            return InputControlPath.ToHumanReadableString(path);
        }

        public static string CanonicalControlToken(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Input control path cannot be empty.", nameof(path));
            int layoutEnd = path.IndexOf(">/", StringComparison.Ordinal);
            if (path[0] != '<' || layoutEnd <= 1 || layoutEnd + 2 >= path.Length)
                throw new ArgumentException(
                    "Input control path is not canonical.", nameof(path));
            return path.Substring(layoutEnd + 2);
        }

        public bool BeginRebind(ProductInputScheme scheme, ProductInputCommand command,
            System.Action<string> completed, System.Action? cancelled = null)
        {
            if (completed == null) throw new ArgumentNullException(nameof(completed));
            if (IsRebinding) return false;
            RequireTarget(scheme, command);

            string layout = scheme == ProductInputScheme.Keyboard ? "<Keyboard>" : "<Gamepad>";
            rebindScheme = scheme;
            pendingRebindPath = null;
            actionsWereEnabledForRebind = actions?.enabled == true;
            if (actionsWereEnabledForRebind) actions!.Disable();
            rebindOperation = new InputActionRebindingExtensions.RebindingOperation()
                .WithExpectedControlType<ButtonControl>()
                .WithControlsHavingToMatchPath(layout)
                .WithControlsExcluding("<Pointer>/position")
                .WithControlsExcluding("<Pointer>/delta")
                .WithCancelingThrough("<Keyboard>/escape")
                .WithTimeout(10f)
                .WithMatchingEventsBeingSuppressed()
                .OnApplyBinding((_, path) => pendingRebindPath = path)
                .OnComplete(operation =>
                {
                    string? path = pendingRebindPath;
                    FinishRebind();
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        cancelled?.Invoke();
                        return;
                    }
                    completed(path!);
                })
                .OnCancel(_ =>
                {
                    FinishRebind();
                    cancelled?.Invoke();
                });
            rebindOperation.Start();
            return true;
        }

        public void CancelRebind()
        {
            if (rebindOperation == null)
                return;
            rebindOperation.Cancel();
        }

        public bool TryValidate(ProductSettings settings, out string error)
        {
            if (settings == null)
            {
                error = "Product settings cannot be null.";
                return false;
            }
            return TryValidate(settings.InputBindings, out error);
        }

        public static bool TryValidate(ProductInputBindings bindings, out string error)
        {
            if (bindings == null)
            {
                error = "Product input bindings cannot be null.";
                return false;
            }
            foreach (ProductInputScheme scheme in ProductInputBindings.EnumerateSchemes())
            {
                foreach (ProductInputCommand command in ProductInputBindings.EnumerateCommands())
                {
                    string path = bindings.Get(scheme, command);
                    string? layout;
                    try
                    {
                        layout = InputControlPath.TryGetControlLayout(path);
                    }
                    catch (Exception exception)
                    {
                        error = "Input binding " + scheme + "/" + command +
                            " is invalid: " + exception.Message;
                        return false;
                    }
                    if (string.IsNullOrWhiteSpace(layout) ||
                        !InputSystem.IsFirstLayoutBasedOnSecond(layout, "Button"))
                    {
                        error = "Input binding " + scheme + "/" + command +
                            " does not identify a registered button control.";
                        return false;
                    }
                }
            }
            string keyboardSubmit = bindings.Get(
                ProductInputScheme.Keyboard, ProductInputCommand.Submit);
            string defaultKeyboardSubmit = ProductInputBindings.Default.Get(
                ProductInputScheme.Keyboard, ProductInputCommand.Submit);
            if (string.Equals(keyboardSubmit, defaultKeyboardSubmit,
                    StringComparison.OrdinalIgnoreCase))
            {
                foreach (ProductInputCommand command in ProductInputBindings.EnumerateCommands())
                {
                    if (command == ProductInputCommand.Submit) continue;
                    if (!string.Equals(bindings.Get(ProductInputScheme.Keyboard, command),
                            KeyboardSpacePath, StringComparison.OrdinalIgnoreCase)) continue;
                    error = "Keyboard Space is reserved as the secondary default Submit control.";
                    return false;
                }
            }
            error = string.Empty;
            return true;
        }

        private void BuildActions()
        {
            if (uiInputModule == null)
                throw new InvalidOperationException("Product input module is not configured.");

            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "ProductInputActions.Runtime";
            InputActionMap map = asset.AddActionMap("UI");

            InputAction point = map.AddAction(
                "Point", InputActionType.PassThrough, expectedControlLayout: "Vector2");
            point.AddBinding("<Mouse>/position", groups: KeyboardGroup);
            InputAction leftClick = map.AddAction(
                "Click", InputActionType.PassThrough, expectedControlLayout: "Button");
            leftClick.AddBinding("<Mouse>/leftButton", groups: KeyboardGroup);
            InputAction rightClick = map.AddAction(
                "RightClick", InputActionType.PassThrough, expectedControlLayout: "Button");
            rightClick.AddBinding("<Mouse>/rightButton", groups: KeyboardGroup);
            InputAction middleClick = map.AddAction(
                "MiddleClick", InputActionType.PassThrough, expectedControlLayout: "Button");
            middleClick.AddBinding("<Mouse>/middleButton", groups: KeyboardGroup);
            InputAction scroll = map.AddAction(
                "ScrollWheel", InputActionType.PassThrough, expectedControlLayout: "Vector2");
            scroll.AddBinding("<Mouse>/scroll", groups: KeyboardGroup);

            InputAction navigate = map.AddAction(
                "Navigate", InputActionType.PassThrough, expectedControlLayout: "Vector2");
            AddNavigationBindings(navigate, ProductInputScheme.Keyboard, KeyboardGroup);
            navigate.AddBinding("<Gamepad>/leftStick", groups: GamepadGroup);
            AddNavigationBindings(navigate, ProductInputScheme.Gamepad, GamepadGroup);

            InputAction submit = map.AddAction(
                "Submit", InputActionType.Button, expectedControlLayout: "Button");
            submitAction = submit;
            AddButtonBinding(submit, ProductInputScheme.Keyboard,
                ProductInputCommand.Submit, KeyboardGroup);
            keyboardSpaceSubmitBindingIndex = submit.bindings.Count;
            submit.AddBinding(KeyboardSpacePath, groups: KeyboardGroup);
            AddButtonBinding(submit, ProductInputScheme.Gamepad,
                ProductInputCommand.Submit, GamepadGroup);

            InputAction cancel = map.AddAction(
                "Cancel", InputActionType.Button, expectedControlLayout: "Button");
            AddButtonBinding(cancel, ProductInputScheme.Keyboard,
                ProductInputCommand.Cancel, KeyboardGroup);
            AddButtonBinding(cancel, ProductInputScheme.Gamepad,
                ProductInputCommand.Cancel, GamepadGroup);

            helpAction = map.AddAction(
                "Help", InputActionType.Button, expectedControlLayout: "Button");
            AddButtonBinding(helpAction, ProductInputScheme.Keyboard,
                ProductInputCommand.Help, KeyboardGroup);
            AddButtonBinding(helpAction, ProductInputScheme.Gamepad,
                ProductInputCommand.Help, GamepadGroup);
            helpAction.performed += HandleHelpPerformed;

            uiInputModule.UnassignActions();
            uiInputModule.actionsAsset = asset;
            uiInputModule.point = Reference(point);
            uiInputModule.leftClick = Reference(leftClick);
            uiInputModule.rightClick = Reference(rightClick);
            uiInputModule.middleClick = Reference(middleClick);
            uiInputModule.scrollWheel = Reference(scroll);
            uiInputModule.move = Reference(navigate);
            uiInputModule.submit = Reference(submit);
            uiInputModule.cancel = Reference(cancel);
            uiInputModule.deselectOnBackgroundClick = false;

            actions = asset;
            asset.Enable();
        }

        private void AddNavigationBindings(InputAction navigate, ProductInputScheme scheme,
            string group)
        {
            int root = navigate.bindings.Count;
            navigate.AddCompositeBinding("2DVector")
                .With("Up", ProductInputBindings.Default.Get(scheme, ProductInputCommand.Up), group)
                .With("Down", ProductInputBindings.Default.Get(scheme, ProductInputCommand.Down), group)
                .With("Left", ProductInputBindings.Default.Get(scheme, ProductInputCommand.Left), group)
                .With("Right", ProductInputBindings.Default.Get(scheme, ProductInputCommand.Right), group);
            Register(scheme, ProductInputCommand.Up, navigate, root + 1);
            Register(scheme, ProductInputCommand.Down, navigate, root + 2);
            Register(scheme, ProductInputCommand.Left, navigate, root + 3);
            Register(scheme, ProductInputCommand.Right, navigate, root + 4);
        }

        private void AddButtonBinding(InputAction action, ProductInputScheme scheme,
            ProductInputCommand command, string group)
        {
            int index = action.bindings.Count;
            action.AddBinding(
                ProductInputBindings.Default.Get(scheme, command), groups: group);
            Register(scheme, command, action, index);
        }

        private void Register(ProductInputScheme scheme, ProductInputCommand command,
            InputAction action, int bindingIndex)
        {
            var key = (scheme, command);
            if (bindingTargets.ContainsKey(key))
                throw new InvalidOperationException("Duplicate product input binding target.");
            bindingTargets.Add(key, new BindingTarget(action, bindingIndex));
        }

        private void ApplyKeyboardSpaceFallback(ProductInputBindings bindings)
        {
            if (submitAction == null || keyboardSpaceSubmitBindingIndex < 0)
                throw new InvalidOperationException(
                    "The secondary keyboard Submit binding is not initialized.");
            string configured = bindings.Get(
                ProductInputScheme.Keyboard, ProductInputCommand.Submit);
            string defaultPath = ProductInputBindings.Default.Get(
                ProductInputScheme.Keyboard, ProductInputCommand.Submit);
            if (string.Equals(configured, defaultPath, StringComparison.OrdinalIgnoreCase))
                submitAction.RemoveBindingOverride(keyboardSpaceSubmitBindingIndex);
            else
                submitAction.ApplyBindingOverride(keyboardSpaceSubmitBindingIndex, string.Empty);
        }

        private BindingTarget RequireTarget(ProductInputScheme scheme, ProductInputCommand command)
        {
            if (!bindingTargets.TryGetValue((scheme, command), out BindingTarget? target))
                throw new ArgumentOutOfRangeException(nameof(command), command,
                    "Unknown product input binding target.");
            return target;
        }

        private InputActionReference Reference(InputAction action)
        {
            InputActionReference reference = InputActionReference.Create(action);
            actionReferences.Add(reference);
            return reference;
        }

        private void HandleHelpPerformed(InputAction.CallbackContext _) => RequestHelp();

        private void HandleDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (!(device is Gamepad)) return;
            if (change == InputDeviceChange.Disconnected || change == InputDeviceChange.Removed)
            {
                if (IsRebinding && rebindScheme == ProductInputScheme.Gamepad) CancelRebind();
                GamepadDisconnected?.Invoke();
            }
            else if (change == InputDeviceChange.Reconnected || change == InputDeviceChange.Added)
                GamepadReconnected?.Invoke();
        }

        private void FinishRebind()
        {
            InputActionRebindingExtensions.RebindingOperation? operation = rebindOperation;
            rebindOperation = null;
            operation?.Dispose();
            rebindScheme = null;
            pendingRebindPath = null;
            if (actionsWereEnabledForRebind && actions != null) actions.Enable();
            actionsWereEnabledForRebind = false;
        }

        private sealed class BindingTarget
        {
            public InputAction Action { get; }
            public int BindingIndex { get; }

            public BindingTarget(InputAction action, int bindingIndex)
            {
                Action = action;
                BindingIndex = bindingIndex;
            }
        }
    }
}
