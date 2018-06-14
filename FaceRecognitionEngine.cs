using Emgu.CV.Structure;
using System;
using System.Diagnostics;

namespace Emgu.CV
{
    /// <summary>
    /// An object recognizer using PCA (Principle Components Analysis)
    /// https://www.codeproject.com/Articles/239849/Multiple-face-detection-and-recognition-in-real
    /// </summary>
    [Serializable]
    public class FaceRecognitionEngine
    {
        /// <summary>
        /// Constructor
        /// </summary>
        private FaceRecognitionEngine()
        {
        }

        /// <summary>
        /// Create an object recognizer using the specific tranning data and parameters, it will always return the most similar object
        /// </summary>
        /// <param name="images">The images used for training, each of them should be the same size. It's recommended the images are histogram normalized</param>
        /// <param name="termCrit">The criteria for recognizer training</param>
        public FaceRecognitionEngine(Image<Gray, byte>[] images, ref MCvTermCriteria termCrit)
           : this(images, GenerateLabels(images.Length), ref termCrit)
        {
        }

        /// <summary>
        /// Create an object recognizer using the specific tranning data and parameters, it will always return the most similar object
        /// </summary>
        /// <param name="images">The images used for training, each of them should be the same size. It's recommended the images are histogram normalized</param>
        /// <param name="labels">The labels corresponding to the images</param>
        /// <param name="termCrit">The criteria for recognizer training</param>
        public FaceRecognitionEngine(Image<Gray, byte>[] images, string[] labels, ref MCvTermCriteria termCrit)
           : this(images, labels, 0, ref termCrit)
        {
        }

        /// <summary>
        /// Create an object recognizer using the specific tranning data and parameters
        /// </summary>
        /// <param name="images">The images used for training, each of them should be the same size. It's recommended the images are histogram normalized</param>
        /// <param name="labels">The labels corresponding to the images</param>
        /// <param name="eigenDistanceThreshold">
        /// The eigen distance threshold, (0, ~1000].
        /// The smaller the number, the more likely an examined image will be treated as unrecognized object. 
        /// If the threshold is &lt; 0, the recognizer will always treated the examined image as one of the known object. 
        /// </param>
        /// <param name="termCrit">The criteria for recognizer training</param>
        public FaceRecognitionEngine(Image<Gray, byte>[] images, string[] labels, double eigenDistanceThreshold, ref MCvTermCriteria termCrit)
        {
            Debug.Assert(images.Length == labels.Length, "The number of images should equals the number of labels");
            Debug.Assert(eigenDistanceThreshold >= 0.0, "Eigen-distance threshold should always >= 0.0");

            CalcEigenObjects(images, ref termCrit, out EigenImages, out AverageImage);
            EigenValues = Array.ConvertAll(images, (Image<Gray, byte> img) => new Matrix<float>(EigenDecomposite(img, EigenImages, AverageImage)));
            Labels = labels;
            EigenDistanceThreshold = eigenDistanceThreshold;
        }

        /// <summary>
        /// Get the eigen vectors that form the eigen space
        /// </summary>
        /// <remarks>The set method is primary used for deserialization, do not attemps to set it unless you know what you are doing</remarks>
        private Image<Gray, float>[] EigenImages;

        /// <summary>
        /// Get the average Image. 
        /// </summary>
        /// <remarks>The set method is primary used for deserialization, do not attemps to set it unless you know what you are doing</remarks>
        private Image<Gray, float> AverageImage;

        /// <summary>
        /// Get the eigen values of each of the training image
        /// </summary>
        /// <remarks>The set method is primary used for deserialization, do not attemps to set it unless you know what you are doing</remarks>
        private Matrix<float>[] EigenValues;

        /// <summary>
        /// Get or set the labels for the corresponding training image
        /// </summary>
        private string[] Labels;

        /// <summary>
        /// Get or set the eigen distance threshold.
        /// The smaller the number, the more likely an examined image will be treated as unrecognized object. 
        /// Set it to a huge number (e.g. 5000) and the recognizer will always treated the examined image as one of the known object. 
        /// </summary>
        private double EigenDistanceThreshold;

        /// <summary>
        /// Set label data
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        private static string[] GenerateLabels(int size)
        {
            string[] labels = new string[size];
            for (int i = 0; i < size; i++)
                labels[i] = i.ToString();
            return labels;
        }

