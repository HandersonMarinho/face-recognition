using System.Collections.Specialized;

namespace MultiFaceRec
{
    public interface IConfiguration
    {
        string VideoUrl { get; set; }
    }

    public class Configuration : IConfiguration
    {
        public Configuration(NameValueCollection configKeys)
        {
            VideoUrl = configKeys["VideoUrl"];
        }

        public string VideoUrl { get; set; }
    }
}
