﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;

public class EvilMemory : MonoBehaviour
{
    //Debug variables
    private const int EXTRA_STAGES = 0;
    private const bool PLAYTEST_MODE = false;
    private const bool PLAYTEST_MODE_SKIP = false;

    //Delay between stages displaying
    private const float STAGE_DELAY = 4;

    //How many unique colours to create for the flashing "free stage check" LED
    private const int STAGE_CHECK_FIDELITY = 25;

    public static string[] ignoredModules = null;

    private static Color LED_OFF = new Color(0, 0, 0, 0);
    private static Color[] LED_COLS = new Color[]{new Color(0.7f, 0,    0, 1),
                                                  new Color(0.5f, 0.5f, 0, 1),
                                                  new Color(0,    0.6f, 0, 1),
                                                  new Color(0,    0.3f, 1, 1)};
    private static Color[] LED_INTENSITY;

    public static int loggingID = 1;
    public int thisLoggingID;

    public KMBombInfo BombInfo;
    public KMAudio Sound;
    public Material HighVisMat;
    public Material SmallHighVisMat;
    public KMModSettings Settings;
    public Mesh StandardMesh, ColourblindMesh;

    public GameObject DialContainer;
    private GameObject[] Dials;
    private MeshRenderer[] DialLED;
    public SmallDial[] LEDDials;
    public KMSelectable Submit;
    public TextMesh Text;
    public Nixie Nix1, Nix2;
    public MeshRenderer FreeCheckLED;
    private MeshRenderer LED;

    private int[] StageOrdering;
    private int[] Solution;

    private int[][] DialDisplay;
    private int[]  NixieDisplay;
    private int[][]  LEDDisplay;

    private EvilFMNSettings SettingData;
    public class EvilFMNSettings {
        public bool highVis = false;             // If true, dials are white on black
        public float scale = 1;                  // Scaling of dials
        public bool advanceWithKey = false;      // If true, stage repeating advances on submit instead of per 4s
        public bool reverseDialControls = false; // If true, flips dial controls
        public int stageRandomForward = 10;      // How many solves ahead a stage can appear (solve 20 can appear as late as 30)
        public int stageRandomBackward = 5;      // How many solves behind a stage can appear (solve 20 can appear as early as 15)
    }

    private static bool VALIDATED = false;
    void DoSettings() {
        SettingData = new EvilFMNSettings();
        if (Settings.SettingsPath.Trim() != "") {
            string path = Settings.SettingsPath;
            System.IO.StreamReader fIn = new System.IO.StreamReader(path);
            JsonUtility.FromJsonOverwrite(fIn.ReadToEnd(), SettingData);
            fIn.Close();
            if (!VALIDATED) {
                System.IO.StreamWriter fOut = new System.IO.StreamWriter(path, false);
                fOut.Write(JsonUtility.ToJson(SettingData, true));
                fOut.Flush();
                fOut.Close();
                VALIDATED = true;
            }
        }
        else JsonUtility.FromJsonOverwrite(Settings.Settings, SettingData);

        if(SettingData.scale != 2) {
            Transform tr = transform.Find("Dial Container").transform;
            float realScalar = SettingData.scale; //temporary to allow manipulating it
            tr.localScale *= realScalar;
            realScalar--;
            tr.localPosition = new Vector3(tr.localPosition.x - realScalar * 0.065f, tr.localPosition.y, tr.localPosition.z);
        }

        KMColorblindMode cb = GetComponent<KMColorblindMode>();
        if (cb != null && !cb.ColorblindModeActive) {
            // is this bad?
            LEDDials[0].gameObject.transform.localScale = new Vector3(0,0,0);
            LEDDials[1].gameObject.transform.localScale = new Vector3(0,0,0);
            LEDDials[2].gameObject.transform.localScale = new Vector3(0,0,0);
            LED.GetComponent<MeshFilter>().mesh = StandardMesh;
        }
        else LED.GetComponent<MeshFilter>().mesh = ColourblindMesh;
        if(SettingData.highVis) {
            LEDDials[0].GetComponent<MeshRenderer>().material = SmallHighVisMat;
            LEDDials[1].GetComponent<MeshRenderer>().material = SmallHighVisMat;
            LEDDials[2].GetComponent<MeshRenderer>().material = SmallHighVisMat;
        }
    }

