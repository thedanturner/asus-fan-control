using System;
using Microsoft.Gaming.XboxGameBar;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace AsusFanProfileSwitcher.GameBar
{
    sealed partial class App : Application
    {
        private XboxGameBarWidget _widget;

        public App()
        {
            InitializeComponent();
            Suspending += OnSuspending;
        }

        protected override void OnActivated(IActivatedEventArgs args)
        {
            XboxGameBarWidgetActivatedEventArgs widgetArgs = null;
            if (args.Kind == ActivationKind.Protocol &&
                args is IProtocolActivatedEventArgs protocolArgs &&
                string.Equals(
                    protocolArgs.Uri.Scheme,
                    "ms-gamebarwidget",
                    StringComparison.OrdinalIgnoreCase))
            {
                widgetArgs = args as XboxGameBarWidgetActivatedEventArgs;
            }

            if (widgetArgs == null || !widgetArgs.IsLaunchActivation)
            {
                return;
            }

            var frame = new Frame();
            frame.NavigationFailed += OnNavigationFailed;
            Window.Current.Content = frame;
            _widget = new XboxGameBarWidget(
                widgetArgs,
                Window.Current.CoreWindow,
                frame);
            frame.Navigate(typeof(WidgetPage));
            Window.Current.Closed += OnWidgetClosed;
            Window.Current.Activate();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            var frame = Window.Current.Content as Frame ?? new Frame();
            frame.NavigationFailed -= OnNavigationFailed;
            frame.NavigationFailed += OnNavigationFailed;
            Window.Current.Content = frame;
            if (frame.Content == null)
            {
                frame.Navigate(typeof(MainPage));
            }
            Window.Current.Activate();
        }

        private void OnWidgetClosed(
            object sender,
            Windows.UI.Core.CoreWindowEventArgs args)
        {
            _widget = null;
            Window.Current.Closed -= OnWidgetClosed;
        }

        private static void OnNavigationFailed(
            object sender,
            NavigationFailedEventArgs args)
        {
            throw new InvalidOperationException(
                "Failed to load " + args.SourcePageType.FullName);
        }

        private void OnSuspending(object sender, SuspendingEventArgs args)
        {
            var deferral = args.SuspendingOperation.GetDeferral();
            _widget = null;
            deferral.Complete();
        }
    }
}
