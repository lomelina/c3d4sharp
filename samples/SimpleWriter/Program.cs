using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vub.Etro.IO;

namespace SimpleWriter
{
    public enum SkeletonMarkers // | Kinect | OpenNI | IISU | Vicon |
    {                           // +---+---+---+---+
        HipCenter = 0,          // | X | - | X | X | 
        Spine = 1,              // | X | X | X | X | 
        ShoulderCenter = 2,     // | X | X | X | X | 
        Head = 3,               // | X | X | X | X | 
        ShoulderLeft = 4,       // | X | X | X | X |  
        ElbowLeft = 5,          // | X | X | X | X | 
        WristLeft = 6,          // | X | - | - | X |  
        HandLeft = 7,           // | X | X | X | X | 
        ShoulderRight = 8,      // | X | X | X | X | 
        ElbowRight = 9,         // | X | X | X | X | 
        WristRight = 10,        // | X | - | - | X | 
        HandRight = 11,         // | X | X | X | X | 
        HipLeft = 12,           // | X | X | X | X |
        KneeLeft = 13,          // | X | X | X | X |
        AnkleLeft = 14,         // | X | - | - | X |
        FootLeft = 15,          // | X | X | X | X |
        HipRight = 16,          // | X | X | X | X |
        KneeRight = 17,         // | X | X | X | X |
        AnkleRight = 18,        // | X | - | - | X |
        FootRight = 19,         // | X | X | X | X |
        Sternum = 20,           // | - | - | X | X |
        Count = 21,             // +---+---+---+---+
    }

    internal static class ArrayCopyHelper
    {
        public static T[] SubArray<T>(this T[] data, int index, int length)
        {
            T[] result = new T[length];
            Array.Copy(data, index, result, 0, length);
            return result;
        }
    }

    public class Program
    {
        //
        //  Let's declare data variables that we'll use to write data to a file
        //
        static Int16[] analogData = null;
        static Vector3[] pointData = null;

        static void Main(string[] args)
        {
            //
            //  Create C3dWriter with events enabled
            //
            C3dWriter writer = new C3dWriter(true);
            
            
            // fill custom parameters in the C3D file
            writer.SetParameter<string[]>("POINT:DATA_TYPE_LABELS", new string[] {
               "Skeleton",
                "Accelerometer",
                "BalanceBoard"
            });
            writer.Header.AnalogChannels = (short)(7);
            analogData = new Int16[writer.Header.AnalogChannels];
            writer.Header.AnalogSamplesPerFrame = 1;
            writer.SetParameter<string>("SUBJECTS:MARKER_SET", "Using ETRO extended marker set");
            writer.SetParameter<string>("INFO:SYSTEM", "ETRO_APP");
            writer.SetParameter<string>("INFO:EVENT", "test");
            writer.SetParameter<string>("INFO:GAME", "C3D TEST");
            
            //
            // If you want to have (per frame) analog data fill ANALOG:USED and ANALOG:LABELS
            // (See the c3d specification for more details)
            //
            // Note: Here we use Analog data to store per frame time. It is very inefficient, but it's just for demonstration 
            //       (normaly time shold be infered from the AnalogRate in the header)
            //
            writer.SetParameter<Int16>("ANALOG:USED", writer.Header.AnalogChannels);
            string[] alabels = new string[] { 
                "year      ", 
                "month     ",
                "day       ",
                "hour      ", 
                "minute    ",
                "second    ",
                "milisecond"};
            writer.SetParameter<string[]>("ANALOG:LABELS", alabels.ToArray<string>());

            //
            //  We create our labels
            //
            string[] labels = Enum.GetNames(typeof(SkeletonMarkers));
            labels = ArrayCopyHelper.SubArray<string>(labels, 0, labels.Length - 1);

            string[] angleLabels = new string[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                angleLabels[i] = labels[i];
            }
            string[] qualityLabels = new string[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                qualityLabels[i] = labels[i] + "Quality";
            }

            writer.SetParameter<string[]>("POINT:LABELS", labels.Union<string>(angleLabels).Union<string>(qualityLabels).ToArray<string>());
            writer.SetParameter<Int16>("POINT:DATA_TYPE", 0);
            writer.SetParameter<Int16>("INFO:SCORE", 0);
            writer.SetParameter<float>("ANALOG:RATE", 30);
            writer.PointsCount = (short)(((int)SkeletonMarkers.Count) + angleLabels.Length + qualityLabels.Length);
            pointData = new Vector3[writer.PointsCount];

            writer.Open("datafile.c3d");

            for (int i = 0; i < (int)SkeletonMarkers.Count - 1; i++)
            {
                pointData[i] = new Vector3(1, 2, 3);
            }

            
            for (int i = 0; i < (int)SkeletonMarkers.Count - 1; i++)
            {

                pointData[i + (int)SkeletonMarkers.Count - 1] = new Vector3(4, 5, 6);
            }

            for (int i = 0; i < (int)SkeletonMarkers.Count - 1; i++)
            {

                pointData[i + 2 * (int)SkeletonMarkers.Count] = new Vector3(7, 8, 9);
            }
            SetAnalogData();

            writer.WriteIntFrame(pointData);
            writer.WriteIntAnalogData(analogData);
            
            writer.AddEvent(new C3dEvent("Start", "Left"));

            writer.WriteIntFrame(pointData);
            writer.WriteIntAnalogData(analogData);

            writer.AddEvent(new C3dEvent("Start", "Right"));

            writer.WriteIntFrame(pointData);
            writer.WriteIntAnalogData(analogData);

            

            writer.AddEvent(new C3dEvent("End", "Left"));
            writer.AddEvent(new C3dEvent("End", "Right"));

            // We cannot add parameters once the file was opened, 
            //   however we can change existing ones
            writer.SetParameter<Int16>("INFO:SCORE", 42);

            writer.Close();
        }

        static void SetAnalogData() {
            int pos = 0;
            analogData[pos++] = (Int16)DateTime.Now.Year;
            analogData[pos++] = (Int16)DateTime.Now.Month;
            analogData[pos++] = (Int16)DateTime.Now.Day;
            analogData[pos++] = (Int16)DateTime.Now.Hour;
            analogData[pos++] = (Int16)DateTime.Now.Minute;
            analogData[pos++] = (Int16)DateTime.Now.Second;
            analogData[pos++] = (Int16)DateTime.Now.Millisecond;
        }

    }
}
