using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using BansheeGz.BGSpline.Curve;
using BansheeGz.BGSpline.Components;
using System;

public class Utilities {
    public static Color ColorWithAlpha(Color tweakColor, float newAlpha) {
        tweakColor.a = newAlpha;
        return tweakColor;
    }
}

public class Globals {
    // 4 lines, first-person perspective, left to right
    public static Color[] StringColorForLine = new Color[] {
        Utilities.ColorWithAlpha(Color.yellow, 0.5f),
        Utilities.ColorWithAlpha(Color.red, 0.5f),
        Utilities.ColorWithAlpha(Color.green, 0.5f),
        Utilities.ColorWithAlpha(Color.blue, 0.5f),
    };
    public const uint NumLines = 4;
    public const float LineSpacing = 0.22f;
}

public class GameController : MonoBehaviour {

    class TreadmillTile {
        private GameObject gameObject;
        public GameObject GameObject {
            get {
                return gameObject;
            }
        }
        private float beat;
        public float Beat {
            get {
                return beat;
            }
        }

        private List<TreadmillNote> noteList = new List<TreadmillNote>();
        public List<TreadmillNote> Notes {
            get {
                return noteList;
            }
        }

        public TreadmillTile(GameObject gameObject) {
            this.gameObject = gameObject;
        }

        public void Show(GameController gameController, float beat, Vector3 startingPos) {
            // Clear our existing notes
            foreach (var note in noteList) {
                gameController.NotePool.Return(note);
            }
            noteList.Clear();

            this.beat = beat;
            // TODO: Formalize player height!
            gameObject.transform.position = new Vector3(startingPos.x,
                startingPos.y - 0.4f, startingPos.z);

            // Spawn our notes
            if (beat > gameController.GameplayStartTime) {
                gameController.ProcessBeatEvents(this, (uint)(beat - gameController.GameplayStartTime));
            }
        }

        public void AddNote(TreadmillNote treadmillNote) {
            noteList.Add(treadmillNote);
        }

        public void UpdateFX(float metronomeTime) {
            foreach (var note in Notes) {
                note.UpdateFX(metronomeTime);
            }
        }
    }

    class TilePool : SimpleObjectPool<TreadmillTile> {
        private GameObject ownerObject;
        private GameObject prefabObject;
        private const int kPoolSize = 4;

        protected override TreadmillTile ItemConstructor(int currentStorageSize) {
            GameObject gameObject = GameObject.Instantiate(prefabObject);
            if (gameObject) {
                gameObject.name = this.GetType().Name + currentStorageSize;
                return new TreadmillTile(gameObject);
            }
            return null;
        }

        public TilePool(GameObject parentObject, GameObject prefabObject) {
            this.ownerObject = parentObject;
            this.prefabObject = prefabObject;
            base.FlushAndRefill(kPoolSize);
        }

        protected override void OnItemReturn(TreadmillTile borrowItem) {
            // Return the notes to the note pool
        }
    }

    public BGCurve Curve;
    public float TrackLength;
    public float SampleRate;
    public float BeatsPerMinute;
    public float RunnerSpeed = 1.0f;
    public float CameraLookAheadDistance = 1.0f;
    public uint TilesToSpawn = 4;
    public float HeadBobHeight = 0.1f;
    public float HeadBobSpeed = 10.0f;
    public GameObject BaseTilePrefab;
    public GameObject NotePrefab;
    public GameObject NotePlow;
    public float GameplayStartTime;
    public AudioSource MusicAudioSource;
    public float TapSlopRush = 0.1f;
    public float TapSlopDrag = 0.1f;

    private bool[] lineTapsActive = new bool[Globals.NumLines];

