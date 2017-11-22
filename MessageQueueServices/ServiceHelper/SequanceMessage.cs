using System;

namespace ServiceHelper
{
    public class SequanceMessage
    {
        public SequanceMessage()
        {
            MessageType = MessageType.File;
            SettingValue = 0;
        }
        public Guid ClientId { get; set; }
        public Guid SequanceId { get; set; }
        public int Position { get; set; }
        public string Label { get; set; }
        public MessageType MessageType { get; set; }
        public int SettingValue { get; set; }
        public byte[] Body { get; set; }
    }

    public enum MessageType
    {
        File,
        ClientStatus,
        Setting
    }
}
