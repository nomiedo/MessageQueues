using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ServiceHelper;

namespace ArchClient
{
    class ArchClient
    {
        private readonly Timer timer;
        private readonly FileHelper fileHelper;
        private readonly MessagingHelper messagingHelper;
        private readonly Guid clientId;


        private int TimerTimeout { get; set; }

        static object _sync = new object();

        public ArchClient()
        {
            timer = new Timer(Handle);
            fileHelper = new FileHelper();
            messagingHelper = new MessagingHelper();
            clientId = Guid.NewGuid();
            TimerTimeout = 3000;
        }

        public bool Start()
        {
            timer.Change(0, TimerTimeout);
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
                Task.Run(() =>
                {
                    Console.WriteLine("Client task runs...");
                    
                    var queue = messagingHelper.GetQueue(ConfigurationManager.AppSettings["QueueName"]);

                    if (queue == null)
                    {
                        Console.WriteLine("ERROR :: Queue for files doesn't exist");
                        return;
                    }

                    var queueStatus = messagingHelper.GetQueue(ConfigurationManager.AppSettings["QueueStatusName"]);

                    if (queueStatus == null)
                    {
                        Console.WriteLine("ERROR :: Queue for status doesn't exist");
                        return;
                    }

                    var resourceDirectory =
                        fileHelper.GetDirectoryWithValidation(ConfigurationManager.AppSettings["ResourceFilePath"]);

                    if (resourceDirectory == null)
                    {
                        Console.WriteLine("ERROR :: Directory is not availanle");
                        return;
                    }
                    if (resourceDirectory.GetFiles("*.jpg").Length < 1)
                    {
                        messagingHelper.SendStatus(queueStatus,"Wait new files", clientId);
                        Console.WriteLine("Wait new files");
                        return;
                    }

                    try
                    {
                        messagingHelper.SendStatus(queueStatus, "I am handling files", clientId);
                        Console.WriteLine("I am handling files");
                        foreach (var file in resourceDirectory.GetFiles("*.jpg"))
                        {
                            List<byte[]> listBytes = fileHelper.SplitFileToListOfByteArray(file.FullName);
                            List<SequanceMessage> batchMessages = messagingHelper.CreateBatchFileMessages(listBytes, file.Name, clientId);
                            messagingHelper.SendMessagesUsingTransactions(queue, batchMessages);
                            fileHelper.DeleteFile(file);
                        }
                        messagingHelper.SendStatus(queueStatus, "SENT", clientId);
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine($"ERROR :: exception {ex.Message}");
                    }

                    Console.WriteLine($"Files were sent to {queue.QueueName}");

                    GetNewSettings();
                });
            }
        }

        private void GetNewSettings()
        {
            Console.WriteLine("Server file task runs...");

            var queue = messagingHelper.GetQueue(ConfigurationManager.AppSettings["QueueSettings"]);

            if (queue == null)
            {
                Console.WriteLine("ERROR :: Queue for files doesn't exist");
                return;
            }

            var messages = messagingHelper.ReceiveMessagesUsingPeek(queue, clientId);
            var newParam = messages.Select(m => m.SettingValue).Last();

            TimerTimeout = newParam;
            timer.Change(0, TimerTimeout);

            Console.WriteLine("Settings was changedt");
            messagingHelper.SendStatus(queue, "Settings was changed", clientId);
        }
    }
}