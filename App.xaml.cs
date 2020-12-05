using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace Vice
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        // Runs at startup
        void App_Startup(object sender, StartupEventArgs e)
        {
            Console.WriteLine("App Started");
            IconSetup();

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            MainWindow = new MainWindow();
            MainVM = new MainVM();

            MainWindow.InitializeComponent();
            MainWindow.DataContext = MainVM;
            MainWindow.Show();
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            File.AppendAllText("C:\\Users\\laure\\Dropbox\\Vice Link\\ViceLog.txt", DateTime.UtcNow + " - Fatal Error: \n" + (e.ExceptionObject as Exception) + Environment.NewLine);
        }

        private new MainWindow MainWindow;
        private MainVM MainVM;

        private NotifyIcon NotifyIcon = new NotifyIcon();

        // Runs at shutdown
        private void Application_Exit(object sender, ExitEventArgs e)
        {
            NotifyIcon.Visible = false;
        }

        // Sets the Docker Icon up
        private void IconSetup()
        {
            NotifyIcon.Visible = true;
            NotifyIcon.Icon = new System.Drawing.Icon(@"C:\Users\laure\source\repos\Vice\Resources\Images\Marionette.ico");
            NotifyIcon.ContextMenu = new ContextMenu();

            NotifyIcon.ContextMenu.MenuItems.Add(new MenuItem()
            {
                Text = "Open"
            });

            NotifyIcon.ContextMenu.MenuItems.Add(new MenuItem()
            {
                Text = "Exit"
            });

            NotifyIcon.ContextMenu.MenuItems[0].Click += OpenWindow;
            NotifyIcon.ContextMenu.MenuItems[1].Click += CloseWindow;

            NotifyIcon.Click += NotifyIcon_Click;

        }

        // runs on exit docker button
        private void CloseWindow(object sender, EventArgs e)
        {
            MainVM.StopThreadCom();
            MainVM.StopVolumeThread();
            MainVM.Sleeper.ForceShutdown();
            MainVM.Data.SaveLocal();
            Current.Shutdown();
        }

        private void NotifyIcon_Click(object sender, EventArgs e)
        {
            MouseEventArgs m = e as MouseEventArgs;

            if (m.Button == MouseButtons.Left)
            {
                if (MainWindow != null)
                {
                    MainWindow.Close();
                    MainWindow = new MainWindow();
                }

                MainWindow.InitializeComponent();
                MainWindow.DataContext = MainVM;
                MainWindow.Show();
            }
        }

        private void OpenWindow(object sender, EventArgs e)
        {
            if (MainWindow != null)
            {
                MainWindow.Close();
                MainWindow = new MainWindow();
            }

            MainWindow.InitializeComponent();
            MainWindow.DataContext = MainVM;
            MainWindow.Show();
        }
    }
}
