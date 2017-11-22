using System;
using System.Collections.Generic;
using System.Linq;
using System.Messaging;

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

        public List<SequanceMessage> CreateBatchMessages(List<byte[]> listBytes, string fileName)
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

        public List<SequanceMessage> ReceiveMessagesUsingPeek(MessageQueue queue)
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
    }
}
