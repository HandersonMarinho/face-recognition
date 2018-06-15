using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Windows.Forms;

namespace MultiFaceRec
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(Bootstrap());
            }
            catch { }
        }

        static FrmPrincipal Bootstrap()
        {
            IConfiguration config = new Configuration(ConfigurationManager.AppSettings);
            IVideoServer video = new VideoServer(config);

            return new FrmPrincipal(video, config);
        }
    }
}