        /// <summary>
        /// Caculate the eigen images for the specific traning image
        /// </summary>
        /// <param name="trainingImages">The images used for training </param>
        /// <param name="termCrit">The criteria for tranning</param>
        /// <param name="eigenImages">The resulting eigen images</param>
        /// <param name="avg">The resulting average image</param>
        public static void CalcEigenObjects(Image<Gray, byte>[] trainingImages, ref MCvTermCriteria termCrit, out Image<Gray, float>[] eigenImages, out Image<Gray, float> avg)
        {
            int width = trainingImages[0].Width;
            int height = trainingImages[0].Height;

            IntPtr[] inObjs = Array.ConvertAll(trainingImages, (Image<Gray, byte> img) => img.Ptr);

            if (termCrit.max_iter <= 0 || termCrit.max_iter > trainingImages.Length)
                termCrit.max_iter = trainingImages.Length;

            int maxEigenObjs = termCrit.max_iter;

            // Initialize eigen images
            eigenImages = new Image<Gray, float>[maxEigenObjs];
            for (int i = 0; i < eigenImages.Length; i++)
                eigenImages[i] = new Image<Gray, float>(width, height);
            IntPtr[] eigObjs = Array.ConvertAll(eigenImages, (Image<Gray, float> img) => img.Ptr);

            avg = new Image<Gray, float>(width, height);

            CvInvoke.cvCalcEigenObjects(inObjs, ref termCrit, eigObjs, null, avg.Ptr);
        }

        /// <summary>
        /// Decompose the image as eigen values, using the specific eigen vectors
        /// </summary>
        /// <param name="src">The image to be decomposed</param>
        /// <param name="eigenImages">The eigen images</param>
        /// <param name="avg">The average images</param>
        /// <returns>Eigen values of the decomposed image</returns>
        public static float[] EigenDecomposite(Image<Gray, byte> src, Image<Gray, float>[] eigenImages, Image<Gray, float> avg)
        {
            return CvInvoke.cvEigenDecomposite(src.Ptr, Array.ConvertAll(eigenImages, (Image<Gray, float> img) => img.Ptr), avg.Ptr);
        }

        /// <summary>
        /// Given the eigen value, reconstruct the projected image
        /// </summary>
        /// <param name="eigenValue">The eigen values</param>
        /// <returns>The projected image</returns>
        public Image<Gray, byte> EigenProjection(float[] eigenValue)
        {
            Image<Gray, byte> res = new Image<Gray, byte>(AverageImage.Width, AverageImage.Height);
            var inputVecs = Array.ConvertAll(EigenImages, (Image<Gray, float> img) => img.Ptr);
            CvInvoke.cvEigenProjection(inputVecs, eigenValue, AverageImage.Ptr, res.Ptr);
            return res;
        }

        /// <summary>
        /// Get the Euclidean eigen-distance between <paramref name="image"/> and every other image in the database
        /// </summary>
        /// <param name="image">The image to be compared from the training images</param>
        /// <returns>An array of eigen distance from every image in the training images</returns>
        public float[] GetEigenDistances(Image<Gray, byte> image)
        {
            using (Matrix<float> eigenValue = new Matrix<float>(EigenDecomposite(image, EigenImages, AverageImage)))
                return Array.ConvertAll(EigenValues, (Matrix<float> eigenValueI) => (float)CvInvoke.cvNorm(eigenValue.Ptr, eigenValueI.Ptr, CvEnum.NORM_TYPE.CV_L2, IntPtr.Zero));
        }

        /// <summary>
        /// Given the <paramref name="image"/> to be examined, find in the database the most similar object, return the index and the eigen distance
        /// </summary>
        /// <param name="image">The image to be searched from the database</param>
        /// <param name="index">The index of the most similar object</param>
        /// <param name="eigenDistance">The eigen distance of the most similar object</param>
        /// <param name="label">The label of the specific image</param>
        public void FindMostSimilarObject(Image<Gray, byte> image, out int index, out float eigenDistance, out string label)
        {
            float[] dist = GetEigenDistances(image);

            index = 0;
            eigenDistance = dist[0];
            for (int i = 1; i < dist.Length; i++)
            {
                if (dist[i] < eigenDistance)
                {
                    index = i;
                    eigenDistance = dist[i];
                }
            }
            label = Labels[index];
        }

        /// <summary>
        /// Try to recognize the image and return its label
        /// </summary>
        /// <param name="image">The image to be recognized</param>
        /// <returns>
        /// String.Empty, if not recognized;
        /// Label of the corresponding image, otherwise
        /// </returns>
        public string Recognize(Image<Gray, byte> image)
        {
            int index;
            float eigenDistance;
            string label;
            FindMostSimilarObject(image, out index, out eigenDistance, out label);

            return (EigenDistanceThreshold <= 0 || eigenDistance < EigenDistanceThreshold) ? Labels[index] : string.Empty;
        }
    }
}
