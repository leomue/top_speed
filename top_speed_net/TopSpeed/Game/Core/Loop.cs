namespace TopSpeed.Game
{
    internal sealed partial class Game
    {
        public void Update(float deltaSeconds)
        {
            _input.Update();
            if (_input.TryGetJoystickState(out var joystick))
                _raceInput.Run(_input.Current, joystick, deltaSeconds, _input.ActiveJoystickIsRacingWheel);
            else
                _raceInput.Run(_input.Current, deltaSeconds);

            TryShowDeviceChoiceDialog();

            _raceInput.SetOverlayInputBlocked(
                _state == AppState.MultiplayerRace &&
                (_multiplayerCoordinator.Questions.HasActiveOverlayQuestion || _dialogs.HasActiveOverlayDialog));

            UpdateTextInputPrompt();

            switch (_state)
            {
                case AppState.Logo:
                    if (_logo == null || _logo.Update(_input, deltaSeconds))
                    {
                        _logo?.Dispose();
                        _logo = null;
                        _menu.ShowRoot("main");
                        if (_needsCalibration)
                        {
                            if (!ShowSettingsIssuesDialog(() => StartCalibrationSequence()))
                                StartCalibrationSequence();
                            else
                                _state = AppState.Menu;
                        }
                        else
                        {
                            ShowSettingsIssuesDialog();
                            _menu.FadeInMenuMusic(force: true);
                            _state = AppState.Menu;
                        }

                        StartAutoUpdateCheck();
                    }
                    break;
                case AppState.Calibration:
                    _menu.Update(_input);
                    if (_calibrationOverlay && !IsCalibrationMenu(_menu.CurrentId))
                    {
                        _calibrationOverlay = false;
                        _state = AppState.Menu;
                    }
                    break;
                case AppState.Menu:
                    UpdateUpdateFlow();

                    if (_session != null)
                    {
                        ProcessMultiplayerPackets();
                        if (_state != AppState.Menu)
                            break;
                    }

                    if (_textInputPromptActive)
                        break;

                    if (UpdateModalOperations())
                        break;

                    if (_inputMapping.IsActive)
                    {
                        _inputMapping.Update();
                        break;
                    }

                    var action = _menu.Update(_input);
                    HandleMenuAction(action);
                    break;
                case AppState.TimeTrial:
                    RunTimeTrial(deltaSeconds);
                    break;
                case AppState.SingleRace:
                    RunSingleRace(deltaSeconds);
                    break;
                case AppState.MultiplayerRace:
                    RunMultiplayerRace(deltaSeconds);
                    break;
                case AppState.Paused:
                    UpdatePaused();
                    break;
            }

            if (_pendingRaceStart)
            {
                _pendingRaceStart = false;
                StartRace(_pendingMode);
            }

            SyncAudioLoopState();
        }
    }
}
