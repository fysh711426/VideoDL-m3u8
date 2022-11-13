﻿using System.Collections.Generic;

namespace VideoDL_m3u8.Parser
{
    public class MediaPlaylist
    {
        public bool IsM3U { get; set; }
        public bool EndList { get; set; }
        public long MediaSequence { get; set; }
        public int TargetDuration { get; set; }
        public double TotalDuration { get; set; }
        public int Version { get; set; }
        public string PlaylistType { get; set; } = "";
        public List<Part> Parts { get; set; } = new();
        //public List<Segment> Segments { get; set; } = new();
        //public List<SegmentKey> Keys { get; set; } = new();
        public string Manifest { get; set; } = "";
    }

    public class Part
    {
        public List<Segment> Segments { get; set; } = new();
    }

    public class Segment
    {
        public long Index { get; set; }
        public double Duration { get; set; }
        public string Uri { get; set; } = "";
        public string Title { get; set; } = "";
        public bool Discontinuity { get; set; }
        public SegmentKey Key { get; set; } = new();
        public ByteRange? ByteRange { get; set; }
        public SegmentMap? SegmentMap { get; set; }
    }

    public class SegmentKey
    {
        public string Method { get; set; } = "NONE";
        public string Uri { get; set; } = "";
        public string IV { get; set; } = "";
    }

    public class ByteRange
    {
        public int Length { get; set; }
        public int? Offset { get; set; }
    }

    public class SegmentMap
    {
        public string Uri { get; set; } = "";
        public ByteRange? ByteRange { get; set; }
    }
}