    void Awake()
    {
        if (ignoredModules == null) {
            ignoredModules = GetComponent<KMBossModule>().GetIgnoredModules("Forget Everything", new string[]{
                "14",
                "42",
                "501",
                "A>N<D",
                "Bamboozling Time Keeper",
                "Black Arrows",
                "Brainf---",
                "Busy Beaver",
                "Don't Touch Anything",
                "Floor Lights",
                "Forget Any Color",
                "Forget Enigma",
                "Forget Everything",
                "Forget Infinity",
                "Forget It Not",
                "Forget Maze Not",
                "Forget Me Later",
                "Forget Me Not",
                "Forget Perspective",
                "Forget The Colors",
                "Forget Them All",
                "Forget This",
                "Forget Us Not",
                "Iconic",
                "Keypad Directionality",
                "Kugelblitz",
                "Multitask",
                "OmegaDestroyer",
                "OmegaForest",
                "Organization",
                "Password Destroyer",
                "Purgatory",
                "RPS Judging",
                "Security Council",
                "Shoddy Chess",
                "Simon Forgets",
                "Simon's Stages",
                "Souvenir",
                "Tallordered Keys",
                "The Time Keeper",
                "Timing is Everything",
                "The Troll",
                "Turn The Key",
                "The Twin",
                "Übermodule",
                "Ultimate Custom Night",
                "The Very Annoying Button",
                "Whiteout"
            });
        }

        LED = transform.Find("Lights").GetComponent<MeshRenderer>();
        DoSettings();

        if(LED_INTENSITY == null) {
            LED_INTENSITY = new Color[STAGE_CHECK_FIDELITY];
            for(int a = 0; a < STAGE_CHECK_FIDELITY; a++) LED_INTENSITY[a] = LED_COLS[0] * (STAGE_CHECK_FIDELITY-a) / STAGE_CHECK_FIDELITY;
        }

        thisLoggingID = loggingID++;
        
        transform.Find("Background").GetComponent<MeshRenderer>().material.color = new Color(0.3f, 0.3f, 0.3f);

        Dials = new GameObject[10];
        DialLED = new MeshRenderer[10];
        for(int a = 0; a < 10; a++) {
            Dials[a] = DialContainer.transform.Find("Dial " + (a+1)).gameObject;
            DialLED[a] = DialContainer.transform.Find("Dial LED " + (a+1)).GetComponent<MeshRenderer>();
            DialLED[a].material.color = LED_OFF;

            if(SettingData.highVis) Dials[a].GetComponent<MeshRenderer>().material = HighVisMat;

            int a2 = a;
            Transform o = DialContainer.transform.Find("Dial " + (a+1) + " Increment");
            if(SettingData.reverseDialControls) {
                o.localPosition = new Vector3(-o.localPosition.x, o.localPosition.y, o.localPosition.z);
                o.localEulerAngles = new Vector3(-o.localEulerAngles.x, o.localEulerAngles.y, o.localEulerAngles.z);
            }
            o.GetComponent<KMSelectable>().OnInteract += delegate() {
                Handle(a2, true);
                return false;
            };
            o = DialContainer.transform.Find("Dial " + (a+1) + " Decrement");
            if(SettingData.reverseDialControls) {
                o.localPosition = new Vector3(-o.localPosition.x, o.localPosition.y, o.localPosition.z);
                o.localEulerAngles = new Vector3(-o.localEulerAngles.x, o.localEulerAngles.y, o.localEulerAngles.z);
            }
            o.GetComponent<KMSelectable>().OnInteract += delegate() {
                Handle(a2, false);
                return false;
            };
        }

        MeshRenderer mr = transform.Find("Display").Find("Wiring").GetComponent<MeshRenderer>();
        mr.materials[2].color = new Color(0.1f, 0.1f, 0.1f);
        mr.materials[1].color = new Color(0.3f, 0.3f, 0.3f);
        mr.materials[0].color = new Color(0.1f, 0.4f, 0.8f);

        LED.materials[0].color = new Color(0.3f, 0.3f, 0.3f);
        LED.materials[1].color = LED_OFF;
        LED.materials[2].color = LED_OFF;
        LED.materials[3].color = LED_OFF;

        transform.Find("Display").Find("Edge").GetComponent<MeshRenderer>().material.color = new Color(0, 0, 0);
        DialContainer.transform.Find("Base").GetComponent<MeshRenderer>().material.color = new Color(0.3f, 0.3f, 0.3f);
        Submit.GetComponent<MeshRenderer>().material.color = new Color(0.8f, 0.8f, 0.2f);

        Text.text = "";

        GetComponent<KMBombModule>().OnActivate += ActivateModule;
        Submit.OnInteract += HandleSubmit;
    }

