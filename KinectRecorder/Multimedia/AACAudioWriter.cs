﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MF = SharpDX.MediaFoundation;

namespace KinectRecorder.Multimedia
{
    class AACAudioWriter : MediaFoundationAudioWriter
    {
        public AACAudioWriter(MF.SinkWriter sinkWriter, ref WAVEFORMATEX waveFormat, int desiredBitRate = 192000)
            : base(sinkWriter, ref waveFormat, desiredBitRate)
        {
        }

        public override Guid AudioFormat
        {
            get
            {
                return MF.AudioFormatGuids.Aac;
            }
        }
    }
}
