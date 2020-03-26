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
    public class C3dEvent
    {
        public C3dEvent(string label, string context, int frame = -1 /* Current frame */ ) {
            Label = label;
            Context = context;
            Description = "";
            Subject = "";
            Frame = 0;
            IconId = 0;
            GenericFlag = 0;
        }

        public string Context { get; set; }

        public string Label { get; set; }
        
        public string Description { get; set; }

        public string Subject { get; set; }

        public int Frame { get; set; }

        public Int16 IconId { get; set; }

        public byte GenericFlag { get; set; }
    }

    public class C3dWriter
    {
        private bool _eventsEnabled;
        private string _c3dFile;
        private FileStream _fs = null;
        private BinaryWriter _writer = null;
        private Dictionary<string, ParameterGroup> _nameToGroups;
        private Dictionary<int, ParameterGroup> _idToGroups;
        
        private int _dataStartOffset;
        private int _pointFramesOffset;
        
        private int _writePos = 0;

        private List<C3dEvent> _events = null;
        
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

        public C3dWriter(bool eventsEnabled = false)
        {
            _eventsEnabled = eventsEnabled;
            _nameToGroups = new Dictionary<string, ParameterGroup>();
            _idToGroups = new Dictionary<int, ParameterGroup>();
            _pointsLabels = new List<string>();
            _header = new C3dHeader();

            SetDefaultParametrs();
        }

        public C3dWriter(C3dReader copyMetadataFrom, bool eventsEnabled = false) : this(eventsEnabled) 
        {
            _header.SetHeader(copyMetadataFrom.Header.GetRawData());
            
            foreach (Parameter p in copyMetadataFrom.AllParameters) {
                string groupName = copyMetadataFrom.GetGroupName(p);
                CreateGroupIfNotExist(groupName);

                ParameterGroup grp = _nameToGroups[groupName];

                if (!grp.HasParameter(p.Name))
                {
                    Parameter newParam = p.Clone();
                    if (_fs == null)
                    {
                        grp.Parameters.Add(newParam);
                    }
                    else
                    {
                        throw new ApplicationException("Cannot create a parameter " + newParam.Name + " after file was open.");
                    }
                }
                else {
                    Parameter param = grp.GetParameter(p.Name);
                    param.CopyDataFrom(p);
                }

                // if file is open and we are modifieng an existig an parameter - update changes.
                if (_fs != null && p.OffsetInFile > 0)
                {
                    UpdateParameter(p);
                }


            }
            
        }
        
        ~C3dWriter() {
            if (_fs != null) {
                Close();
            }
        }

        private static string GetTempFile(string file){
            return Path.GetDirectoryName(file) + "/~tmp." + Path.GetFileName(file);
        }

        public bool Open(string c3dFile)
        {
            
            _c3dFile = c3dFile;
            _header.LastSampleNumber = 0;
            try
            {
                //PrepareEvents();
                _fs = new FileStream(_eventsEnabled? GetTempFile(_c3dFile) : _c3dFile, FileMode.OpenOrCreate);
                _writer = new BinaryWriter(_fs);
            
                WriteHeader();
                WriteParameters();

                //_writer.BaseStream.Seek(_dataStart, 0);
            }
            catch (IOException e)
            {
                Console.Error.WriteLine("C3dReader.Open(\"" + c3dFile + "\"): " + e.Message);
                throw new ApplicationException("C3dReader.Open(\"" + c3dFile + "\"): " + e.Message);
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

            if (_eventsEnabled) {
                RewriteWithEvents();
                RemoveTempFile();
            }
            return true;
        }

        private void RemoveTempFile()
        {
            File.Delete(GetTempFile(_c3dFile));
        }

        private void RewriteWithEvents() 
        {
            WriteEventContexts();
            WriteEvents();

            // reset parameters' offsets to enable writing to new file
            foreach (int id in _idToGroups.Keys)
            {
                _idToGroups[id].ResetOffsetInFile();
            }
            
            _eventsEnabled = false;
            C3dReader reader = new C3dReader();
            reader.Open(GetTempFile(_c3dFile));
            Open(_c3dFile);

            for (int i = 0; i < reader.FramesCount; i++) 
            {
                Vector4 [] points = reader.ReadFrame();
                if(reader.IsFloat) {
                    this.WriteFloatFrame(points);
                }
                else if (reader.IsInterger) { 
                    this.WriteIntFrame(points);
                }
            }


            reader.Close();
            this.Close();
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

            _header.NumberOfPoints = 25;//21;
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

        private void CreateGroupIfNotExist(string element)
        {
            if (!_nameToGroups.ContainsKey(element))
            {
                if (_fs == null)
                {
                    ParameterGroup group = new ParameterGroup();
                    group.Id = _nextGroupId--;
                    group.Name = element;
                    _nameToGroups.Add(group.Name, group);
                    _idToGroups.Add(group.Id, group);
                }
                else
                {
                    throw new ApplicationException("Cannot create a parameter group " + element + " after file was open.");
                }

            }
        }

        private void CheckRequiredParameters<T>(string[] elements, T parameterValue)
        {
            if (elements[0] == "POINT" && elements[1] == "LABELS") {
                if (typeof(T) != typeof(string[])) {
                    throw new ApplicationException("ERROR: Parameter POINT:LABELS has to be a string array!");
                }
                _header.NumberOfPoints = (Int16)((string[])((object)parameterValue)).Length;
                SetParameter<Int16>("POINT:USED", (Int16)_header.NumberOfPoints);
            }
        }

        public void SetParameter<T>(string path, T parameterValue)
        {
            string[] elements = path.Split(':');
            if (elements.Length != 2)
            {
                throw new ApplicationException("Wrong path format (use GROUP:PARAMETER)");
            }

            CreateGroupIfNotExist(elements[0]);

            CheckRequiredParameters<T>(elements, parameterValue);

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

        public void AddEvent(C3dEvent e) {
            if (_events == null){
                _events = new List<C3dEvent>();
            }

            if (e.Frame == 0) {
                e.Frame = _header.LastSampleNumber;
            }
            _events.Add(e);
        }



        public void WriteEventContexts() 
        {
            if(_events == null) return;
            IEnumerable<string> contexts = _events.Select(x=>x.Context).Distinct();
            string[] descs = new string[contexts.Count<string>()];
            for (int i = 0; i < descs.Length; i++) descs[i] = "";

            Int16[] icon_ids = new Int16[contexts.Count<string>()];
            Int16[] colours = new Int16[contexts.Count<string>()];
            
            SetParameter<Int16>("EVENT_CONTEXT:USED", (Int16)contexts.Count<string>());

            SetParameter<string[]>("EVENT_CONTEXT:LABELS", contexts.ToArray<string>());

            SetParameter<string[]>("EVENT_CONTEXT:DESCRIPTIONS", descs);

            SetParameter<Int16[]>("EVENT_CONTEXT:ICON_IDS", icon_ids);

            SetParameter<Int16[]>("EVENT_CONTEXT:COLOURS", colours);
        }

        private void WriteEvents() {
            if (_events == null) return;

            string[] labels = new string[_events.Count];
            string[] contexts = new string[_events.Count];
            string[] descriptions = new string[_events.Count];
            string[] subjects = new string[_events.Count];
            float[,] times = new float[2,_events.Count];
            Int16[] icon_ids = new Int16[_events.Count];
            byte[] generic_flags = new byte[_events.Count];

            for (int i = 0; i < labels.Length; i++)
            {
                labels[i]        = _events[i].Label;
                contexts[i]      = _events[i].Context;
                descriptions[i]  = _events[i].Description;
                subjects[i]      = _events[i].Subject;
                icon_ids[i]      = _events[i].IconId;
                generic_flags[i] = _events[i].GenericFlag;

                float t = _events[i].Frame / _header.FrameRate;
                times[0, i] = ((int)t) / 60; // compute minutes
                times[1, i] = t % 60;        // seconds and fraction of seconds
                
            }

            SetParameter<Int16>("EVENT:USED", (Int16)contexts.Length);
            SetParameter<string[]>("EVENT:CONTEXTS", contexts);
            SetParameter<string[]>("EVENT:LABELS", labels);
            SetParameter<string[]>("EVENT:DESCRIPTIONS", descriptions);
            SetParameter<string[]>("EVENT:SUBJECTS", subjects);
            SetParameter<float[,]>("EVENT:TIMES", times);
            SetParameter<Int16[]>("EVENT:ICON_IDS", icon_ids);
            SetParameter<byte[]>("EVENT:GENERIC_FLAGS", generic_flags);
        }

        public void WriteFloatFrame(Vector4[] data)
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



        public void WriteIntFrame(Vector4[] data)
        {
            _header.LastSampleNumber++;
            for (int i = 0; i < data.Length; i++)
            {
                _writer.Write((Int16)data[i].X);
                _writer.Write((Int16)data[i].Y);
                _writer.Write((Int16)data[i].Z);
                _writer.Write((Int16)data[i].W);

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
