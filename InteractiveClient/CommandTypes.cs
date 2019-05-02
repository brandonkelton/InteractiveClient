using System;
using System.Collections.Generic;
using System.Text;

namespace InteractiveClient
{
    public class CommandTypes
    {
        public const string CONNECT = "connect";
        public const string REPEAT = "repeat";
        public const string REPEAT_WAIT = "repeatwait";
        public const string START_CONSUMERS = "startconsumers";
        public const string STOP_CONSUMERS = "stopconsumers";
        public const string STOP_ALL_CONSUMERS = "stopallconsumers";
        public const string CONSUMER_STATUS = "consumerstatus";
        public const string LIST_WORDS = "listwords";
        public const string SAVE_WORDS = "savewords";
        public const string CLEAR = "clear";
        public const string EXIT = "exit";
    }
}
