using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MultiFaceRec
{
    public interface IVideoServer
    {
        void Start(Action<string, Exception> onConnect);
        void Stop();
        void Serve(byte[] frameBytes);
    }

    public class VideoServer : IVideoServer
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public VideoServer(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        /// <summary>
        /// Http service lister used to serve images.
        /// </summary>
        private HttpListener Listener { get; set; }

        /// <summary>
        /// Place holder used to frame the photo.
        /// </summary>
        private byte[] PhotoFrame { get; set; }

        /// <summary>
        /// USed to check is is the first time the video server is being executed.
        /// </summary>
        private bool IsFirstFrame = false;

        /// <summary>
        /// Http server context.
        /// </summary>
        private HttpListenerContext ActiveContext { get; set; }

        /// <summary>
        /// Component configuration
        /// </summary>
        private IConfiguration Configuration { get; set; }

        /// <summary>
        /// Start video server
        /// </summary>
        /// <param name="videoUrl">Url that will exposed the http video server endpoint.</param>
        /// <param name="onConnect">Callback when video server is fully connected.</param>
        public void Start(Action<string, Exception> onConnect)
        {
            try
            {
                if (string.IsNullOrEmpty(Configuration.VideoUrl))
                    throw new ArgumentException($"{nameof(Configuration.VideoUrl)} is mandatory.");

                if (Configuration.VideoUrl.StartsWith("http") == false)
                    throw new ArgumentException($"{nameof(Configuration.VideoUrl)} should start with http or https.");

                if (onConnect == null)
                    throw new ArgumentException($"{nameof(onConnect)} is mandatory.");

                if (!Configuration.VideoUrl.EndsWith("/"))
                    Configuration.VideoUrl = $"{Configuration.VideoUrl}/";
                
                InitializeListener(Configuration.VideoUrl);
                onConnect?.Invoke("ConnectOk", null);
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                onConnect?.Invoke("ConnectFail", ex);
            }
        }

        /// <summary>
        /// Stops video streaming.
        /// </summary>
        public void Stop()
        {
            try
            {
                if (Listener != null)
                {
                    Listener.Stop();
                    Listener = null;

                    // Delay to avoid a new flight request during video service shutdown.
                    Task.Delay(5000).Wait();
                }
            }
            catch (Exception ex)
            {
                Listener = null;
            }
        }

        /// <summary>
        /// Initialize internal server instances.
        /// </summary>
        /// <param name="videoUrl"></param>
        private void InitializeListener(string videoUrl)
        {
            IsFirstFrame = true;
            PhotoFrame = Encoding.UTF8.GetBytes($"--frameBoundary{Environment.NewLine}{Environment.NewLine}");
            Listener = new HttpListener();
            Listener.Prefixes.Clear();
            Listener.Prefixes.Add(videoUrl);
            Listener.Start();
        }

        /// <summary>
        /// Send a new image frame to the server.
        /// </summary>
        /// <param name="frameBytes"></param>
        public void Serve(byte[] frameBytes)
        {
            try
            {
                if (Listener.IsListening == false)
                    return;

                if (IsFirstFrame)
                {
                    IsFirstFrame = false;
                    ThreadPool.QueueUserWorkItem((c) =>
                    {
                        var ctx = c as HttpListenerContext;
                        try
                        {
                            ctx.Response.KeepAlive = true;
                            ctx.Response.ContentType = "multipart/x-mixed-replace; boundary=frameBoundary";
                            IsFirstFrame = false;
                            ActiveContext = ctx;
                        }
                        catch(Exception ex) { System.Diagnostics.Debug.Print(ex.Message); }
                    }, Listener.GetContext());
                }
                else
                {
                    ServeFrame(ActiveContext, frameBytes);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Print(ex.Message);
            }
        }

        /// <summary>
        /// Send a new image frame to the server.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="frameBytes"></param>
        private void ServeFrame(HttpListenerContext context, byte[] frameBytes)
        {
            try
            {
                context.Response.OutputStream.Write(PhotoFrame, 0, PhotoFrame.Length);
                context.Response.OutputStream.Write(frameBytes, 0, frameBytes.Length);
            }
            catch
            {
                // client disconnect
                context.Response.OutputStream.Close();
                IsFirstFrame = true;
            }
        }
    }
}
