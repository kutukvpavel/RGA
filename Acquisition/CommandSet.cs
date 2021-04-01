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
            return true;
        }

        public static bool ParseHighVoltage(Command cmd, Head h, string resp)
        {
            return true;
        }

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
        public static Command StartAnalogScan = new Command("SC", null, 1);

        public static readonly Dictionary<HeadState, CommandSequence> Sequences = new Dictionary<HeadState, CommandSequence>()
        {
            { 
                HeadState.PowerUp, 
                new CommandSequence(HeadState.PowerUp, HeadState.Initialized)
                {
                    ShutDownMassFilter,
                    InitializeCommunication,
                    IdentificationQuery
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
                new CommandSequence(HeadState.PowerUp, HeadState.DetectorON)
                {
                    ShutDownMassFilter,
                    StatusQuery,
                    FilamentCurrentQuery,
                    HighVoltageQuery
                }
            },
            {
                HeadState.StartScan,
                new CommandSequence(HeadState.DetectorON, HeadState.Scanning)
                {
                    StartAnalogScan
                }
            }
        };
    }
}
