using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace SpotifyNow
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            var application = new Application();
            application.Run(new NowPlayingWindow());
        }
    }

    internal sealed class NowPlayingWindow : Window
    {
        private const string WaitingText = "Open Spotify to begin";

        private readonly Image _artwork;
        private readonly TextBlock _trackText;
        private readonly TextBlock _playPauseGlyph;
        private readonly DispatcherTimer _timer;

        private GlobalSystemMediaTransportControlsSessionManager _manager;
        private bool _refreshInProgress;
        private bool _controlInProgress;

        public NowPlayingWindow()
        {
            Title = "Now Playing";
            Width = 646;
            Height = 92;
            MinWidth = MaxWidth = Width;
            MinHeight = MaxHeight = Height;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = true;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;

            var canvas = new Canvas
            {
                Width = 646,
                Height = 92,
                Background = Brushes.Transparent
            };
            Content = canvas;

            _artwork = new Image
            {
                Width = 76,
                Height = 76,
                Stretch = Stretch.UniformToFill,
                Clip = new RectangleGeometry(new Rect(0, 0, 76, 76), 18, 18)
            };
            var artworkSurface = new Border
            {
                Width = 76,
                Height = 76,
                Background = new SolidColorBrush(Color.FromArgb(232, 29, 29, 29)),
                CornerRadius = new CornerRadius(18),
                Child = _artwork
            };
            Canvas.SetLeft(artworkSurface, 8);
            Canvas.SetTop(artworkSurface, 8);

            _trackText = new TextBlock
            {
                Text = WaitingText,
                Foreground = Brushes.White,
                FontFamily = AppleStyleFontFamily(),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(18, 0, 18, 0)
            };
            var textSurface = new Border
            {
                Width = 280,
                Height = 76,
                Background = new SolidColorBrush(Color.FromArgb(232, 25, 25, 25)),
                CornerRadius = new CornerRadius(20),
                Child = _trackText
            };
            Canvas.SetLeft(textSurface, 92);
            Canvas.SetTop(textSurface, 8);

            var previousButton = CreateRoundButton("⏮", 48, 15);
            previousButton.MouseLeftButtonUp += delegate { ExecuteControl(session => session.TrySkipPreviousAsync()); };
            Canvas.SetLeft(previousButton, 384);
            Canvas.SetTop(previousButton, 22);

            var playPauseButton = CreateRoundButton("⏸", 48, 16);
            _playPauseGlyph = (TextBlock)playPauseButton.Child;
            playPauseButton.MouseLeftButtonUp += delegate { ExecuteControl(session => session.TryTogglePlayPauseAsync()); };
            Canvas.SetLeft(playPauseButton, 440);
            Canvas.SetTop(playPauseButton, 22);

            var nextButton = CreateRoundButton("⏭", 48, 15);
            nextButton.MouseLeftButtonUp += delegate { ExecuteControl(session => session.TrySkipNextAsync()); };
            Canvas.SetLeft(nextButton, 496);
            Canvas.SetTop(nextButton, 22);

            var minimizeButton = CreateRoundButton("−", 32, 15);
            minimizeButton.MouseLeftButtonUp += delegate { WindowState = WindowState.Minimized; };
            Canvas.SetLeft(minimizeButton, 556);
            Canvas.SetTop(minimizeButton, 30);

            var closeButton = CreateRoundButton("×", 32, 17);
            closeButton.MouseEnter += delegate { closeButton.Background = new SolidColorBrush(Color.FromRgb(156, 56, 56)); };
            closeButton.MouseLeave += delegate { closeButton.Background = ControlBrush(); };
            closeButton.MouseLeftButtonUp += delegate { Close(); };
            Canvas.SetLeft(closeButton, 596);
            Canvas.SetTop(closeButton, 30);

            canvas.Children.Add(artworkSurface);
            canvas.Children.Add(textSurface);
            canvas.Children.Add(previousButton);
            canvas.Children.Add(playPauseButton);
            canvas.Children.Add(nextButton);
            canvas.Children.Add(minimizeButton);
            canvas.Children.Add(closeButton);

            AttachDragBehavior(artworkSurface);
            AttachDragBehavior(textSurface);
            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Escape)
                    Close();
            };

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _timer.Tick += delegate { RefreshNowPlaying(); };
            Loaded += delegate
            {
                RefreshNowPlaying();
                _timer.Start();
            };
            Closed += delegate { _timer.Stop(); };
        }

        private Border CreateRoundButton(string glyph, double diameter, double fontSize)
        {
            var button = new Border
            {
                Width = diameter,
                Height = diameter,
                CornerRadius = new CornerRadius(diameter / 2),
                Background = ControlBrush(),
                Cursor = Cursors.Hand,
                SnapsToDevicePixels = false,
                Child = new TextBlock
                {
                    Text = glyph,
                    Foreground = Brushes.White,
                    FontFamily = new FontFamily("Segoe UI Symbol"),
                    FontSize = fontSize,
                    FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsHitTestVisible = false
                }
            };

            button.MouseEnter += delegate { button.Background = new SolidColorBrush(Color.FromRgb(70, 70, 70)); };
            button.MouseLeave += delegate { button.Background = ControlBrush(); };
            return button;
        }

        private static SolidColorBrush ControlBrush()
        {
            return new SolidColorBrush(Color.FromArgb(238, 38, 38, 38));
        }

        private async void RefreshNowPlaying()
        {
            if (_refreshInProgress)
                return;

            _refreshInProgress = true;
            try
            {
                var mediaSession = await GetMediaSession();
                if (mediaSession == null)
                {
                    ShowWaitingState();
                    return;
                }

                var media = await mediaSession.TryGetMediaPropertiesAsync().AsTask();
                var displayText = String.Format("{0} — {1}",
                    ValueOr(media.Title, "Unknown track"),
                    ValueOr(media.Artist, "Unknown artist"));
                var playback = mediaSession.GetPlaybackInfo();
                _playPauseGlyph.Text = playback != null &&
                    playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing
                    ? "⏸"
                    : "▶";

                if (!String.Equals(_trackText.Text, displayText, StringComparison.Ordinal))
                {
                    _trackText.Text = displayText;
                    _artwork.Source = await LoadArtwork(media.Thumbnail);
                }
            }
            catch (Exception)
            {
                // The media-session service can restart when Spotify updates or exits.
                _manager = null;
                ShowWaitingState();
            }
            finally
            {
                _refreshInProgress = false;
            }
        }

        private async void ExecuteControl(Func<GlobalSystemMediaTransportControlsSession, Windows.Foundation.IAsyncOperation<bool>> action)
        {
            if (_controlInProgress)
                return;

            _controlInProgress = true;
            try
            {
                var mediaSession = await GetMediaSession();
                if (mediaSession != null)
                    await action(mediaSession).AsTask();
            }
            catch (Exception)
            {
                _manager = null;
            }
            finally
            {
                _controlInProgress = false;
                RefreshNowPlaying();
            }
        }

        private async Task<GlobalSystemMediaTransportControlsSession> GetMediaSession()
        {
            if (_manager == null)
                _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask();

            var currentSession = _manager.GetCurrentSession();
            if (currentSession != null)
                return currentSession;

            return FindPlayingSession(_manager);
        }

        private static GlobalSystemMediaTransportControlsSession FindPlayingSession(
            GlobalSystemMediaTransportControlsSessionManager manager)
        {
            foreach (var session in manager.GetSessions())
            {
                var playback = session.GetPlaybackInfo();
                if (playback != null &&
                    playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                    return session;
            }

            return null;
        }

        private async Task<BitmapSource> LoadArtwork(IRandomAccessStreamReference thumbnail)
        {
            if (thumbnail == null)
                return null;

            var stream = await thumbnail.OpenReadAsync().AsTask();
            var reader = new DataReader(stream.GetInputStreamAt(0));
            await reader.LoadAsync(checked((uint)stream.Size)).AsTask();

            var bytes = new byte[reader.UnconsumedBufferLength];
            reader.ReadBytes(bytes);
            reader.Dispose();

            using (var memory = new MemoryStream(bytes))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = memory;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
        }

        private void ShowWaitingState()
        {
            _trackText.Text = WaitingText;
            _artwork.Source = null;
            _playPauseGlyph.Text = "▶";
        }

        private static string ValueOr(string value, string fallback)
        {
            return String.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static FontFamily AppleStyleFontFamily()
        {
            foreach (var family in Fonts.SystemFontFamilies)
            {
                if (String.Equals(family.Source, "SF Pro Display", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(family.Source, "SF Pro Text", StringComparison.OrdinalIgnoreCase))
                    return family;
            }

            return new FontFamily("Segoe UI");
        }

        private void AttachDragBehavior(UIElement element)
        {
            element.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.ChangedButton == MouseButton.Left)
                    DragMove();
            };
        }
    }
}
