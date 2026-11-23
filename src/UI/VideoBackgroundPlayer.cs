using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OmniBattery.UI
{
    /// <summary>
    /// Player de vídeo em segundo plano (MP4) de alta performance:
    /// Reproduz silenciosamente (volume zero / mutado) em loop contínuo,
    /// capturando os quadros para exibição translúcida no fundo da aplicação.
    /// </summary>
    public class VideoBackgroundPlayer : IDisposable
    {
        private MediaPlayer _player;
        private Timer _timer;
        private DrawingVisual _visual;
        private RenderTargetBitmap _rtb;
        private Bitmap _currentBitmap;
        private bool _isDisposed = false;
        private int _targetWidth = 640;
        private int _targetHeight = 360;
        private readonly object _lock = new object();

        public event Action<Bitmap> FrameReady;

        public bool IsPlaying { get; private set; }

        public VideoBackgroundPlayer()
        {
            try
            {
                _player = new MediaPlayer();
                _player.Volume = 0;
                _player.IsMuted = true;
                _player.ScrubbingEnabled = true;

                _player.MediaOpened += OnMediaOpened;
                _player.MediaEnded += OnMediaEnded;
                _player.MediaFailed += OnMediaFailed;

                _visual = new DrawingVisual();

                _timer = new Timer();
                _timer.Interval = 33; // ~30 FPS suave
                _timer.Tick += OnTimerTick;
            }
            catch
            {
                // Fallback silencioso caso WPF/MediaFoundation falhe
            }
        }

        public void LoadVideo(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return;

            try
            {
                Stop();
                if (_player != null)
                {
                    _player.Open(new Uri(Path.GetFullPath(filePath)));
                    _player.Play();
                    IsPlaying = true;
                    if (_timer != null)
                        _timer.Start();
                }
            }
            catch
            {
                Stop();
            }
        }

        private void OnMediaOpened(object sender, EventArgs e)
        {
            try
            {
                int naturalW = _player.NaturalVideoWidth;
                int naturalH = _player.NaturalVideoHeight;
                if (naturalW > 0 && naturalH > 0)
                {
                    // Limita a resolução máxima de renderização para garantir suavidade e baixo consumo de CPU
                    if (naturalW > 1280 || naturalH > 720)
                    {
                        double scale = Math.Min(1280.0 / naturalW, 720.0 / naturalH);
                        _targetWidth = (int)(naturalW * scale);
                        _targetHeight = (int)(naturalH * scale);
                    }
                    else
                    {
                        _targetWidth = naturalW;
                        _targetHeight = naturalH;
                    }
                }
                else
                {
                    _targetWidth = 640;
                    _targetHeight = 360;
                }

                lock (_lock)
                {
                    _rtb = new RenderTargetBitmap(_targetWidth, _targetHeight, 96, 96, PixelFormats.Pbgra32);
                    if (_currentBitmap != null)
                        _currentBitmap.Dispose();
                    _currentBitmap = new Bitmap(_targetWidth, _targetHeight, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                }
            }
            catch { }
        }

        private void OnMediaEnded(object sender, EventArgs e)
        {
            try
            {
                if (_player != null)
                {
                    _player.Position = TimeSpan.Zero;
                    _player.Play();
                }
            }
            catch { }
        }

        private void OnMediaFailed(object sender, ExceptionEventArgs e)
        {
            Stop();
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            if (!IsPlaying || _player == null || _rtb == null)
                return;

            try
            {
                lock (_lock)
                {
                    using (DrawingContext dc = _visual.RenderOpen())
                    {
                        dc.DrawVideo(_player, new System.Windows.Rect(0, 0, _targetWidth, _targetHeight));
                    }

                    _rtb.Render(_visual);

                    BitmapData data = _currentBitmap.LockBits(
                        new Rectangle(0, 0, _targetWidth, _targetHeight),
                        ImageLockMode.WriteOnly,
                        System.Drawing.Imaging.PixelFormat.Format32bppPArgb);

                    _rtb.CopyPixels(
                        new System.Windows.Int32Rect(0, 0, _targetWidth, _targetHeight),
                        data.Scan0,
                        data.Stride * _targetHeight,
                        data.Stride);

                    _currentBitmap.UnlockBits(data);
                }

                if (FrameReady != null)
                    FrameReady(_currentBitmap);
            }
            catch
            {
                // Ignora falhas pontuais de frame sem travar a interface
            }
        }

        public void Stop()
        {
            IsPlaying = false;
            try
            {
                if (_timer != null)
                    _timer.Stop();
                if (_player != null)
                {
                    _player.Stop();
                    _player.Close();
                }
            }
            catch { }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            Stop();

            if (_timer != null)
            {
                _timer.Dispose();
                _timer = null;
            }

            lock (_lock)
            {
                if (_currentBitmap != null)
                {
                    _currentBitmap.Dispose();
                    _currentBitmap = null;
                }
                _rtb = null;
                _visual = null;
                _player = null;
            }
        }
    }
}
