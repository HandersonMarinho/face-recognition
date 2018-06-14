using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MultiFaceRec
{
    public partial class FrmPrincipal : Form
    {
        private Image<Bgr, byte> CurrentFrame { get; set; }
        private Capture Grabber { get; set; }
        private HaarCascade Face { get; set; }
        private MCvFont FontRender { get; set; }
        private Image<Gray, byte> Result { get; set; }
        private Image<Gray, byte> TrainedFace { get; set; }
        private Image<Gray, byte> Gray { get; set; }
        private List<Image<Gray, byte>> TrainingImages { get; set; }
        private List<string> LabelList { get; set; }
        private int TrainedFacesCounter { get; set; }

        public FrmPrincipal()
        {
            InitializeComponent();
            lblLabelName.Text = string.Empty;
            FontRender = new MCvFont(FONT.CV_FONT_HERSHEY_TRIPLEX, 0.5d, 0.5d);
            TrainingImages = new List<Image<Gray, byte>>();
            LabelList = new List<string>();
            LoadTrainnedFace();
        }

        private void Detect_Click(object sender, EventArgs e)
        {
            // Initialize the capture device
            Grabber = new Capture();
            Grabber.QueryFrame();

            Task.Factory.StartNew(() =>
            {
                var timer = new System.Timers.Timer();
                timer.Elapsed += FaceMonitoring;
                timer.Interval = 200;
                timer.Enabled = true;
            }, CancellationToken.None, TaskCreationOptions.None,
               TaskScheduler.FromCurrentSynchronizationContext());

            Detect.Enabled = false;
        }

        [HandleProcessCorruptedStateExceptions]
        private void AddFace_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(textBox1.Text))
            {
                MessageBox.Show("Please inform a name.");
                return;
            }

            AddFace.Enabled = false;

            progressBar1.Maximum = 120;
            progressBar1.Step = 1;
            progressBar1.Value = 0;

            int progress = 7;
            var bw = new BackgroundWorker();
            bw.WorkerReportsProgress = true;
            bw.ProgressChanged += (pa, pb) =>
            {
                progress += 7;
                progressBar1.Value = progress;
            };
            bw.DoWork += (s, ev) =>
            {
                for (int i = 0; i < 15; i++)
                {                    
                    bw.ReportProgress(7);
                    SavePicture(null, null);
                    Task.Delay(200).Wait();
                }
            };
            bw.RunWorkerCompleted += (s1, ev1) =>
            {
                MethodInvoker inv2 = delegate { lblLabelName.Text = string.Empty; };
                Invoke(inv2);
                progressBar1.Value = 0;
                LoadTrainnedFace();
            };
            bw.RunWorkerAsync();
            AddFace.Enabled = true;
        }

        [HandleProcessCorruptedStateExceptions]
        private void SavePicture(object sender, EventArgs e)
        {
            try
            {
                TrainedFacesCounter++;

                // Get a gray frame from capture device
                Gray = Grabber.QueryGrayFrame().Resize(320, 240, INTER.CV_INTER_CUBIC);

                // Face Detector
                MCvAvgComp[][] facesDetected = Gray.DetectHaarCascade(Face, 1.2, 10, HAAR_DETECTION_TYPE.DO_CANNY_PRUNING, new Size(20, 20));

                // Action for each element detected
                foreach (MCvAvgComp f in facesDetected[0])
                {
                    TrainedFace = CurrentFrame.Copy(f.rect).Convert<Gray, byte>();
                    break;
                }

                string labelName = textBox1.Text.Trim().Replace(" ", "_").ToLower();

                // Resize face detected image in order to force to compare the same size with the 
                // Test image with cubic interpolation type method
                TrainedFace = Result.Resize(100, 100, INTER.CV_INTER_CUBIC);
                //TrainingImages.Add(TrainedFace);
                //LabelList.Add(labelName);

                // Show face added in gray scale
                imageBox1.Image = TrainedFace;

                // Write the labels of triained faces in a file text for further load
                //for (int i = 1; i < TrainingImages.ToArray().Length + 1; i++)
                //{
                //    var imgPath = $"/TrainedFaces/{labelName}@face{i}.bmp";
                //    TrainingImages.ToArray()[i - 1].Save(Application.StartupPath + imgPath);
                //}
                var imgPath = $"/TrainedFaces/{labelName}@face{DateTime.Now.ToString("yyyymmddhhmmssmm")}.bmp";
                TrainedFace.Save(Application.StartupPath + imgPath);
            }
            catch { }
        }

        [HandleProcessCorruptedStateExceptions]
        private void FaceMonitoring(object sender, EventArgs e)
        {
            try
            {
                // Get the current frame form capture device
                CurrentFrame = Grabber.QueryFrame().Resize(320, 240, INTER.CV_INTER_CUBIC);

                // Convert it to Grayscale
                Gray = CurrentFrame.Convert<Gray, byte>();

                // Face Detector
                MCvAvgComp[][] facesDetected = Gray.DetectHaarCascade(Face, 1.2, 10, HAAR_DETECTION_TYPE.DO_CANNY_PRUNING, new Size(20, 20));

                if (facesDetected == null)
                    return;

                // Action for each element detected
                foreach (MCvAvgComp face in facesDetected[0])
                {
                    Result = CurrentFrame.Copy(face.rect).Convert<Gray, byte>().Resize(100, 100, INTER.CV_INTER_CUBIC);

                    // Draw the face detected in the 0th (gray) channel with blue color
                    CurrentFrame.Draw(face.rect, new Bgr(Color.Red), 2);

                    // Check if there is trained images to find a match
                    if (TrainingImages.ToArray().Length != 0)
                    {
                        // TermCriteria for face recognition with numbers of trained images like maxIteration
                        MCvTermCriteria termCrit = new MCvTermCriteria(TrainedFacesCounter, 0.001);

                        // Eigen face recognizer
                        FaceRecognitionEngine recognizer = new FaceRecognitionEngine(TrainingImages.ToArray(), LabelList.ToArray(), 3000, ref termCrit);
                        var imgLabel = recognizer.Recognize(Result);

                        // Draw the label for each face detected and recognized
                        // CurrentFrame.Draw(imgLabel, ref Font, new Point(face.rect.X - 2, face.rect.Y - 2), new Bgr(Color.White));                    
                        MethodInvoker inv = delegate { lblLabelName.Text = imgLabel.Replace("_", " ").ToUpper(); };
                        Invoke(inv);
                    }
                }

                // Show the face procesed and recognized
                imageBoxFrameGrabber.Image = CurrentFrame;
            }
            catch
            {
            }
        }

        private void LoadTrainnedFace()
        {
            try
            {
                LabelList.Clear();
                TrainingImages.Clear();

                // Load haarcascades for face detection
                Face = new HaarCascade("haarcascade_frontalface_default.xml");

                // Load the number of trained faces
                var files = GetAllFiles(Application.StartupPath + "/TrainedFaces");
                TrainedFacesCounter = files.Count;

                foreach (var file in files)
                {
                    TrainingImages.Add(new Image<Gray, byte>(file.FullName));
                    LabelList.Add(file.Name.Split('@')[0]);
                }
            }
            catch
            {
                // By pass
            }
        }

        public static List<FileInfo> GetAllFiles(string rootPath)
        {
            List<FileInfo> listFiles = new List<FileInfo>();
            DirectoryInfo Dir = new DirectoryInfo(rootPath);
            foreach (FileInfo File in Dir.GetFiles("*", SearchOption.AllDirectories))
            {
                listFiles.Add(File);
            }
            return listFiles;
        }
    }
}