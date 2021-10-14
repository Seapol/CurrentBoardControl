using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Runtime.InteropServices;
using CypressSemiconductor.ChinaManufacturingTest;

namespace CyBLE_MTK_Application
{
    public class MTKTestCurrent : MTKTest
    {
        public double MinCurrentMA;

        private int _MaxCurrentMA;
        public int MaxCurrentMA
        {
            get
            {
                return _MaxCurrentMA;
            }

            set
            {
                _MaxCurrentMA = value;
                //Only for test purpose.
                MultiMeter.UserInputMaxCurrentForFakeValueGeneration = MaxCurrentMA;
            }
        }
        public int SampleCount;
        public int SampleIntervalMs;
        public int CurrCalcMs;
        public UInt32 CombinedPassCondition;
        public UInt32 ExtFlags;
        public MTKTestCurrent() : base()
        {
            Init();
        }

        public MTKTestCurrent(LogManager Logger)
            : base(Logger)
        {
            Init();
        }

        public MTKTestCurrent(LogManager Logger, SerialPort MTKPort, SerialPort DUTPort)
            : base(Logger, MTKPort, DUTPort)
        {
            Init();
        }

        public enum EnumPassConPerSample
        {
            AVERAGE = 0,    //Check average value
            MAX_AND_MIN = 1 //Check Max and Min average
        };

        public enum EnumPassConOverall
        {
            All_SAMPLES = 0, //Pass is all samples pass
            ONE_SAMPLE = 1   //Pass if one sample pass
        };

        public EnumPassConPerSample PassConPerSample
        {
            get {
                if ((CombinedPassCondition & 0xFF) == 0)
                {
                    return EnumPassConPerSample.AVERAGE;
                }
                else
                {
                    return EnumPassConPerSample.MAX_AND_MIN;
                }
            }
            set {
                CombinedPassCondition &= 0xFFFFFF00;
                CombinedPassCondition |= (UInt32)value;
            }
        }

        public EnumPassConOverall PassConOverall
        {
            get
            {
                UInt32 con = (CombinedPassCondition & 0xFF00);
                con >>= 8;

                if (con == 0)
                {
                    return EnumPassConOverall.All_SAMPLES;
                }
                else
                {
                    return EnumPassConOverall.ONE_SAMPLE;
                }
            }
            set
            {
                CombinedPassCondition &= 0xFFFF00FF;
                CombinedPassCondition |= ((UInt32)value << 8);
            }
        }

        private void Init()
        {
            TestParameterCount = 7;
            TmplSFCSErrCode = SFCS.ERROR_CODE_DMM;
            MinCurrentMA = 5;
            MaxCurrentMA = 20;
            SampleCount = 5;
            ExtFlags = 0;
            SampleIntervalMs = 5;
            CurrCalcMs = 500;
            CombinedPassCondition = 0;
        }

        public override string GetDisplayText()
        {
            return "Current Test | Range " + MinCurrentMA.ToString("f1") + " ~ " + MaxCurrentMA.ToString() + "mA | Samples " 
                + SampleCount.ToString() + " | Interval " + SampleIntervalMs.ToString() + "ms | CalcTime " + 
                CurrCalcMs.ToString() + "ms | Con1 " + PassConPerSample.ToString() + " | Con2 " + PassConOverall.ToString();
        }

        public override string GetTestParameter(int TestParameterIndex)
        {
            switch (TestParameterIndex)
            {
                case 0:
                    return MinCurrentMA.ToString();
                case 1:
                    return MaxCurrentMA.ToString();
                case 2:
                    return SampleCount.ToString();
                case 3:
                    return SampleIntervalMs.ToString();
                case 4:
                    return CurrCalcMs.ToString();
                case 5:
                    return CombinedPassCondition.ToString();
                case 6:
                    return ExtFlags.ToString();
            }
            return base.GetTestParameter(TestParameterIndex);
        }

        public override string GetTestParameterName(int TestParameterIndex)
        {
            switch (TestParameterIndex)
            {
                case 0:
                    return "MinCurrentMA";
                case 1:
                    return "MaxCurrentMA";
                case 2:
                    return "SampleCount";
                case 3:
                    return "SampleIntervalMs";
                case 4:
                    return "CurrCalcMs";
                case 5:
                    return "CombinedPassCondition";
                case 6:
                    return "ExtFlags";
            }
            return base.GetTestParameterName(TestParameterIndex);
        }

