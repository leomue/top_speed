using System;
using TopSpeed.Audio;
using TopSpeed.Core;
using TopSpeed.Core.Multiplayer;
using TopSpeed.Core.Settings;
using TopSpeed.Core.Updates;
using TopSpeed.Data;
using TopSpeed.Input;
using TopSpeed.Menu;
using TopSpeed.Network;
using TopSpeed.Speech;
using TopSpeed.Windowing;

namespace TopSpeed.Game
{
    internal sealed partial class Game
    {
        public Game(GameWindow window)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _settingsManager = new SettingsManager();
            var settingsLoad = _settingsManager.Load();
            _settings = settingsLoad.Settings;
            _settingsIssues = settingsLoad.Issues;
            _audio = new AudioManager(_settings.HrtfAudio, _settings.AutoDetectAudioDeviceFormat);
            _input = new InputManager(_window.Handle);
            _speech = new SpeechService(_input.IsAnyInputHeld);
            _speech.ScreenReaderRateMs = _settings.ScreenReaderRateMs;
            _input.JoystickScanTimedOut += () => _speech.Speak("No joystick detected.");
            _input.SetDeviceMode(_settings.DeviceMode);
            _raceInput = new RaceInput(_settings);
            _setup = new RaceSetup();
            _menu = new MenuManager(_audio, _speech, () => _settings.UsageHints);
            _dialogs = new DialogManager(_menu);
            _choices = new ChoiceDialogManager(_menu, message => _speech.Speak(message));
            _menu.SetWrapNavigation(_settings.MenuWrapNavigation);
            _menu.SetMenuSoundPreset(_settings.MenuSoundPreset);
            _menu.SetMenuNavigatePanning(_settings.MenuNavigatePanning);
            _selection = new RaceSelection(_setup, _settings);
            _menuRegistry = new MenuRegistry(_menu, _settings, _setup, _raceInput, _selection, this, this, this, this, this, this);
            _inputMapping = new InputMappingHandler(_input, _raceInput, _settings, _speech, SaveSettings);
            _updateConfig = UpdateConfig.Default;
            _updateService = new UpdateService(_updateConfig);
            _multiplayerCoordinator = new MultiplayerCoordinator(
                _menu,
                _dialogs,
                _audio,
                _speech,
                _settings,
                new MultiplayerConnector(),
                BeginPromptTextInput,
                SaveSettings,
                EnterMenuState,
                SetSession,
                GetSession,
                ClearSession,
                ResetPendingMultiplayerState,
                SetMultiplayerLoadout);
            _mpPktReg = new ClientPktReg();
            _queuedMultiplayerPackets = new System.Collections.Concurrent.ConcurrentQueue<QueuedIncomingPacket>();
            RegisterMultiplayerPacketHandlers();
            _menuRegistry.RegisterAll();
            _multiplayerCoordinator.ConfigureMenuCloseHandlers();
            _settings.AudioVolumes ??= new AudioVolumeSettings();
            _settings.SyncAudioCategoriesFromMusicVolume();
            ApplyAudioSettings();
            _needsCalibration = _settings.ScreenReaderRateMs <= 0f;
        }

        public void Initialize()
        {
            _logo = new LogoScreen(_audio);
            _logo.Start();
            _state = AppState.Logo;
        }
    }
}
