using Lambda.UI;
using EFT.InputSystem;
using EFT.UI.Screens;
using System;

namespace Lambda.Core.Main.UI
{
    internal class FactionSelectionEftScreen : EftScreen<FactionSelectionEftScreen.FactionSelectionEftScreenController, FactionSelectionEftScreen>
    {
        public const EEftScreenType FAKETYPE = EEftScreenType.DressRoom; // unused for screen register

        internal class FactionSelectionEftScreenController : EftScreenManager.GClass3861<FactionSelectionEftScreenController, FactionSelectionEftScreen>
        {
            public override EEftScreenType ScreenType => FAKETYPE;
            public override EStateSwitcher UnrestrictedFrameRate => EStateSwitcher.Enabled;
            public override EStateSwitcher MenuChatBarVisibility => EStateSwitcher.Disabled;
            public override EStateSwitcher TaskBarButtonsAvailability => EStateSwitcher.Disabled;
            public override EStateSwitcher IgnorePlayerInput => EStateSwitcher.Enabled;
            public override EStateSwitcher ShowEnvironment => EStateSwitcher.Disabled;
            public override EStateSwitcher EnvironmentOverlay => EStateSwitcher.Disabled;
            public override EStateSwitcher ShowEnvironmentCamera => EStateSwitcher.Disabled;
            public override EStateSwitcher CameraBlur => EStateSwitcher.Enabled;

            readonly Action<Faction> onSelected;

            internal FactionSelectionEftScreenController(Action<Faction> selectionCallback) : base()
            {
                this.onSelected = selectionCallback;
            }

            internal void SendSelected(Faction faction)
            {
                onSelected?.Invoke(faction);
            }
        }

        FactionSelection module;

        void Awake()
        {
            module = GetComponent<FactionSelection>();
            module.OnFactionSelected += Module_OnFactionSelected;
        }

        public override void Show(FactionSelectionEftScreenController controller)
        {
            ShowGameObject();
        }

        private void Module_OnFactionSelected(Faction faction)
        {
            ScreenController?.SendSelected(faction);
            ScreenController?.CloseScreen();
        }

        public void Cancel()
        {
            ScreenController?.CloseScreen();
        }

        public override ETranslateResult TranslateCommand(ECommand command) => ETranslateResult.BlockAll;
    }
}