        public override bool SetTestParameter(int TestParameterIndex, string ParameterValue)
        {
            if (ParameterValue == "")
            {
                return false;
            }
            switch (TestParameterIndex)
            {
                case 0:
                    MinCurrentMA = double.Parse(ParameterValue);
                    return true;
                case 1:
                    MaxCurrentMA = int.Parse(ParameterValue);
                    return true;
                case 2:
                    SampleCount = int.Parse(ParameterValue);
                    return true;
                case 3:
                    SampleIntervalMs = int.Parse(ParameterValue);
                    return true;
                case 4:
                    CurrCalcMs = int.Parse(ParameterValue);
                    return true;
                case 5:
                    CombinedPassCondition = UInt32.Parse(ParameterValue);
                    return true;
                case 6:
                    ExtFlags = UInt32.Parse(ParameterValue);
                    return true;
            }
            return false;
        }

        private bool DoesCurrentPass(double measuredvalue)
        {
            if (measuredvalue < MinCurrentMA)
            {
                TmplSFCSErrCode = SFCS.ERROR_CODE_DMM_LOW;
            }
            else if (measuredvalue > MaxCurrentMA)
            {
                TmplSFCSErrCode = SFCS.ERROR_CODE_DMM_HIGH;
            }
            else
            {
                return true;
            }

            return false;
        }

        private MTKTestError _RunTest()
        {
            MTKTestError RetVal = MTKTestError.NoError;
            int retry = 0;

            /*
             * Try three times to swith
             */
            //Check if dmm&switch setup.
            if (!MTKInstruments.SwitchDutPower(DUTPort, Log, this, MTKInstruments.PowerState.PowerOn, BCMStartupMode.App))
            {
                TestStatusUpdate(MTKTestMessageType.Failure, "SW FAIL");
                this.Log.PrintLog(this, "Fail to switch DUT power on.", LogDetailLevel.LogRelevant);
                RetVal = MTKTestError.TestFailed;
                TestResult.Result = "FAIL";
                TestResultUpdate(TestResult);
                return RetVal;
            }
            /*
             * IsAppRunning() will send UART command to DUT. This will affect current test value on SOME CYBT-413055-02(20719B1) module SOMETIMES.
             * e.g. 
             *  1. If there is no delay between calling IsAppRunning() and current test. 
             *     The 1st current test value could be 5.2mA. The 2nd current test value could drop to 1.5mA (Normal).
             *  2. If add delay here or don't call IsAppRunning(), the 1st current test value will be 1.5mA.
             * So, if current test is not the last serial test, I won't check app before current test. 
             * If app is not running, the next test will fail. So it is recommended add firmware checking after current test.
             * [One Exception]
             *   CYBT-483056-02/CYBT-483062-02 modules has a bug: A lot of unwanted bytes will rush out on the first UART-Command (reset/CUS 11).
             *   It is not easy to pick correct response from stuff response.(Ref to ReadAPacketUntilTimeout). So, set bit0 of ExtFlags to indicate
             *   must check application after power-on.
             */
            if (((ExtFlags & 0x01) == 1) || IsTheLastRunInAppModeTest || !IsTheFirstSerialTest)
            {
                while (retry < 3)
                {
                    if (retry > 0)
                        Thread.Sleep((CyBLE_MTK_Application.Properties.Settings.Default.AppLaunchTimeOverride > 0) ?
                        CyBLE_MTK_Application.Properties.Settings.Default.AppLaunchTimeOverride : 500);

                    //See the comments of MTKBCMHelper.ResetDutToApp()
                    if (!DUTPort.IsAppRunning())
                    {
                        TestStatusUpdate(MTKTestMessageType.Failure, "NotInApp");
                        this.Log.PrintLog(this, "Application is not running after switch DUT power on after retry " + retry.ToString(), LogDetailLevel.LogRelevant);
                    }
                    else
                    {
                        this.Log.PrintLog(this, "CheckAppRunning SUCC. Starting current test...",
                            HCISupport.IsAdditionalDbgFlagEnabled(HCISupport.AddDbgFlags.Trace_instrment) ? LogDetailLevel.LogRelevant : LogDetailLevel.LogEverything);
                        break;
                    }
                    retry++;
                }

                if (retry == 3)
                {
                    TestStatusUpdate(MTKTestMessageType.Failure, "SWAPP FAIL");
                    this.Log.PrintLog(this, "CheckAppRunning FAIL.", LogDetailLevel.LogRelevant);
                    RetVal = MTKTestError.TestFailed;
                    TestResult.Result = "FAIL";
                    TestResultUpdate(TestResult);
                    return RetVal;
                }
            }
            else
            {
                this.Log.PrintLog(this, "CheckAppRunning SKIP. Starting current test...",
                            HCISupport.IsAdditionalDbgFlagEnabled(HCISupport.AddDbgFlags.Trace_instrment) ? LogDetailLevel.LogRelevant : LogDetailLevel.LogEverything);

                Thread.Sleep((CyBLE_MTK_Application.Properties.Settings.Default.AppLaunchTimeOverride > 0) ?
                            CyBLE_MTK_Application.Properties.Settings.Default.AppLaunchTimeOverride : 500);
            }

            //Read current value.
            int i;
            bool overallPass = false;
            TestResult.Measured = "";
            for (i = 0; i < SampleCount; i++)
            {
                Current curr = MTKInstruments.MeasureChannelCurrent(CurrCalcMs);

                bool samplePass;
                string sampleMeasuredMsg;

                if (PassConPerSample == EnumPassConPerSample.AVERAGE)
                {
                    samplePass = DoesCurrentPass(curr.average);
                    sampleMeasuredMsg = curr.average.ToString();
                }
                else
                {
                    samplePass = DoesCurrentPass(curr.min) && DoesCurrentPass(curr.max);
                    sampleMeasuredMsg = curr.min.ToString() + ":" + curr.max.ToString();
                }

                if (i == 0)
                    TestResult.Measured += sampleMeasuredMsg;
                else
                    TestResult.Measured += ("|" + sampleMeasuredMsg);

                Log.PrintLog(this, "Current[" + i.ToString() + "] Min " + curr.min.ToString()
                    + " Max " + curr.max.ToString() + " Avg " + curr.average.ToString() + (samplePass ? "" : " --> X"), LogDetailLevel.LogEverything);

                if (samplePass)
                {
                    if ((PassConOverall == EnumPassConOverall.ONE_SAMPLE) || (i == SampleCount - 1))
                    {
                        overallPass = true;
                        break;
                    }
                }
                else
                {
                    TestStatusUpdate(MTKTestMessageType.Failure, TestResult.Measured);

                    if (PassConOverall == EnumPassConOverall.All_SAMPLES || (i == SampleCount - 1))
                    {
                        overallPass = false;
                        break;
                    }
                }

                Thread.Sleep(SampleIntervalMs);
            }

            TestResult.Measured += "(mA)";

            if (!overallPass)
            {
                TestResult.Result = "FAIL";
                RetVal = MTKTestError.TestFailed;
            }
            else
            {
                TestStatusUpdate(MTKTestMessageType.Success, "PASS");
                TestResult.Result = "PASS";
            }

            TestResultUpdate(TestResult);

            return RetVal;
        }

