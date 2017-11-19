using System;
using System.IO;
using System.Threading;
using System.IO.Compression;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections;
using System.Messaging;

namespace ClientArchivator
{
    public class Notification
    {
        Guid Id { get; set; }
        string FilePath { get; set; }
    }

    public class ClientA
    {
        private readonly Timer timer;
        private readonly string resourceFolderPath;
        private readonly string resultFolderPath;
        private readonly string wrongFolderPath;
        private Regex re = new Regex(@"(^IMG_[0-9][0-9][0-9].[jpg])\w+");
        List<FileInfo> batch = new List<FileInfo>();
        Task currentTask;
        const string MessageQueueName = @".\private$\MyObjectQueue";
        MessageQueue queue;

        public ClientA()
        {
            timer = new Timer(Handle);
            resourceFolderPath = System.Configuration.ConfigurationManager.AppSettings["ResourceFilePath"];
        }

        public bool Start()
        {
            timer.Change(0, 30000);

            if (MessageQueue.Exists(MessageQueueName))
                queue = new MessageQueue(MessageQueueName);
            else
                queue = MessageQueue.Create(MessageQueueName);

            queue.Formatter = new XmlMessageFormatter(new Type[] { typeof(Notification), typeof(string) });

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

        static object _sync = new object();

        private void Handle(object target)
        {
            lock (_sync)
            {
                currentTask = Task.Factory.StartNew(() =>
                {
                    int count = Archivation();
                    if (count == -1) { Console.WriteLine(@"Directory is not exist"); }
                    if (count == 0) { Console.WriteLine(@"There are no files to create archive"); }
                });
            }
        }

        private int Archivation()
        {
            DirectoryInfo resourceDirectory = new DirectoryInfo(resourceFolderPath);

            if (!resourceDirectory.Exists)
                return -1;

            if (resourceDirectory.GetFiles("*.jpg").Length < 1)
                return 0;

            int fileIndex = 0;
            foreach (var file in resourceDirectory.GetFiles("*.jpg"))
            {
                lock (_sync)
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

                    }

                    batch.Add(file);
                    fileIndex = row;
                }

            }
            return 1;
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

    }
}
