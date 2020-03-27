using System;
using System.Linq;
using Vub.Etro.IO;

namespace WritingMultipleAnalogData
{
    class Program
    {
        public const int ANALOG_SAMPLING_RATE = 30;
        public const int POINT_RATE = 33;
        public static Random rand = new Random(42);

        public static string[] pointNames = new string[] { "position" };
        public static string[] analogChannelNames = new string[] { "CH1 Raw", "CH2 Raw" };

        static void Main(string[] args)
        {
            float[] analogData = new float[ANALOG_SAMPLING_RATE];
            Vector4[] pointData = new Vector4[1];
            analogData[0] = 42.5f;
            analogData[1] = 12.1f;

            //
            //  Create C3dWriter without events enabled
            //
            C3dWriter writer = new C3dWriter(pointNames, POINT_RATE, analogChannelNames, ANALOG_SAMPLING_RATE, false);
            writer.Header.ScaleFactor = -1;

            // fill custom parameters in the C3D file
            writer.SetParameter<string[]>("POINT:DATA_TYPE_LABELS", new string[] {
                "Skeleton",
                "Accelerometer",
                "BalanceBoard",
                "EMG"
            });
            
            writer.SetParameter<string>("SUBJECTS:MARKER_SET", "Using ETRO extended marker set");
            writer.SetParameter<string>("INFO:SYSTEM", "ETRO_APP");
            writer.SetParameter<string>("INFO:GAME", "C3D TEST");
            
            //
            //  We create our labels
            //
            writer.SetParameter<Int16>("POINT:DATA_TYPE", 0);
            writer.SetParameter<float>("POINT:SCALE", -1);
            writer.SetParameter<Int16>("INFO:SCORE", 0);
            writer.SetParameter<float[]>("ANALOG:SCALE", new float[] { 1f, 1f});
            
            

            writer.Open("simulated_emg.c3d");


            for (int point = 0; point < 20; point++)
            {
                pointData[0] = new Vector4(point, (float)rand.NextDouble()-0.5f,1,0); 
                writer.WriteFloatFrame(pointData);
                //for (int analog = 0; analog < (POINT_RATE / ANALOG_RATE) * 2; analog++)
                //{
                //    for (int analogCh = 0; analogCh < ANALOG_CHANNELS; analogCh++)
                //    {
                //        analogData[analogCh] = (float )(analog%30<15?((rand.NextDouble()*10)-5):((rand.NextDouble() * 2) - 1));
                //    }
                    
                writer.WriteFloatAnalogData(analogData);
                writer.WriteFloatAnalogData(analogData);
                //}

            }
            

            // We cannot add parameters once the file was opened, 
            //   however we can change existing ones
            writer.SetParameter<Int16>("INFO:SCORE", 42);

            writer.Close();
        }
    }
}
