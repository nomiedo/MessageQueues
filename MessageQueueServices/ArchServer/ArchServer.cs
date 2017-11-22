using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ServiceHelper;

namespace ArchServer
{
    class ArchServer
    {
        private readonly Timer statusTimer;
        private readonly FileHelper fileHelper;
        private readonly MessagingHelper messagingHelper;

        static object _sync3 = new object();

        public ArchServer()
        {
            statusTimer = new Timer(HandleStatuses);
            fileHelper = new FileHelper();
            messagingHelper = new MessagingHelper();
        }

        public bool Start()
        {
            statusTimer.Change(0, 1000);
            return true;
        }

        public bool Stop()
        {
            lock (_sync3)
            {
                statusTimer.Change(Timeout.Infinite, 0);
                return true;
            }
        }

        private void HandleStatuses(object target)
        {
            lock (_sync3)
            {
                Task.Run(() =>
                {
                    var queueStatus = messagingHelper.GetQueue(ConfigurationManager.AppSettings["QueueStatusName"]);

                    if (queueStatus == null)
                    {
                        Console.WriteLine("ERROR :: Queue for status doesn't exist");
                        return;
                    }

                    try
                    {
                        List<SequanceMessage> messages = messagingHelper.ReceiveMessagesUsingEnumerator(queueStatus);

                        foreach (var message in messages)
                        {
                            Console.WriteLine($"Client {message.ClientId} : Status {message.Label}");
                            if (message.Label.Equals("SENT"))
                            {
                                HandleFiles();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ERROR ::  exception {ex.Message}");
                    }
                });
            }
        }

        private void HandleFiles()
        {
            Console.WriteLine("Server file task runs...");

            var resultFilePath = ConfigurationManager.AppSettings["ResultFilePath"];
            var wrongFolderPath = ConfigurationManager.AppSettings["WrongFolderPath"];
            var resultDirectory = fileHelper.GetDirectoryWithValidation(resultFilePath);
            var wrongDirectory = fileHelper.GetDirectoryWithValidation(wrongFolderPath);

            if (resultDirectory == null)
            {
                Console.WriteLine("ERROR :: Result directory is not availanle");
                return;
            }

            if (wrongFolderPath == null)
            {
                Console.WriteLine("ERROR :: Wrong directory is not availanle");
                return;
            }

            var queue = messagingHelper.GetQueue(ConfigurationManager.AppSettings["QueueName"]);

            if (queue == null)
            {
                Console.WriteLine("ERROR :: Queue for files doesn't exist");
                return;
            }

            List<SequanceMessage> messages = messagingHelper.ReceiveMessagesUsingEnumerator(queue);

            if (messages.Any())
            {
                Console.WriteLine("I am handling file messages");
            }

            foreach (var list in messages.OrderBy(m => m.Position).GroupBy(m => m.SequanceId))
            {
                byte[] fileBytes = fileHelper.CombineGroupOfBytes(list);
                string path = resultFilePath + list.Select(m => m.Label).First();
                fileHelper.AppendAllBytes(path, fileBytes);
            }

            if (resultDirectory.GetFiles("*.jpg").Any())
            {
                fileHelper.Archivation(resultDirectory, wrongDirectory);
                Console.WriteLine("New archive was created");
            }

            Guid clientGuid = messages.Select(m => m.ClientId).First();
            if (clientGuid != Guid.Empty)
            {
                SendNewSettings(clientGuid);
                Console.WriteLine($"New settings was sent to client {clientGuid}");
            }
            else
            {
                Console.WriteLine("ERROR :: Client id is not specified");
            }
        }

        private void SendNewSettings(Guid clientId)
        {
            Console.WriteLine("Send new settings...");

            var queue = messagingHelper.GetQueue(ConfigurationManager.AppSettings["QueueSettings"]);

            if (queue == null)
            {
                Console.WriteLine("ERROR :: Queue for settings doesn't exist");
                return;
            }

            messagingHelper.SendSettings(queue, 10000, clientId);
        }
    }
}