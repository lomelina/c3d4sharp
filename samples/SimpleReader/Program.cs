using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vub.Etro.IO;

namespace SimpleReader
{
    public class Program
    {

        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Wrong number of arguments");
                return;
            }
            try
            {
                C3dReader reader = new C3dReader();

                if (!reader.Open(args[0]))
                {
                    Console.Error.WriteLine("Error: Unable to open file " + args[0]);
                    return;
                }

                for (int i = 0; i < /*reader.GetParameter<Int16>("POINT:FRAMES")*/ 100000; i++)
                {
                    // returns an array of all points, it is necessary to call this method in each cycle
                    Vector4[] array = reader.ReadFrame();
                    //Vector3 e = reader["Spine"];
                    // we can ask for specific point - you can check labels in reader.Labels
                    Vector4 spine = array[1];

                    // get analog data for this frame
                    //float value = reader.AnalogData["Fx1", 0 /* from 0 to reader.AnalogChannels*/];
                    // OR 
                    // value = reader.AnalogData[0, 0];

                    // OR
                    //float [,] analogData = reader.AnalogData.Data;


                    //Console.WriteLine("Frame " + i + ": Spine.X " + spine.X + ",  Spine.Y " + spine.Y + ": Spine.Z " + spine.Z);
                }

                // Don't forget to close the reader
                // - it updates the frames count information in the c3d file header and parameters section
                reader.Close();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
                Console.Error.WriteLine(e.StackTrace);
            }
        }
    }
}
