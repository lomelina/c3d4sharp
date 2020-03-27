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
        static Vector4[] pointData = null;

        static void Main(string[] args)
        {
            string[] labels = Enum.GetNames(typeof(SkeletonMarkers));
            labels = ArrayCopyHelper.SubArray<string>(labels, 0, labels.Length - 1);

            string[] angleLabels = new string[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                angleLabels[i] = labels[i] + "Angle";
            }
            string[] qualityLabels = new string[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                qualityLabels[i] = labels[i] + "Quality";
            }

            labels = labels.Union<string>(angleLabels).Union<string>(qualityLabels).ToArray<string>();

            string[] analogLabels = new string[] {
                "year      ",
                "month     ",
                "day       ",
                "hour      ",
                "minute    ",
                "second    ",
                "milisecond"};


            pointData = new Vector4[labels.Length];
            analogData = new Int16[analogLabels.Length];

            //
            //  Create C3dWriter with events enabled
            //
            C3dWriter writer = new C3dWriter(labels, 30, analogLabels, 1, true);
            
            
            // fill custom parameters in the C3D file
            writer.SetParameter<string[]>("POINT:DATA_TYPE_LABELS", new string[] {
               "Skeleton",
                "Accelerometer",
                "BalanceBoard"
            });

            writer.SetParameter<Int16>("POINT:DATA_TYPE", 0);

            writer.SetParameter<string>("SUBJECTS:MARKER_SET", "Using ETRO extended marker set");
            writer.SetParameter<string>("INFO:SYSTEM", "ETRO_APP");
            writer.SetParameter<string>("INFO:EVENT", "test");
            writer.SetParameter<string>("INFO:GAME", "C3D TEST");
            
            writer.SetParameter<Int16>("INFO:SCORE", 0);
            writer.Open("datafile.c3d");

            for (int i = 0; i < (int)SkeletonMarkers.Count - 1; i++)
            {
                pointData[i] = new Vector4(1, 2, 3,0);
            }

            
            for (int i = 0; i < (int)SkeletonMarkers.Count - 1; i++)
            {

                pointData[i + (int)SkeletonMarkers.Count - 1] = new Vector4(4, 5, 6,0);
            }

            for (int i = 0; i < (int)SkeletonMarkers.Count - 1; i++)
            {

                pointData[i + 2 * (int)SkeletonMarkers.Count] = new Vector4(7, 8, 9,0);
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
