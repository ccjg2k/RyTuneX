﻿using System.Diagnostics;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using RyTuneX.Contracts.Services;
using RyTuneX.Helpers;
using RyTuneX.ViewModels;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.System;
using Windows.UI.ViewManagement;

namespace RyTuneX.Views;

public sealed partial class ShellPage : Page
{
    public static ShellPage? Current
    {
        get; private set;
    } // Static reference to the current ShellPage
    public ShellViewModel ViewModel
    {
        get;
    }

    public ShellPage(ShellViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Current = this;
        LogHelper.Log("Initializing ShellPage");
        ViewModel.NavigationService.Frame = NavigationFrame;
        ViewModel.NavigationViewService.Initialize(NavigationViewControl);
        var packageVersion = Package.Current.Id.Version;
        AppTitleBarVersion.Text = $"{packageVersion.Major}.{packageVersion.Minor}.{packageVersion.Build}";
        App.MainWindow.ExtendsContentIntoTitleBar = true;
        App.MainWindow.SetTitleBar(AppTitleBar);
        App.MainWindow.Activated += MainWindow_Activated;

        // Subscribe to the ActualThemeChanged event
        this.ActualThemeChanged += ShellPage_ActualThemeChanged;

        // Update the window icon based on the current theme
        UpdateWindowIcon();
    }

    private void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        TitleBarHelper.UpdateTitleBar(RequestedTheme);

        KeyboardAccelerators.Add(BuildKeyboardAccelerator(VirtualKey.Left, VirtualKeyModifiers.Menu));
        KeyboardAccelerators.Add(BuildKeyboardAccelerator(VirtualKey.GoBack));