    private string niceCols(int[] list) {
        string resp = "";
        foreach(int i in list) {
            if(i == 0)      resp += "R";
            else if(i == 1) resp += "Y";
            else if(i == 2) resp += "G";
            else            resp += "B";
        }
        return resp;
    }

    private void ActivateModule()
    {
        int count = BombInfo.GetSolvableModuleNames().Where(x => !ignoredModules.Contains(x)).Count() + EXTRA_STAGES;
        if(count == 0) { //Prevent deadlock
            Debug.Log("[Forget Everything #"+thisLoggingID+"] No valid stage modules, auto-solving.");
            GetComponent<KMBombModule>().HandlePass();
            ShowNumber(new int[]{0,1,2,3,4,5,6,7,8,9});
            done = true;
            return;
        }
        if(count > 99) { //More than 99 stages will cause issues as the stage display only has 2 digits
            Debug.Log("[Forget Everything #"+thisLoggingID+"] More than 99 stages, capping at 99.");
            count = 99;
        }
        else Debug.Log("[Forget Everything #"+thisLoggingID+"] Stages: " + count);

        StageOrdering = new int[count];
        List<int> stages = new List<int>();
        for(int a = 0; a < count; a++) stages.Add(a);

        DialDisplay  = new int[count][];
        NixieDisplay = new int[count];
        LEDDisplay   = new int[count][];

        Solution = new int[10];

        int opCount = 1;
        int stageCounter = 1;
        for(int a = 0; a < count; a++) {
            int min = a-SettingData.stageRandomForward;
            List<int> availStages = stages.Where(it => it <= a+SettingData.stageRandomBackward && it >= a-SettingData.stageRandomForward).ToList();
            if (availStages.Count == 0) {
                // This should never happen, but just in case...
                Debug.Log("[Forget Everything #"+thisLoggingID+"] WARN: Stage " + (a+1).ToString("D2") + " was forced to pick outside the config-specified range!");
                int nearLow = -1, nearHigh = -1;
                stages.ForEach(it => {
                    if (it < a) nearLow = Mathf.Max(nearLow, it);
                    else nearHigh = Mathf.Min(nearHigh, it);
                });
                if (nearLow != -1) availStages.Add(nearLow);
                if (nearHigh != -1) availStages.Add(nearHigh);
            }
            int p = availStages[Random.Range(0, availStages.Count)];
            if (availStages.Contains(min)) p = min;
            StageOrdering[a] = p;
            stages.Remove(p);

            DialDisplay[a] = new int[10];
            for(int b = 0; b < 10; b++) DialDisplay[a][b] = Random.Range(0, 10);
            NixieDisplay[a] = Random.Range(0, 100);
            LEDDisplay[a] = new int[3];
            for(int b = 0; b < 3; b++) LEDDisplay[a][b] = Random.Range(0, 4);

            if(a == 0) {
                //First stage, solution starts as the shown dials and the nixie tubes are ignored.
                string ans = "";
                for(int b = 0; b < 10; b++) {
                    Solution[b] = DialDisplay[0][b];
                    ans += Solution[b];
                }
                Debug.Log("[Forget Everything #"+thisLoggingID+"] Initial answer (stage 1 display): " + ans);
            }
            else {
                //Work out if the stage matters or not
                int n1 = NixieDisplay[a] / 10, n2 = NixieDisplay[a] % 10;

                bool found = false;
                if(stageCounter <= -2) found = true; //A stage is always valid if the last two were invalid
                else if(stageCounter < 2) {          //A stage is always invalid if the last two were valid
                    for(int b = 0; b < 10; b++) {    //A stage is valid if both nixie numbers are on the dials
                        if(DialDisplay[a][b] == n1) {
                            found = true;
                            break;
                        }
                    }
                    if(found && n1 != n2) {
                        found = false;
                        for(int b = 0; b < 10; b++) {
                            if(DialDisplay[a][b] == n2) {
                                found = true;
                                break;
                            }
                        }
                    }
                }

                if(found) {
                    //This stage is important
                    opCount++;
                    if(stageCounter <= 0) stageCounter = 1;
                    else stageCounter++;

                    //Determine primary colour (the one missing, or the most frequent)
                    int primaryCol;
                    bool colFromMissing = false;
                    if(LEDDisplay[a][0] == LEDDisplay[a][1] || LEDDisplay[a][0] == LEDDisplay[a][2]) primaryCol = LEDDisplay[a][0];
                    else if (LEDDisplay[a][1] == LEDDisplay[a][2]) primaryCol = LEDDisplay[a][1];
                    else {
                        colFromMissing = true;
                             if(LEDDisplay[a][0] != 0 && LEDDisplay[a][1] != 0 && LEDDisplay[a][2] != 0) primaryCol = 0;
                        else if(LEDDisplay[a][0] != 1 && LEDDisplay[a][1] != 1 && LEDDisplay[a][2] != 1) primaryCol = 1;
                        else if(LEDDisplay[a][0] != 2 && LEDDisplay[a][1] != 2 && LEDDisplay[a][2] != 2) primaryCol = 2;
                        else                                                                             primaryCol = 3;
                    }
                    string colName;
                    int dial = a % 10;
                    if(primaryCol == 0) {
                        colName = "Red    (a+b)";
                        Solution[dial] = (Solution[dial] + DialDisplay[a][dial]) % 10;
                    }
                    else if(primaryCol == 1) {
                        colName = "Yellow (a-b)";
                        Solution[dial] = (Solution[dial] - DialDisplay[a][dial] + 10) % 10;
                    }
                    else if(primaryCol == 2) {
                        colName = "Green(a+b+5)";
                        Solution[dial] = (Solution[dial] + DialDisplay[a][dial] + 5) % 10;
                    }
                    else {
                        colName = "Blue   (b-a)";
                        Solution[dial] = (DialDisplay[a][dial] - Solution[dial] + 10) % 10;
                    }

                    string ans = "", disp = "";
                    for(int b = 0; b < 10; b++) {
                        ans += Solution[b];
                        disp += DialDisplay[a][b];
                    }
                    Debug.Log("[Forget Everything #"+thisLoggingID+"] Stage " + (a+1).ToString("D2") + " - Display: " + disp + ", Tubes: " + n1+n2 + ", Colours: " + niceCols(LEDDisplay[a]) + " " + colName + " " + (colFromMissing ? "(missing colour)" : " (most frequent)") + ", New answer: " + ans);
                }
                else {
                    if(stageCounter >= 0) stageCounter = -1;
                    else stageCounter--;
                    string disp = "";
                    for(int b = 0; b < 10; b++) {
                        disp += DialDisplay[a][b];
                    }
                    Debug.Log("[Forget Everything #"+thisLoggingID+"] Stage " + (a+1).ToString("D2") + " - Display: " + disp + ", Tubes: " + n1+n2 + ", Not important.");
                }
            }
        }
        Debug.Log("[Forget Everything #"+thisLoggingID+"] Stage order: " + StageOrdering.ToList().ConvertAll(it => (it+1).ToString("D2")).Join(","));
        Debug.Log("[Forget Everything #"+thisLoggingID+"] Total important stages: " + opCount);
        string ans2 = "";
        for(int b = 0; b < 10; b++) ans2 += Solution[b];
        Debug.Log("[Forget Everything #"+thisLoggingID+"] Final answer: " + ans2);

        if(PLAYTEST_MODE_SKIP) displayCurStage = StageOrdering.Length;
    }