        public override MTKTestError RunTest()
        {
            MTKTestError RetVal = MTKTestError.TestFailed;

            this.InitializeTestResult();

            if (!MTKInstruments.Connected)
            {
                TestStatusUpdate(MTKTestMessageType.Failure, "DMM/SW FAIL");
                this.Log.PrintLog(this, "Instruments for current test are not ready.", LogDetailLevel.LogRelevant);
                RetVal = MTKTestError.TestFailed;
                TestResult.Result = "FAIL";
                TestResultUpdate(TestResult);
                return RetVal;
            }

            try
            {
                RetVal = _RunTest();
            }
            catch
            {
                RetVal = MTKTestError.TestFailed;
                TestResult.Result = "FAIL";
                TestResultUpdate(TestResult);
            }
            finally
            {
                /*
                 * If current test is not the first serial execution test case, recover all duts' power is needed.
                 * One exception, if there is a program all at end. So will power on all duts at beginning of programall.
                 */
                if (!IsTheFirstSerialTest)
                {
                    if (!MTKInstruments.SwitchDutPower(null, Log, this, MTKInstruments.PowerState.PowerOnAll, BCMStartupMode.App))
                    {
                        this.Log.PrintLog(this, "Fail to recover power after current test.", LogDetailLevel.LogRelevant);
                    }
                }
            }

            return RetVal;
        }

        protected override void InitializeTestResult()
        {
            base.InitializeTestResult();
            TestResult.PassCriterion = MinCurrentMA.ToString("f1") + "~" + MaxCurrentMA.ToString() + "(mA)";
            TestResult.Measured = "N/A";
            TmplSFCSErrCode = SFCS.ERROR_CODE_DMM;
        }

        public override string ToString()
        {
            return "#CURR" + DUTPort.PortName;
        }
    }
}
