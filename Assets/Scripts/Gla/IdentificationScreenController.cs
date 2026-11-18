using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UIElements;

namespace FlipNflop.Gla
{
    /// <summary>
    /// UI Toolkit controller of the entry screen of the main menu (gla-13 designs D2, D5 and D6).
    /// Overlays <c>Resources/UI/Identification.uxml</c> (with <c>Identification.uss</c>) on the root of
    /// the menu <see cref="UIDocument"/>: the waiting view while <see cref="PlayerSession.Mode"/> is
    /// <see cref="PlayerMode.Loading"/>, and the e-mail, code and guest consent steps of
    /// <see cref="IdentificationScreenModel"/> while it is <see cref="PlayerMode.Undecided"/>. The menu
    /// under the overlay is disabled, so it is available only after a choice. It also adds to the
    /// <c>MainMenu</c> panel the button "Identificado — Trocar e-mail" or "Convidado — Identificar-me",
    /// which calls <see cref="PlayerSession.ResetIdentity"/>.
    /// The texts come from the localization table <see cref="LocalizationTable"/>, loaded
    /// asynchronously (WebGL cannot wait for it), with the Portuguese texts of this class as fallback.
    /// Created by <see cref="GlaServices.AttachEntryScreen"/>; disposed when the overlay leaves the panel.
    /// </summary>
    public sealed class IdentificationScreenController : IDisposable
    {
        #region Constants

        public const string ResourcePath = "UI/Identification";
        public const string LocalizationTable = "Identification";
        public const string OverlayName = "GlaEntryOverlay";
        public const string IdentityButtonName = "GlaIdentityButton";
        private const string MainMenuPanelName = "MainMenu";
        private const string IdentityButtonClass = "gla-identity-button";

        #endregion

        #region Texts

        /// <summary>
        /// The keys of the table <see cref="LocalizationTable"/> and their pt-BR texts, used until the
        /// table loads and whenever an entry is missing.
        /// </summary>
        private static readonly Dictionary<string, string> FallbackTexts = new Dictionary<string, string>
        {
            ["loading"] = "Carregando...",
            ["entry_title"] = "Antes de jogar",
            ["consent_notice"] = "Ao informar seu e-mail institucional, você aceita que suas jogadas sejam registradas e vistas pelo seu professor.",
            ["email_label"] = "E-mail institucional (opcional)",
            ["request_code"] = "Receber código",
            ["enter_as_guest"] = "Entrar como convidado",
            ["shared_computer_tip"] = "Em computador compartilhado, use \"Trocar e-mail\" no menu ao terminar.",
            ["code_title"] = "Código de verificação",
            ["code_prompt"] = "Digite o código que enviamos para o seu e-mail.",
            ["code_label"] = "Código",
            ["confirm"] = "Confirmar",
            ["resend_code"] = "Reenviar código",
            ["back"] = "Voltar",
            ["code_resent"] = "Enviamos um novo código para o seu e-mail.",
            ["guest_title"] = "Entrar como convidado",
            ["guest_question"] = "Você aceita que suas jogadas sejam registradas de forma anônima, sem o seu e-mail, para o seu professor ver os resultados da turma?",
            ["guest_accept"] = "Sim, aceito",
            ["guest_refuse"] = "Não, jogar sem registro",
            ["network_error"] = "Não foi possível falar com o servidor. Tente de novo ou entre como convidado.",
            ["menu_identified"] = "Identificado — Trocar e-mail",
            ["menu_guest"] = "Convidado — Identificar-me",
        };

