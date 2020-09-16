using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Vice
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        // enter on the sleepbar
        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                MainVM vm = DataContext as MainVM;
                vm.Sleeper.Timer = int.Parse(SleepText.Text as string);

                if (vm.Sleeper.Timer > 0)
                    vm.Sleeper.ButtonCom();

                //MoveFocus(new TraversalRequest(FocusNavigationDirection.Right));
            }
        }

        private void SleepText_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (SleepText.Text == "0")
                SleepText.Text = "";
        }
    }
}
