//-----------------------------------------------------------------------------
// C3dWriter.cs
//
// Writes data to C3D files 
//
// ETRO, Vrije Universiteit Brussel
// Copyright (C) 2015 Lubos Omelina. All rights reserved.
//-----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.CompilerServices;

namespace Vub.Etro.IO
{
    public class C3dWriter
    {
        private string _c3dFile;
        private FileStream _fs = null;
        private BinaryWriter _writer = null;
        private Dictionary<string, ParameterGroup> _nameToGroups;
        private Dictionary<int, ParameterGroup> _idToGroups;
        private HashSet<Parameter> _allParameters;
        
        private int _dataStartOffset;
        private int _pointFramesOffset;
        
        private int _writePos = 0;

        #region Properties

        private List<string> _pointsLabels;
        public IList<string> Labels { get { return _pointsLabels.AsReadOnly(); } }

        private int _currentFrame = 0;
        public int CurrentFrame { get { return _currentFrame; } }

        public int FramesCount { get { return _header.LastSampleNumber; } }

        public Int16 PointsCount
        {
            get { return _header.NumberOfPoints; }
            set
            {
                _header.NumberOfPoints = value;
            }
        }

        private C3dHeader _header = null;
        public C3dHeader Header { get { return _header; } }

        #endregion Properties


        public C3dWriter()
        {
            _nameToGroups = new Dictionary<string, ParameterGroup>();
            _idToGroups = new Dictionary<int, ParameterGroup>();
            _pointsLabels = new List<string>();
            _allParameters = new HashSet<Parameter>();
            _header = new C3dHeader();

            SetDefaultParametrs();
        }
        
        ~C3dWriter() {
            if (_fs != null) {
                Close();
            }
        }


