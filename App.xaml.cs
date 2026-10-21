using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace Digits
{
    sealed partial class App : Application
    {
        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;

        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            if (rootFrame == null)
            {
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                Window.Current.Content = rootFrame;
            }

            var navigationManager = SystemNavigationManager.GetForCurrentView();
            navigationManager.BackRequested -= OnBackRequested;
            navigationManager.BackRequested += OnBackRequested;
            Window.Current.VisibilityChanged -= Window_VisibilityChanged;
            Window.Current.VisibilityChanged += Window_VisibilityChanged;

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                }

                Window.Current.Activate();
            }
        }
        
        private void OnBackRequested(object sender, BackRequestedEventArgs e)
        {
            Frame frame = Window.Current.Content as Frame;

            if (frame.CanGoBack)
            {
                e.Handled = true;
                frame.GoBack(new DrillInNavigationTransitionInfo());
            }
        }

        private async void Window_VisibilityChanged(object sender, VisibilityChangedEventArgs e)
        {
            if (e.Visible)
            {
                return;
            }

            await LockWindowAsync();
        }

        private async Task LockWindowAsync()
        {
            var frame = Window.Current.Content as Frame;
            if (frame == null)
            {
                return;
            }

            if (frame.Content is AddPage scanner)
            {
                await scanner.StopForBackgroundAsync();
                if (frame.Content == scanner && frame.CanGoBack)
                {
                    frame.GoBack();
                }
            }

            if (frame.Content is MainPage mainPage)
            {
                mainPage.LockForBackground();
            }
            else if (AppPreferences.IsAppLockEnabled)
            {
                AppPreferences.LockOnNextMainPageNavigation = true;
            }
        }


        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception(AppText.Format("PageLoadFailed", e.SourcePageType.FullName));
        }
        
        private async void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            try
            {
                await LockWindowAsync();
            }
            finally
            {
                deferral.Complete();
            }
        }
    }
}
