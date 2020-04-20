using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Vice.Resources;
using System.Threading;
using System.Windows.Input;
using System.IO;
using System.IO.Ports;
using System.Diagnostics;
using System.Runtime.InteropServices;
using AudioSwitcher.AudioApi.CoreAudio;
using Telegram.Bot;

namespace Vice
{
    public class MainVM : Notify
    {
        [DllImport("User32.dll", SetLastError = true)]
        public static extern bool LockWorkStation();

        [DllImport("User32.dll", SetLastError = true)]
        public static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, IntPtr extraInfo);
        public const int VK_MEDIA_NEXT_TRACK = 0xB0;
        public const int VK_MEDIA_PLAY_PAUSE = 0xB3;
        public const int VK_MEDIA_PREV_TRACK = 0xB1;
        public const int KEYEVENTF_EXTENDEDKEY = 0x0001; //Key down flag
        public const int KEYEVENTF_KEYUP = 0x0002; //Key up flag


        public MainVM()
        {
            StartThreadCom();
        }

        #region Properties

        private ManualResetEvent ExitWait = new ManualResetEvent(true);
        private ManualResetEvent ColourWait = new ManualResetEvent(true);

        Thread MainThread;
        
        private bool LoopKiller = false;

        public bool Running
        {
            get
            {
                if (MainThread != null)
                    return true;
                else
                    return false;
            }
        }

        private string _activeColour = "Red";
        public string ActiveColour
        {
            get { return _activeColour; }
            set { _activeColour = value; NotifyPropertyChanged(); }
        }

        // in millseconds
        int Inverval = 400;
        int LightInterval = 200;

        string CommandStem = "C:\\Users\\laure\\DropBox\\Vice Link\\CommandStem.txt";
        string LogLocation = "C:\\Users\\laure\\Dropbox\\Vice Link\\ViceLog.txt";

        private string _output = "Start Up Successful";
        public string Output
        {
            get => _output;
            set { _output = value; NotifyPropertyChanged(); }
        }

        DateTime CommandRead;

        #endregion Properties

        #region Methods

        private void Run()
        {
            ColourWait.Set();
            ActiveColour = "Green";

            while (!LoopKiller)
            {
                Console.WriteLine("Checked");

                try
                {
                    if (File.Exists(CommandStem))
                    {
                        ActiveColour = "Green";
                        //ColourChangeCreate("Blue");

                        CommandRead = DateTime.UtcNow;

                        string[] ReadLines = File.ReadAllLines(CommandStem);

                        string TestWord = ReadLines[0];
                        string ValueWord = "";

                        if (ReadLines.Length > 1)
                            ValueWord = ReadLines[1];

                        if (TestWord == "Test")
                            TestCommand();

                        // Power Options
                        else if (TestWord == "Lock")
                            LockCommand();
                        else if (TestWord == "Sleep")
                            SleepCommand();
                        else if (TestWord == "Hibernate")
                            HibernateCommand();
                        else if (TestWord == "PowerOff")
                            PowerCommand();

                        // Sound Options
                        else if (TestWord == "Volume Control")
                            VolumeCommand(ValueWord);
                        else if (TestWord == "Media Control")
                            MediaCommand(ValueWord);

                        Thread.Sleep(LightInterval);
                    }
                    else
                    {
                        ActiveColour = "LightGreen";
                        Thread.Sleep(LightInterval);
                    }
                    //ColourChangeCreate("LightGreen");
                }
                catch (Exception ex)
                {
                    File.Delete(CommandStem);
                    File.AppendAllText(LogLocation, ex.ToString());
                }

                ActiveColour = "Green";
                Thread.Sleep(Inverval);
            }

            ExitWait.Set();
        }

        private void ColourChangeCreate(string colour)
        {
            Thread temp = new Thread(() => ColourChange(colour));
            temp.Start();
        }

        private void ColourChange(string colour)
        {
            ColourWait.Reset();
            ActiveColour = colour;
            Thread.Sleep(200);
            ActiveColour = "Green";
            ColourWait.Set();
        }

        // Runs at the end of the command - removes the document and writes to log
        private void WipeStem(string Command = "")
        {
            //File.WriteAllText(CommandStem, "Waiting");

            File.Delete(CommandStem);

            if (Command != "")
            {
                Console.WriteLine(Command);
                Output += string.Format("\n" + DateTime.UtcNow + " - Command received: " + Command);
                File.AppendAllText( LogLocation, DateTime.UtcNow + " - Command received: " + Command + Environment.NewLine);
                //Output += "\n" + (DateTime.UtcNow - CommandRead).ToString();
            }
        }

        // returns a number if 
        private int ReturnNumber(string no)
        {
            int output = 0;
            try
            {
                output = int.Parse(no);
            }
            catch
            {
                output = Statics.NumList.IndexOf(no.ToLower());
                if (output < 0)
                    output = 0;
            }

            return output;
        }

