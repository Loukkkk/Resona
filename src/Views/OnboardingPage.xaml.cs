using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using WinRT;
using WinRT.Interop;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace Resona.Views;

public sealed partial class OnboardingPage : Page
{

	public event Action? OnboardingCompleted;

	public OnboardingPage()
	{
		InitializeComponent();
		base.Loaded += delegate
		{
			PlayEntranceAnimation();
		};
	}

	private void PlayEntranceAnimation()
	{
		Storyboard sb = new Storyboard();
		BackEase easing = new BackEase
		{
			Amplitude = 0.6,
			EasingMode = EasingMode.EaseOut
		};
		AddAnim(LogoCircle, "Opacity", 0.0, 1.0, 0, 600);
		AddAnim(LogoScale, "ScaleX", 0.6, 1.0, 0, 600, easing);
		AddAnim(LogoScale, "ScaleY", 0.6, 1.0, 0, 600, easing);
		AddAnim(TitleText, "Opacity", 0.0, 1.0, 300, 500);
		AddAnim(TitleTranslate, "Y", 20.0, 0.0, 300, 500);
		AddAnim(SubtitleText, "Opacity", 0.0, 1.0, 500, 500);
		AddAnim(ActionPanel, "Opacity", 0.0, 1.0, 800, 500);
		sb.Begin();
		void AddAnim(DependencyObject target, string property, double from, double to, int beginMs, int durationMs, EasingFunctionBase? easingFunction = null)
		{
			DoubleAnimation doubleAnimation = new DoubleAnimation
			{
				From = from,
				To = to,
				Duration = new Duration(TimeSpan.FromMilliseconds(durationMs)),
				BeginTime = TimeSpan.FromMilliseconds(beginMs),
				EasingFunction = easingFunction
			};
			Storyboard.SetTarget(doubleAnimation, target);
			Storyboard.SetTargetProperty(doubleAnimation, property);
			sb.Children.Add(doubleAnimation);
		}
	}

	private async void ChooseFolder_Click(object sender, RoutedEventArgs e)
	{
		FolderPicker folderPicker = new FolderPicker();
		InitializeWithWindow.Initialize(folderPicker, WindowNative.GetWindowHandle(App.MainWindowInstance));
		folderPicker.FileTypeFilter.Add("*");
		StorageFolder storageFolder = await folderPicker.PickSingleFolderAsync();
		if (storageFolder != null)
		{
			App.Settings.Current.MusicFolders.Add(storageFolder.Path);
		}
		App.Settings.Current.HasCompletedOnboarding = true;
		await App.Settings.SaveAsync();
		this.OnboardingCompleted?.Invoke();
	}

	private async void Skip_Click(object sender, RoutedEventArgs e)
	{
		App.Settings.Current.HasCompletedOnboarding = true;
		await App.Settings.SaveAsync();
		this.OnboardingCompleted?.Invoke();
	}
}


