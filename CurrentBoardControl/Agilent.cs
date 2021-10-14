using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;

namespace CypressSemiconductor.ChinaManufacturingTest
{
    public class Switch
    {
        public int PowerRelayDelay = 0;

        protected bool devReadyStatus = false;

        virtual public bool IsDevReady
        {
            get { return devReadyStatus; }
        }

        public virtual bool Connect(string ConnectString)
        {
            return false;
        }

        public virtual bool SetRelayWellA(byte RelaySetCh1, byte RelaySetCh2)
        {
            return false;
        }
    }

    public class SwitchFake : Switch
    {
        public override bool Connect(string ConnectString)
        {
            devReadyStatus = true;
            return true;
        }

        public override bool SetRelayWellA(byte RelaySetCh1, byte RelaySetCh2)
        {
            Thread.Sleep(PowerRelayDelay);
            return true;
        }
    }

    public class AgilentMessageEventArgs : EventArgs
    {
        public AgilentMessageEventArgs(string s)
        {
            message = s;
        }
        private string message;

        public string Message
        {
            get { return message; }
            set { message = value; }
        }
    }


}//end of namespace