        #endregion Methods

        #region Stem Commands

        private void VolumeCommand(string Value = "0" )        
        {
            WipeStem("Volume Control " + Value);
            try
            {
                int Percentage = 0;
                CoreAudioDevice defaultPlaybackDevice = new CoreAudioController().DefaultPlaybackDevice;

                if (Value == "Mute")
                    Percentage = 0;
                else if (Value.Split(' ')[0] == "Down")
                {
                    double CheckVal = defaultPlaybackDevice.Volume - ReturnNumber(Value.Split(' ')[1]);
                    if (CheckVal < 0)
                        CheckVal = 0;

                    defaultPlaybackDevice.Volume = CheckVal;
                    return;
                }
                else if (Value.Split(' ')[0] == "Up")
                {
                    double CheckVal = defaultPlaybackDevice.Volume + ReturnNumber(Value.Split(' ')[1]);
                    if (CheckVal > 100)
                        CheckVal = 0;

                    defaultPlaybackDevice.Volume = CheckVal;
                    return;
                }
                else
                {
                    Percentage = ReturnNumber(Value);

                    if (Percentage > 100)
                        Percentage = 100;
                    else if (Percentage < 0)
                        Percentage = 0;
                }

                defaultPlaybackDevice.Volume = Percentage;
            }
            catch (Exception ex)
            {
                File.AppendAllText(LogLocation, DateTime.UtcNow + " - Command Failed: " + "Volume Control" + Environment.NewLine
                    + ex + Environment.NewLine);
            }

        }

        private void MediaCommand(string com)
        {
            WipeStem("Media Control " + com);

            if (com == "Next")
            {
                keybd_event(VK_MEDIA_NEXT_TRACK, 0, KEYEVENTF_EXTENDEDKEY, IntPtr.Zero);
                keybd_event(VK_MEDIA_NEXT_TRACK, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
            }
            else if (com == "Previous")
            {
                keybd_event(VK_MEDIA_NEXT_TRACK, 0, KEYEVENTF_EXTENDEDKEY, IntPtr.Zero);
                keybd_event(VK_MEDIA_NEXT_TRACK, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
            }
            else if (com == "Play/Pause")
            {
                keybd_event(VK_MEDIA_PLAY_PAUSE, 0, KEYEVENTF_EXTENDEDKEY, IntPtr.Zero);
                keybd_event(VK_MEDIA_PLAY_PAUSE, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
            }
        }

        private void LockCommand()
        {
            WipeStem("Lock");
            LockWorkStation();
        }

        private void SleepCommand()
        {
            WipeStem("Sleep");
            Application.SetSuspendState(PowerState.Suspend, true, false);
        }

        private void PowerCommand()
        {
            WipeStem("PowerOff");

            var psi = new ProcessStartInfo("shutdown", "/s /t 2");
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            Process.Start(psi);
        }

        private void HibernateCommand()
        {
            WipeStem("Hibernate");
            Application.SetSuspendState(PowerState.Hibernate, true, false);
        }

        private void TestCommand()
        {
            WipeStem("Test");
        }

        #endregion Stem Commands

        #region Command Methods

        public void StopThreadCom()
        {
            LoopKiller = true;
            ExitWait.WaitOne(200);
            MainThread = null;
            NotifyPropertyChanged("Running");
            ActiveColour = "Red";
        }

        public void StartThreadCom()
        {
            LoopKiller = false;
            ExitWait.Reset();
            MainThread = new Thread(new ThreadStart(Run));
            MainThread.Start();
            NotifyPropertyChanged("Running");
        }

        public void SendCommandCom()
        {
            try
            {
                if (!ArdPort.IsOpen)
                    ArdPort.Open();

                string[] Liststrings = SerialPort.GetPortNames();
                ArdPort.Handshake = Handshake.None;
                ArdPort.Write("com");
                ArdPort.Close();
            }
            catch (Exception ex)
            {
                 
            }
        }

        static SerialPort ArdPort = new SerialPort("COM3", 9600, Parity.None, 8, StopBits.One);

        #endregion Command Methods

        #region Commands

        private RelayCommand _stopThread;
        public ICommand StopThread
        {
            get
            {
                if (_stopThread == null)
                {
                    _stopThread = new RelayCommand(param => StopThreadCom());
                }
                return _stopThread;
            }
        }

        private RelayCommand _startThread;
        public ICommand StartThread
        {
            get
            {
                if (_startThread == null)
                {
                    _startThread = new RelayCommand(param => StartThreadCom());
                }
                return _startThread;
            }
        }

        private RelayCommand _sendCommand;
        public ICommand SendCommand
        {
            get
            {
                if (_sendCommand == null)
                {
                    _sendCommand = new RelayCommand(param => SendCommandCom());
                }
                return _sendCommand;
            }
        }

        #endregion Commands


    }
}
