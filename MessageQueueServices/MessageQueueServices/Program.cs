using Topshelf;

namespace ArchClient
{
    class Program
    {
        static void Main(string[] args)
        {
            HostFactory.Run(x =>
            {
                x.Service<ArchClient>(conf =>
                {
                    conf.ConstructUsing(() => new ArchClient());
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
