using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using TopSpeed.Windowing;

namespace TopSpeed.Game
{
    internal sealed partial class GameApp : IDisposable
    {
        private const int GameLoopIntervalMs = 8;
        private readonly GameWindow _window;
        private Game? _game;
        private readonly Stopwatch _stopwatch;
        private long _lastTicks;
        private Thread? _gameThread;
        private volatile bool _running;

        public GameApp()
        {
            _window = new GameWindow();
            _window.FormClosed += OnFormClosed;
            _window.Load += OnLoad;
            _stopwatch = new Stopwatch();
        }

        public void Run()
        {
            Application.Run(_window);
        }

        public void Dispose()
        {
            _window.Dispose();
            StopGameThread();
            _game?.Dispose();
        }
    }
}
