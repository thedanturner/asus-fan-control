using System;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace AsusFanProfileSwitcher.GameBar
{
    public sealed partial class WidgetPage : Page
    {
        private readonly DispatcherTimer _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        private bool _refreshing;

        public WidgetPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            _refreshTimer.Tick += RefreshTimer_Tick;
        }

        private async void OnLoaded(object sender, RoutedEventArgs args)
        {
            _refreshTimer.Start();
            await RefreshAsync();
        }

        private void OnUnloaded(object sender, RoutedEventArgs args)
        {
            _refreshTimer.Stop();
        }

        private async void RefreshTimer_Tick(object sender, object args)
        {
            await RefreshAsync(false);
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs args)
        {
            await RefreshAsync();
        }

        private async Task RefreshAsync(bool showBusy = true)
        {
            if (_refreshing)
            {
                return;
            }

            _refreshing = true;
            if (showBusy)
            {
                BusyIndicator.IsActive = true;
            }
            try
            {
                var response = await GameBarClient.GetStateAsync();
                ResultText.Text = response.Message;
                ProfilesPanel.Children.Clear();
                if (response.State == null)
                {
                    ConnectionText.Text = "●  CONTROLLER OFFLINE";
                    ConnectionText.Foreground = new SolidColorBrush(
                        Color.FromArgb(255, 242, 173, 62));
                    return;
                }

                ConnectionText.Text = response.State.Connected
                    ? "●  ASUS SERVICE CONNECTED"
                    : "●  ASUS SERVICE UNAVAILABLE";
                ConnectionText.Foreground = new SolidColorBrush(
                    response.State.Connected
                        ? Color.FromArgb(255, 62, 205, 151)
                        : Color.FromArgb(255, 242, 173, 62));

                foreach (var profile in response.State.Profiles)
                {
                    ProfilesPanel.Children.Add(CreateProfileButton(profile));
                }
                if (response.State.Profiles.Count == 0)
                {
                    ProfilesPanel.Children.Add(new TextBlock
                    {
                        Text = "No valid Fan Xpert XML profiles were found.",
                        Foreground = (Brush)Application.Current.Resources["MutedBrush"],
                        TextWrapping = TextWrapping.Wrap
                    });
                }
            }
            finally
            {
                BusyIndicator.IsActive = false;
                _refreshing = false;
            }
        }

        private Button CreateProfileButton(GameBarProfile profile)
        {
            var button = new Button
            {
                Content = profile.IsActive
                    ? "●  " + profile.DisplayName + "   ACTIVE"
                    : profile.DisplayName,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 9),
                Padding = new Thickness(14, 12, 14, 12),
                Tag = profile,
                IsEnabled = !profile.IsActive
            };
            if (!profile.IsActive)
            {
                button.Background = (Brush)Application.Current.Resources["CardBrush"];
            }
            button.Click += ProfileButton_Click;
            return button;
        }

        private async void ProfileButton_Click(object sender, RoutedEventArgs args)
        {
            var button = (Button)sender;
            var profile = (GameBarProfile)button.Tag;
            var dialog = new ContentDialog
            {
                Title = "Apply cooling profile?",
                Content = "The ASUS fan service will briefly restart. The current configuration will be backed up.",
                PrimaryButtonText = "Apply " + profile.DisplayName,
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            BusyIndicator.IsActive = true;
            ProfilesPanel.IsHitTestVisible = false;
            try
            {
                var response = await GameBarClient.ApplyAsync(profile.Name);
                ResultText.Text = response.Message;
            }
            finally
            {
                ProfilesPanel.IsHitTestVisible = true;
                BusyIndicator.IsActive = false;
            }
            await RefreshAsync(false);
        }
    }
}
