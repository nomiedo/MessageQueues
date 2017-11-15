using System;
using System.IO;
using System.Threading;
using System.IO.Compression;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WSFilesArchivator
{
    public class ArchiveService
    {
        private readonly Timer timer;  
        private readonly string resourceFolderPath;
        private readonly string resultFolderPath;
        private readonly string wrongFolderPath;
        private Regex re = new Regex(@"(^IMG_[0-9][0-9][0-9].[jpg])\w+");
        List<FileInfo> batch = new List<FileInfo>();
        Task currentTask;

        public ArchiveService()
        {
            timer = new Timer(Handle);
            resourceFolderPath = System.Configuration.ConfigurationManager.AppSettings["ResourceFilePath"];
            resultFolderPath = System.Configuration.ConfigurationManager.AppSettings["ResultFilePath"];
            wrongFolderPath = System.Configuration.ConfigurationManager.AppSettings["WrongFilePath"];
        }

        public bool Start()
        {
            timer.Change(0, 30000);
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
                    if (count > 0) { CreateArchive(batch); }
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
            return 1;
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
}
