using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Vice.Resources;

namespace Vice.Models
{
    public class SleepObject : Notify
    {
        private bool _active = false;
        public bool Active
        {
            get => _active;
            set { _active = value; NotifyPropertyChanged(); }
        }

        private int _timer = 0;
        public int Timer
        {
            get => _timer;
            set 
            { 
                _timer = value;
                NotifyPropertyChanged(); 
            }
        }

        Thread SleepThread;

        public event StemEvent SleepTrigger;

        // if reset allows sleepthread to get wiped
        private ManualResetEvent SleepEnd = new ManualResetEvent(true);

        public void ButtonCom()
        {
            if (Timer < 1)
                return;

            if (SleepThread != null && Active)
            {
                Active = false;
                SleepEnd.WaitOne(300);
                SleepThread = null;
            }
            else if (SleepThread != null)
            {
                SleepThread.Abort();
                SleepThread = null;
            }
            else if (!Active)
            {
                Active = true;
                StartThread();                
            }
        }

        public void ForceShutdown()
        {
            if (Active)
            {
                Active = false;
                SleepEnd.WaitOne(300);
                SleepThread = null;
            }
        }

        // Starts the sleep thread
        private void StartThread()
        {
            SleepThread = new Thread(() => Running());
            SleepThread.Start();
            SleepEnd.Reset();
        }

        DateTime OldTime;

        private void Running()
        {
            OldTime = DateTime.UtcNow;

            while (Active)
            {
                Thread.Sleep(300);

                // Safety
                if (OldTime + TimeSpan.FromMinutes(5) < DateTime.UtcNow)
                    Active = false;
                else if (OldTime + TimeSpan.FromMinutes(1) < DateTime.UtcNow)
                {
                    Timer--;
                    OldTime += TimeSpan.FromMinutes(1);
                }

                // When timer hits zero
                if (Timer < 1 && Active)
                {
                    Active = false;
                    SleepTrigger("Bed time", "", false);
                }
            }

            SleepEnd.Set();
        }

    }
}
