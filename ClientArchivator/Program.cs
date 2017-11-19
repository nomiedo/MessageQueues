using System;
using System.IO;
using System.Messaging;
using System.Configuration;
using System.Collections.Generic;

namespace ClientArchivator
{
    public class SequenceMessage
    {
        public int Sequence { get; set; }
        public int Position { get; set; }
        public byte[] Body { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            string queueName = System.Configuration.ConfigurationManager.AppSettings["QueueName"];
            string resourceFolderPath = System.Configuration.ConfigurationManager.AppSettings["ResourceFilePath"];
            DirectoryInfo resourceDirectory = GetDirectoryWithValidation(resourceFolderPath);
            MessageQueue queue = GetQueue(queueName);

            if (resourceDirectory == null) ;
                return;

            foreach (var file in resourceDirectory.GetFiles("*.jpg"))
            {
                FileStream FileStream = new FileStream(file.FullName, System.IO.FileMode.Open);
                byte[] binary = ReadFully(FileStream);
                SequenceMessage message = new SequenceMessage()
                {
                    Sequence = 1,
                    Position = 1,
                    Body = binary
                };

                queue.Send(message);
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

        private static void SendMessagesUsingTransactions(MessageQueue queue, List<Message> meassges)
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

        public static byte[] ReadFully(Stream input)
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

    }
}
