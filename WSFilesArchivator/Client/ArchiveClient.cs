using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Messaging;
using System.Threading;
using System.Threading.Tasks;

namespace ArchiveClient
{
    class ArchiveClient
    {
        private readonly Timer timer;
        private readonly string queueName;
        private readonly string resourceFolderPath;
        int messageSize = (1024 * 4);
        static object _sync = new object();
        Task currentTask;

        private DirectoryInfo resourceDirectory;
        private MessageQueue queue;

        public ArchiveClient()
        {
            timer = new Timer(Handle);
            queueName = ConfigurationManager.AppSettings["QueueName"];
            queue = GetQueue(queueName);
            resourceFolderPath = ConfigurationManager.AppSettings["ResourceFilePath"];
            resourceDirectory = GetDirectoryWithValidation(resourceFolderPath);

            if (resourceDirectory == null)
                Console.WriteLine("Directory is not availanle");
        }

        public bool Start()
        {
            timer.Change(0, 5000);
            return true;
        }

        public bool Stop()
        {
            lock (_sync)
            {
                currentTask.Wait();
                timer.Change(Timeout.Infinite, 0);
                return true;
            }
        }

        private void Handle(object target)
        {
            lock (_sync)
            {
                currentTask = Task.Factory.StartNew(() =>
                {

                    if (resourceDirectory.GetFiles("*.jpg").Length < 0)
                        return;

                    foreach (var file in resourceDirectory.GetFiles("*.jpg"))
                    {
                        byte[] bytes = File.ReadAllBytes(file.FullName);
                        List<byte[]> listBytes = new List<byte[]>();
                        if (bytes.Length > messageSize)
                            listBytes = SplitByteArray(bytes, messageSize);
                        else
                            listBytes.Add(bytes);

                        List<SequanceMessage> batchMessages = CreateBatchMessages(listBytes, file.Name);
                        SendMessagesUsingTransactions(queue, batchMessages);
                        file.Delete();
                    }
                });
            }
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

        private MessageQueue GetQueue(string queueName)
        {
            MessageQueue queue;

            if (MessageQueue.Exists(queueName))
                queue = new MessageQueue(queueName);
            else
                queue = MessageQueue.Create(queueName, true);

            queue.Formatter = new XmlMessageFormatter(new Type[] { typeof(SequanceMessage), typeof(string) });
            return queue;
        }



        private List<byte[]> SplitByteArray(byte[] source, int size)
        {
            List<byte[]> result = new List<byte[]>();
            int handledLenght = 0;
            for (int i = 0; i < source.Length; i += size)
            {
                int bufferSize;
                if (source.Length - handledLenght > size)
                    bufferSize = size;
                else
                    bufferSize = source.Length - handledLenght;

                byte[] buffer = new byte[bufferSize];
                Buffer.BlockCopy(source, i, buffer, 0, bufferSize);
                result.Add(buffer);

                handledLenght += size;
            }
            return result;
        }

        private List<SequanceMessage> CreateBatchMessages(List<byte[]> listBytes, string fileName)
        {
            var sequenceId = Guid.NewGuid();
            var position = 0;
            var result = new List<SequanceMessage>();

            foreach (var bytes in listBytes)
            {
                SequanceMessage message = new SequanceMessage
                {
                    Label = fileName,
                    SequanceId = sequenceId,
                    Position = position,
                    Parts = listBytes.Count,
                    Body = bytes
                };
                position++;
                result.Add(message);
            }
            return result;
        }

        private void SendMessagesUsingTransactions(MessageQueue queue, List<SequanceMessage> meassges)
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
    }

    public class SequanceMessage
    {
        public string Label { get; set; }
        public Guid SequanceId { get; set; }
        public int Position { get; set; }
        public int Parts { get; set; }
        public byte[] Body { get; set; }
        public override string ToString()
        {
            return SequanceId + ":" + Position;
        }
    }
}