    private float runnerPosInMeters = 0.0f;
    private float lastRunningTime = 0.0f;
    private TilePool tilePool;
    private TreadmillNotePool treadmillNotePool;
    public TreadmillNotePool NotePool {
        get {
            return treadmillNotePool;
        }
    }
    private LinkedList<TreadmillTile> activeTileList = new LinkedList<TreadmillTile>();
    private Vector3 runnerPosOnPath;
    private TrackInfo trackInfo;
    private List<TrackInfo.TimelineData.BeatEvent> beatEvents = new List<TrackInfo.TimelineData.BeatEvent>();
    private uint lastBeatProcessed;
    private Vector3 lookAtPos;
    private Vector3 notePlowPosition;
    private Vector3 lineP0;
    private Vector3 lineP1;
    private float keyPressTime = 0.0f;
    private String debugString;
    private Vector3 notePlowBigScale = new Vector3(1.0f, 0.11f, 0.03f);
    private Vector3 notePlowNormalScale = new Vector3(1.0f, 0.1f, 0.01f);
    private float notePlowScaleTime = 0.5f;


    private BGCurveBaseMath curveBaseMath;

    private float headBobTimer = 0.0f;

    private Stopwatch stopwatch = new Stopwatch();

    public float SimBeatsPerMinute {
        get {
            return BeatsPerMinute; // * TimeScale;
        }
    }

    public float SimBeatsPerSecond {
        get {
            return SimBeatsPerMinute / 60.0f;
        }
    }

    public float SimSecondsPerBeat {
        get {
            return 60.0f / SimBeatsPerMinute;
        }
    }

    public float CurrentMetronomeTime {
        get {
            return ((float)stopwatch.ElapsedMilliseconds / (SimSecondsPerBeat * 1000.0f)) + 1.0f;
        }
    }

    public float MetersPerBeat {
        get {
            return 1.0f;
        }
    }

    // Use this for initialization
    void Start () {
        stopwatch.Reset();
        stopwatch.Start();

        var curvePoints = new BGCurvePoint[(int)(TrackLength / SampleRate) + 1];

        var pointPosition = transform.localPosition;
        for (int i = 0; i < curvePoints.Length; ++ i) {
            curvePoints[i] = Curve.CreatePointFromLocalPosition(pointPosition, 
                BGCurvePoint.ControlTypeEnum.Absent);
            pointPosition.z += TrackLength / SampleRate;
        }

        Curve.AddPoints(curvePoints);

        curveBaseMath = new BGCurveBaseMath(Curve, 
            new BGCurveBaseMath.Config(BGCurveBaseMath.Fields.Position));

        // Pool for treadmill notes
        treadmillNotePool = new TreadmillNotePool(null, NotePrefab);

        // Load 'em up
        trackInfo = TrackLoader.LoadTrack("bensound-funnysong_track");

        // Get a disposable copy of the beat events
        if (trackInfo != null) {
            beatEvents = new List<TrackInfo.TimelineData.BeatEvent>(trackInfo.Timeline.GetBeatEvents());
        }
        else {
            beatEvents = new List<TrackInfo.TimelineData.BeatEvent>();
        }

        // Initialize the tile pool
        tilePool = new TilePool(null, BaseTilePrefab);

        // Spawn the first N tiles
        var startingPos = curveBaseMath.CalcPositionByDistance(0.0f);
        startingPos.z += BaseTilePrefab.transform.localScale.z * 0.5f;
        for (uint i = 0; i < TilesToSpawn; ++ i) {
            var newTile = tilePool.Borrow();
            newTile.Show(this, (float)i + 1.0f, startingPos);
            startingPos.z += BaseTilePrefab.transform.localScale.z;
            activeTileList.AddLast(newTile);
        }

    }

    private TreadmillTile GetTileForBeat(float beat) {
        TreadmillTile tileForBeat = null;
        foreach (var activeTile in activeTileList) {
            if (activeTile.Beat >= beat && activeTile.Beat < (beat + 1.0f)) {
                tileForBeat = activeTile;
                break;
            }
        }
        return tileForBeat;
    }

