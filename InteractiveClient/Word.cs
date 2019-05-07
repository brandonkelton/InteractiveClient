using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace InteractiveClient
{
    [DataContract]
    public class Word
    {
        [DataMember]
        public long Index { get; set; }

        [DataMember]
        public string Text { get; set; }

        [DataMember]
        public double BufferLevel { get; set; }
    }
}
