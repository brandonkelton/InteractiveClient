using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace InteractiveClient
{
    public class Messenger
    {
        public static int BufferSize { get; set; } = 1024;

        public byte[] Buffer { get; set; } = new byte[BufferSize];

        public StringBuilder Message { get; set; } = new StringBuilder();

        public bool HasMessage => Message.Length > 0;

        public bool IsFormatted => Message.ToString().StartsWith("<FORMAT>");

        public bool IsError { get; set; }

        public void Clear()
        {
            Message.Clear();
            IsError = false;
        }

        public void ClearBuffer()
        {
            Buffer = new byte[BufferSize];
        }
    }
}
