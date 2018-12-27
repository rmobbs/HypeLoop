using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TreadmillNote {
    public GameObject gameObject;
    public float beat;
    public bool isUsed;
    public uint line;
    public bool isPlayed;
    public float score;

    private float emissive;
    private float playedTime;

    private Vector3 noteBigScale = new Vector3(0.175f, 0.05f, 0.125f);
    private Vector3 noteNormalScale = new Vector3(0.14f, 0.05f, 0.1f);
    private float noteScaleTime = 0.5f;
    private float noteEmissivePulseTime = 0.5f;

    public TreadmillNote(int globalIndex, GameObject noteObject, GameObject ownerObject) {

        gameObject = GameObject.Instantiate(noteObject);
        gameObject.GetComponent<Renderer>().enabled = false;
        if (ownerObject) {
            gameObject.transform.parent = ownerObject.transform;
        }
    }

    public void Show(float beat, uint lineIndex, Color baseColor, Vector3 localPosition) {
        var rend = gameObject.GetComponent<Renderer>();

        rend.material.color = baseColor;
        rend.material.EnableKeyword("_EMISSION");
        emissive = 0.1f;
        rend.material.SetColor("_EmissionColor", Color.white * Mathf.LinearToGammaSpace(emissive));
        rend.enabled = true;

        this.beat = beat;
        this.line = lineIndex;
        this.isUsed = true;
        this.isPlayed = false;
        this.score = 0.0f;

        gameObject.transform.localPosition = localPosition;
    }

    public void Hide() {
        gameObject.GetComponent<Renderer>().enabled = false;
    }

    public void OnPlayed(float score, float metronomeTime) {
        emissive = 1.0f;
        gameObject.transform.localScale = new Vector3(0.175f, gameObject.transform.localScale.y, 0.125f);
        gameObject.GetComponent<Renderer>().material.
            SetColor("_EmissionColor", Color.white * Mathf.LinearToGammaSpace(emissive));
        playedTime = metronomeTime;
    }

    public void UpdateFX(float metronomeTime) {
        if (isPlayed) {
            gameObject.transform.localScale = Vector3.Lerp(noteBigScale, 
                noteNormalScale, (metronomeTime - playedTime) / noteScaleTime);
            emissive = Mathf.Lerp(1.0f, 0.1f, (metronomeTime - playedTime) / noteEmissivePulseTime);
            gameObject.GetComponent<Renderer>().material.
                SetColor("_EmissionColor", Color.white * Mathf.LinearToGammaSpace(emissive));
        }
    }
}

public class TreadmillNotePool : SimpleObjectPool<TreadmillNote> {
    private GameObject ownerObject;
    private GameObject noteObject;
    private const int kPoolSize = 20;

    protected override TreadmillNote ItemConstructor(int currentStorageSize) {
        return new TreadmillNote(currentStorageSize, noteObject, ownerObject);
    }

    protected override void OnItemReturn(TreadmillNote usedObject) {
        usedObject.Hide();
    }

    public TreadmillNotePool(GameObject parentObject, GameObject noteObject) {
        this.ownerObject = parentObject;
        this.noteObject = noteObject;
        base.FlushAndRefill(kPoolSize);
    }

    public TreadmillNote GetInstance(float beat, GameObject parentTile, uint lineIndex) {
        TreadmillNote treadmillNote = Borrow();

        var spawnPos = Vector3.zero;

        // Line/beat offset relative to parent track 
        float linePct = (float)lineIndex / (float)(Globals.NumLines - 1);
        float spacing = Globals.LineSpacing * (Globals.NumLines - 1);
        spawnPos.x = (linePct * spacing) - (spacing * 0.5f);
        spawnPos.y = 0.03f;
        spawnPos.z = 
            // Localized beat location
            ((beat - Mathf.Floor(beat)) * parentTile.transform.localScale.z) - 
            // Position of tile is its center, so move forward by half
            (parentTile.transform.localScale.z * 0.5f) + 
            // And we position by center so move forward by half
            (treadmillNote.gameObject.transform.localScale.z * 0.5f);

        treadmillNote.gameObject.transform.parent = parentTile.transform;

        treadmillNote.Show(beat, lineIndex, Globals.StringColorForLine[lineIndex], spawnPos);

        return treadmillNote;
    }

}
