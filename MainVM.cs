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
        #region DLL

        [DllImport("User32.dll", SetLastError = true)]
        public static extern bool LockWorkStation();

        [DllImport("User32.dll", SetLastError = true)]
        public static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, IntPtr extraInfo);
        public const int VK_MEDIA_NEXT_TRACK = 0xB0;
        public const int VK_MEDIA_PLAY_PAUSE = 0xB3;
        public const int VK_MEDIA_PREV_TRACK = 0xB1;
        public const int Right_Arrow = 0x27;
        public const int Left_Arrow = 0x25;
        public const int KEYEVENTF_EXTENDEDKEY = 0x0001; //Key down flag
        public const int KEYEVENTF_KEYUP = 0x0002; //Key up flag

        #endregion DLL

        public MainVM()
        {
            StartThreadCom();
        }

        #region Properties

        private ManualResetEvent ExitWait = new ManualResetEvent(true);
        private ManualResetEvent ColourWait = new ManualResetEvent(true);

        Thread MainThread;

        private string _serialPort = "COM3";
        public string SerialPortName
        {
            get => _serialPort;
            set { _serialPort = value; NotifyPropertyChanged(); }
        }

        static SerialPort ArdPort = new SerialPort("COM3", 9600, Parity.None, 8, StopBits.One);

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
                        string SecondValueWord = "";

                        // if attached parameter, sets from additional line, and for 3rd line
                        if (ReadLines.Length > 1)
                            ValueWord = ReadLines[1];                        
                        if (ReadLines.Length > 2)
                            SecondValueWord = ReadLines[2];

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

                        // Control Options
                        else if (TestWord == "Type")
                            TypeCommand(ValueWord);
                        else if (TestWord == "Remote")
                            RemoteCommand(ValueWord, SecondValueWord);

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

        // Sends down the serial connection
        private void SendSerial(string content, bool Continue = false, int WaitTime = 0)
        {
            try
            {
                if (!ArdPort.IsOpen)
                    ArdPort.Open();

                ArdPort.Handshake = Handshake.None;
                ArdPort.Write(content);

                if (!Continue)
                    ArdPort.Close();

                if (WaitTime > 0)
                    Thread.Sleep(WaitTime);

                Console.WriteLine("X");
            }
            catch (Exception ex)
            {
                File.AppendAllText(LogLocation, ex.ToString());
            }
        }

        #endregion Methods

        #region Stem Commands

        // sets the volume
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

        // Controls the music playing
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
                keybd_event(VK_MEDIA_PREV_TRACK, 0, KEYEVENTF_EXTENDEDKEY, IntPtr.Zero);
                keybd_event(VK_MEDIA_PREV_TRACK, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
                keybd_event(VK_MEDIA_PREV_TRACK, 0, KEYEVENTF_EXTENDEDKEY, IntPtr.Zero);
                keybd_event(VK_MEDIA_PREV_TRACK, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
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

        // Types the command word
        private void TypeCommand(string setting)
        {
            WipeStem("Type Control " + setting);

            // Sets the word variables
            string DeciderWord = setting.Split(' ')[0];
            string ControlWord = "";
            bool firstRun = true;

            if (setting.Split(' ').Count() == 2)
            {
                ControlWord = setting.Split(' ')[1];
            }
            else if (setting.Split(' ').Count() > 2)
            {
                foreach (string x in setting.Split(' '))
                {
                    if (!firstRun)
                        ControlWord = ControlWord + x + " ";
                    else
                        firstRun = false;

                }
            }

            if (DeciderWord == "Left")
            {
                for (int x = ReturnNumber(ControlWord); x > 0; x--)
                {
                    keybd_event(Left_Arrow, 0, KEYEVENTF_EXTENDEDKEY, IntPtr.Zero);
                    keybd_event(Left_Arrow, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
                    Thread.Sleep(20);
                }
            }
            else if (DeciderWord == "Right")
            {
                for (int x = ReturnNumber(ControlWord); x > 0; x--)
                {
                    keybd_event(Right_Arrow, 0, KEYEVENTF_EXTENDEDKEY, IntPtr.Zero);
                    keybd_event(Right_Arrow, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
                    Thread.Sleep(20);
                }
            }
            else if (DeciderWord == "Type")
            {
                foreach (char x in ControlWord.ToUpper()) 
                {                    
                    byte keypress = (byte)x;
                    keybd_event(keypress, 0, KEYEVENTF_EXTENDEDKEY, IntPtr.Zero);
                    keybd_event(keypress, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
                    Thread.Sleep(10);
                }
            }
        }

        // Mimics a tv remote for the tv/sound bar
        private void RemoteCommand(string command, string Args = "")
        {
            WipeStem("Remote" + command);

            if (command == "NightMode")
            {
                SendSerial("BAR B");
            }
            else if (command == "Volume")
            {
                int amount = int.Parse(Args.Split(' ')[1]);
                string Direction = Args.Split(' ')[0];

                // Itterates in a loop for n-1 times with continue set true and a timer
                for(int x = 0; x + 1 < amount; x++)
                {
                    if (Direction == "Up")
                        SendSerial("BAR M", true, 240);
                    else if (Direction == "Down")
                        SendSerial("BAR N", true, 240);
                }

                // Final run with continue set false
                if (Direction == "Up")
                    SendSerial("BAR M");
                else if (Direction == "Down")
                    SendSerial("BAR N");
            }
            else if (command == "Woofer")
            {
                int amount = int.Parse(Args.Split(' ')[1]);
                string Direction = Args.Split(' ')[0];

                // Itterates in a loop for n-1 times with continue set true
                for (int x = 0; x + 1 < amount; x++)
                {
                    if (Direction == "Up")
                        SendSerial("BAR O", true, 240);
                    else if (Direction == "Down")
                        SendSerial("BAR P", true, 240);
                }

                // Final run with continue set false
                if (Direction == "Up")
                    SendSerial("BAR O");
                else if (Direction == "Down")
                    SendSerial("BAR P");
            }
            else if (command == "Power")
            {
                if (Args == "TV")
                    SendSerial("TV 1");
                else if (Args == "Bar")
                    SendSerial("BAR 0");
            }
            else if (command == "Tv Mode")
            {
                SendSerial("TV B", true, 400);
                SendSerial("TV 6", true, 300);
                SendSerial("TV 6", true, 400);
                SendSerial("TV 6", true, 800);

                if (Args == "Up")
                    SendSerial("TV 5", true, 200);
                else if (Args == "Down")
                    SendSerial("TV 7", true, 200);

                SendSerial("TV 9");
            }
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

        // Used from test button
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
                File.AppendAllText(LogLocation, ex.ToString());
            }
        }


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
