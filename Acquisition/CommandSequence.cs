using System;
using System.Collections.Generic;
using System.Text;

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

    public enum CommandEnum
    {
        StatusQuery,
        InitializeCommunication,
        IdentificationQuery
    }

    public static class CommandSet
    {
        public static readonly string QueryParameter = "?";
        public static readonly string DefaultParameter = "*";

        public static readonly Dictionary<CommandEnum, Command> Commands = new Dictionary<CommandEnum, Command>()
        {
            { CommandEnum.StatusQuery, new Command("ER", QueryParameter) },
            { CommandEnum.InitializeCommunication, new Command("IN0")  },
            { CommandEnum.IdentificationQuery, new Command("ID", QueryParameter) }
        };

        public static readonly Dictionary<HeadState, CommandSequence> Sequences = new Dictionary<HeadState, CommandSequence>()
        {
            { 
                HeadState.PowerUp, 
                new CommandSequence(HeadState.PowerUp, HeadState.Initialized) 
                {
                    Commands[CommandEnum.IdentificationQuery],
                    Commands[CommandEnum.InitializeCommunication]
                }
            },
            {
                HeadState.Initialized,
                new CommandSequence(HeadState.Initialized, HeadState.IonizerON)
                {

                }
            },
            {
                HeadState.IonizerON,
                new CommandSequence(HeadState.Initialized, HeadState.DetectorON)
                {

                }
            },
            {
                HeadState.DetectorON,
                new CommandSequence(HeadState.Initialized, HeadState.Scanning)
                {

                }
            },
            {
                HeadState.StartScan,
                new CommandSequence(HeadState.AbortScan, HeadState.Scanning)
                {

                }
            },
            {
                HeadState.AbortScan,
                new CommandSequence(HeadState.PowerUp, HeadState.DetectorON)
                {

                }
            }
        };
    }
}
