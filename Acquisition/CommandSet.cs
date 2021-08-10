using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace Acquisition
{
    public class CommandSequence : List<Command>
    {
        public CommandSequence(HeadState resetTo, HeadState continueTo)
        {
            ErrorResetState = resetTo;
            SuccessNextState = continueTo;
        }

        public HeadState ErrorResetState { get; }
        public HeadState SuccessNextState { get; }
    }

    public static class CommandSet
    {
        public static readonly string QueryParameter = "?";
        public static readonly string DefaultParameter = "*";
        public static readonly int NoiseFloor = 3;

        #region Callbacks

        public static bool ParseNoiseFloor(Command cmd, Head h, string resp)
        {
            return int.Parse(resp) == NoiseFloor;
        }

        public static bool ParseStatusByte(Command cmd, Head h, string resp)
        {
            var i = int.Parse(resp);
            h.SetStatus((HeadStatusBits)i);
            return i == 0;
        }

        public static bool ParseIDString(Command cmd, Head h, string resp)
        {
            h.SetID(resp);
            return resp.Length > 0;
        }

        public static bool ParseFilamentCurrent(Command cmd, Head h, string resp)
        {
            return (float.Parse(resp, System.Globalization.CultureInfo.InvariantCulture) - 
                float.Parse(TurnFilamentON.Parameter, System.Globalization.CultureInfo.InvariantCulture)) < 0.1;
        }

        public static bool ParseHighVoltage(Command cmd, Head h, string resp)
        {
            return (int.Parse(resp) - int.Parse(TurnHVON.Parameter)) < 50;
        }

        public static bool ParseTotalScanPoints(Command cmd, Head h, string resp)
        {
            h.SetTotalScanPoints(int.Parse(resp));
            return true;
        }

        public static bool ParseStartAMU(Command cmd, Head h, string resp)
        {
            h.SetStartAMU(int.Parse(resp));
            return true;
        }

        public static bool ParseStopAMU(Command cmd, Head h, string resp)
        {
            h.SetEndAMU(int.Parse(resp));
            return true;
        }

        public static bool ParsePointsPerAMU(Command cmd, Head h, string resp)
        {
            h.SetPointsPerAMU(int.Parse(resp));
            return true;
        }

        public static bool ParseHVCalibrated(Command cmd, Head h, string resp)
        {
            if (!int.TryParse(resp, out _)) return false;
            if (int.Parse(TurnHVON.Parameter) > 0) TurnHVON.Parameter = resp;
            return true;
        }

        public static bool ParseCdemGain(Command cmd, Head h, string resp)
        {
            if (int.Parse(TurnHVON.Parameter) > 0) //MG commands returns CDEM gain in units of thousands
                h.SetCdemGain((int)(float.Parse(resp, System.Globalization.CultureInfo.InvariantCulture) * 1000));
            return true;
        }

        #endregion

        public static Command StatusQuery = new Command("ER", ParseStatusByte, QueryParameter);
        public static Command InitializeCommunication = new Command("IN", ParseStatusByte, 0);
        public static Command IdentificationQuery = new Command("ID", ParseIDString, QueryParameter);
        public static Command ShutDownMassFilter = new Command("MR", null, 0, true);
        public static Command TurnFilamentON = new Command("FL", ParseStatusByte, 1.0);
        public static Command TurnFilamentOFF = new Command("FL", ParseStatusByte, 0.0);
        public static Command FilamentCurrentQuery = new Command("FL", ParseFilamentCurrent, QueryParameter);
        public static Command TurnHVOFF = new Command("HV", ParseStatusByte, 0.0);
        public static Command HighVoltageQuery = new Command("HV", ParseHighVoltage, QueryParameter);
        public static Command TurnHVON = new Command("HV", ParseStatusByte, 1449);
        public static Command StartAnalogScan = new Command("SC", null, 1, true);
        public static Command SetStartAMU = new Command("MI", null, 29, true);
        public static Command SetEndAMU = new Command("MF", null, 200, true); //Use default values here
        public static Command SetPointsPerAMU = new Command("SA", null, 10, true);
        public static Command TotalScanPointsQuery = new Command("AP", ParseTotalScanPoints, QueryParameter);
        public static Command QueryStartAMU = new Command("MI", ParseStartAMU, QueryParameter);
        public static Command QueryStopAMU = new Command("MF", ParseStopAMU, QueryParameter);
        public static Command QueryPointsPerAMU = new Command("SA", ParsePointsPerAMU, QueryParameter);
        public static Command QueryHVCalibrated = new Command("MV", ParseHVCalibrated, QueryParameter);
        public static Command QueryCdemGain = new Command("MG", ParseCdemGain, QueryParameter);
        public static Command ResetRS232Error = new Command("EC", null, QueryParameter);
        public static Command SetNoiseFloor = new Command("NF", null, NoiseFloor, true);
        public static Command QueryNoiseFloor = new Command("NF", ParseNoiseFloor, QueryParameter);

        public static readonly Dictionary<HeadState, CommandSequence> Sequences = new Dictionary<HeadState, CommandSequence>()
        {
            { 
                HeadState.PowerUp, 
                new CommandSequence(HeadState.PowerUp, HeadState.Initialized)
                {
                    ShutDownMassFilter,
                    InitializeCommunication,
                    IdentificationQuery,
                    QueryHVCalibrated,
                    QueryCdemGain
                }
            },
            {
                HeadState.Initialized,
                new CommandSequence(HeadState.Initialized, HeadState.IonizerON)
                {
                    TurnFilamentON,
                    FilamentCurrentQuery
                }
            },
            {
                HeadState.IonizerON,
                new CommandSequence(HeadState.Initialized, HeadState.DetectorON)
                {
                    FilamentCurrentQuery,
                    TurnHVON,
                    HighVoltageQuery
                }
            },
            {
                HeadState.DetectorON,
                new CommandSequence(HeadState.PowerUp, HeadState.ReadyToScan)
                {
                    ShutDownMassFilter,
                    StatusQuery,
                    FilamentCurrentQuery,
                    HighVoltageQuery,
                    SetNoiseFloor,
                    QueryNoiseFloor
                }
            },
            {
                HeadState.StartScan,
                new CommandSequence(HeadState.DetectorON, HeadState.Scanning)
                {
                    SetPointsPerAMU,
                    QueryPointsPerAMU,
                    SetStartAMU,
                    SetEndAMU,
                    ResetRS232Error,
                    SetStartAMU,
                    QueryStartAMU,
                    SetEndAMU,
                    QueryStopAMU,
                    StatusQuery,
                    TotalScanPointsQuery,
                    StartAnalogScan
                }
            },
            {
                HeadState.PowerDown,
                new CommandSequence(HeadState.PowerDown, HeadState.PowerDown)
                {
                    ShutDownMassFilter,
                    TurnHVOFF,
                    TurnFilamentOFF,
                    StatusQuery
                }
            }
        };
    }
}
