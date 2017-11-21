using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading;
using ServiceHelper;

namespace ArchClient
{
    class ArchClient
    {
        private readonly Timer timer;
        private readonly FileHelper fileHelper;
        private readonly MessagingHelper messagingHelper;

        static object _sync = new object();

        public ArchClient()
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
            lock (_sync)
            {
                timer.Change(Timeout.Infinite, 0);
                return true;
            }
        }

        private void Handle(object target)
        {
            lock (_sync)
            {
                var resourceDirectory =
                    fileHelper.GetDirectoryWithValidation(ConfigurationManager.AppSettings["ResourceFilePath"]);

                if (resourceDirectory == null)
                {
                    Console.WriteLine("Directory is not availanle");
                    return;
                }
                if (resourceDirectory.GetFiles("*.jpg").Length < 0)
                {
                    Console.WriteLine("Directory doesn contains files");
                    return;
                }

                var queue = messagingHelper.GetQueue(ConfigurationManager.AppSettings["QueueName"]);

                if (queue == null)
                {
                    return;
                }

                foreach (var file in resourceDirectory.GetFiles("*.jpg"))
                {
                    List<byte[]> listBytes = fileHelper.SplitFileToListOfByteArray(file.FullName);
                    List<SequanceMessage> batchMessages = messagingHelper.CreateBatchMessages(listBytes, file.Name);
                    messagingHelper.SendMessagesUsingTransactions(queue, batchMessages);
                    file.Delete();
                }
            }
        }
    }
}