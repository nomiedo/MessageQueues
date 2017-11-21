using ServiceHelper;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    class ArchiveServer
    {
        private readonly Timer timer;
        private readonly FileHelper fileHelper;
        private readonly MessagingHelper messagingHelper;

        static object _sync2 = new object();

        public ArchiveServer()
        {
            timer = new Timer(Handle);
            fileHelper = new FileHelper();
            messagingHelper = new MessagingHelper();
        }

        public bool Start()
        {
            timer.Change(0, 3000);
            return true;
        }

        public bool Stop()
        {
            lock (_sync2)
            {
                timer.Change(Timeout.Infinite, 0);
                return true;
            }
        }

        private void Handle(object target)
        {
            lock (_sync2)
            {
                var resultFilePath = ConfigurationManager.AppSettings["ResultFilePath"];
                var wrongFolderPath = ConfigurationManager.AppSettings["WrongFolderPath"];
                var resultDirectory = fileHelper.GetDirectoryWithValidation(resultFilePath);
                var wrongDirectory = fileHelper.GetDirectoryWithValidation(wrongFolderPath);

                if (resultDirectory == null)
                {
                    Console.WriteLine("Result directory is not availanle");
                    return;
                }

                if (wrongFolderPath == null)
                {
                    Console.WriteLine("Wrong directory is not availanle");
                    return;
                }

                var queue = messagingHelper.GetQueue(ConfigurationManager.AppSettings["QueueName"]);

                if (queue == null)
                {
                    return;
                }

                List<SequanceMessage> messages = messagingHelper.ReceiveMessagesUsingPeek(queue);

                foreach (var list in messages.OrderBy(m => m.Position).GroupBy(m => m.SequanceId))
                {
                    byte[] fileBytes = fileHelper.CombineGroupOfBytes(list);
                    string path = resultFilePath + list.Select(m => m.Label).First();
                    fileHelper.AppendAllBytes(path, fileBytes);
                }

                if (resultDirectory.GetFiles("*.jpg").Any())
                    fileHelper.Archivation(resultDirectory, wrongDirectory);
            }
        }
    }
}