        /// <summary>
        /// The element of the layout that shows each fixed text: a <see cref="Label"/>, a
        /// <see cref="Button"/> or the label of a <see cref="TextField"/>.
        /// </summary>
        private static readonly KeyValuePair<string, string>[] TextBindings =
        {
            new KeyValuePair<string, string>("WaitingLabel", "loading"),
            new KeyValuePair<string, string>("EmailTitle", "entry_title"),
            new KeyValuePair<string, string>("ConsentNotice", "consent_notice"),
            new KeyValuePair<string, string>("EmailField", "email_label"),
            new KeyValuePair<string, string>("RequestCodeButton", "request_code"),
            new KeyValuePair<string, string>("GuestButton", "enter_as_guest"),
            new KeyValuePair<string, string>("SharedComputerTip", "shared_computer_tip"),
            new KeyValuePair<string, string>("CodeTitle", "code_title"),
            new KeyValuePair<string, string>("CodePrompt", "code_prompt"),
            new KeyValuePair<string, string>("CodeField", "code_label"),
            new KeyValuePair<string, string>("ConfirmButton", "confirm"),
            new KeyValuePair<string, string>("ResendCodeButton", "resend_code"),
            new KeyValuePair<string, string>("CodeBackButton", "back"),
            new KeyValuePair<string, string>("CodeGuestButton", "enter_as_guest"),
            new KeyValuePair<string, string>("GuestTitle", "guest_title"),
            new KeyValuePair<string, string>("GuestQuestion", "guest_question"),
            new KeyValuePair<string, string>("GuestAcceptButton", "guest_accept"),
            new KeyValuePair<string, string>("GuestRefuseButton", "guest_refuse"),
            new KeyValuePair<string, string>("GuestBackButton", "back"),
        };

        #endregion

        #region Fields

        private readonly VisualElement root;
        private readonly VisualElement overlay;
        private readonly Button identityButton;
        private readonly PlayerSession session;
        private readonly IdentificationScreenModel model;
        private readonly IGlaLog log;
        private readonly Dictionary<string, string> texts = new Dictionary<string, string>(FallbackTexts);
        private readonly List<VisualElement> disabledMenuElements = new List<VisualElement>();

        private VisualElement waitingView;
        private VisualElement emailView;
        private VisualElement codeView;
        private VisualElement guestView;
        private TextField emailField;
        private TextField codeField;
        private Label messageLabel;
        private Button[] actionButtons;

        private PlayerMode renderedMode;
        private IdentificationStep renderedStep;
        private bool disposed;

        #endregion

        #region Construction

        /// <summary>
        /// Builds the overlay on the root of the menu document, replacing one left by a previous call.
        /// Interacts with <see cref="Resources.Load{T}(string)"/>.
        /// </summary>
        /// <param name="root">The root visual element of the menu document.</param>
        /// <param name="session">The identification state.</param>
        /// <param name="log">The GLA log.</param>
        /// <returns>The controller, or <c>null</c> when the layout is missing (then the menu stays as in v0.15.0 and nothing is collected).</returns>
        public static IdentificationScreenController Attach(VisualElement root, PlayerSession session, IGlaLog log)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (log == null) throw new ArgumentNullException(nameof(log));

            root.Q<VisualElement>(OverlayName)?.RemoveFromHierarchy();
            root.Q<VisualElement>(IdentityButtonName)?.RemoveFromHierarchy();

            VisualTreeAsset layout = Resources.Load<VisualTreeAsset>(ResourcePath);
            if (layout == null)
            {
                log.Warning($"Resources/{ResourcePath}.uxml was not found: the entry screen cannot be shown.");
                return null;
            }

            StyleSheet styleSheet = Resources.Load<StyleSheet>(ResourcePath);
            if (styleSheet == null)
                log.Warning($"Resources/{ResourcePath}.uss was not found: the entry screen is shown without its style.");

            return new IdentificationScreenController(root, layout, styleSheet, session, log);
        }

