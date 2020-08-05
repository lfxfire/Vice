using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Vice.Resources;

namespace Vice.Models
{
    public class SaveData : Notify
    {
        public bool AllowedToSave = false;

        private string _serialPort = "COM3";
        public string SerialPortName
        {
            get => _serialPort;
            set { _serialPort = value; NotifyPropertyChanged(); }
        }

        public string CommandStem { get; set; } = "C:\\Users\\laure\\DropBox\\Vice Link\\CommandStem.txt";
        public string LogLocation { get; set; } = "C:\\Users\\laure\\Dropbox\\Vice Link\\ViceLog.txt";

        private bool _tvPower = true;
        public bool TvPower
        {
            get => _tvPower;
            set { _tvPower = value; NotifyPropertyChanged(); StartThreadWait(); }
        }

        private bool _barPower = true;
        public bool BarPower
        {
            get => _barPower;
            set { _barPower = value; NotifyPropertyChanged(); StartThreadWait(); }
        }

        // represents the state, 3 = normal, 2 = cinema, 1 = true cinema
        private int _tvMode = 3;
        public int TvMode
        {
            get => _tvMode;
            set { _tvMode = value; NotifyPropertyChanged(); StartThreadWait(); }
        }

        private int _tvVolume = 35;
        public int TvVolume
        {
            get => _tvVolume;
            set { _tvVolume = value; NotifyPropertyChanged(); StartThreadWait(); }
        }

        private int _SubVolume = 12;
        public int SubVolume
        {
            get => _SubVolume;
            set { _SubVolume = value; NotifyPropertyChanged(); StartThreadWait(); }
        }

        private bool _nightMode = false;
        public bool NightMode
        {
            get => _nightMode;
            set { _nightMode = value; NotifyPropertyChanged(); StartThreadWait(); }
        }

        // saves
        public void SaveLocal()
        {
            JsonSerializerSettings JsonSettings = new JsonSerializerSettings() { Formatting = Formatting.Indented };
            string localpath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            if (!Directory.Exists(localpath + "\\Vice"))
                Directory.CreateDirectory(localpath + "\\Vice");
            else
            {
                AllowedToSave = false;
                string Tempwrite = JsonConvert.SerializeObject(this, JsonSettings);
                AllowedToSave = true;
                File.WriteAllText(localpath + "\\Vice\\Data.txt", Tempwrite);
            }
        }

        // Starts a thread with the wait timer
        public void StartThreadWait()
        {
            if (!AllowedToSave)
                return;

            if (WaitThread != null)
            {
                WaitThread.Abort();
                WaitThread = null;
            }

            WaitThread = new Thread(WaitForSave);
            WaitThread.Start();
        }

        Thread WaitThread;

        // 5 second delay on changes before saving
        private void WaitForSave()
        {
            Thread.Sleep(5000);

            SaveLocal();
        }
    }
}
