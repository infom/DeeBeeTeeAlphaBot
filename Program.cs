using System.Reflection;
using Topshelf;

namespace DeeBeeTeeAlphaBot
{

    class Program
    {

        
        static void Main(string[] args)
        {
            var rc = HostFactory.Run(x =>                                   //1
            {
                x.Service<Bot>(s =>                                   //2
                {
                    s.ConstructUsing(name => new Bot());                //3
                    s.WhenStarted(tc => tc.Start());                         //4
                    s.WhenStopped(tc => tc.Stop());                          //5
                });
                x.RunAsLocalSystem();                                       //6

                x.StartAutomatically();

                x.SetDescription("DeeBeeTee Bot for Telegram");                   //7
                x.SetDisplayName("DeeBeeTee Telegram Bot v"+ Assembly.GetExecutingAssembly().GetName().Version.ToString());                                  //8
                x.SetServiceName("DeeBeeTeeTelegramBot");               
                x.EnableServiceRecovery(r =>
                {
                        r.RestartService(0);
                        r.RestartService(0);
                        r.RestartService(0);
                });
            });



        }

       

    }
}
