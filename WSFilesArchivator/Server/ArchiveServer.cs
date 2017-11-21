using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Messaging;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    class ArchiveServer
    {
        private readonly Timer timer;
        private readonly string queueName;
        private readonly string resultFolderPath;
        private readonly string wrongFolderPath;
        private Regex re = new Regex(@"(^IMG_[0-9][0-9][0-9].[jpg])\w+");
        static object _sync = new object();
        Task currentTask;

        private DirectoryInfo resultDirectory;
        private MessageQueue queue;

        public ArchiveServer()
        {
            timer = new Timer(Handle);
            queueName = ConfigurationManager.AppSettings["QueueName"];
            queue = GetQueue(queueName);
            resultFolderPath = ConfigurationManager.AppSettings["ResultFilePath"];
            wrongFolderPath = ConfigurationManager.AppSettings["WrongFolderPath"];
            resultDirectory = GetDirectoryWithValidation(resultFolderPath);

            if (resultDirectory == null)
                Console.WriteLine("Directory is not availanle");
        }

        public bool Start()
        {
            timer.Change(0, 3000);
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
                    List<SequanceMessage> messages = ReceiveMessagesUsingPeek(queue);

                    foreach (var list in messages.OrderBy(m => m.Position).GroupBy(m => m.SequanceId))
                    {
                        List<byte[]> fileBodyArray = new List<byte[]>();
                        foreach (var m in list)
                        {
                            fileBodyArray.Add(m.Body);
                        }

                        var rv = Combine(fileBodyArray.ToArray());
                        string path = resultDirectory + list.Select(m => m.Label).First();
                        AppendAllBytes(path, rv);
                        Archivation();
                    }
                });
            }
        }

        private void Archivation()
        {
            int fileIndex = 0;
            List<FileInfo> batch = new List<FileInfo>();

            foreach (var file in resultDirectory.GetFiles("*.jpg"))
            {
                if (!re.IsMatch(file.Name))
                {
                    file.MoveTo(wrongFolderPath + file.Name);
                    continue;
                }

                // remove extension from the name
                string name = file.Name.Substring(0, file.Name.Length - 4);
                // get a number
                int row, a = getIndexofNumber(name);
                string number = file.Name.Substring(a, name.Length - a);
                row = Convert.ToInt32(number);

                if (row - fileIndex != 1 && fileIndex > 0)
                {
                    CreateArchive(batch);
                    batch.Clear();
                }

                batch.Add(file);
                fileIndex = row;
            }
        }

        private DirectoryInfo GetDirectoryWithValidation(string path)
        {
            DirectoryInfo directory = new DirectoryInfo(path);

            if (!directory.Exists)
            {
                Console.WriteLine(@"Directory is not exist");
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
            queue.Purge();

            return result;
        }

        private byte[] Combine(params byte[][] arrays)
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

        private void AppendAllBytes(string path, byte[] bytes)
        {
            //argument-checking here.

            using (var stream = new FileStream(path, FileMode.Append))
            {
                stream.Write(bytes, 0, bytes.Length);
            }
        }

        private int getIndexofNumber(string cell)
        {
            int indexofNum = -1;
            foreach (char c in cell)
            {
                indexofNum++;
                if (Char.IsDigit(c))
                {
                    return indexofNum;
                }
            }
            return indexofNum;
        }

        private void CreateArchive(List<FileInfo> files)
        {
            string zipName = $@"{resultFolderPath}{DateTime.Now:yyyyMMdd_hhmmss_ff}.zip";
            using (ZipArchive newFile = ZipFile.Open(zipName, ZipArchiveMode.Create))
            {
                foreach (var file in files)
                {
                    Thread.Sleep(1000);
                    newFile.CreateEntryFromFile(file.FullName, file.Name);
                }
            }
            foreach (var file in files)
            {
                DoSeveralAttempts(() => file.Delete(), new TimeSpan(1000), 5);
            }
        }

        public static void DoSeveralAttempts(
            Action action,
            TimeSpan retryInterval,
            int maxAttemptCount = 3)
        {
            Do<object>(() =>
            {
                action();
                return null;
            }, retryInterval, maxAttemptCount);
        }

        public static T Do<T>(
            Func<T> action,
            TimeSpan retryInterval,
            int maxAttemptCount = 3)
        {
            var exceptions = new List<Exception>();

            for (int attempted = 0; attempted < maxAttemptCount; attempted++)
            {
                try
                {
                    if (attempted > 0)
                    {
                        Thread.Sleep(retryInterval);
                    }
                    return action();
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }
            throw new AggregateException(exceptions);
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
