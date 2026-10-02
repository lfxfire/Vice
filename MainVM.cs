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
using Vice.Server;

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

            // Starts the polling thread if config says to by default
            if (Data.DefaultStartPolling)
                StartThreadCom();

            StartVolumeThread();

            // Sets the port name incase its been changed
            ArdPort.PortName = Data.SerialPortName;

            // Events
            Sleeper.SleepTrigger += RemoteCommand;
            Data.Saved += SavedFlashStarter;

            StartPhoneApi(true);
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
        // Only one thing may talk to the IR transmitter at a time. Always release in a finally block,
        // otherwise one failed command blocks every later one
        private readonly SemaphoreSlim RemoteLock = new SemaphoreSlim(1, 1);

        Thread MainThread;
        Thread VolumeThread;

        private volatile bool VolumeKiller = false;

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

        private volatile bool LoopKiller = false;

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

        // Bool for pausing commands being send to the IR transmitter
        private bool pauseCommandBool = false;
        public bool PauseCommandBool
        {
            get => pauseCommandBool;
            set
            {
                pauseCommandBool = value;
            }
        }

        #endregion Properties

        #region Methods

        #region Start up

        // Main polling method
        private void RunPolling()
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

                        // Deletes the file before running so a bad or crashing command is never re-read
                        File.Delete(Data.CommandStem);

                        // Dropbox can create the file before its contents arrive
                        if (ReadLines.Length > 0)
                        {
                            string TestWord = ReadLines[0];
                            string ValueWord = "";
                            string SecondValueWord = "";

                            // if attached parameter, sets from additional line, and for 3rd line
                            if (ReadLines.Length > 1)
                                ValueWord = ReadLines[1];
                            if (ReadLines.Length > 2)
                                SecondValueWord = ReadLines[2];

                            ExecuteCommand(TestWord, ValueWord, SecondValueWord);
                        }

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
                    // Dropbox can still have the file locked, so this delete must not take the thread down
                    try
                    {
                        File.Delete(Data.CommandStem);
                        File.AppendAllText(Data.LogLocation, DateTime.UtcNow + " - Polling error: " + ex + Environment.NewLine);
                    }
                    catch { }
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

        #endregion Start up

        #region Shutdown

        // Forces app to close in its entirety
        private void ApplicationClose()
        {
            StopThreadCom();
            StopVolumeThread();
            StopPhoneApi();
            Sleeper.ForceShutdown();
            Data.SaveLocal();
            App.Current.Shutdown();
        }

        #endregion Shutdown
        
        // Writes the command to the output box and log. The polling loop deletes the command file itself,
        // because commands can now also arrive from the phone API
        private void LogCommand(string Command = "")
        {
            if (Command == "")
                return;

            Console.WriteLine(Command);
            LogEvent("Command received: " + Command);
        }

        // Writes a line to the output box and log
        private void LogEvent(string message)
        {
            // Commands can arrive on the polling thread and phone API threads at the same time
            lock (LogLock)
            {
                Output += "\n" + DateTime.UtcNow + " - " + message;

                try
                {
                    File.AppendAllText(Data.LogLocation, DateTime.UtcNow + " - " + message + Environment.NewLine);
                }
                catch { }
            }
        }

        private readonly object LogLock = new object();

        // Checks and changes the volume
        private void VolChange()
        {
            while (!VolumeKiller)
            {
                Thread.Sleep(100);

                // Works out which single step moves the stored volumes towards their targets
                string step = null;
                if (Data.TvVolume > TvTargetVolume)
                    step = "BAR N";
                else if (Data.TvVolume < TvTargetVolume)
                    step = "BAR M";
                else if (Data.SubVolume > SubTargetVolume)
                    step = "BAR P";
                else if (Data.SubVolume < SubTargetVolume)
                    step = "BAR O";

                if (step == null)
                    continue;

                // waits till free then steps
                RemoteLock.Wait();
                try
                {
                    SendSerial(step);

                    if (step == "BAR N")
                        Data.TvVolume--;
                    else if (step == "BAR M")
                        Data.TvVolume++;
                    else if (step == "BAR P")
                        Data.SubVolume--;
                    else
                        Data.SubVolume++;

                    Thread.Sleep(100);
                }
                finally
                {
                    RemoteLock.Release();
                }
            }
        }

        // returns a number if it can read one, otherwise 0
        private int ReturnNumber(string no)
        {
            int output;
            return TryReturnNumber(no, out output) ? output : 0;
        }

        // Reads digits or a number word up to nineteen ("five"). False when it's neither
        private bool TryReturnNumber(string no, out int number)
        {
            no = (no ?? "").Trim().ToLower();

            if (int.TryParse(no, out number))
                return true;

            number = Statics.NumList.IndexOf(no);
            return number >= 0;
        }

        // Sends down the serial connection
        private void SendSerial(string content, bool Continue = false, int WaitTime = 0)
        {
            if (PauseCommandBool)
                return;

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

        // Runs one command. Used by both the Dropbox command file and the phone API.
        // command, value and value2 match lines 1, 2 and 3 of the command file
        public CommandResult ExecuteCommand(string command, string value = "", string value2 = "")
        {
            command = (command ?? "").Trim();
            value = (value ?? "").Trim();
            value2 = (value2 ?? "").Trim();

            try
            {
                switch (command)
                {
                    case "Test":
                        TestCommand();
                        break;

                    // Power Options
                    case "Lock":
                        LockCommand();
                        break;
                    case "Sleep":
                        SleepCommand();
                        break;
                    case "Hibernate":
                        HibernateCommand();
                        break;
                    case "PowerOff":
                        PowerCommand();
                        break;

                    // Sound Options
                    case "Volume Control":
                        if (value != "Mute" && !TryAdjustValue(value, 0, 0, 100, out _))
                            return CommandFailed(command, "Volume Control needs Mute, Up 10, Down 10 or a number");
                        VolumeCommand(value);
                        break;
                    case "Media Control":
                        if (value != "Next" && value != "Previous" && value != "Play/Pause")
                            return CommandFailed(command, "Media Control needs Next, Previous or Play/Pause");
                        MediaCommand(value);
                        break;

                    // Control Options
                    case "Type":
                        TypeCommand(value);
                        break;
                    case "Remote":
                        if (!RemoteCommandNames.Contains(value))
                            return CommandFailed(command, "Unknown remote command: " + value);
                        RemoteCommand(value, value2);
                        break;

                    // Soundbar volume, moved by the volume thread towards the target
                    case "TV Volume":
                        LogCommand("TV Volume " + value);
                        if (!TryAdjustValue(value, TvTargetVolume, 0, 50, out int tvVolume))
                            return CommandFailed(command, "TV Volume needs Up 2, Down 2, Set 30 or a number");
                        TvTargetVolume = tvVolume;
                        break;
                    case "Woofer Volume":
                        LogCommand("Woofer Volume " + value);
                        if (!TryAdjustValue(value, SubTargetVolume, 0, 12, out int wooferVolume))
                            return CommandFailed(command, "Woofer Volume needs Up 2, Down 2, Set 6 or a number");
                        SubTargetVolume = wooferVolume;
                        break;

                    case "TV Mode":
                        if (ModeChanging)
                            return CommandFailed(command, "The TV mode is already changing");
                        LogCommand("TV Mode " + value);
                        if (value == "Normal")
                            TvModeComN();
                        else if (value == "Cinema")
                            TvModeComC();
                        else if (value == "True Cinema")
                            TvModeComTC();
                        else
                            return CommandFailed(command, "TV Mode needs Normal, Cinema or True Cinema");
                        break;

                    case "Sleep Timer":
                        LogCommand("Sleep Timer " + value);
                        string action = value.Split(' ')[0];
                        if (action == "Start")
                        {
                            if (!Sleeper.Active)
                                Sleeper.ButtonCom();
                        }
                        else if (action == "Stop")
                        {
                            if (Sleeper.Active)
                                Sleeper.ButtonCom();
                        }
                        else if (TryAdjustValue(value.Replace("Add", "Up"), Sleeper.Timer, 0, 600, out int minutes))
                            Sleeper.Timer = minutes;
                        else
                            return CommandFailed(command, "Sleep Timer needs Add 5, Set 30, Start or Stop");
                        break;

                    default:
                        return CommandFailed(command, "Unknown command: " + command);
                }

                return CommandResult.Success();
            }
            catch (Exception ex)
            {
                try
                {
                    File.AppendAllText(Data.LogLocation, DateTime.UtcNow + " - Command Failed: " + command + Environment.NewLine + ex + Environment.NewLine);
                }
                catch { }

                return CommandResult.Failure(ex.Message);
            }
        }

        // Logs a rejected command and returns the failure
        private CommandResult CommandFailed(string command, string reason)
        {
            LogCommand("Rejected " + command + " - " + reason);
            return CommandResult.Failure(reason);
        }

        // Applies "Up 5", "Down 2", "Set 30" or a bare number to a value, keeping it in range.
        // Returns false when the value can't be read, so an empty or garbled command can't zero the volume
        private bool TryAdjustValue(string value, int current, int min, int max, out int result)
        {
            string[] parts = (value ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int number = 0;
            result = current;

            if (parts.Length == 0)
                return false;

            if (parts[0] == "Up" || parts[0] == "Down")
            {
                // No count means one step, but a count that can't be read is refused
                if (parts.Length > 1 && !TryReturnNumber(parts[1], out number))
                    return false;

                int step = Math.Max(1, number);
                result = parts[0] == "Up" ? current + step : current - step;
            }
            else if (parts[0] == "Set")
            {
                if (parts.Length < 2 || !TryReturnNumber(parts[1], out number))
                    return false;

                result = number;
            }
            else if (TryReturnNumber(parts[0], out number))
                result = number;
            else
                return false;

            result = Math.Max(min, Math.Min(max, result));
            return true;
        }

        // Snapshot of what Vice believes the TV, soundbar and timer are doing, for the phone app
        public Dictionary<string, object> GetState()
        {
            return new Dictionary<string, object>()
            {
                { "tvPower", Data.TvPower },
                { "barPower", Data.BarPower },
                { "tvVolume", Data.TvVolume },
                { "tvTargetVolume", TvTargetVolume },
                { "wooferVolume", Data.SubVolume },
                { "wooferTargetVolume", SubTargetVolume },
                { "tvMode", Data.TvMode == 3 ? "Normal" : Data.TvMode == 2 ? "Cinema" : "True Cinema" },
                { "tvModeChanging", ModeChanging },
                { "nightMode", Data.NightMode },
                { "sleepTimerActive", Sleeper.Active },
                { "sleepTimerMinutes", Sleeper.Timer },
                { "sleepTimerAction", new[] { "Lock", "Sleep", "Hibernate", "PowerOff" }[Math.Max(0, Math.Min(3, Data.SleepLockValue))] },
                { "sendingBlocked", PauseCommandBool },
                { "dropboxPolling", Running }
            };
        }

        // sets the PC volume from "Mute", "Up 10", "Down 10" or a number
        private void VolumeCommand(string Value = "0" )
        {
            LogCommand("Volume Control " + Value);
            try
            {
                CoreAudioDevice defaultPlaybackDevice = new CoreAudioController().DefaultPlaybackDevice;

                int Percentage;
                if (Value == "Mute")
                    Percentage = 0;
                else if (!TryAdjustValue(Value, (int)Math.Round(defaultPlaybackDevice.Volume), 0, 100, out Percentage))
                    return;

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
            LogCommand("Media Control " + com);

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
            LogCommand("Lock");
            LockWorkStation();
        }

        private void SleepCommand()
        {
            LogCommand("Sleep");
            Application.SetSuspendState(PowerState.Suspend, true, false);
        }

        private void PowerCommand()
        {
            LogCommand("PowerOff");

            var psi = new ProcessStartInfo("shutdown", "/s /t 2");
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            Process.Start(psi);
        }

        private void HibernateCommand()
        {
            LogCommand("Hibernate");
            Application.SetSuspendState(PowerState.Hibernate, true, false);
        }

        private void TestCommand()
        {
            LogCommand("Test");
        }

        // Types the command word
        private void TypeCommand(string setting)
        {
            LogCommand("Type Control " + setting);

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
                LogCommand("Remote " + command + " " + Args);

            RemoteLock.Wait();
            try
            {
                RemoteCommandLocked(command, Args);
            }
            finally
            {
                RemoteLock.Release();
            }
        }

        // Body of RemoteCommand, only called while RemoteLock is held
        private void RemoteCommandLocked(string command, string Args)
        {
            if (command == "NightMode")
            {
                SendSerial("BAR B");
                Data.NightMode = !Data.NightMode;
            }
            else if (command == "Volume")
            {
                int amount = StepAmount(Args);
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
                int amount = StepAmount(Args);
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
                    Sleeper.Timer = ReturnNumber(Args);

                    if (!Sleeper.Active)
                        Sleeper.ButtonCom();
                }
            }
        }

        // Remote commands RemoteCommand understands, used to reject typos before they reach the lock
        private static readonly HashSet<string> RemoteCommandNames = new HashSet<string>()
        {
            "NightMode", "Volume", "Woofer", "Power", "Tv Mode", "Bed time"
        };

        // Reads the step count from arguments like "Up 3", defaulting to one step
        private int StepAmount(string Args)
        {
            string[] parts = (Args ?? "").Split(' ');

            if (parts.Length < 2)
                return 1;

            int amount = ReturnNumber(parts[1]);
            return amount < 1 ? 1 : amount;
        }

        #endregion Stem Commands

        #region Command Methods

        #region Phone API

        private PhoneApiServer PhoneApi;

        // Turns the phone API on or off from the Phone menu
        public bool PhoneApiOn
        {
            get => Data.PhoneApiEnabled;
            set
            {
                Data.PhoneApiEnabled = value;
                Data.SaveLocal();
                RestartPhoneApi();
                NotifyPropertyChanged();
            }
        }

        // Starts the API the phone app talks to. On first run it makes a pairing code, and if Windows
        // hasn't given Vice permission to listen yet it offers to fix that once
        private void StartPhoneApi(bool offerAccess = false)
        {
            if (!Data.PhoneApiEnabled)
                return;

            if (string.IsNullOrWhiteSpace(Data.PhoneApiToken))
            {
                Data.PhoneApiToken = PhoneApiServer.NewToken();
                Data.SaveLocal();
            }

            PhoneApi = new PhoneApiServer(Data.PhoneApiPort, Data.PhoneApiToken,
                (command, value, value2) => ExecuteCommand(command, value, value2),
                () => GetState(),
                message => LogEvent(message));

            bool accessDenied;
            if (PhoneApi.Start(out accessDenied))
                return;

            PhoneApi = null;

            if (accessDenied && offerAccess &&
                MessageBox.Show("Vice needs a one-time Windows permission so your phone can reach it over Tailscale. Allow it now?",
                    "Vice Remote", MessageBoxButtons.YesNo) == DialogResult.Yes &&
                PhoneApiServer.GrantAccess(Data.PhoneApiPort))
            {
                StartPhoneApi();
            }
        }

        public void StopPhoneApi()
        {
            PhoneApi?.Stop();
            PhoneApi = null;
        }

        private void RestartPhoneApi()
        {
            StopPhoneApi();
            StartPhoneApi();
        }

        // Shows what to type into the Vice Remote app
        private void ShowPhonePairing()
        {
            string status = PhoneApi != null && PhoneApi.IsRunning
                ? "Running"
                : Data.PhoneApiEnabled ? "Not running. Try Phone > Allow phone access" : "Turned off";

            MessageBox.Show(
                "Enter these in the Vice Remote app:\n\n" +
                "PC address:  " + Environment.MachineName.ToLower() + "\n" +
                "Port:  " + Data.PhoneApiPort + "\n" +
                "Pairing code:  " + Data.PhoneApiToken + "\n\n" +
                "The PC address is this PC's name in Tailscale. Tailscale must be on for both the PC and the phone.\n\n" +
                "Phone API: " + status,
                "Vice Remote pairing");
        }

        #endregion Phone API

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
            MainThread = new Thread(new ThreadStart(RunPolling));
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

            // Gives a step already in progress the chance to finish
            if (RemoteLock.Wait(200))
                RemoteLock.Release();
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
            RemoteLock.Wait();
            try
            {
                SendSerial("BAR B");
                Data.NightMode = !Data.NightMode;

                Thread.Sleep(100);
            }
            finally
            {
                RemoteLock.Release();
            }
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

        private RelayCommand _exitButton;
        public ICommand ExitButton
        {
            get
            {
                if (_exitButton == null)
                {
                    _exitButton = new RelayCommand(param => ApplicationClose());
                }
                return _exitButton;
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
                    _powerTvCommand = new RelayCommand(param => Task.Run(() => RemoteCommand("Power", "TV", false)));
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
                    _powerBarCommand = new RelayCommand(param => Task.Run(() => RemoteCommand("Power", "Bar", false)));
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

        #region Phone Menu

        private RelayCommand _phoneShowPairing;
        public ICommand PhoneShowPairing
        {
            get
            {
                if (_phoneShowPairing == null)
                {
                    _phoneShowPairing = new RelayCommand(param => ShowPhonePairing());
                }
                return _phoneShowPairing;
            }
        }

        private RelayCommand _phoneCopyCode;
        public ICommand PhoneCopyCode
        {
            get
            {
                if (_phoneCopyCode == null)
                {
                    _phoneCopyCode = new RelayCommand(param => Clipboard.SetText(Data.PhoneApiToken));
                }
                return _phoneCopyCode;
            }
        }

        private RelayCommand _phoneAllowAccess;
        public ICommand PhoneAllowAccess
        {
            get
            {
                if (_phoneAllowAccess == null)
                {
                    _phoneAllowAccess = new RelayCommand(param =>
                    {
                        if (PhoneApiServer.GrantAccess(Data.PhoneApiPort))
                            RestartPhoneApi();
                    });
                }
                return _phoneAllowAccess;
            }
        }

        private RelayCommand _phoneNewCode;
        public ICommand PhoneNewCode
        {
            get
            {
                if (_phoneNewCode == null)
                {
                    _phoneNewCode = new RelayCommand(param =>
                    {
                        if (MessageBox.Show("Make a new pairing code? Your phone will need the new code before it works again.",
                            "Vice Remote", MessageBoxButtons.OKCancel) != DialogResult.OK)
                            return;

                        Data.PhoneApiToken = PhoneApiServer.NewToken();
                        Data.SaveLocal();
                        RestartPhoneApi();
                        ShowPhonePairing();
                    });
                }
                return _phoneNewCode;
            }
        }

        #endregion Phone Menu

        #endregion Commands


    }
}