    int ticker = 0, displayOverride = -1;
    bool done = false;

    int displayCurStage = 0;
    const float PER_LED_TIME = 0.1f;
    float displayTimer = 3, flourishTimer = PER_LED_TIME * 10;
    bool freeCheckActive = false;
    void FixedUpdate()
    {
        if(freeCheckActive) FreeCheckLED.material.color = LED_INTENSITY[STAGE_CHECK_FIDELITY-(int)(BombInfo.GetTime()*STAGE_CHECK_FIDELITY)%STAGE_CHECK_FIDELITY-1];
        else FreeCheckLED.material.color = LED_OFF;

        if(done) {
            flourishTimer = (flourishTimer + Time.fixedDeltaTime) % (PER_LED_TIME * 20);
            if(flourishTimer >= PER_LED_TIME * 10) {
                float time = flourishTimer - PER_LED_TIME * 10;
                for(int a = 0; a < 10; a++) {
                    if(time >= PER_LED_TIME * a) DialLED[a].material.color = LED_OFF;
                    else DialLED[a].material.color = LED_COLS[2];
                }
            }
            else {
                for(int a = 0; a < 10; a++) {
                    if(flourishTimer >= PER_LED_TIME * a) DialLED[a].material.color = LED_COLS[2];
                    else DialLED[a].material.color = LED_OFF;
                }
            }
        }

        if(done || StageOrdering == null) return;
        if(displayTimer > 0) displayTimer -= Time.fixedDeltaTime;

        if(displayOverride != -1 && !SettingData.advanceWithKey) {
            if(displayTimer <= 0) {
                displayTimer = STAGE_DELAY;
                displayOverride += 10;
                if(displayOverride >= StageOrdering.Length) displayOverride = -1;
            }
        }

        ticker++;
        if(ticker == 15)
        {
            ticker = 0;
            int progress = BombInfo.GetSolvedModuleNames().Where(x => !ignoredModules.Contains(x)).Count() + EXTRA_STAGES;
            if(progress > displayCurStage) {
                if(!PLAYTEST_MODE && displayTimer <= 0) {
                    displayTimer = STAGE_DELAY;
                    displayCurStage++;
                }
                progress = displayCurStage;
            }
            if(progress >= StageOrdering.Length && displayOverride == -1) {
                //Ready to solve
                if(!Text.text.Equals("--")) ShowNumber(new int[]{0,0,0,0,0,0,0,0,0,0});
                Text.text = "--";
                Nix1.SetValue(-1);
                Nix2.SetValue(-1);
                LED.materials[1].color = LED_OFF;
                LEDDials[0].Move(0);
                LED.materials[2].color = LED_OFF;
                LEDDials[1].Move(0);
                LED.materials[3].color = LED_OFF;
                LEDDials[2].Move(0);
            }
            else {
                //Showing stages
                int stage;
                if(displayOverride != -1) stage = displayOverride;
                else stage = StageOrdering[progress];

                Text.text = (stage+1).ToString("D2");
                Nix1.SetValue(NixieDisplay[stage] / 10);
                Nix2.SetValue(NixieDisplay[stage] % 10);
                ShowNumber(DialDisplay[stage]);
                LED.materials[1].color = LED_COLS[LEDDisplay[stage][0]];
                LEDDials[0].Move(LEDDisplay[stage][0]);
                LED.materials[2].color = LED_COLS[LEDDisplay[stage][1]];
                LEDDials[1].Move(LEDDisplay[stage][1]);
                LED.materials[3].color = LED_COLS[LEDDisplay[stage][2]];
                LEDDials[2].Move(LEDDisplay[stage][2]);
            }
        }
    }

