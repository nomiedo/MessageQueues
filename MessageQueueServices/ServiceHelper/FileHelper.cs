using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace ServiceHelper
{
    public class FileHelper
    {
        private int messageSize = (1024 * 4);
        private Regex re = new Regex(@"(^IMG_[0-9][0-9][0-9].[jpg])\w+");

        public DirectoryInfo GetDirectoryWithValidation(string path)
        {
            DirectoryInfo directory = new DirectoryInfo(path);

            if (!directory.Exists)
            {
                return null;
            }

            return directory;
        }

        public List<byte[]> SplitFileToListOfByteArray(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            List<byte[]> listBytes = new List<byte[]>();

            if (bytes.Length > messageSize)
                listBytes = SplitByteArray(bytes, messageSize);
            else
                listBytes.Add(bytes);

            return listBytes;
        }

        public void Archivation(DirectoryInfo resourceDirectory, DirectoryInfo wrongDirectory)
        {
            int fileIndex = 0;
            List<FileInfo> batch = new List<FileInfo>();

            foreach (var file in resourceDirectory.GetFiles("*.jpg"))
            {
                if (!re.IsMatch(file.Name))
                {
                    try
                    {
                        var tName = wrongDirectory + file.Name;
                        file.MoveTo(tName);
                    }
                    catch
                    {
                        Console.WriteLine("The same file name, file was deleted");
                        DeleteFile(file);
                    }                    
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
                    CreateArchive(batch, resourceDirectory.ToString());
                    batch.Clear();
                }

                batch.Add(file);
                fileIndex = row;
            }
            CreateArchive(batch, resourceDirectory.ToString());
            batch.Clear();
        }

        public void CreateArchive(List<FileInfo> files, string path)
        {
            string zipName = $@"{path}{DateTime.Now:yyyyMMdd_hhmmss_ff}.zip";
            using (ZipArchive newFile = ZipFile.Open(zipName, ZipArchiveMode.Create))
            {
                foreach (var file in files)
                {
                    newFile.CreateEntryFromFile(file.FullName, file.Name);
                }
            }
            foreach (var file in files)
            {
                DeleteFile(file);
            }
        }

        public void DeleteFile(FileInfo file)
        {
            DoSeveralAttempts(() => file.Delete(), new TimeSpan(1000), 5);
        }

        public void AppendAllBytes(string path, byte[] bytes)
        {
            //argument-checking here.
            using (var stream = new FileStream(path, FileMode.Append))
            {
                stream.Write(bytes, 0, bytes.Length);
            }
        }

        public byte[] CombineGroupOfBytes(IGrouping<Guid, SequanceMessage> list)
        {
            List<byte[]> fileBodyArray = new List<byte[]>();
            foreach (var m in list)
            {
                fileBodyArray.Add(m.Body);
            }
            var t = Combine(fileBodyArray.ToArray());
            return t;
        }

        #region Private methods

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

        private void DoSeveralAttempts(
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

        private T Do<T>(
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

        #endregion


    }
}
