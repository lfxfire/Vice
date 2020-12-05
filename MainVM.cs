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
using System.Windows.Threading;
using Vice.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Vice.Commands;

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
            ReadLocal();

            StartThreadCom();
            StartVolumeThread();

            // Sets the port name incase its been changed
            ArdPort.PortName = Data.SerialPortName;

            // Events
            Sleeper.SleepTrigger += RemoteCommand;
            Data.Saved += SavedFlashStarter;
        }

        #region Properties

        #region Remote Holding Values

        private int _tvTVolume = 35;
        public int TvTargetVolume
        {
            get => _tvTVolume;
            set { _tvTVolume = value; NotifyPropertyChanged(); }
        }

        private int _subTarget = 12;
        public int SubTargetVolume
        {
            get => _subTarget;
            set { _subTarget = value; NotifyPropertyChanged(); }
        }

        // sets if the tv mode is currently state changing
        private bool _modechanging = false;
        public bool ModeChanging
        {
            get => _modechanging;
            set { _modechanging = value; NotifyPropertyChanged(); }
        }

        #endregion Remote Holding Values

        #region Sleep timer bools

        // sets all the other bools to false when set to true
        private bool _sleepLock = false;
        public bool SleepLock
        {
            get => _sleepLock;
            set
            {
                if (value)
                {
                    AllowSleepFunctionSetFalse = true;
                    _sleepLock = value;

                    //_sleepLock = false;
                    SleepSleep = false;
                    SleepHibernate = false;
                    SleepPower = false;

                    Data.SleepLockValue = 0;

                    AllowSleepFunctionSetFalse = false;
                }
                else if (AllowSleepFunctionSetFalse)
                    _sleepLock = value;

                NotifyPropertyChanged();
            }
        }

        // sets all the other bools to false when set to true
        private bool _sleepSleep = false;
        public bool SleepSleep
        {
            get => _sleepSleep;
            set
            {
                if (value)
                {
                    AllowSleepFunctionSetFalse = true;
                    _sleepSleep = value;

                    SleepLock = false;
                    //_sleepSleep = false;
                    SleepHibernate = false;
                    SleepPower = false;

                    Data.SleepLockValue = 1;

                    AllowSleepFunctionSetFalse = false;
                }
                else if (AllowSleepFunctionSetFalse)
                    _sleepSleep = value;

                NotifyPropertyChanged();
            }
        }

        // sets all the other bools to false when set to true
        private bool _sleepHibernate = false;
        public bool SleepHibernate
        {
            get => _sleepHibernate;
            set
            {
                if (value)
                {
                    AllowSleepFunctionSetFalse = true;
                    _sleepHibernate = value;

                    SleepLock = false;
                    SleepSleep = false;
                    //_sleepHibernate = false;
                    SleepPower = false;

                    Data.SleepLockValue = 2;

                    AllowSleepFunctionSetFalse = false;
                }
                else if (AllowSleepFunctionSetFalse)
                    _sleepHibernate = value;

                NotifyPropertyChanged();
            }
        }

        // sets all the other bools to false when set to true
        private bool _sleepPower = false;
        public bool SleepPower
        {
            get => _sleepPower;
            set
            {
                if (value)
                {
                    AllowSleepFunctionSetFalse = true;
                    _sleepPower = value;

                    SleepLock = false;
                    SleepSleep = false;
                    SleepHibernate = false;
                    //_sleepPower = false;

                    Data.SleepLockValue = 3;

                    AllowSleepFunctionSetFalse = false;
                }
                else if (AllowSleepFunctionSetFalse)
                    _sleepPower = value;

                NotifyPropertyChanged();
            }
        }

        // bool which stops user from setting all sleep timer bools to false
        private bool AllowSleepFunctionSetFalse;

        #endregion Sleep timer bools

        private ManualResetEvent ExitWait = new ManualResetEvent(true);
        private ManualResetEvent RemoteWait = new ManualResetEvent(true);

        Thread MainThread;
        Thread VolumeThread;

        private bool VolumeKiller = false;

        private SaveData _data = new SaveData();
        public SaveData Data
        {
            get => _data;
            set { _data = value; NotifyPropertyChanged();}
        }

        private SleepObject _sleepObject = new SleepObject();
        public SleepObject Sleeper
        {
            get => _sleepObject;
            set { _sleepObject = value; NotifyPropertyChanged(); }
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

        // Colour of the saver circle
        private string _activeColourSaver = "White";
        public string ActiveColourSaver
        {
            get { return _activeColourSaver; }
            set { _activeColourSaver = value; NotifyPropertyChanged(); }
        }

        // in millseconds
        int Inverval = 400;
        int LightInterval = 200;

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
            ActiveColour = "Green";

            while (!LoopKiller)
            {
                //Console.WriteLine("Checked");

                try
                {
                    if (File.Exists(Data.CommandStem))
                    {
                        ActiveColour = "Green";
                        //ColourChangeCreate("Blue");

                        CommandRead = DateTime.UtcNow;

                        string[] ReadLines = File.ReadAllLines(Data.CommandStem);

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
                    File.Delete(Data.CommandStem);
                    File.AppendAllText(Data.LogLocation, ex.ToString());
                }

                ActiveColour = "Green";
                Thread.Sleep(Inverval);
            }

            ExitWait.Set();
        }

        // Sets the value of the front end menu bools
        private void AssignTimerBools()
        {
            if (Data != null)
            {
                // Sets the bool value based on Data's equivlent value
                if (Data.SleepLockValue == 0)
                    SleepLock = true;
                else if (Data.SleepLockValue == 1)
                    SleepSleep = true;
                else if (Data.SleepLockValue == 2)
                    SleepHibernate = true;
                else if (Data.SleepLockValue == 3)
                    SleepPower = true;
            }
        }

        // Reads the data file
        private void ReadLocal()
        {
            string localpath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            JsonSerializerSettings JsonSettings = new JsonSerializerSettings() { Formatting = Formatting.Indented };

            if (!Directory.Exists(localpath + "\\Vice"))
                Directory.CreateDirectory(localpath + "\\Vice");
            else if (File.Exists(localpath + "\\Vice\\Data.txt"))
            {
                string tempread = File.ReadAllText(localpath + "\\Vice\\Data.txt");
                Data = JsonConvert.DeserializeObject<SaveData>(tempread, JsonSettings);
                Data.AllowedToSave = true;

                TvTargetVolume = Data.TvVolume;
                SubTargetVolume = Data.SubVolume;
            }

            AssignTimerBools();
        }

        // Runs at the end of the command - removes the document and writes to log
        private void WipeStem(string Command = "")
        {
            //File.WriteAllText(CommandStem, "Waiting");

            File.Delete(Data.CommandStem);

            if (Command != "")
            {
                Console.WriteLine(Command);
                Output += string.Format("\n" + DateTime.UtcNow + " - Command received: " + Command);
                File.AppendAllText( Data.LogLocation, DateTime.UtcNow + " - Command received: " + Command + Environment.NewLine);
                //Output += "\n" + (DateTime.UtcNow - CommandRead).ToString();
            }
        }

        // Checks and changes the volume
        private void VolChange()
        {
            while (!VolumeKiller)
            {
                Thread.Sleep(100);

                // if higher then waits till free then decreases
                if (Data.TvVolume > TvTargetVolume)
                {
                    RemoteWait.WaitOne();
                    RemoteWait.Reset();

                    SendSerial("BAR N");
                    Data.TvVolume--;

                    Thread.Sleep(100);

                    RemoteWait.Set();
                }
                // if Lower then waits till free then decreases
                else if (Data.TvVolume < TvTargetVolume)
                {
                    RemoteWait.WaitOne();
                    RemoteWait.Reset();

                    SendSerial("BAR M");
                    Data.TvVolume++;

                    Thread.Sleep(100);

                    RemoteWait.Set();
                }

                // if higher then waits till free then decreases
                else if (Data.SubVolume > SubTargetVolume)
                {
                    RemoteWait.WaitOne();
                    RemoteWait.Reset();

                    SendSerial("BAR P");
                    Data.SubVolume--;

                    Thread.Sleep(100);

                    RemoteWait.Set();
                }
                // if Lower then waits till free then decreases
                else if (Data.SubVolume < SubTargetVolume)
                {
                    RemoteWait.WaitOne();
                    RemoteWait.Reset();

                    SendSerial("BAR O");
                    Data.SubVolume++;

                    Thread.Sleep(100);

                    RemoteWait.Set();
                }
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
                File.AppendAllText(Data.LogLocation, ex.ToString());
            }
        }

        #region Save Flasher

        // Starts the flash thread
        private void SavedFlashStarter()
        {
            Thread SaveFlasher = new Thread(() => SaveFlash());
            SaveFlasher.Start();
        }

        // Flashes the circle
        private void SaveFlash()
        {
            // Sets colour light blue
            ActiveColourSaver = "#FF3DC1FF";
            Thread.Sleep(300);
            ActiveColourSaver = "White";
        }

        #endregion Save Flasher

        #endregion Methods

        #region Stem Commands

        // sets the volume
        private void VolumeCommand(string Value = "0" )
        {
            WipeStem("Volume Control " + Value);
            try
            {
                //throw new Exception();

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
                File.AppendAllText(Data.LogLocation, DateTime.UtcNow + " - Command Failed: " + "Volume Control" + Environment.NewLine
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
        private void RemoteCommand(string command, string Args = "", bool Wipe = true)
        {
            if (Wipe)
                WipeStem("Remote" + command);

            RemoteWait.WaitOne();
            RemoteWait.Reset();

            if (command == "NightMode")
            {
                SendSerial("BAR B");
                Data.NightMode = !Data.NightMode;
            }
            else if (command == "Volume")
            {
                int amount = int.Parse(Args.Split(' ')[1]);
                string Direction = Args.Split(' ')[0];

                // Itterates in a loop for n-1 times with continue set true and a timer
                for (int x = 0; x + 1 < amount && x < 50; x++)
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
                for (int x = 0; x + 1 < amount && x < 12; x++)
                {
                    if (Direction == "Up")
                    {
                        SendSerial("BAR O", true, 240);
                        Data.SubVolume++;
                        SubTargetVolume++;
                    }
                    else if (Direction == "Down")
                    {
                        SendSerial("BAR P", true, 240);
                        Data.SubVolume--;
                        SubTargetVolume--;
                    }
                }

                // Final run with continue set false
                if (Direction == "Up")
                {
                    SendSerial("BAR O");
                    Data.SubVolume++;
                    SubTargetVolume++;
                }
                else if (Direction == "Down")
                {
                    SendSerial("BAR P");
                    Data.SubVolume--;
                    SubTargetVolume--;
                }
            }
            else if (command == "Power")
            {
                if (Args == "TV")
                {
                    SendSerial("TV 1");
                    Data.TvPower = !Data.TvPower;
                }
                else if (Args == "Bar")
                {
                    SendSerial("BAR 0");
                    Data.BarPower = !Data.BarPower;
                }
            }
            else if (command == "Tv Mode")
            {
                // Uses dispactcher so works from background thread
                App.Current.Dispatcher.Invoke(() => { ModeChanging = true; });

                // Opens the menu where the mode can be changed 
                SendSerial("TV B", true, 400);
                SendSerial("TV 6", true, 300);
                SendSerial("TV 6", true, 400);
                SendSerial("TV 6", true, 800);

                // goes up or down and adjusts the Tv mode value, Upw/DownW does two commands rather than one
                if (Args == "Up" || Args == "UpW")
                {
                    SendSerial("TV 5", true, 200);
                    if (Data.TvMode < 3)
                        App.Current.Dispatcher.Invoke(() => { Data.TvMode++; });

                    // Second up command
                    if (Args == "UpW")
                    {
                        SendSerial("TV 5", true, 200);
                        if (Data.TvMode < 3)
                            App.Current.Dispatcher.Invoke(() => { Data.TvMode++; });
                    }
                }
                else if (Args == "Down" || Args == "DownW")
                {
                    SendSerial("TV 7", true, 200);
                    if (Data.TvMode > 1)
                        App.Current.Dispatcher.Invoke(() => { Data.TvMode--; });

                    // Second down command
                    if (Args == "DownW")
                    {
                        SendSerial("TV 7", true, 200);
                        if (Data.TvMode > 1)
                            App.Current.Dispatcher.Invoke(() => { Data.TvMode--; });
                    }
                }

                SendSerial("TV 9", false, 500);

                //if (Args == "UpW" || Args == "DownW")
                //    Thread.Sleep(1000);

                App.Current.Dispatcher.Invoke(() => { ModeChanging = false; });
            }
            // Command for both tv off and timer power
            else if(command == "Bed time")
            {
                if (Args == "")
                {
                    if (Data.TvPower)
                    {
                        SendSerial("TV 1");
                        Data.TvPower = !Data.TvPower;
                        // 6 second delay to account for state change
                        Thread.Sleep(6000);                        
                    }

                    // Carrys out the power mode set in data
                    if (Data.SleepLockValue == 0)
                        LockCommand();
                    else if (Data.SleepLockValue == 1)
                        SleepCommand();
                    else if (Data.SleepLockValue == 2)
                        HibernateCommand();
                    else if (Data.SleepLockValue == 3)
                        PowerCommand();
                }
                else
                {
                    Sleeper.Timer = int.Parse(Args);

                    if (!Sleeper.Active)
                        Sleeper.ButtonCom();
                }
            }

            RemoteWait.Set();
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

        public void StartVolumeThread()
        {
            VolumeKiller = false;
            VolumeThread = new Thread(() => VolChange());
            VolumeThread.Start();
        }

        public void StopVolumeThread()
        {
            VolumeKiller = true;
            RemoteWait.WaitOne(200);
            VolumeThread = null;
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
                File.AppendAllText(Data.LogLocation, ex.ToString());
            }
        }

        // Creates a thread which turns Up the tv mode
        public void TvUpCreater(bool DoubleSend = false)
        {
            if (!DoubleSend)
            {
                var ThreadUp = new Thread(() => RemoteCommand("Tv Mode", "Up", false));
                ThreadUp.Start();
            }
            else
            {
                // does a double up
                var ThreadUp = new Thread(() => RemoteCommand("Tv Mode", "UpW", false));
                ThreadUp.Start();
            }
        }

        // Creates a thread which turns down the tv mode
        public void TvDownCreater(bool DoubleSend = false)
        {
            if (!DoubleSend)
            {
                var ThreadUp = new Thread(() => RemoteCommand("Tv Mode", "Down", false));
                ThreadUp.Start();
            }
            else
            {
                // does a double down
                var ThreadUp = new Thread(() => RemoteCommand("Tv Mode", "DownW", false));
                ThreadUp.Start();
            }
        }

        // Creates multipule threads to turd up and down the tv mode
        public void TvModeComN()
        {
            if (!ModeChanging)
            {
                if (Data.TvMode == 1)
                {
                    TvUpCreater(true);
                    //TvUpCreater();
                }
                else if (Data.TvMode == 2)
                    TvUpCreater();
            }
        }

        // Creates multipule threads to turd up and down the tv mode
        public void TvModeComC()
        {
            if (!ModeChanging)
            {
                if (Data.TvMode == 3)
                {
                    TvDownCreater();
                }
                else if (Data.TvMode == 1)
                    TvUpCreater();
            }
        }

        // Creates multipule threads to turn up and down the tv mode
        public void TvModeComTC()
        {
            if (!ModeChanging)
            {
                if (Data.TvMode == 3)
                {
                    TvDownCreater(true);
                    //TvDownCreater();
                }
                else if (Data.TvMode == 2)
                    TvDownCreater();
            }
        }

        public void NightModeCom()
        {
            RemoteWait.WaitOne();

            // Checks no other has passed through at the same time
            Thread.Sleep(50);
            RemoteWait.WaitOne();

            RemoteWait.Reset();

            SendSerial("BAR B");
            Data.NightMode = !Data.NightMode;

            Thread.Sleep(100);

            RemoteWait.Set();
        }

        public void NightModeStarter()
        {
            var NightModeThread = new Thread(() => NightModeCom());
            NightModeThread.Start();
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

        private ICommand _defaultTVPower;
        public ICommand DefaultTVPower
        {
            get
            {
                if (_defaultTVPower == null)
                {
                    _defaultTVPower = new RelayCommand(param => Data.TvPower = !Data.TvPower);
                }
                return _defaultTVPower;
            }
        }

        private RelayCommand _defaultBarPower;
        public ICommand DefaultBarPower
        {
            get
            {
                if (_defaultBarPower == null)
                {
                    _defaultBarPower = new RelayCommand(param => Data.BarPower = !Data.BarPower);
                }
                return _defaultBarPower;
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

        private RelayCommand _testCommand;
        public ICommand TestCommandcom
        {
            get
            {
                if (_testCommand == null)
                {
                    _testCommand = new RelayCommand(param => BrightnessCom.Test());
                }
                return _testCommand;
            }
        }

        private RelayCommand _defaultN;
        public ICommand DefaultN
        {
            get
            {
                if (_defaultN == null)
                {
                    _defaultN = new RelayCommand(param => { Data.TvMode = 3; });
                }
                return _defaultN;
            }
        }

        private RelayCommand _defaultC;
        public ICommand DefaultC
        {
            get
            {
                if (_defaultC == null)
                {
                    _defaultC = new RelayCommand(param => { Data.TvMode = 2; });
                }
                return _defaultC;
            }
        }

        private RelayCommand _defaultTC;
        public ICommand DefaultTC
        {
            get
            {
                if (_defaultTC == null)
                {
                    _defaultTC = new RelayCommand(param => { Data.TvMode = 1; });
                }
                return _defaultTC;
            }
        }

        private RelayCommand _sleepStartStop;
        public ICommand SleepStartStop
        {
            get
            {
                if (_sleepStartStop == null)
                {
                    _sleepStartStop = new RelayCommand(param => { Sleeper.ButtonCom(); });
                }
                return _sleepStartStop;
            }
        }

        private RelayCommand _sleepFive;
        public ICommand SleepFive
        {
            get
            {
                if (_sleepFive == null)
                {
                    _sleepFive = new RelayCommand(param => { Sleeper.Timer += 5; });
                }
                return _sleepFive;
            }
        }

        #region Remote Buttons

        private RelayCommand _powerTvCommand;
        public ICommand PowerTvCommand
        {
            get
            {
                if (_powerTvCommand == null)
                {
                    _powerTvCommand = new RelayCommand(param => RemoteCommand("Power", "TV", false));
                }
                return _powerTvCommand;
            }
        }

        private RelayCommand _powerBarCommand;
        public ICommand PowerBarCommand
        {
            get
            {
                if (_powerBarCommand == null)
                {
                    _powerBarCommand = new RelayCommand(param => RemoteCommand("Power", "Bar", false));
                }
                return _powerBarCommand;
            }
        }

        private RelayCommand _tvUp;
        public ICommand TvUp
        {
            get
            {
                if (_tvUp == null)
                {
                    _tvUp = new RelayCommand(param => TvUpCreater());
                }
                return _tvUp;
            }
        }
        
        private RelayCommand _tvDown;
        public ICommand TvDown
        {
            get
            {
                if (_tvDown == null)
                {
                    _tvDown = new RelayCommand(param => TvDownCreater());
                }
                return _tvDown;
            }
        }

        private RelayCommand _tvModeC;
        public ICommand TvModeC
        {
            get
            {
                if (_tvModeC == null)
                {
                    _tvModeC = new RelayCommand(param => TvModeComC());
                }
                return _tvModeC;
            }
        }

        private RelayCommand _tvModeN;
        public ICommand TvModeN
        {
            get
            {
                if (_tvModeN == null)
                {
                    _tvModeN = new RelayCommand(param => TvModeComN());
                }
                return _tvModeN;
            }
        }

        private RelayCommand _tvModeTC;
        public ICommand TvModeTC
        {
            get
            {
                if (_tvModeTC == null)
                {
                    _tvModeTC = new RelayCommand(param => TvModeComTC());
                }
                return _tvModeTC;
            }
        }

        private RelayCommand _tvVolUp1;
        public ICommand TvVolUp1
        {
            get
            {
                if (_tvVolUp1 == null)
                {
                    _tvVolUp1 = new RelayCommand(param => { TvTargetVolume++; });
                }
                return _tvVolUp1;
            }
        }

        private RelayCommand _tvVolUp5;
        public ICommand TvVolUp5
        {
            get
            {
                if (_tvVolUp5 == null)
                {
                    _tvVolUp5 = new RelayCommand(param => { TvTargetVolume += 5; });
                }
                return _tvVolUp5;
            }
        }

        private RelayCommand _tvVolDown5;
        public ICommand TvVolDown5
        {
            get
            {
                if (_tvVolDown5 == null)
                {
                    _tvVolDown5 = new RelayCommand(param => { TvTargetVolume -= 5; });
                }
                return _tvVolDown5;
            }
        }

        private RelayCommand _tvVolDown1;
        public ICommand TvVolDown1
        {
            get
            {
                if (_tvVolDown1 == null)
                {
                    _tvVolDown1 = new RelayCommand(param => { TvTargetVolume--; });
                }
                return _tvVolDown1;
            }
        }

        private RelayCommand _SubVolDown1;
        public ICommand SubVolDown2
        {
            get
            {
                if (_SubVolDown1 == null)
                {
                    _SubVolDown1 = new RelayCommand(param => { SubTargetVolume -= 2; });
                }
                return _SubVolDown1;
            }
        }

        private RelayCommand _SubVolup2;
        public ICommand SubVolUp2
        {
            get
            {
                if (_SubVolup2 == null)
                {
                    _SubVolup2 = new RelayCommand(param => { SubTargetVolume += 2; });
                }
                return _SubVolup2;
            }
        }

        private RelayCommand _nightmodeComm;
        public ICommand NightmodeComm
        {
            get
            {
                if (_nightmodeComm == null)
                {
                    _nightmodeComm = new RelayCommand(param => { NightModeStarter(); });
                }
                return _nightmodeComm;
            }
        }

        private RelayCommand _nightModeSwap;
        public ICommand NightModeSwap
        {
            get
            {
                if (_nightModeSwap == null)
                {
                    _nightModeSwap = new RelayCommand(param => { Data.NightMode = !Data.NightMode; });
                }
                return _nightModeSwap;
            }
        }

        #endregion Remote Buttons

        #endregion Commands


    }
}
