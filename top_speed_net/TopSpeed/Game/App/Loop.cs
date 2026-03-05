using System.Diagnostics;
using System.Threading;

namespace TopSpeed.Game
{
    internal sealed partial class GameApp
    {
        private void StartGameThread()
        {
            if (_gameThread != null)
                return;

            _running = true;
            _gameThread = new Thread(GameLoop)
            {
                IsBackground = true,
                Name = "GameLoop"
            };
            _gameThread.Start();
        }

        private void StopGameThread()
        {
            _running = false;
            if (_gameThread == null)
                return;
            if (_gameThread.IsAlive)
                _gameThread.Join(200);
            _gameThread = null;
        }

        private void GameLoop()
        {
            while (_running)
            {
                var game = _game;
                if (game != null && !game.IsModalInputActive)
                {
                    var now = _stopwatch.ElapsedTicks;
                    var deltaSeconds = (float)(now - _lastTicks) / Stopwatch.Frequency;
                    _lastTicks = now;
                    game.Update(deltaSeconds);
                }

                var intervalMs = game != null ? game.LoopIntervalMs : GameLoopIntervalMs;
                if (intervalMs <= 0)
                    intervalMs = GameLoopIntervalMs;
                Thread.Sleep(intervalMs);
            }
        }
    }
}
