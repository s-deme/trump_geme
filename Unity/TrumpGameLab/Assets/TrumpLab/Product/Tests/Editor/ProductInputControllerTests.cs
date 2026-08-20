#nullable enable

using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace TrumpLab.Product.Tests
{
    public sealed class ProductInputControllerTests : InputTestFixture
    {
        private GameObject? eventSystemObject;
        private ProductInputController? controller;
        private InputSystemUIInputModule? inputModule;

        [SetUp]
        public override void Setup()
        {
            base.Setup();

            eventSystemObject = new GameObject("Product input test event system");
            eventSystemObject.SetActive(false);
            eventSystemObject.AddComponent<EventSystem>();
            inputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
            controller = eventSystemObject.AddComponent<ProductInputController>();
            controller.Configure(inputModule);
            eventSystemObject.SetActive(true);
            controller.Initialize();
        }

        [TearDown]
        public override void TearDown()
        {
            if (eventSystemObject != null)
                UnityEngine.Object.DestroyImmediate(eventSystemObject);
            controller = null;
            inputModule = null;
            eventSystemObject = null;

            base.TearDown();
        }

        [Test]
        public void CanonicalControlTokenDoesNotExposeDeviceOrLocalizedDisplayNames()
        {
            Assert.That(ProductInputController.CanonicalControlToken(
                "<Keyboard>/enter"), Is.EqualTo("enter"));
            Assert.That(ProductInputController.CanonicalControlToken(
                "<Gamepad>/dpad/up"), Is.EqualTo("dpad/up"));
            Assert.Throws<ArgumentException>(() =>
                ProductInputController.CanonicalControlToken("Enter [Keyboard]"));
        }

        [Test]
        public void KeyboardHelpBindingRaisesHelpRequested()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            int requests = 0;
            Controller.HelpRequested += () => requests++;

            Press(keyboard.f1Key);
            Release(keyboard.f1Key);

            Assert.That(requests, Is.EqualTo(1));
        }

        [Test]
        public void GamepadHelpBindingRaisesHelpRequested()
        {
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            int requests = 0;
            Controller.HelpRequested += () => requests++;

            Press(gamepad.buttonNorth);
            Release(gamepad.buttonNorth);

            Assert.That(requests, Is.EqualTo(1));
        }

        [Test]
        public void ApplyingBindingOverrideMovesKeyboardHelpToTheNewControl()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            ProductInputBindings bindings = ProductInputBindings.Default.With(
                ProductInputScheme.Keyboard, ProductInputCommand.Help, "<Keyboard>/f2");
            int requests = 0;
            Controller.HelpRequested += () => requests++;

            Controller.ApplyBindings(bindings);

            Assert.That(Controller.EffectivePath(
                ProductInputScheme.Keyboard, ProductInputCommand.Help),
                Is.EqualTo("<Keyboard>/f2"));
            Press(keyboard.f1Key);
            Release(keyboard.f1Key);
            Assert.That(requests, Is.Zero);

            Press(keyboard.f2Key);
            Release(keyboard.f2Key);
            Assert.That(requests, Is.EqualTo(1));
        }

        [Test]
        public void GamepadRemovalAndAdditionRaiseConnectionEvents()
        {
            Gamepad first = InputSystem.AddDevice<Gamepad>();
            int disconnected = 0;
            int reconnected = 0;
            Controller.GamepadDisconnected += () => disconnected++;
            Controller.GamepadReconnected += () => reconnected++;

            InputSystem.RemoveDevice(first);
            InputSystem.AddDevice<Gamepad>();

            Assert.That(disconnected, Is.EqualTo(1));
            Assert.That(reconnected, Is.EqualTo(1));
        }

        [Test]
        public void MouseKeyboardAndGamepadDriveTheUiActions()
        {
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            InputSystemUIInputModule module = InputModule;
            int keyboardSubmits = 0;
            int keyboardCancels = 0;
            module.submit.action!.performed += _ => keyboardSubmits++;
            module.cancel.action!.performed += _ => keyboardCancels++;

            Move(mouse.position, new Vector2(320f, 180f));
            Assert.That(module.point.action!.ReadValue<Vector2>(),
                Is.EqualTo(new Vector2(320f, 180f)));
            Press(mouse.leftButton);
            Assert.That(module.leftClick.action!.ReadValue<float>(), Is.EqualTo(1f));
            Release(mouse.leftButton);

            Press(keyboard.upArrowKey);
            Assert.That(module.move.action!.ReadValue<Vector2>(), Is.EqualTo(Vector2.up));
            Release(keyboard.upArrowKey);
            Press(keyboard.enterKey);
            Release(keyboard.enterKey);
            Press(keyboard.spaceKey);
            Release(keyboard.spaceKey);
            Press(keyboard.escapeKey);
            Release(keyboard.escapeKey);

            Press(gamepad.dpad.right);
            Assert.That(module.move.action!.ReadValue<Vector2>(), Is.EqualTo(Vector2.right));
            Release(gamepad.dpad.right);
            Press(gamepad.buttonSouth);
            Release(gamepad.buttonSouth);
            Press(gamepad.buttonEast);
            Release(gamepad.buttonEast);

            Assert.That(keyboardSubmits, Is.EqualTo(3));
            Assert.That(keyboardCancels, Is.EqualTo(2));
        }

        [Test]
        public void RebindingKeyboardSubmitDisablesTheDefaultSpaceFallback()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            int submits = 0;
            InputModule.submit.action!.performed += _ => submits++;

            ProductInputBindings rebound = ProductInputBindings.Default.With(
                ProductInputScheme.Keyboard, ProductInputCommand.Submit, "<Keyboard>/q");
            Controller.ApplyBindings(rebound);
            Press(keyboard.spaceKey);
            Release(keyboard.spaceKey);
            Press(keyboard.qKey);
            Release(keyboard.qKey);

            Assert.That(submits, Is.EqualTo(1));
        }

        [Test]
        public void CancellingRebindRestoresExistingActions()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            bool cancelled = false;
            int requests = 0;
            Controller.HelpRequested += () => requests++;

            Assert.That(Controller.BeginRebind(ProductInputScheme.Keyboard,
                ProductInputCommand.Help, _ => Assert.Fail("Rebind unexpectedly completed."),
                () => cancelled = true), Is.True);
            Assert.That(Controller.IsRebinding, Is.True);

            Controller.CancelRebind();

            Assert.That(cancelled, Is.True);
            Assert.That(Controller.IsRebinding, Is.False);
            Press(keyboard.f1Key);
            Release(keyboard.f1Key);
            Assert.That(requests, Is.EqualTo(1));
        }

        [Test]
        public void InteractiveRebindCompletesWithCanonicalControlPath()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            string? completedPath = null;

            Assert.That(Controller.BeginRebind(ProductInputScheme.Keyboard,
                ProductInputCommand.Help, path => completedPath = path), Is.True);

            Press(keyboard.f2Key);
            Release(keyboard.f2Key);

            Assert.That(Controller.IsRebinding, Is.False);
            Assert.That(completedPath, Is.EqualTo("<Keyboard>/f2"));
        }

        [Test]
        public void GamepadRemovalCancelsGamepadRebindAndRestoresKeyboardActions()
        {
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            bool cancelled = false;
            int helpRequests = 0;
            Controller.HelpRequested += () => helpRequests++;

            Assert.That(Controller.BeginRebind(ProductInputScheme.Gamepad,
                ProductInputCommand.Help, _ => Assert.Fail("Rebind unexpectedly completed."),
                () => cancelled = true), Is.True);

            InputSystem.RemoveDevice(gamepad);

            Assert.That(cancelled, Is.True);
            Assert.That(Controller.IsRebinding, Is.False);
            Press(keyboard.f1Key);
            Release(keyboard.f1Key);
            Assert.That(helpRequests, Is.EqualTo(1));
        }

        [Test]
        public void SemanticValidationRejectsUnknownAndNonButtonControls()
        {
            ProductInputBindings unknown = ProductInputBindings.Default.With(
                ProductInputScheme.Keyboard, ProductInputCommand.Help,
                "<Keyboard>/notARealKey");
            ProductInputBindings nonButton = ProductInputBindings.Default.With(
                ProductInputScheme.Gamepad, ProductInputCommand.Help,
                "<Gamepad>/leftStick");

            Assert.That(ProductInputController.TryValidate(unknown, out string unknownError),
                Is.False);
            Assert.That(unknownError, Does.Contain("registered button"));
            Assert.That(ProductInputController.TryValidate(nonButton, out string controlError),
                Is.False);
            Assert.That(controlError, Does.Contain("registered button"));
            Assert.Throws<ArgumentException>(() => Controller.ApplyBindings(unknown));
            Assert.Throws<ArgumentException>(() => Controller.ApplyBindings(nonButton));
        }

        [Test]
        public void BindingsRejectDuplicatesAndDeviceMismatches()
        {
            Assert.Throws<ArgumentException>(() => ProductInputBindings.Default.With(
                ProductInputScheme.Keyboard, ProductInputCommand.Help,
                "<Keyboard>/enter"));
            Assert.Throws<ArgumentException>(() => ProductInputBindings.Default.With(
                ProductInputScheme.Keyboard, ProductInputCommand.Help,
                "<Keyboard>/ENTER"));
            Assert.Throws<ArgumentException>(() => ProductInputBindings.Default.With(
                ProductInputScheme.Keyboard, ProductInputCommand.Help,
                "<Gamepad>/buttonNorth"));
            Assert.Throws<ArgumentException>(() => ProductInputBindings.Default.With(
                ProductInputScheme.Gamepad, ProductInputCommand.Help,
                "<Keyboard>/f1"));
            ProductInputBindings reservedSpace = ProductInputBindings.Default.With(
                ProductInputScheme.Keyboard, ProductInputCommand.Help,
                "<Keyboard>/space");
            Assert.That(ProductInputController.TryValidate(
                reservedSpace, out string reservedError), Is.False);
            Assert.That(reservedError, Does.Contain("Space"));
        }

        private ProductInputController Controller => controller ??
            throw new InvalidOperationException("The product input controller is not ready.");
        private InputSystemUIInputModule InputModule => inputModule ??
            throw new InvalidOperationException("The product input module is not ready.");
    }
}
