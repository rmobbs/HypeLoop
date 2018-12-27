using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using UnityEngine;

[XmlRoot("track")]
public class TrackData {
    [XmlAttribute("bpm")]
    public uint BeatsPerMinute;

    public class MusicInfo {
        [XmlAttribute("name")]
        public string name;
        [XmlAttribute("offset_ms")]
        public int musicOffsetMs;
        [XmlAttribute("skip_ms")]
        public uint musicSkipMs;
    }

    [XmlElement("music")]
    public MusicInfo musicInfo;

    public class NoteData {
        [XmlAttribute("beat")]
        public float beat;
        [XmlAttribute("line")]
        public uint line;
    }

    public class LoopData {
        [XmlAttribute("name")]
        public string name;

        [XmlArray("notes"), XmlArrayItem("note")]
        public NoteData[] Notes;
    }

    [XmlArray("loops"), XmlArrayItem("loop")]
    public LoopData[] loopData;

    public class LoopEvent {
        [XmlAttribute("beat")]
        public int beat;
        [XmlAttribute("name")]
        public string name;
    }

    [XmlArray("timeline"), XmlArrayItem("loop")]
    public LoopEvent[] loopEvents;
}

public class TrackInfo {
    public TrackInfo(TrackData trackData) {
        this.trackData = trackData;

        audioClip = Resources.Load("Audio/BackgroundTracks/" + trackData.musicInfo.name) as AudioClip;
        if (audioClip == null) {
            throw new System.Exception("Track file loaded correctly but music clip did not load");
        }

        loopTable = new Dictionary<string, TrackData.LoopData>();
        foreach (var loopDatum in trackData.loopData) {
            loopTable.Add(loopDatum.name, loopDatum);
        }

        timeline = new TimelineData(this);
    }

    public class TimelineData {
        public class BeatEvent {
            public float beat = 0;
            public uint line = 0;
        }

        private List<BeatEvent> beatEvents = new List<BeatEvent>();
        public IList<BeatEvent> GetBeatEvents() {
            return beatEvents.AsReadOnly();
        }

        public TimelineData(TrackInfo trackInfo) {
            beatEvents.Clear();

            foreach (var loopEvent in trackInfo.trackData.loopEvents) {
                if (trackInfo.loopTable.ContainsKey(loopEvent.name)) {
                    var loop = trackInfo.loopTable[loopEvent.name];
                    foreach (var note in loop.Notes) {
                        //Debug.Log("Loop " + loop.Loop.Name + " adding note on beat " + (loop.Beat + (note.Beat - 1.0f)));
                        beatEvents.Add(new BeatEvent {
                            beat = note.beat + loopEvent.beat - 1.0f,
                            line = note.line
                        });
                    }
                }
                beatEvents.Sort(delegate (TrackInfo.TimelineData.BeatEvent e1, TrackInfo.TimelineData.BeatEvent e2) {
                    return e2.beat.CompareTo(e1.beat);
                });
            }

        }
    }

    protected Dictionary<string, TrackData.LoopData> loopTable;

    private TrackData trackData;
    public TrackData TrackData {
        get {
            return trackData;
        }
    }

    private TimelineData timeline;
    public TimelineData Timeline {
        get {
            return timeline;
        }
    }
    
    private AudioClip audioClip;
    public AudioClip AudioClip {
        get {
            return audioClip;
        }
    }
}

public class TrackLoader {
    public static TrackInfo LoadTrack(string trackName) {
        TrackData trackData = null;
        var trackTextAsset = Resources.Load("Tracks/" + trackName) as TextAsset;
        if (trackTextAsset != null) {
            var trackMemoryStream = new MemoryStream(trackTextAsset.bytes);
            if (trackMemoryStream != null) {
                try {
                    XmlSerializer xmlSerializer = new XmlSerializer(typeof(TrackData));

                    xmlSerializer.UnknownNode +=
                        new XmlNodeEventHandler((object sender, XmlNodeEventArgs e) => {
                            Debug.Log("Unknown node: " + e.Name + "\t" + e.Text);
                        });
                    xmlSerializer.UnknownAttribute +=
                        new XmlAttributeEventHandler((object sender, XmlAttributeEventArgs e) => {
                            Debug.Log("Unknown attribute: " + e.Attr.Name + "\t" + e.Attr.Value);
                        });
                    xmlSerializer.UnknownElement +=
                        new XmlElementEventHandler((object sender, XmlElementEventArgs e) => {
                            Debug.Log("Unknown element: " + e.Element.Name + "\t" + e.Element.Value);
                        });

                    trackData = xmlSerializer.Deserialize(trackMemoryStream) as TrackData;
                }
                catch (Exception e) {
                    Debug.Log("Failed to parse track file \'" + trackName + ": " + e.ToString());
                }
            }
        }

        if (trackData != null) {
            return new TrackInfo(trackData);
        }

        return null;
    }
}