        /// <summary>
        /// Creates the overlay and the identity button, wires the buttons and draws the current state.
        /// </summary>
        /// <param name="root">The root visual element of the menu document.</param>
        /// <param name="layout">The entry screen layout.</param>
        /// <param name="styleSheet">The entry screen style, or <c>null</c>.</param>
        /// <param name="session">The identification state.</param>
        /// <param name="log">The GLA log.</param>
        private IdentificationScreenController(VisualElement root, VisualTreeAsset layout, StyleSheet styleSheet, PlayerSession session, IGlaLog log)
        {
            this.root = root;
            this.session = session;
            this.log = log;
            model = new IdentificationScreenModel(session);

            if (styleSheet != null && !root.styleSheets.Contains(styleSheet))
                root.styleSheets.Add(styleSheet);

            overlay = layout.Instantiate();
            overlay.name = OverlayName;
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0;
            overlay.style.top = 0;
            overlay.style.right = 0;
            overlay.style.bottom = 0;
            root.Add(overlay);

            identityButton = new Button { name = IdentityButtonName };
            identityButton.AddToClassList(IdentityButtonClass);
            identityButton.clicked += OnIdentityButtonClicked;
            VisualElement mainMenuPanel = root.Q<VisualElement>(MainMenuPanelName);
            (mainMenuPanel ?? root).Add(identityButton);

            BindElements();
            ApplyTexts();

            renderedMode = session.Mode;
            renderedStep = model.Step;
            session.Changed += Render;
            model.Changed += Render;
            overlay.RegisterCallback<DetachFromPanelEvent>(OnOverlayDetached);
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;

            LoadLocalizedTexts();
            Render();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Stops listening to <see cref="PlayerSession"/>, to the model and to the locale, gives the
        /// menu back its interaction and removes the overlay and the identity button.
        /// </summary>
        public void Dispose()
        {
            if (disposed) return;

            Release();
            SetMenuInteractive(true);
            overlay.RemoveFromHierarchy();
            identityButton.RemoveFromHierarchy();
        }

        /// <summary>
        /// Stops listening to <see cref="PlayerSession"/>, to the model and to the locale, without
        /// touching the visual tree, which may be leaving its panel.
        /// </summary>
        private void Release()
        {
            if (disposed) return;

            disposed = true;
            session.Changed -= Render;
            model.Changed -= Render;
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            overlay.UnregisterCallback<DetachFromPanelEvent>(OnOverlayDetached);
        }

        #endregion

        #region Binding

        /// <summary>
        /// Finds the elements of the layout by name and wires the buttons and the Enter key to the model.
        /// A missing element is logged and skipped.
        /// </summary>
        private void BindElements()
        {
            waitingView = Find<VisualElement>("WaitingView");
            emailView = Find<VisualElement>("EmailView");
            codeView = Find<VisualElement>("CodeView");
            guestView = Find<VisualElement>("GuestView");
            emailField = Find<TextField>("EmailField");
            codeField = Find<TextField>("CodeField");
            messageLabel = Find<Label>("MessageLabel");

            Button requestCodeButton = Bind("RequestCodeButton", () => model.RequestCode(emailField?.value));
            Button guestButton = Bind("GuestButton", model.ChooseGuest);
            Button confirmButton = Bind("ConfirmButton", () => model.ConfirmCode(codeField?.value));
            Button resendButton = Bind("ResendCodeButton", model.ResendCode);
            Button codeBackButton = Bind("CodeBackButton", model.Back);
            Button codeGuestButton = Bind("CodeGuestButton", model.ChooseGuest);
            Button acceptButton = Bind("GuestAcceptButton", () => model.AnswerGuestConsent(true));
            Button refuseButton = Bind("GuestRefuseButton", () => model.AnswerGuestConsent(false));
            Button guestBackButton = Bind("GuestBackButton", model.Back);
            actionButtons = new[]
            {
                requestCodeButton, guestButton, confirmButton, resendButton, codeBackButton,
                codeGuestButton, acceptButton, refuseButton, guestBackButton,
            };

            emailField?.RegisterCallback<KeyDownEvent>(OnEmailKeyDown, TrickleDown.TrickleDown);
            codeField?.RegisterCallback<KeyDownEvent>(OnCodeKeyDown, TrickleDown.TrickleDown);
        }

        /// <summary>
        /// Finds an element of the overlay by name.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="name">The element name in <c>Identification.uxml</c>.</param>
        /// <returns>The element, or <c>null</c>.</returns>
        private T Find<T>(string name) where T : VisualElement
        {
            T element = overlay.Q<T>(name);
            if (element == null)
                log.Warning($"Identification.uxml has no {typeof(T).Name} named {name}.");

            return element;
        }

        /// <summary>
        /// Finds a button and wires its click.
        /// </summary>
        /// <param name="name">The button name.</param>
        /// <param name="action">What the click does.</param>
        /// <returns>The button, or <c>null</c>.</returns>
        private Button Bind(string name, Action action)
        {
            Button button = Find<Button>(name);
            if (button != null) button.clicked += action;
            return button;
        }

        #endregion

        #region Rendering

        /// <summary>
        /// Draws the state of <see cref="PlayerSession"/> and of the model: overlay or menu, the
        /// visible step, the enabled buttons, the message and the identity button. When the mode
        /// returns to <see cref="PlayerMode.Undecided"/>, the screen restarts at the e-mail step with
        /// empty fields, so that a second student never sees the e-mail of the first.
        /// </summary>
        private void Render()
        {
            if (disposed) return;

            PlayerMode mode = session.Mode;
            if (mode == PlayerMode.Undecided && renderedMode != PlayerMode.Undecided)
            {
                renderedMode = mode;
                emailField?.SetValueWithoutNotify(string.Empty);
                codeField?.SetValueWithoutNotify(string.Empty);
                model.Reset();
                return;
            }

            renderedMode = mode;
            bool covering = mode == PlayerMode.Loading || mode == PlayerMode.Undecided;
            bool deciding = mode == PlayerMode.Undecided;
            SetVisible(overlay, covering);
            SetMenuInteractive(!covering);

            SetVisible(waitingView, mode == PlayerMode.Loading);
            SetVisible(emailView, deciding && model.Step == IdentificationStep.Email);
            SetVisible(codeView, deciding && model.Step == IdentificationStep.Code);
            SetVisible(guestView, deciding && model.Step == IdentificationStep.GuestConsent);

            string message = MessageText();
            if (messageLabel != null) messageLabel.text = message ?? string.Empty;
            SetVisible(messageLabel, deciding && message != null);

            foreach (Button button in actionButtons)
                button?.SetEnabled(!model.Busy);

            bool identityVisible = (mode == PlayerMode.Identified || mode == PlayerMode.Guest) && session.State != SessionState.Rejected;
            identityButton.text = Text(mode == PlayerMode.Identified ? "menu_identified" : "menu_guest");
            SetVisible(identityButton, identityVisible);

            if (deciding && model.Step != renderedStep)
            {
                if (model.Step == IdentificationStep.Code)
                {
                    codeField?.SetValueWithoutNotify(string.Empty);
                    codeField?.Focus();
                }
                else if (model.Step == IdentificationStep.Email)
                {
                    emailField?.Focus();
                }
            }

            renderedStep = model.Step;
        }

        /// <summary>
        /// The text of the current message (design D6), or <c>null</c> without message.
        /// </summary>
        /// <returns>The text.</returns>
        private string MessageText()
        {
            switch (model.Message)
            {
                case IdentificationMessage.ApiMessage:
                    return model.ApiMessageText;
                case IdentificationMessage.NetworkError:
                    return Text("network_error");
                case IdentificationMessage.CodeResent:
                    return Text("code_resent");
                default:
                    return null;
            }
        }

        /// <summary>
        /// Enables or disables the menu elements under the overlay (the siblings of the overlay in the
        /// root), so that the keyboard cannot reach them while a choice is pending.
        /// </summary>
        /// <param name="interactive">Whether the menu can be used.</param>
        private void SetMenuInteractive(bool interactive)
        {
            if (interactive)
            {
                foreach (VisualElement element in disabledMenuElements)
                    element.SetEnabled(true);

                disabledMenuElements.Clear();
                return;
            }

            foreach (VisualElement child in root.Children())
            {
                if (child == overlay || !child.enabledSelf || disabledMenuElements.Contains(child)) continue;

                child.SetEnabled(false);
                disabledMenuElements.Add(child);
            }
        }

        /// <summary>
        /// Shows or hides an element through its inline display, which does not depend on the style sheets.
        /// </summary>
        /// <param name="element">The element, or <c>null</c>.</param>
        /// <param name="visible">Whether it is shown.</param>
        private static void SetVisible(VisualElement element, bool visible)
        {
            if (element == null) return;
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        #endregion

        #region Localization

        /// <summary>
        /// Requests the table <see cref="LocalizationTable"/> of the selected locale without blocking.
        /// Interacts with <see cref="LocalizationSettings.StringDatabase"/>.
        /// </summary>
        private void LoadLocalizedTexts()
        {
            try
            {
                AsyncOperationHandle<StringTable> handle = LocalizationSettings.StringDatabase.GetTableAsync(LocalizationTable);
                handle.Completed += OnTableLoaded;
            }
            catch (Exception exception)
            {
                log.Warning($"The localization table {LocalizationTable} could not be requested ({exception.Message}); using the texts in code.");
            }
        }

        /// <summary>
        /// Replaces the fallback texts by the entries of the table, when it loaded, and redraws.
        /// </summary>
        /// <param name="handle">The table loading.</param>
        private void OnTableLoaded(AsyncOperationHandle<StringTable> handle)
        {
            if (disposed) return;

            texts.Clear();
            foreach (KeyValuePair<string, string> fallback in FallbackTexts)
                texts[fallback.Key] = fallback.Value;

            StringTable table = handle.Status == AsyncOperationStatus.Succeeded ? handle.Result : null;
            if (table != null)
            {
                foreach (KeyValuePair<string, string> fallback in FallbackTexts)
                {
                    StringTableEntry entry = table.GetEntry(fallback.Key);
                    string localized = entry?.GetLocalizedString();
                    if (!string.IsNullOrEmpty(localized))
                        texts[fallback.Key] = localized;
                }
            }

            ApplyTexts();
            Render();
        }

        /// <summary>
        /// Reloads the texts when the player changes the language.
        /// </summary>
        /// <param name="locale">The new locale.</param>
        private void OnLocaleChanged(Locale locale)
        {
            if (!disposed) LoadLocalizedTexts();
        }

        /// <summary>
        /// Writes the current texts on the elements of <see cref="TextBindings"/>.
        /// </summary>
        private void ApplyTexts()
        {
            foreach (KeyValuePair<string, string> binding in TextBindings)
            {
                VisualElement element = overlay.Q<VisualElement>(binding.Key);
                string text = Text(binding.Value);
                if (element is TextField field)
                    field.label = text;
                else if (element is TextElement textElement)
                    textElement.text = text;
            }
        }

        /// <summary>
        /// The current text of a key.
        /// </summary>
        /// <param name="key">The key of the table.</param>
        /// <returns>The localized text, or the fallback.</returns>
        private string Text(string key)
        {
            return texts.TryGetValue(key, out string text) ? text : key;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// "Trocar e-mail" / "Identificar-me": calls <see cref="PlayerSession.ResetIdentity"/>.
        /// </summary>
        private void OnIdentityButtonClicked()
        {
            session.ResetIdentity();
        }

        /// <summary>
        /// Enter in the e-mail field asks for the code.
        /// </summary>
        /// <param name="evt">The key event.</param>
        private void OnEmailKeyDown(KeyDownEvent evt)
        {
            if (IsSubmitKey(evt.keyCode)) model.RequestCode(emailField.value);
        }

        /// <summary>
        /// Enter in the code field confirms the code.
        /// </summary>
        /// <param name="evt">The key event.</param>
        private void OnCodeKeyDown(KeyDownEvent evt)
        {
            if (IsSubmitKey(evt.keyCode)) model.ConfirmCode(codeField.value);
        }

        /// <summary>
        /// Stops listening when the menu document goes away (scene change).
        /// </summary>
        /// <param name="evt">The detach event.</param>
        private void OnOverlayDetached(DetachFromPanelEvent evt)
        {
            Release();
        }

        /// <summary>
        /// Whether a key submits a field.
        /// </summary>
        /// <param name="keyCode">The key.</param>
        /// <returns><c>true</c> for Return and the keypad Enter.</returns>
        private static bool IsSubmitKey(KeyCode keyCode) => keyCode == KeyCode.Return || keyCode == KeyCode.KeypadEnter;

        #endregion
    }
}