    private void ProcessBeatEvents(TreadmillTile treadmillTile, uint beatToProcess) {
        if (beatToProcess > lastBeatProcessed) {
            //Debug.Log("predicted beat is " + predictBeat);

            // Handle any events for this beat
            while (beatEvents.Count > 0) {
                var beatEvent = beatEvents[beatEvents.Count - 1];
                var wholeBeat = (int)beatEvent.beat;

                if (wholeBeat < beatToProcess) {
                    //Debug.Log("BURNING OFF BEATS THAT'S A BAD THING");
                    beatEvents.RemoveAt(beatEvents.Count - 1);
                    continue;
                }

                if (wholeBeat == beatToProcess) {
                    // Remap the beat to the global timeline for easier tracking
                    var globalBeat = (float)Math.Truncate(GameplayStartTime) + beatEvent.beat - 1.0f;

                    var treadmillNote = treadmillNotePool.GetInstance(globalBeat, 
                        treadmillTile.GameObject, beatEvent.line - 1);
                    treadmillTile.AddNote(treadmillNote);

                    beatEvents.RemoveAt(beatEvents.Count - 1);
                    continue;
                }
                break;
            }

            lastBeatProcessed = beatToProcess;
        }
    }

    // Update is called once per frame
    void Update () {

        float runningTime = CurrentMetronomeTime;

        if (MusicAudioSource != null) {
            if (MusicAudioSource.clip == null) {
                MusicAudioSource.clip = trackInfo.AudioClip;

                // Wait through n-1 beats
                float delayBeats = GameplayStartTime - runningTime;

                // Take out the note plow offset
                delayBeats -= 1.0f;

                // Wait throuh beat - 1, take into account the note plow offset
                MusicAudioSource.PlayDelayed((delayBeats * SimSecondsPerBeat) - 0.35f);
            }
        }

        // Ride the snake
        Transform newTransform = transform;

        // Trying a simple sine wave for head bob
        float headBobSin = Mathf.Sin(headBobTimer);
        headBobTimer += HeadBobSpeed * Time.deltaTime;
        while (headBobTimer > Mathf.PI * 2.0f) {
            headBobTimer -= Mathf.PI * 2.0f;
        }

        //runnerPosInMeters = MetersPerBeat * runningTime;
        runnerPosInMeters += MetersPerBeat * (runningTime - lastRunningTime);
        lastRunningTime = runningTime;

        // Position on path
        /*
        // Get the tween keys
        var Key0 = Curve.Points[(int)runnerPosInMeters + 0] as BGCurvePoint;
        var Key1 = Curve.Points[(int)runnerPosInMeters + 1] as BGCurvePoint;

        newTransform.position = curveBaseMath.CalcPositionByT(Key0, Key1, runnerPosInMeters - Mathf.Floor(runnerPosInMeters));
        */
        runnerPosOnPath = curveBaseMath.CalcPositionByDistance(runnerPosInMeters);
        newTransform.position = runnerPosOnPath;

        // Orient camera
        lookAtPos = curveBaseMath.CalcPositionByDistance(runnerPosInMeters +
            CameraLookAheadDistance);
        lookAtPos.y -= 0.4f;
        newTransform.LookAt(lookAtPos);

        UpdateNotePlow(runningTime);

        // Apply head bob to camera
        var headBobOffset = new Vector3(Mathf.Sign(headBobSin) * -0.5f, 1.0f, 0.0f);
        headBobOffset.Normalize();
        headBobOffset *= headBobSin * HeadBobHeight;
        GetComponentInChildren<Camera>().transform.localPosition = headBobOffset;

        transform.position = newTransform.position;
        transform.rotation = newTransform.rotation;

        // Expire past tiles, bring in new ones
        var runnerTile = activeTileList.First;
        while (runnerTile != null) {
            // If we were or are on the tile
            if (runnerPosOnPath.z >= Mathf.Floor(runnerTile.Value.GameObject.
                transform.position.z) - (BaseTilePrefab.transform.localScale.z * 0.5f)) {

                // If we're past the tile
                if (runnerPosOnPath.z >= Mathf.Floor(runnerTile.Value.GameObject.
                    transform.position.z) + (BaseTilePrefab.transform.localScale.z * 0.5f)) {

                    var newTile = tilePool.Borrow();

                    // TODO: Formalize player height!
                    newTile.Show(this,
                        activeTileList.Last.Value.Beat + 1.0f,
                        new Vector3(runnerPosOnPath.x, runnerPosOnPath.y,
                            activeTileList.Last.Value.GameObject.transform.position.z +
                            BaseTilePrefab.transform.localScale.z));

                    runnerTile = runnerTile.Next;

                    activeTileList.AddLast(newTile);
                    tilePool.Return(activeTileList.First.Value);
                    activeTileList.RemoveFirst();
                    continue;
                }
            }
            break;
        }

        var lastGameplayTile = runnerTile.Next;

        // Update tile FX
        for (var currentTile = activeTileList.First; currentTile != lastGameplayTile.Next; currentTile = currentTile.Next) {
            currentTile.Value.UpdateFX(runningTime + 1.0f);
        }

        // Check input
        bool anyKeyDown = false;
        if (Input.GetKeyDown(KeyCode.A)) {
            anyKeyDown = true;
            TapLine(0, runnerPosInMeters);
        }
        if (Input.GetKeyDown(KeyCode.S)) {
            anyKeyDown = true;
            TapLine(1, runnerPosInMeters);
        }
        if (Input.GetKeyDown(KeyCode.D)) {
            anyKeyDown = true;
            TapLine(2, runnerPosInMeters);
        }
        if (Input.GetKeyDown(KeyCode.F)) {
            anyKeyDown = true;
            TapLine(3, runnerPosInMeters);
        }

        if (NotePlow != null) {
            if (anyKeyDown) {
                keyPressTime = runningTime;
            }

            NotePlow.transform.localScale = Vector3.Lerp(notePlowBigScale, 
                notePlowNormalScale, (runningTime - keyPressTime) / notePlowScaleTime);
        }

        // Probably will not be deferring the behavior to TradmillTile as we
        // may need to handle multi-tile events
        CheckPlayedFret(runnerTile.Value, runningTime + 1.0f);
        CheckPlayedFret(runnerTile.Next.Value, runningTime + 1.0f);
        CheckPlayedFret(runnerTile.Next.Next.Value, runningTime + 1.0f);

        for (int lineIndex = 0; lineIndex < Globals.NumLines; ++lineIndex) {
            lineTapsActive[lineIndex] = false;
        }

        

    }

