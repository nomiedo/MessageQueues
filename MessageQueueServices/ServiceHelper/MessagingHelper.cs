using System;
using System.Collections.Generic;
using System.Messaging;
using System.Threading;

namespace ServiceHelper
{
    public class MessagingHelper
    {
        public MessageQueue GetQueue(string queueName)
        {
            MessageQueue queue;

            if (MessageQueue.Exists(queueName))
                queue = new MessageQueue(queueName);
            else
                queue = MessageQueue.Create(queueName, true);

            queue.Formatter = new XmlMessageFormatter(new Type[] { typeof(SequanceMessage), typeof(string) });
            return queue;
        }

        public List<SequanceMessage> CreateBatchFileMessages(List<byte[]> listBytes, string fileName, Guid clientId)
        {
            var sequenceId = Guid.NewGuid();
            var position = 0;
            var result = new List<SequanceMessage>();

            foreach (var bytes in listBytes)
            {
                SequanceMessage message = new SequanceMessage
                {
                    ClientId = clientId,
                    Label = fileName,
                    SequanceId = sequenceId,
                    MessageType = MessageType.File,
                    Position = position,
                    Body = bytes
                };
                position++;
                result.Add(message);
            }
            return result;
        }

        public void SendStatus(MessageQueue queue, string status, Guid clientId)
        {
            SequanceMessage message = new SequanceMessage
            {
                ClientId = clientId,
                Label = status,
                MessageType = MessageType.ClientStatus,
            };
            SendMessagesUsingTransactions(queue, new List<SequanceMessage> { message });
        }

        public void SendSettings(MessageQueue queue, int settingValue, Guid clientId)
        {
            SequanceMessage message = new SequanceMessage
            {
                ClientId = clientId,
                SettingValue = settingValue,
                MessageType = MessageType.Setting
            };
            SendMessagesUsingTransactions(queue, new List<SequanceMessage> { message });
        }

        public void SendMessagesUsingTransactions(MessageQueue queue, List<SequanceMessage> meassges)
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

        public List<SequanceMessage> ReceiveMessagesUsingEnumerator(MessageQueue queue)
        {
            List<SequanceMessage> result = new List<SequanceMessage>();

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

            return result;
        }

        public List<SequanceMessage> ReceiveMessagesUsingPeek(MessageQueue queue, Guid clientId)
        {
            List<SequanceMessage> result = new List<SequanceMessage>();

            var responces = queue.GetAllMessages();
            foreach (var message in responces)
            {
                if (message?.Body is SequanceMessage)
                {
                    var tempMessage = (SequanceMessage)message.Body;
                    if (tempMessage.ClientId != clientId) continue;
                    var rMessage = queue.ReceiveById(message.Id);
                    if (rMessage != null) result.Add((SequanceMessage) rMessage.Body);
                }
            }

            return result;
        }
    }
}