        // Show restore point dialog if it's the first run
        if (!ApplicationData.Current.LocalSettings.Values.ContainsKey("FirstRun"))
        {
            DispatcherQueue.TryEnqueue(async () => await ShowRestorePointDialogAsync());
        }
        else
        {
            // Show restore point dialog if he setting is set to true
            if ((bool)ApplicationData.Current.LocalSettings.Values["FirstRun"] != false)
            {
                DispatcherQueue.TryEnqueue(async () => await ShowRestorePointDialogAsync());
            }

        }
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        App.AppTitlebar = AppTitleBarText as UIElement;
    }

    private void NavigationViewControl_DisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args)
    {
        AppTitleBar.Margin = new Thickness()
        {
            Left = sender.CompactPaneLength * (sender.DisplayMode == NavigationViewDisplayMode.Minimal ? 2 : 1),
            Top = AppTitleBar.Margin.Top,
            Right = AppTitleBar.Margin.Right,
            Bottom = AppTitleBar.Margin.Bottom
        };
    }

    private static KeyboardAccelerator BuildKeyboardAccelerator(VirtualKey key, VirtualKeyModifiers? modifiers = null)
    {
        var keyboardAccelerator = new KeyboardAccelerator() { Key = key };

        if (modifiers.HasValue)
        {
            keyboardAccelerator.Modifiers = modifiers.Value;
        }

        keyboardAccelerator.Invoked += OnKeyboardAcceleratorInvoked;

        return keyboardAccelerator;
    }

    private static void OnKeyboardAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        var navigationService = App.GetService<INavigationService>();

        var result = navigationService.GoBack();

        args.Handled = result;
    }
    private void IssueButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/rayenghanmi/rytunex/issues/new",
            UseShellExecute = true
        });
    }

    private void SupportButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://buymeacoffee.com/rayen.ghanmi.22",
            UseShellExecute = true
        });
    }

    private async Task ShowRestorePointDialogAsync()
    {
        var neverShowAgain = new CheckBox
        {
            Content = "DoNotShowCheckBox".GetLocalized(),
            Margin = new Thickness(0, 10, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };

        var dialog = new ContentDialog
        {
            Title = "RestorePointTitle".GetLocalized(),
            Content = new StackPanel
            {
                Children =
            {
                new TextBlock
                {
                    Text = "RestorePointDialogText".GetLocalized(),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 10)
                },
                neverShowAgain
            }
            },
            PrimaryButtonText = "Continue".GetLocalized(),
            CloseButtonText = "Close".GetLocalized(),
            XamlRoot = this.Content.XamlRoot,
            PrimaryButtonStyle = (Style)Application.Current.Resources["AccentButtonStyle"],
            Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
            BorderBrush = (SolidColorBrush)Application.Current.Resources["AccentAAFillColorDefaultBrush"],
        };
        await LogHelper.Log("Showing Restore Point Dialog");
        var result = await dialog.ShowAsync();
        if (neverShowAgain.IsChecked == true)
        {
            ApplicationData.Current.LocalSettings.Values["FirstRun"] = false;
        }
        if (result == ContentDialogResult.Primary)
        {
            var progressDialog = new ContentDialog
            {
                Title = "CreatingRestorePoint".GetLocalized(),
                Content = new ProgressRing { IsActive = true, Width = 50, Height = 50 },
                XamlRoot = Content.XamlRoot,
                Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
                BorderBrush = (SolidColorBrush)Application.Current.Resources["AccentAAFillColorDefaultBrush"],
                IsPrimaryButtonEnabled = false
            };

            await LogHelper.Log($"Showing Restore Point Loading Dialog. CheckBox is checked: {neverShowAgain.IsChecked}");

            // Show the progress dialog in a separate task
            _ = progressDialog.ShowAsync().AsTask();
            try
            {
                await CreateRestorePointAsync();
                progressDialog.Hide();
                await LogHelper.Log("Showing Restore Point Creation Success");
                await new ContentDialog
                {
                    Title = "RestorePointCreated".GetLocalized(),
                    Content = "RestorePointCreationSuccess".GetLocalized(),
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot,
                    PrimaryButtonStyle = (Style)Application.Current.Resources["AccentButtonStyle"],
                    Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
                    BorderBrush = (SolidColorBrush)Application.Current.Resources["AccentAAFillColorDefaultBrush"],
                }.ShowAsync();
            }
            catch (Exception ex)
            {
                progressDialog.Hide();
                await LogHelper.Log("Showing Restore Point Creation Error");
                await new ContentDialog
                {
                    Title = "UnexpectedError".GetLocalized(),
                    Content = $"RestorePointCreationError".GetLocalized(),
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot,
                    PrimaryButtonStyle = (Style)Application.Current.Resources["AccentButtonStyle"],
                    Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
                    BorderBrush = (SolidColorBrush)Application.Current.Resources["AccentAAFillColorDefaultBrush"],
                }.ShowAsync();
                await LogHelper.LogError(ex.Message);
            }
        }
    }

    private async Task CreateRestorePointAsync()
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = "-Command \"Set-ExecutionPolicy RemoteSigned -Scope CurrentUser -Force; Checkpoint-Computer -Description 'RyTuneX Restore Point' -RestorePointType 'MODIFY_SETTINGS'\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = processInfo };
        await LogHelper.Log("Starting Restore Point Creation Process");
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        if (process.ExitCode != 0 || !string.IsNullOrEmpty(error))
        {
            await LogHelper.Log(error);
            throw new Exception($"Restore Point Creation Process exited with code {process.ExitCode}");
        }
    }

    private void ShellPage_ActualThemeChanged(FrameworkElement sender, object args)
    {
        UpdateWindowIcon();
    }

    private void UpdateWindowIcon()
    {
        var themeSelectorService = App.GetService<IThemeSelectorService>();
        var theme = themeSelectorService.Theme;

        if (theme == ElementTheme.Default)
        {
            var uiSettings = new UISettings();
            var background = uiSettings.GetColorValue(UIColorType.Background);
            theme = background == Colors.White ? ElementTheme.Light : ElementTheme.Dark;
        }

        var iconPath = theme switch
        {
            ElementTheme.Light => "Assets/LightWindowIcon.ico",
            _ => "Assets/WindowIcon.ico"
        };

        ShellTitleBarImage.Source = new BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory, iconPath)));
    }

    public static void ShowNotification(string title, string message, InfoBarSeverity severity, int duration)
    {
        Current?.ShowNotificationInstance(title, message, severity, duration);
    }

    private void ShowNotificationInstance(string title, string message, InfoBarSeverity severity, int duration)
    {
        // Show notification in the NotificationQueue
        NotificationQueue.Show(new CommunityToolkit.WinUI.Behaviors.Notification
        {
            Title = title,
            Message = message,
            Severity = severity,
            Duration = TimeSpan.FromMilliseconds(duration)
        });

        // Trigger animation for showing the notification
        var showStoryboard = (Storyboard)infoBar.Resources["ShowNotificationStoryboard"];
        showStoryboard.Begin();

        // Start ProgressBar animation
        var progressBarAnimationStoryboard = (Storyboard)infoBar.Resources["ProgressBarAnimationStoryboard"];
        progressBarAnimationStoryboard.Begin();
    }
}