        public bool Open(string c3dFile)
        {

            _c3dFile = c3dFile;
            _header.LastSampleNumber = 0;
            try
            {
                _fs = new FileStream(_c3dFile, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                _writer = new BinaryWriter(_fs);
                WriteHeader();
                WriteParameters();

                //_writer.BaseStream.Seek(_dataStart, 0);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("C3dReader.Open(\"" + c3dFile + "\"): " + e.Message);
                return false;
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Close()
        {
            if (_fs == null) {
                return false;
            }
            
            // write number of frames
            SetParameter<Int16>("POINT:FRAMES", (Int16)_header.LastSampleNumber);

            // update header (data start together with number of frames)
            long position = _writer.BaseStream.Position;
            Parameter p = _nameToGroups["POINT"].GetParameter("DATA_START");
            _header.DataStart = (short)p.GetData<Int16>();
            _writer.Seek(0, 0);
            _writer.Write(_header.GetRawData());

            _writer.Seek((int)position, 0); // to be sure, put pointer to the end
            _writer.Close();
            _writer = null;
            _fs.Close();
            _fs = null;

            return true;
        }

        public void UpdateParameter(Parameter p)
        {
            long position = _writer.BaseStream.Position;

            _writer.Seek((int)p.OffsetInFile, 0);
            p.WriteTo(_writer);

            _writer.Seek((int)position, 0);
        }

        private void WriteParameters()
        {
            byte[] parameters = new byte[4] { 0x01, 0x50, 0x02, 0x54 };
            _writer.Write(parameters, 0, 4);
            _writePos += 4;


            foreach (int id in _idToGroups.Keys)
            {
                ParameterGroup grp = _idToGroups[id];

                grp.WriteTo(_writer);

                WriteParametersOfGroup(grp);
            }

            // update data start offset
            int dataStart = (int)((_writer.BaseStream.Position
                + 5  // size of the last group
                 ) / ParameterModel.BLOCK_SIZE)
                 + 2; // 1 because we are counting from zero and 1 because we want to point on to the next block


            SetParameter<Int16>("POINT:DATA_START", (Int16)dataStart);

            long position = _writer.BaseStream.Position;
            _writer.Seek((int)512, 0);
            parameters[2] = (byte)(dataStart - 2); // number of blocks with parameters is one less than the number of the data starting block without first block
            _writer.Write(parameters, 0, 4);
            _writer.Seek((int)position, 0);


            // write last special group
            ParameterGroup lastTag = new ParameterGroup();
            lastTag.Id = 0;
            lastTag.Name = "";
            lastTag.Description = "";
            lastTag.WriteTo(_writer, true);

            _writer.Write(new byte[(dataStart - 1) * 512 - _writer.BaseStream.Position]);
        }

        private void WriteParametersOfGroup(ParameterGroup grp)
        {
            foreach (Parameter p in grp.Parameters)
            {
                p.Id = (sbyte)-grp.Id;
                p.OffsetInFile = _writer.BaseStream.Position;
                p.WriteTo(_writer);
            }
        }

        private void WriteHeader()
        {
            _writer.Write(_header.GetRawData());
            _writePos += 512;
        }

        private void SetDefaultParametrs()
        {
            SetParameter<Int16>("POINT:DATA_START", (Int16)2);

            _header.NumberOfPoints = 21;
            SetParameter<Int16>("POINT:USED", (Int16)_header.NumberOfPoints);

            _header.LastSampleNumber = 0;
            SetParameter<Int16>("POINT:FRAMES", (Int16)_header.LastSampleNumber);

            _header.ScaleFactor = 1f;
            SetParameter<float>("POINT:SCALE", _header.ScaleFactor);

            _header.FrameRate = 30;
            SetParameter<float>("POINT:RATE", _header.FrameRate);

            _header.AnalogSamplesPerFrame = 0;
            SetParameter<float>("ANALOG:RATE", _header.AnalogSamplesPerFrame);

            _header.AnalogChannels = 0;
            SetParameter<Int16>("ANALOG:USED", (Int16)_header.AnalogChannels);

            SetParameter<float[]>("ANALOG:SCALE", new float[] { });

            SetParameter<float>("ANALOG:GEN_SCALE", 1);

            SetParameter<Int16[]>("ANALOG:OFFSET", new Int16[] { });
        }

        private sbyte _nextGroupId = -1;
        public void SetParameter<T>(string path, T parameterValue)
        {
            string[] elements = path.Split(':');
            if (elements.Length != 2)
            {
                throw new ApplicationException("Wrong path format (use GROUP:PARAMETER)");
            }

            if (!_nameToGroups.ContainsKey(elements[0]))
            {
                if (_fs == null)
                {
                    ParameterGroup group = new ParameterGroup();
                    group.Id = _nextGroupId--;
                    group.Name = elements[0];
                    _nameToGroups.Add(group.Name, group);
                    _idToGroups.Add(group.Id, group);
                }
                else
                {
                    throw new ApplicationException("Cannot create a parameter group " + elements[0] + " after file was open.");
                }

            }

            ParameterGroup grp = _nameToGroups[elements[0]];

            Parameter p = grp.HasParameter(elements[1]) ?
                grp.GetParameter(elements[1]) : new Parameter();

            p.Name = elements[1];
            p.SetData<T>(parameterValue);

            if (!grp.Parameters.Contains(p))
            {
                if (_fs == null)
                {
                    grp.Parameters.Add(p);
                }
                else
                {
                    throw new ApplicationException("Cannot create a parameter " + elements[0] + " after file was open.");
                }
            }

            // if file is open and we are modifieng an existig an parameter - update changes.
            if (_fs != null && p.OffsetInFile > 0)
            {
                UpdateParameter(p);
            }
        }

        public void WriteFloatFrame(Vector3[] data)
        {
            _header.LastSampleNumber++;
            for (int i = 0; i < data.Length; i++)
            {
                _writer.Write(data[i].X);
                _writer.Write(data[i].Y);
                _writer.Write(data[i].Z);

                // TODO
                _writer.Write((float)0);
                //int cc = (int)_reader.ReadSingle();
            }
        }



        public void WriteIntFrame(Vector3[] data)
        {
            _header.LastSampleNumber++;
            for (int i = 0; i < data.Length; i++)
            {
                _writer.Write((Int16)data[i].X);
                _writer.Write((Int16)data[i].Y);
                _writer.Write((Int16)data[i].Z);

                // TODO
                _writer.Write((Int16)0);

            }
        }

        public void WriteFloatAnalogData(float[] data_channels)
        {
            if (data_channels.Length != _header.AnalogChannels)
            {
                throw new ApplicationException(
                "Number of channels in data has to be the same as it is declared in header and parameters' section");
            }

            for (int i = 0; i < data_channels.Length; i++)
            {
                _writer.Write(data_channels[i]);
            }
        }

        public void WriteIntAnalogData(Int16[] data_channels)
        {
            if (data_channels.Length != _header.AnalogChannels)
            {
                throw new ApplicationException(
                "Number of channels in data has to be the same as it is declared in header and parameters' section");
            }

            for (int i = 0; i < data_channels.Length; i++)
            {
                _writer.Write(data_channels[i]);
            }
        }

    }
}
