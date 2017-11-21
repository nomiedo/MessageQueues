using Topshelf;

namespace Server
{
    class Program
    {
        static void Main(string[] args)
        {
            HostFactory.Run(x =>
            {
                x.Service<ArchiveServer>(conf =>
                {
                    conf.ConstructUsing(() => new ArchiveServer());
                    conf.WhenStarted(s => s.Start());
                    conf.WhenStopped(s => s.Stop());
                });
                x.StartAutomaticallyDelayed();
                x.RunAsLocalService();
                x.EnableServiceRecovery(r => r.RestartService(0).RestartService(1));
            });
        }
    }
}