    void CheckPlayedFret(TreadmillTile currentTile, float metronomeTime) {
        foreach (var note in currentTile.Notes) {
            if (!note.isPlayed) {
                float window0 = note.beat - (TapSlopRush * MetersPerBeat);
                float window1 = note.beat + (TapSlopDrag * MetersPerBeat) + (NotePrefab.transform.localScale.z * 0.5f);
                if (metronomeTime >= window0 && metronomeTime <= window1) {
                    if (lineTapsActive[note.line]) {
                        lineTapsActive[note.line] = false;

                        note.isPlayed = true;

                        // Success events
                        note.OnPlayed(1.0f, metronomeTime);
                    }
                }
            }
        }

    }

    void UpdateNotePlow(float metronomeTime) {
        /*
        if (NotePlow != null) {
            Vector3 bottomOfCameraWorld0 = Camera.main.ViewportToWorldPoint(new Vector3(0.5f, 0.0f, 0.0f));
            Vector3 bottomOfCameraWorld1 = Camera.main.ViewportToWorldPoint(new Vector3(0.5f, 0.0f, 1.0f));

            lineP0 = bottomOfCameraWorld0;
            lineP1 = bottomOfCameraWorld1;

            // Cast a ray from there, along the view axis, at the treadmill
            Vector3 rayPosition = bottomOfCameraWorld0;
            Vector3 rayDirection = bottomOfCameraWorld1 - bottomOfCameraWorld0;
            rayDirection.Normalize();
            Vector3 planeUp = Vector3.up;
            float d = Vector3.Dot(planeUp, rayDirection);
            if (Mathf.Abs(d) > Mathf.Epsilon) {
                float t = Vector3.Dot(rayPosition, planeUp) / d;
                if (t >= 0) {
                    notePlowPosition = new Vector3(0.0f, 0.01f, (rayPosition + (rayDirection * t)).z - 0.06f);
                }
            }
            NotePlow.transform.position = notePlowPosition;
            NotePlow.transform.rotation = Quaternion.identity;
        }
        */
    }

    public void TapLine(uint lineIndex, float timeOfTap) {
        lineTapsActive[(int)lineIndex] = true;
    }

    void OnDrawGizmosSelected() {
    }

    private void OnGUI() {
        GUI.Label(new Rect(0, 0, 100, 100), debugString);
    }
}
