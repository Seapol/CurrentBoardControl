using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;
using System.Diagnostics;
using Microsoft.CSharp.RuntimeBinder;

namespace CypressSemiconductor.ChinaManufacturingTest
{
    public struct Current
    {
        public double max;
        public double min;
        public double average;
    }

    public class MultiMeter
    {
        public static int UserInputMaxCurrentForFakeValueGeneration = 20;

        protected bool devReadyStatus = false;

        virtual public bool IsDevReady
        {
            get { return devReadyStatus; }
        }

        virtual public bool Connect(string ConnectString)
        {
            return false;
        }

        virtual public Current MeasureChannelCurrent(int MeasureMs)
        {
            Current cur = new Current();
            cur.max = cur.average = cur.min = 0;
            return cur;
        }

        protected int CurrResutMultiplier = 1000;

        public void SetCurrentUnit(string UnitStr)
        {
            if (UnitStr.ToLower().Trim() == "ma")
            {
                CurrResutMultiplier = 1;
            }
            else
            {
                CurrResutMultiplier = 1000;
            }
        }

        virtual public bool IsMTKCurrentMeasureBoard()
        {
            return false;
        }
    }

    public class MultiMeterFake : MultiMeter
    {
        private Random Ran = new Random();

        override public bool Connect(string ConnectString)
        {
            devReadyStatus = true;
            return devReadyStatus;
        }

        override public Current MeasureChannelCurrent(int MeasureMs)
        {
            Current ch_current = new Current();
            ch_current.average = (Ran.NextDouble() * UserInputMaxCurrentForFakeValueGeneration * 2);
            ch_current.min = ch_current.average * (Ran.NextDouble() * 0.9);
            ch_current.max = ch_current.average * (Ran.NextDouble() + 1);
            ch_current.average = Math.Round(ch_current.average, 2);
            ch_current.min = Math.Round(ch_current.min, 2);
            ch_current.max = Math.Round(ch_current.max, 2);
            return ch_current;
        }
    }

}//end of namespace
