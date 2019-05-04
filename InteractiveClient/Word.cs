using System;
using System.Collections.Generic;
using System.Text;

namespace InteractiveClient
{
    public class Word
    {
        public long Index { get; set; }

        public string Text { get; set; }

        public double BufferLevel { get; set; }

        public DateTime Received { get; set; }
    }
}