    private void Handle(int val, bool increment) {
        Submit.AddInteractionPunch(0.1f);
        Sound.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, transform);
        if(done || StageOrdering == null || doingSolve) return;
        
        if(LEDactive) {
            for(int a = 0; a < 10; a++) DialLED[a].material.color = LED_OFF;
            LEDactive = false;
        }
        
        if(displayCurStage < StageOrdering.Length) {
            Debug.Log("[Forget Everything #"+thisLoggingID+"] Tried to turn a dial too early.");
            GetComponent<KMBombModule>().HandleStrike();
            return;
        }
        
        displayOverride = -1;
        if(increment) Dials[val].GetComponent<Dial>().Increment();
        else          Dials[val].GetComponent<Dial>().Decrement();
    }

    private bool HandleSubmit() {
        Submit.GetComponent<KMSelectable>().AddInteractionPunch(0.5f);
        Sound.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);

        if (displayOverride != -1 && SettingData.advanceWithKey) {
            displayOverride += 10;
            if(displayOverride >= StageOrdering.Length) displayOverride = -1;
            return false;
        }

        displayOverride = -1;
        if(done || StageOrdering == null || doingSolve) return false;

        if(PLAYTEST_MODE) {
            int progress = BombInfo.GetSolvedModuleNames().Where(x => !ignoredModules.Contains(x)).Count() + EXTRA_STAGES;
            if(displayCurStage < progress) {
                displayCurStage++;
                return false;
            }
        }
        
        if(LEDactive) {
            for(int a = 0; a < 10; a++) DialLED[a].material.color = LED_OFF;
            LEDactive = false;
        }

        if(displayCurStage < StageOrdering.Length) {
            Debug.Log("[Forget Everything #"+thisLoggingID+"] Tried to submit an answer too early.");
            GetComponent<KMBombModule>().HandleStrike();
            return false;
        }

        for(int a = 0; a < 10; a++) {
            if(Dials[a].GetComponent<Dial>().GetValue() == -1) {
                Debug.Log("[Forget Everything #"+thisLoggingID+"] Tried to submit whilst dials are spinning.");
                GetComponent<KMBombModule>().HandleStrike();
                return false;
            }
        }

        Submit.transform.localRotation = Quaternion.Euler(0, 115, 0);

        doingSolve = true;
        StartCoroutine(SolveAnim());

        return false;
    }

    private bool doingSolve = false, LEDactive = false;
    private IEnumerator SolveAnim() {
        List<int> slots = new List<int>();
        for(int a = 0; a < 10; a++) slots.Add(a);

        bool correct = true;
        string[] ans = new string[10];
        while(slots.Count > 0) {
            yield return new WaitForSeconds(PER_LED_TIME);
            //int p = Random.Range(0, slots.Count);
            int p = 0;
            int slot = slots[p];
            slots.RemoveAt(p);

            int val = Dials[slot].GetComponent<Dial>().GetValue();
            ans[slot] = ""+val;
            if(val == Solution[slot]) {
                DialLED[slot].material.color = LED_COLS[2]; //green
            }
            else {
                correct = false;
                DialLED[slot].material.color = LED_COLS[0]; //red
            }
        }

        if(correct) {
            Debug.Log("[Forget Everything #"+thisLoggingID+"] Module solved.");
            GetComponent<KMBombModule>().HandlePass();
            Sound.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, transform);
            Submit.GetComponent<KMSelectable>().AddInteractionPunch(0.25f);
            Submit.transform.localRotation = Quaternion.Euler(0, 140, 0);
            done = true;
        }
        else {
            int dispOver = -1;
            bool wantsOver = true;
            for(int a = 0; a < 10; a++) {
                int v = Dials[a].GetComponent<Dial>().GetValue();
                if(v == 1) {
                    if(dispOver == -1) dispOver = a;
                    else {
                        wantsOver = false;
                        break;
                    }
                }
                else if(v != 0) {
                    wantsOver = false;
                    break;
                }
            }
            if(dispOver == -1) wantsOver = false;
            if(wantsOver) {
                displayOverride = dispOver;
                displayTimer = STAGE_DELAY;
                Debug.Log("[Forget Everything #"+thisLoggingID+"] Position check override in exchange for strike: Position " + (dispOver+1));
                for(int a = 0; a < 10; a++) DialLED[a].material.color = LED_OFF;
            }
            else {
                //If you've noticed this, keep it a secret.
                //Yes, that means you, Mr. Repository Examiner!

                string str = BombInfo.GetFormattedTime().Replace(":", "");
                wantsOver = true;
                for(int a = 0; a < 10; a++) {
                    if(a < str.Length) {
                        if(Dials[a].GetComponent<Dial>().GetValue() != str[a] - '0') {
                            wantsOver = false;
                            break;
                        }
                    }
                    else if(Dials[a].GetComponent<Dial>().GetValue() != 0) {
                        wantsOver = false;
                        break;
                    }
                }
                
                if(wantsOver) {
                    Debug.Log("[Forget Everything #"+thisLoggingID+"] Has someone been poking around in the repository?");
                    Debug.Log("[Forget Everything #"+thisLoggingID+"] Showing all states in order...hope you can write it down quickly!");

                    //Remove order shuffle
                    for(int a = 0; a < StageOrdering.Length; a++) StageOrdering[a] = a;

                    //Reset display
                    displayCurStage = 0;
                    displayTimer = 3;

                    //Avoid strike, because it's a secret...
                    doingSolve = false;
                    for(int a = 0; a < 10; a++) DialLED[a].material.color = LED_OFF;
                    Submit.transform.localRotation = Quaternion.Euler(0, 90, 0);
                    Submit.GetComponent<KMSelectable>().AddInteractionPunch(0.25f);
                    Sound.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, transform);
                    yield break;
                }
                else {
                    Debug.Log("[Forget Everything #"+thisLoggingID+"] Incorrect answer: " + string.Join("", ans));
                    if(!freeCheckActive) Debug.Log("[Forget Everything #"+thisLoggingID+"] Free check added.");
                    freeCheckActive = true;
                }
            }
            if(freeCheckActive && displayOverride != -1) {
                //We had a free check, and a stage is being shown
                freeCheckActive = false;
                Sound.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, transform);
                Debug.Log("[Forget Everything #"+thisLoggingID+"] Free check was available, consuming it.");
            }
            else {
                if (displayOverride != -1) {
                    Debug.Log("[Forget Everything #"+thisLoggingID+"] Free check was NOT available, striking and adding it.");
                    freeCheckActive = true; //Took a strike for a check, award an extra
                }
                GetComponent<KMBombModule>().HandleStrike();
            }
            Submit.transform.localRotation = Quaternion.Euler(0, 90, 0);
            Submit.GetComponent<KMSelectable>().AddInteractionPunch(0.25f);
            LEDactive = true;
        }

        doingSolve = false;
    }

    private void ShowNumber(int[] nums) {
        for(int a = 0; a < 10; a++) {
            Dials[a].GetComponent<Dial>().Move(nums[a]);
        }
    }

    //Twitch Plays support

    #pragma warning disable 0414
    string TwitchHelpMessage = "Submit answers with 'submit 1234567890'. Request position checks with 'submit 0000010000'. Advance position checks with 'advance' (requires config option). Toggle colourblind mode with 'colourblind'.";
    #pragma warning restore 0414

    public void TwitchHandleForcedSolve() {
        Debug.Log("[Forget Everything #"+thisLoggingID+"] Module forcibly solved.");
        ShowNumber(Solution);
        Submit.GetComponent<KMSelectable>().AddInteractionPunch(0.5f);
        Submit.transform.localRotation = Quaternion.Euler(0, 140, 0);
        Sound.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
        GetComponent<KMBombModule>().HandlePass();
        done = true;
    }

    public IEnumerator ProcessTwitchCommand(string cmd) {
        cmd = cmd.ToLowerInvariant();
        if(cmd.Equals("colourblind") || cmd.Equals("colorblind") || cmd.Equals("blind") || cmd.Equals("useyourhumaneyes")) {
            if (LEDDials[0].gameObject.transform.localScale.x == 0) {
                LEDDials[0].gameObject.transform.localScale = new Vector3(2.25f,2.25f,2.25f);
                LEDDials[1].gameObject.transform.localScale = new Vector3(2.25f,2.25f,2.25f);
                LEDDials[2].gameObject.transform.localScale = new Vector3(2.25f,2.25f,2.25f);
                LED.GetComponent<MeshFilter>().mesh = ColourblindMesh;
            }
            else {
                LEDDials[0].gameObject.transform.localScale = new Vector3(0,0,0);
                LEDDials[1].gameObject.transform.localScale = new Vector3(0,0,0);
                LEDDials[2].gameObject.transform.localScale = new Vector3(0,0,0);
                LED.GetComponent<MeshFilter>().mesh = StandardMesh;
            }
        }
        if(cmd.Equals("advance") || cmd.Equals("next")) {
            if (!SettingData.advanceWithKey) {
                yield return "sendtochaterror Advancing with key is disabled.";
                yield break;
            }
            yield return "Forget Everything";
            HandleSubmit();
            yield break;
        }
        if(cmd.StartsWith("press ") || cmd.StartsWith("submit ")) {
            cmd = cmd.Substring(cmd.IndexOf(' ')+1);
            if(cmd.Length == 10) {
                int[] values = new int[10];
                for(int a = 0; a < 10; a++) {
                    int v = cmd[a] - '0';
                    if(v < 0 || v > 9) {
                        yield return "sendtochaterror Unknown character: " + cmd[a];
                        yield break;
                    }
                    values[a] = v;
                }

                yield return "Forget Everything";
                ShowNumber(values);
                for(int a = 0; a < 10; a++) {
                    while(Dials[a].GetComponent<Dial>().GetValue() == -1) yield return new WaitForSeconds(0.1f);
                }
                HandleSubmit();
                yield break;
            }
            yield return "sendtochaterror Answers need 10 digits.";
            yield break;
        }
        yield return "sendtochaterror Commands must start with 'submit'.";
        yield break;
    }
}