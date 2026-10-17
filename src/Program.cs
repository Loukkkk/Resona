using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Resona
{
    public static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();
            bool isRedirect = DecideRedirection();
            if (!isRedirect)
            {
                Microsoft.UI.Xaml.Application.Start((p) =>
                {
                    var context = new DispatcherQueueSynchronizationContext(
                        DispatcherQueue.GetForCurrentThread());
                    SynchronizationContext.SetSynchronizationContext(context);
                    new App();
                });
            }
        }

        private static bool DecideRedirection()
        {
            bool isRedirect = false;
            AppActivationArguments args = AppInstance.GetCurrent().GetActivatedEventArgs();
            AppInstance keyInstance = AppInstance.FindOrRegisterForKey("ResonaMainInstance");

            if (keyInstance.IsCurrent)
            {
                keyInstance.Activated += OnActivated;
            }
            else
            {
                isRedirect = true;
                keyInstance.RedirectActivationToAsync(args).AsTask().Wait();
            }

            return isRedirect;
        }

        private static void OnActivated(object sender, AppActivationArguments args)
        {
            // Son / dossier ouvert depuis Windows (double-clic, "Ouvrir avec Resona") pendant que Resona tourne deja :
            // la 2e instance nous transmet sa ligne de commande avant de se fermer.
            try
            {
                if (args.Kind == ExtendedActivationKind.Launch
                    && args.Data is Windows.ApplicationModel.Activation.ILaunchActivatedEventArgs launch)
                {
                    ExternalOpenService.SubmitFromCommandLine(launch.Arguments);
                }
            }
            catch (Exception ex) { Resona.Services.LogService.Write("ExternalOpen", ex, "Activation redirigee"); }

            App.MainWindowInstance?.DispatcherQueue.TryEnqueue(() =>
            {
                var window = App.MainWindowInstance;
                if (window != null)
                {
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                    App.ShowWindow(hwnd, 9); // SW_RESTORE
                    App.SetForegroundWindow(hwnd);
                    window.AppWindow.Show(true);
                    window.AppWindow.MoveInZOrderAtTop();
                    App.TrayIcon?.Hide();
                }
            });
        }
    }
}
