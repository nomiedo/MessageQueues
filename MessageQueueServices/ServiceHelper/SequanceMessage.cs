using System;

namespace ServiceHelper
{
    public class SequanceMessage
    {
        public string Label { get; set; }
        public Guid SequanceId { get; set; }
        public int Position { get; set; }
        public int Parts { get; set; }
        public byte[] Body { get; set; }
    }
}
