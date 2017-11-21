using System;
using System.IO;
using System.Messaging;
using System.Configuration;
using System.Collections.Generic;
using System.Linq;

namespace ClientArchivator
{
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

    class Program
    {
        static void Main(string[] args)
        {
            string queueName = ConfigurationManager.AppSettings["QueueName"];
            string resourceFolderPath = ConfigurationManager.AppSettings["ResourceFilePath"];
            DirectoryInfo resourceDirectory = GetDirectoryWithValidation(resourceFolderPath);
            MessageQueue queue = GetQueue(queueName);
            int messageSize = (1024 * 4);

            
            if (resourceDirectory == null)
                return;

            // send message
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
            }

            // read message
            List<SequanceMessage> messages = ReceiveMessagesUsingPeek(queue);

            foreach (var list in messages.OrderBy(m => m.Position).GroupBy(m => m.SequanceId))
            {
                List<byte[]> fileBodyArray = new List<byte[]>();
                foreach (var m in list)
                {
                    fileBodyArray.Add(m.Body);
                }

                var rv = Combine(fileBodyArray.ToArray());
                string path = resourceDirectory + list.Select(m => m.Label).First();
                AppendAllBytes(path, rv);
            }

            Console.Read();
        }

        private static byte[] Combine(params byte[][] arrays)
        {
            byte[] rv = new byte[arrays.Sum(a => a.Length)];
            int offset = 0;
            foreach (byte[] array in arrays)
            {
                Buffer.BlockCopy(array, 0, rv, offset, array.Length);
                offset += array.Length;
            }
            return rv;
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
                queue = MessageQueue.Create(queueName, true);

            queue.Formatter = new XmlMessageFormatter(new Type[] { typeof(SequanceMessage), typeof(string) });
            return queue;
        }

        private static List<SequanceMessage> CreateBatchMessages(List<byte[]> listBytes, string fileName)
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

        private static void SendMessagesUsingTransactions(MessageQueue queue, List<SequanceMessage> meassges)
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

        private static List<SequanceMessage> ReceiveMessagesUsingPeek(MessageQueue queue)
        {
            List<SequanceMessage> result = new List<SequanceMessage>();

            var asyncReceive = queue.BeginPeek();

            var enumerator = queue.GetMessageEnumerator2();

            while (enumerator.MoveNext())
            {
                if (enumerator.Current != null)
                {
                    var resp = queue.ReceiveById(enumerator.Current.Id);
                    if (resp?.Body is SequanceMessage)
                    {
                        var message = (SequanceMessage)resp.Body;
                        result.Add(message);
                    }
                }
            }

            queue.EndPeek(asyncReceive);

            return result;
           
        }

        private static SequanceMessage ReceiveMessageUsingTransactions(MessageQueue queue)
        {
            using (var trans = new MessageQueueTransaction())
            {
                SequanceMessage responce = null;
                trans.Begin();
                var res = queue.Receive(trans);

                if (res?.Body is SequanceMessage)
                {
                    responce = (SequanceMessage)res.Body;
                }

                trans.Abort();
                return responce;
            }
        }

        public static byte[] GetByteArray(Stream input)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                input.CopyTo(ms);
                return ms.ToArray();
            }
        }

        public static List<byte[]> SplitByteArray(byte[] source, int size)
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

        public static void AppendAllBytes(string path, byte[] bytes)
        {
            //argument-checking here.

            using (var stream = new FileStream(path, FileMode.Append))
            {
                stream.Write(bytes, 0, bytes.Length);
            }
        }
    }
}
