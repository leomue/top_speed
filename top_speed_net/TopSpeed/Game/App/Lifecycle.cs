using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TopSpeed.Game
{
    internal sealed partial class GameApp
    {
        private void OnLoad(object? sender, EventArgs e)
        {
            _game = new Game(_window);
            _game.ExitRequested += async () =>
            {
                _game.FadeOutMenuMusic(500);
                await Task.Delay(500).ConfigureAwait(true);
                _window.Close();
            };
            _game.Initialize();
            _stopwatch.Start();
            _lastTicks = _stopwatch.ElapsedTicks;
            StartGameThread();
        }

        private void OnFormClosed(object? sender, FormClosedEventArgs e)
        {
            StopGameThread();
            _game?.Dispose();
            _game = null;
        }
    }
}
