using System;
using System.IO;
using System.Messaging;
using System.Configuration;
using System.Collections.Generic;

namespace ClientArchivator
{
    public class SequenceMessage
    {
        public Guid Sequence { get; set; }
        public int Position { get; set; }
        public byte[] Body { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            string queueName = ConfigurationManager.AppSettings["QueueName"];
            string resourceFolderPath = ConfigurationManager.AppSettings["ResourceFilePath"];
            DirectoryInfo resourceDirectory = GetDirectoryWithValidation(resourceFolderPath);
            MessageQueue queue = GetQueue(queueName);
            int bodySize = 1024 * 4;

            if (resourceDirectory == null)
                return;

            foreach (var file in resourceDirectory.GetFiles("*.jpg"))
            {
                FileStream FileStream = new FileStream(file.FullName, FileMode.Open);
                byte[] bytes = GetByteArray(FileStream);
                List<byte[]> listBytes = SplitByteArray(bytes, bodySize);
                List<SequenceMessage> batchMessages = CreateBatchMessages(listBytes);
                SendMessagesUsingTransactions(queue, batchMessages);
            }

            Console.Read();
        }

        private static DirectoryInfo GetDirectoryWithValidation(string path)
        {
            DirectoryInfo directory = new DirectoryInfo(path);

            if (!directory.Exists)
            {
                Console.WriteLine(@"Directory is not exist");
                return null;
            }

            if (directory.GetFiles("*.jpg").Length < 1)
            {
                Console.WriteLine(@"Directory does not contain any files.");
                return null;
            }

            return directory;
        }

        private static MessageQueue GetQueue(string queueName)
        {
            MessageQueue queue;

            if (MessageQueue.Exists(queueName))
                queue = new MessageQueue(queueName);
            else
                queue = MessageQueue.Create(queueName);

            queue.Formatter = new XmlMessageFormatter(new Type[] { typeof(SequenceMessage), typeof(string) });

            return queue;
        }

        private static List<SequenceMessage> CreateBatchMessages(List<byte[]> listBytes)
        {
            var id = Guid.NewGuid();
            var position = 0;
            var result = new List<SequenceMessage>();

            foreach (var bytes in listBytes)
            {
                SequenceMessage message = new SequenceMessage
                {
                    Sequence = id,
                    Position = position,
                    Body = bytes
                };
                position++;
                result.Add(message);
            }
            return result;
        }

        private static void SendMessagesUsingTransactions(MessageQueue queue, List<SequenceMessage> meassges)
        {
            using (var trans = new MessageQueueTransaction())
            {
                trans.Begin();
                foreach (var message in meassges)
                {
                    queue.Send(message, trans);
                }
                trans.Commit();
            }
        }

        public static byte[] GetByteArray(Stream input)
        {
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }

        public static List<byte[]> SplitByteArray(byte[] source, int size)
        {
            List<byte[]> result = new List<byte[]>();
            for (int i = 0; i < source.Length; i += size)
            {
                byte[] buffer = new byte[size];
                Buffer.BlockCopy(source, i, buffer, 0, size);
                result.Add(buffer);
            }
            return result;
        }
    }
}
