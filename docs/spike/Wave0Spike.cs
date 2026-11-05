// Wave0Spike.cs - throwaway measurement script for the Wave 0 engine-assumption spike.
// Drop this single file into an empty URP 2D project, attach Wave0Spike to any GameObject,
// press Play, and filter the Console by "[SPIKE]". It measures U1, U2, U2b, U23, U5, U7, U8,
// U9/U30, U11, U31, U32, U35, U36 and prints one "[SPIKE][Uxx] RESULT=YES|NO|UNKNOWN detail=..." line
// per item, then "[SPIKE][DONE]". It is NOT implementation code and must not be shipped.
//
// Only UnityEngine (no Input System, no Newtonsoft) is used so it compiles in a fresh project.
// Lines prefixed "[SPIKE-TRACE]" are verbose traces; they do not match a "[SPIKE]" filter.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

// ---------------------------------------------------------------------------------------------
// Shared clock. Runs first in every callback so every other component can read a consistent
// "global step / step-in-frame / frame index" without depending on Time.frameCount semantics.
// ---------------------------------------------------------------------------------------------
[DefaultExecutionOrder(-10000)]
public class SpikeClock : MonoBehaviour
{
    // Global FixedUpdate counter since Play started (1-based after the first step).
    public static int Step;
    // 1-based index of the current FixedUpdate inside the current frame.
    public static int StepInFrame;
    // Number of frames whose Update has already completed; the frame currently running has this index.
    public static int FrameIndex;
    // Number of FixedUpdates that ran in the frame whose Update just completed.
    public static int StepsInLastFrame;

    private int stepsSinceLastUpdate;

    private void FixedUpdate()
    {
        Step++;
        stepsSinceLastUpdate++;
        StepInFrame = stepsSinceLastUpdate;
    }

    private void Update()
    {
        StepsInLastFrame = stepsSinceLastUpdate;
        stepsSinceLastUpdate = 0;
        StepInFrame = 0;
        FrameIndex++;
    }
}

// ---------------------------------------------------------------------------------------------
// U1 probes: two classes with different DefaultExecutionOrder, two instances each.
// ---------------------------------------------------------------------------------------------
[DefaultExecutionOrder(-100)]
public class SpikeOrderEarly : MonoBehaviour
{
    public string Tag = "E?";
    private void FixedUpdate() { Wave0Spike.RecordOrder(Tag); }
}

[DefaultExecutionOrder(100)]
public class SpikeOrderLate : MonoBehaviour
{
    public string Tag = "L?";
    private void FixedUpdate() { Wave0Spike.RecordOrder(Tag); }
}

// ---------------------------------------------------------------------------------------------
// Trigger probe attached to both physics bodies. Forwards callbacks with timing context.
// ---------------------------------------------------------------------------------------------
public class SpikeTriggerProbe : MonoBehaviour
{
    public string Label = "?";
    private void OnTriggerEnter2D(Collider2D other) { Wave0Spike.RecordTrigger("Enter", Label, other); }
    private void OnTriggerExit2D(Collider2D other) { Wave0Spike.RecordTrigger("Exit", Label, other); }
}

// ---------------------------------------------------------------------------------------------
// U31 probe: default execution order (0). Created or enabled from inside the driver's
// FixedUpdate (order 1000) during a multi-step frame; records when it is first called.
// ---------------------------------------------------------------------------------------------
public class SpikeLateStarter : MonoBehaviour
{
    public string Tag = "?";
    public readonly List<string> Calls = new List<string>();
    public int FirstFrame = -1;
    public int FirstStepInFrame = -1;
    public int FirstStep = -1;

    private void FixedUpdate()
    {
        if (Calls.Count >= 6) return;
        if (FirstFrame < 0)
        {
            FirstFrame = SpikeClock.FrameIndex;
            FirstStepInFrame = SpikeClock.StepInFrame;
            FirstStep = SpikeClock.Step;
        }
        Calls.Add("f" + SpikeClock.FrameIndex + "/s" + SpikeClock.StepInFrame);
    }
}

// ---------------------------------------------------------------------------------------------
// Driver. Runs last (order 1000) so it observes everything else in the same step, calls
// Physics2D.Simulate once per FixedUpdate (simulationMode = Script), and advances a
// step/frame-driven scenario state machine. No WaitForSeconds anywhere.
// ---------------------------------------------------------------------------------------------
[DefaultExecutionOrder(1000)]
public class Wave0Spike : MonoBehaviour
{
    private enum Phase { Warmup, U1, U2, U2b, U23, U31, U5, U8, U9, U11, U32, U35, Done }

    private struct TriggerEvent
    {
        public string Kind;      // "Enter" / "Exit"
        public string Self;      // label of the body receiving the callback
        public string Other;     // name of the other collider
        public int Step;         // SpikeClock.Step at callback time
        public int Frame;        // SpikeClock.FrameIndex at callback time
        public bool InsideSimulate;
    }

    // --- static hooks used by the probe classes ---
    public static bool InsideSimulate;
    // Set once "[SPIKE][DONE]" has been logged; polled by the PlayMode test wrapper.
    public static bool IsDone;
    // Current phase name, for the wrapper's timeout message.
    public static string PhaseName = "NotStarted";
    private static readonly List<string> orderLog = new List<string>();
    private static readonly List<TriggerEvent> events = new List<TriggerEvent>();

    public static void RecordOrder(string tag) { orderLog.Add(tag); }

    public static void RecordTrigger(string kind, string self, Collider2D other)
    {
        var e = new TriggerEvent
        {
            Kind = kind,
            Self = self,
            Other = other != null ? other.name : "null",
            Step = SpikeClock.Step,
            Frame = SpikeClock.FrameIndex,
            InsideSimulate = InsideSimulate,
        };
        events.Add(e);
        Debug.Log("[SPIKE-TRACE][trigger] " + kind + " on " + self + " other=" + e.Other
                  + " step=" + e.Step + " frame=" + e.Frame + " insideSimulate=" + e.InsideSimulate);
    }

    // --- scene objects ---
    private Rigidbody2D rbA, rbB;
    private CircleCollider2D colA, colB;
    private SpikeLateStarter preStarter;   // created disabled in Awake, enabled during U31
    private SpikeLateStarter newStarter;   // created during U31
    private readonly List<Collider2D> queryBuf = new List<Collider2D>(16);

    // --- state machine ---
    private Phase phase = Phase.Warmup;
    private int phaseStep;     // FixedUpdates since the phase began
    private int phaseFrame;    // Updates since the phase began
    private Action afterSimulate;  // one-shot hook run right after Physics2D.Simulate in the same step

    // --- collected output ---
    private readonly List<string> spikeLines = new List<string>();
    private readonly List<string> resultLines = new List<string>();
    private string overlay = "";
    private GUIStyle overlayStyle;

    private const float Radius = 0.5f;
    private static readonly Vector2 FarB = new Vector2(10f, 0f);
    private static readonly Vector2 OverlapB = new Vector2(0.5f, 0f);

    private static string F(float v) { return v.ToString("R", CultureInfo.InvariantCulture); }
    private static string F(Vector2 v) { return "(" + F(v.x) + "," + F(v.y) + ")"; }

    private void Log(string line)
    {
        Debug.Log(line);
        spikeLines.Add(line);
        if (line.Contains("RESULT=")) resultLines.Add(line);
        RefreshOverlay();
    }

    private static void Trace(string line) { Debug.Log("[SPIKE-TRACE] " + line); }

    private void RefreshOverlay()
    {
        var sb = new StringBuilder();
        int start = Mathf.Max(0, spikeLines.Count - 18);
        for (int i = start; i < spikeLines.Count; i++) sb.AppendLine(spikeLines[i]);
        overlay = sb.ToString();
    }

    private void Advance(Phase next)
    {
        Trace("phase " + phase + " -> " + next + " at step=" + SpikeClock.Step + " frame=" + SpikeClock.FrameIndex);
        phase = next;
        PhaseName = next.ToString();
        phaseStep = 0;
        phaseFrame = 0;
        afterSimulate = null;
    }

    // ------------------------------------------------------------------ setup

    private void Awake()
    {
        IsDone = false;
        PhaseName = Phase.Warmup.ToString();
        InsideSimulate = false;
        orderLog.Clear();
        events.Clear();

        // Physics settings exactly as the implementation design assumes (tech/5.09-collision.md).
        var prevMode = Physics2D.simulationMode;
        Physics2D.simulationMode = SimulationMode2D.Script;
        Physics2D.callbacksOnDisable = false;
        Physics2D.queriesHitTriggers = true;
        Physics2D.autoSyncTransforms = false;
        // Must be set BEFORE colliders are created: it is the offset newly created colliders receive.
        Physics2D.defaultContactOffset = 0.0001f;
        float offsetReadback = Physics2D.defaultContactOffset;

        Time.fixedDeltaTime = 1f / 60f;
        Time.maximumDeltaTime = 0.05f;
        Time.captureDeltaTime = 1f / 60f;   // deterministic frame length while measuring

        new GameObject("SpikeClock").AddComponent<SpikeClock>();

        // U1 probes, created in this order: E1, E2, L1, L2.
        new GameObject("U1_Early_1").AddComponent<SpikeOrderEarly>().Tag = "E1";
        new GameObject("U1_Early_2").AddComponent<SpikeOrderEarly>().Tag = "E2";
        new GameObject("U1_Late_1").AddComponent<SpikeOrderLate>().Tag = "L1";
        new GameObject("U1_Late_2").AddComponent<SpikeOrderLate>().Tag = "L2";

        CreateBody("SpikeBodyA", Vector2.zero, out rbA, out colA);
        CreateBody("SpikeBodyB", FarB, out rbB, out colB);

        // U31 pre-created, disabled component.
        var pre = new GameObject("U31_PreCreated");
        preStarter = pre.AddComponent<SpikeLateStarter>();
        preStarter.Tag = "pre";
        preStarter.enabled = false;

        Log("[SPIKE][ENV] unity=" + Application.unityVersion + " platform=" + Application.platform
            + " isEditor=" + Application.isEditor + " isBatchMode=" + Application.isBatchMode
            + " simulationMode(prev=" + prevMode + ", now=" + Physics2D.simulationMode + ")"
            + " callbacksOnDisable=" + Physics2D.callbacksOnDisable
            + " queriesHitTriggers=" + Physics2D.queriesHitTriggers
            + " autoSyncTransforms=" + Physics2D.autoSyncTransforms
            + " fixedDeltaTime=" + F(Time.fixedDeltaTime)
            + " maximumDeltaTime=" + F(Time.maximumDeltaTime)
            + " captureDeltaTime=" + F(Time.captureDeltaTime));

        bool offsetOk = Mathf.Abs(offsetReadback - 0.0001f) < 1e-7f;
        Log("[SPIKE][U30] RESULT=" + (offsetOk ? "YES" : "NO")
            + " detail=defaultContactOffset set=0.0001 readback=" + F(offsetReadback)
            + " (YES = 설정한 값이 그대로 읽힘)");
    }

    private static void CreateBody(string name, Vector2 pos, out Rigidbody2D rb, out CircleCollider2D col)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        rb.gravityScale = 0f;
        rb.interpolation = RigidbodyInterpolation2D.None;
        col = go.AddComponent<CircleCollider2D>();
        col.radius = Radius;
        col.isTrigger = true;
        go.AddComponent<SpikeTriggerProbe>().Label = name == "SpikeBodyA" ? "A" : "B";
    }

    private void Start()
    {
        LogScreenInfo("U35-editor-ref", "(Start 시점 값" + (Application.isEditor ? ", 에디터 값, 참고용" : ", 빌드 값") + ")");
        lastMode = Screen.fullScreenMode;
        lastFullScreen = Screen.fullScreen;
        lastW = Screen.width;
        lastH = Screen.height;
    }

    private static string SafeWindowPosition()
    {
        try { return Screen.mainWindowPosition.ToString(); }
        catch (Exception ex) { return "EXC " + ex.GetType().Name; }
    }

    private void LogScreenInfo(string tag, string note)
    {
        string wa = "n/a";
        string disp = "n/a";
        try
        {
            var di = Screen.mainWindowDisplayInfo;
            wa = "x=" + di.workArea.x + " y=" + di.workArea.y + " w=" + di.workArea.width + " h=" + di.workArea.height;
            disp = di.width + "x" + di.height + " name=" + di.name;
        }
        catch (Exception ex) { wa = "EXC " + ex.GetType().Name; }
        Log("[SPIKE][" + tag + "] " + note
            + " mainWindowDisplayInfo.workArea{" + wa + "} display=" + disp
            + " mainWindowPosition=" + SafeWindowPosition()
            + " Screen.width/height=" + Screen.width + "x" + Screen.height
            + " currentResolution=" + Screen.currentResolution.width + "x" + Screen.currentResolution.height
            + " dpi=" + F(Screen.dpi)
            + " fullScreenMode=" + Screen.fullScreenMode
            + " (workArea 단위는 픽셀로 가정; 모니터 배율 150%라면 1920x1080 모니터에서 w=1920 근처가 나와야 픽셀 단위)");
    }

    // ------------------------------------------------------------------ helpers

    private void SetB(Vector2 pos)
    {
        rbB.linearVelocity = Vector2.zero;
        rbB.position = pos;
    }

    private void ClearEvents() { events.Clear(); }

    private int CountEvents(string kind, string self)
    {
        int n = 0;
        for (int i = 0; i < events.Count; i++)
            if (events[i].Kind == kind && (self == null || events[i].Self == self)) n++;
        return n;
    }

    private bool FirstEvent(string kind, out TriggerEvent e)
    {
        for (int i = 0; i < events.Count; i++)
            if (events[i].Kind == kind) { e = events[i]; return true; }
        e = default(TriggerEvent);
        return false;
    }

    // Returns true when an OverlapCircle query at 'point' (small radius) returns 'target'.
    private bool QueryFinds(Vector2 point, Collider2D target)
    {
        var filter = new ContactFilter2D();
        filter.NoFilter();
        filter.useTriggers = true;
        queryBuf.Clear();
        Physics2D.OverlapCircle(point, 0.05f, filter, queryBuf);
        return queryBuf.Contains(target);
    }

    private static string YN(bool b) { return b ? "YES" : "NO"; }

    // ------------------------------------------------------------------ main loop

    private void FixedUpdate()
    {
        phaseStep++;   // 1-based step index inside the current phase (Advance resets it to 0)
        StepPhase();
        InsideSimulate = true;
        Physics2D.Simulate(Time.fixedDeltaTime);
        InsideSimulate = false;
        if (afterSimulate != null) { var a = afterSimulate; afterSimulate = null; a(); }
        if (phase == Phase.U1) EvaluateOrderStep();
        orderLog.Clear();
    }

    private void Update()
    {
        phaseFrame++;  // 1-based frame index inside the current phase
        FramePhase();
        WatchScreen();
    }

    // ------------------------------------------------------------------ per-item state

    // U1
    private readonly List<string> u1Sequences = new List<string>();
    private bool u1AllOrdered = true;
    private bool u1AllComplete = true;
    // U2
    private readonly List<int> u2Counts = new List<int>();
    // U2b (real stalled frames via Thread.Sleep)
    private readonly List<int> u2bSteps = new List<int>();
    private readonly List<float> u2bDelta = new List<float>();
    private readonly List<float> u2bUnscaled = new List<float>();
    // U23 (timeScale = 3)
    private readonly List<int> u23Steps = new List<int>();
    private float u23MaxDelta, u23MaxUnscaled;
    private int prevTargetFrameRate, prevVSyncCount;
    // U31
    private bool u31Triggered;
    private int u31TriggerFrame = -1, u31TriggerStep = -1, u31StepsInTriggerFrame = -1;
    // U5 / U7
    private int u5Round, u5RoundStart, u5SetStep;
    private readonly bool[] u5RoundOk = new bool[2];
    private readonly string[] u5RoundInfo = new string[2];
    private bool u7Seen, u7Inside, u7SameStep;
    private string u7Detail = "";
    // U8
    private int u8EnterBefore, u8ExitAtDisableCall;
    // U9
    private static readonly float[] U9Cases = { 1.0f, 1.0001f, 0.9999f };
    private int u9Case, u9CaseStart;
    private readonly bool[] u9Enter = new bool[3];
    private readonly string[] u9Info = new string[3];
    // U11
    private static readonly bool[] U11Modes = { false, true };
    private int u11Mode, u11ModeStart;
    private readonly bool[] u11After = new bool[2];
    private readonly string[] u11Info = new string[2];
    // U32
    private bool u32NoSync, u32Synced, u32AfterSim, u32tNoSync, u32tSynced, u32tAfterSim;
    private string u32Detail = "";
    // U36 watcher
    private FullScreenMode lastMode;
    private bool lastFullScreen;
    private int lastW, lastH;
    private string resultFilePath = "";

    // ------------------------------------------------------------------ step-driven phases

    private void StepPhase()
    {
        switch (phase)
        {
            case Phase.Warmup:
                if (phaseStep == 1) { SetB(FarB); ClearEvents(); }
                if (phaseStep >= 3) Advance(Phase.U1);
                break;

            case Phase.U1:
                // Sequences are collected by EvaluateOrderStep (after Simulate) for steps 1..5.
                if (phaseStep == 6)
                {
                    var distinct = new List<string>();
                    foreach (var s in u1Sequences) if (!distinct.Contains(s)) distinct.Add(s);
                    bool sameClassMatchesCreation = distinct.Count == 1 && distinct[0] == "E1 E2 L1 L2";
                    bool ok = u1AllOrdered && u1AllComplete;
                    Log("[SPIKE][U1] RESULT=" + YN(ok)
                        + " detail=steps=" + u1Sequences.Count
                        + " all_early(-100)_before_late(+100)=" + YN(u1AllOrdered)
                        + " all_four_called_each_step=" + YN(u1AllComplete)
                        + " observed_sequences=[" + string.Join(" | ", distinct.ToArray()) + "]"
                        + " creation_order=E1,E2,L1,L2 same_class_order_equals_creation=" + YN(sameClassMatchesCreation)
                        + " (YES = 다른 클래스 간 순서가 속성값을 따름; 같은 클래스 인스턴스 순서는 관찰값일 뿐 보장 아님)");
                    Advance(Phase.U2);
                }
                break;

            case Phase.U2:
                break; // frame-driven, see FramePhase

            case Phase.U31:
                if (!u31Triggered && phaseFrame >= 1 && SpikeClock.StepInFrame == 1)
                {
                    u31Triggered = true;
                    u31TriggerFrame = SpikeClock.FrameIndex;
                    u31TriggerStep = SpikeClock.Step;
                    var go = new GameObject("U31_NewComponent");
                    newStarter = go.AddComponent<SpikeLateStarter>();
                    newStarter.Tag = "new";
                    preStarter.enabled = true;
                    Trace("U31 trigger at frame=" + u31TriggerFrame + " stepInFrame=1 globalStep=" + u31TriggerStep);
                }
                break;

            case Phase.U5:
                StepU5();
                break;

            case Phase.U8:
                StepU8();
                break;

            case Phase.U9:
                StepU9();
                break;

            case Phase.U11:
                StepU11();
                break;

            case Phase.U32:
                StepU32();
                break;
        }
    }

    private void EvaluateOrderStep()
    {
        if (phaseStep < 1 || phaseStep > 5) return;
        string seq = string.Join(" ", orderLog.ToArray());
        int lastEarly = -1, firstLate = int.MaxValue;
        for (int i = 0; i < orderLog.Count; i++)
        {
            if (orderLog[i].StartsWith("E")) lastEarly = Mathf.Max(lastEarly, i);
            if (orderLog[i].StartsWith("L")) firstLate = Mathf.Min(firstLate, i);
        }
        bool ordered = orderLog.Count == 4 && lastEarly < firstLate;
        if (orderLog.Count != 4) u1AllComplete = false;
        if (!ordered) u1AllOrdered = false;
        u1Sequences.Add(seq);
        Trace("U1 step " + phaseStep + " sequence=" + seq + " ordered=" + ordered);
    }

    private void StepU5()
    {
        int s = phaseStep - u5RoundStart;
        if (s == 1) { SetB(FarB); ClearEvents(); }
        else if (s == 2) { ClearEvents(); SetB(OverlapB); u5SetStep = SpikeClock.Step; }
        else if (s == 4) { SetB(FarB); }
        else if (s == 6)
        {
            int enterA = CountEvents("Enter", "A"), enterB = CountEvents("Enter", "B");
            int exitA = CountEvents("Exit", "A"), exitB = CountEvents("Exit", "B");
            bool ok = enterA > 0 && exitA > 0;
            u5RoundOk[u5Round] = ok;
            u5RoundInfo[u5Round] = "round" + u5Round + "(sleepMode=" + rbA.sleepMode + "): enterA=" + enterA
                                   + " enterB=" + enterB + " exitA=" + exitA + " exitB=" + exitB;
            TriggerEvent fe;
            if (!u7Seen && FirstEvent("Enter", out fe))
            {
                u7Seen = true;
                u7Inside = fe.InsideSimulate;
                u7SameStep = fe.Step == u5SetStep;
                u7Detail = "enter_callback insideSimulate=" + YN(fe.InsideSimulate) + " callback_step=" + fe.Step
                           + " position_set_step=" + u5SetStep + " callback_frame=" + fe.Frame
                           + " (round" + u5Round + ")";
            }
            if (u5Round == 0)
            {
                rbA.sleepMode = RigidbodySleepMode2D.NeverSleep;
                rbB.sleepMode = RigidbodySleepMode2D.NeverSleep;
                u5Round = 1;
                u5RoundStart = phaseStep;
            }
            else
            {
                bool any = u5RoundOk[0] || u5RoundOk[1];
                Log("[SPIKE][U5] RESULT=" + YN(any) + " detail=" + u5RoundInfo[0] + "; " + u5RoundInfo[1]
                    + (any && !u5RoundOk[0] ? "; NOTE: NeverSleep에서만 동작" : "")
                    + " (YES = Kinematic-Kinematic 트리거 Enter+Exit 모두 수신)");
                if (!u7Seen)
                    Log("[SPIKE][U7] RESULT=UNKNOWN detail=Enter 콜백이 한 번도 오지 않아 판정 불가(U5 참고)");
                else
                    Log("[SPIKE][U7] RESULT=" + YN(u7Inside && u7SameStep) + " detail=" + u7Detail
                        + " (YES = Physics2D.Simulate() 호출 안에서 같은 스텝에 동기적으로 콜백)");
                ClearEvents();
                Advance(Phase.U8);
            }
        }
    }

    private void StepU8()
    {
        int s = phaseStep;
        if (s == 1) { SetB(FarB); ClearEvents(); }
        else if (s == 2) { SetB(OverlapB); }
        else if (s == 4)
        {
            u8EnterBefore = CountEvents("Enter", null);
            ClearEvents();
            colB.enabled = false;
            u8ExitAtDisableCall = CountEvents("Exit", null);
        }
        else if (s == 6)
        {
            int exits = CountEvents("Exit", null);
            Log("[SPIKE][U8] RESULT=" + YN(exits == 0)
                + " detail=callbacksOnDisable=" + Physics2D.callbacksOnDisable
                + " enter_before_disable=" + u8EnterBefore
                + " exit_during_enabled=false_call=" + u8ExitAtDisableCall
                + " exit_total_after_disable(2 steps)=" + exits
                + " (YES = Exit 안 옴 = 설계가 기대하는 결과)");
            colB.enabled = true;
        }
        else if (s == 7) { SetB(FarB); }
        else if (s == 8) { ClearEvents(); Advance(Phase.U9); }
    }

    private void StepU9()
    {
        int s = phaseStep - u9CaseStart;
        float d = U9Cases[u9Case];
        if (s == 1) { SetB(FarB); ClearEvents(); }
        else if (s == 2) { ClearEvents(); SetB(new Vector2(d, 0f)); }
        else if (s == 3)
        {
            // One Simulate has run since the placement.
            var cd = Physics2D.Distance(colA, colB);
            int enterA = CountEvents("Enter", "A");
            u9Enter[u9Case] = enterA > 0;
            u9Info[u9Case] = "d=" + F(d) + " actual_center_dist=" + F(Vector2.Distance(rbA.position, rbB.position))
                             + " enterA=" + enterA + " box2d.distance=" + F(cd.distance)
                             + " box2d.isOverlapped=" + cd.isOverlapped;
        }
        else if (s == 4) { SetB(FarB); }
        else if (s == 5)
        {
            ClearEvents();
            if (u9Case == U9Cases.Length - 1)
            {
                bool ok = u9Enter[2] && !u9Enter[1];
                Log("[SPIKE][U9] RESULT=" + YN(ok) + " detail=r=0.5+0.5 contactOffset=" + F(Physics2D.defaultContactOffset)
                    + " | " + u9Info[0] + " | " + u9Info[1] + " | " + u9Info[2]
                    + " (YES = 0.9999는 Enter, 1.0001은 Enter 없음; d=1.0 정확히 경계는 관찰값만 기록)");
                Advance(Phase.U11);
            }
            else { u9Case++; u9CaseStart = phaseStep; }
        }
    }

    private void StepU11()
    {
        int s = phaseStep - u11ModeStart;
        int m = u11Mode;
        bool mode = U11Modes[m];
        if (s == 1) { Physics2D.autoSyncTransforms = mode; SetB(FarB); ClearEvents(); }
        else if (s == 3)
        {
            var target = new Vector2(20f, 0f);
            rbB.MovePosition(target);
            bool before = QueryFinds(target, colB);
            string posBefore = F(rbB.position);
            afterSimulate = () =>
            {
                bool after = QueryFinds(target, colB);
                u11After[m] = after;
                u11Info[m] = "autoSyncTransforms=" + mode + ": before_simulate=" + YN(before)
                             + " after_simulate(same step)=" + YN(after)
                             + " rb.position before=" + posBefore + " after=" + F(rbB.position)
                             + " transform.after=" + F((Vector2)rbB.transform.position);
            };
        }
        else if (s == 4) { SetB(FarB); }
        else if (s == 6)
        {
            if (m == U11Modes.Length - 1)
            {
                Physics2D.autoSyncTransforms = false;
                bool ok = u11After[0] && u11After[1];
                Log("[SPIKE][U11] RESULT=" + YN(ok) + " detail=" + u11Info[0] + " | " + u11Info[1]
                    + " (YES = MovePosition은 Simulate 뒤에 OverlapCircle에 보임; before_simulate가 NO면 Simulate 전에는 안 보임)");
                ClearEvents();
                Advance(Phase.U32);
            }
            else { u11Mode++; u11ModeStart = phaseStep; }
        }
    }

    private void StepU32()
    {
        int s = phaseStep;
        if (s == 1) { Physics2D.autoSyncTransforms = false; SetB(FarB); ClearEvents(); }
        else if (s == 3)
        {
            var p = new Vector2(30f, 0f);
            rbB.linearVelocity = Vector2.zero;
            rbB.position = p;
            u32NoSync = QueryFinds(p, colB);
            string tBefore = F((Vector2)rbB.transform.position);
            Physics2D.SyncTransforms();
            u32Synced = QueryFinds(p, colB);
            string tAfter = F((Vector2)rbB.transform.position);
            u32Detail = "rb.position=(30,0): no_sync=" + YN(u32NoSync) + " after_SyncTransforms=" + YN(u32Synced)
                        + " transform before_sync=" + tBefore + " after_sync=" + tAfter;
            afterSimulate = () => { u32AfterSim = QueryFinds(p, colB); };
        }
        else if (s == 4)
        {
            // Extra: transform.position assignment (not rb.position) for IMP-SPATIAL reference.
            var p2 = new Vector2(40f, 0f);
            rbB.transform.position = p2;
            u32tNoSync = QueryFinds(p2, colB);
            Physics2D.SyncTransforms();
            u32tSynced = QueryFinds(p2, colB);
            afterSimulate = () => { u32tAfterSim = QueryFinds(p2, colB); };
        }
        else if (s == 5) { SetB(FarB); }
        else if (s == 6)
        {
            Log("[SPIKE][U32] RESULT=" + YN(u32NoSync) + " detail=autoSyncTransforms=false " + u32Detail
                + " after_Simulate=" + YN(u32AfterSim)
                + " | extra transform.position=(40,0): no_sync=" + YN(u32tNoSync)
                + " after_SyncTransforms=" + YN(u32tSynced) + " after_Simulate=" + YN(u32tAfterSim)
                + " (YES = rb.position 대입 직후 Simulate·SyncTransforms 없이 OverlapCircle이 트리거를 찾음)");
            ClearEvents();
            Advance(Phase.U35);
        }
    }

    // ------------------------------------------------------------------ frame-driven phases

    private void FramePhase()
    {
        switch (phase)
        {
            case Phase.U2:
                if (phaseFrame == 1)
                {
                    Time.captureDeltaTime = 0.1f;
                    Trace("U2 captureDeltaTime=0.1 set at frame " + SpikeClock.FrameIndex);
                }
                else if (phaseFrame >= 3 && phaseFrame <= 12)
                {
                    u2Counts.Add(SpikeClock.StepsInLastFrame);
                    if (phaseFrame == 12)
                    {
                        int max = 0, min = int.MaxValue;
                        var parts = new List<string>();
                        foreach (var c in u2Counts) { max = Mathf.Max(max, c); min = Mathf.Min(min, c); parts.Add(c.ToString()); }
                        Log("[SPIKE][U2] RESULT=" + YN(max == 3) + " detail=expected=3 actual_max=" + max + " actual_min=" + min
                            + " per_frame=[" + string.Join(",", parts.ToArray()) + "]"
                            + " captureDeltaTime=0.1 maximumDeltaTime=" + F(Time.maximumDeltaTime)
                            + " fixedDeltaTime=" + F(Time.fixedDeltaTime) + " Time.deltaTime=" + F(Time.deltaTime)
                            + " (YES = 한 프레임 최대 정확히 3스텝)"
                            + " (captureDeltaTime은 maximumDeltaTime 클램프를 우회함 — U2b 참조)");
                        Advance(Phase.U2b);
                    }
                }
                break;

            case Phase.U2b:
                FrameU2b();
                break;

            case Phase.U23:
                FrameU23();
                break;

            case Phase.U31:
                if (!u31Triggered) break;
                if (SpikeClock.FrameIndex - 1 == u31TriggerFrame)
                {
                    u31StepsInTriggerFrame = SpikeClock.StepsInLastFrame;
                }
                else if (SpikeClock.FrameIndex - 1 >= u31TriggerFrame + 2)
                {
                    string newInfo = DescribeStarter(newStarter);
                    string preInfo = DescribeStarter(preStarter);
                    bool newOk = newStarter.FirstFrame == u31TriggerFrame && newStarter.FirstStepInFrame == 2;
                    bool preOk = preStarter.FirstFrame == u31TriggerFrame && preStarter.FirstStepInFrame == 2;
                    string result = u31StepsInTriggerFrame < 2 ? "UNKNOWN" : YN(newOk && preOk);
                    Log("[SPIKE][U31] RESULT=" + result
                        + " detail=trigger=f" + u31TriggerFrame + "/s1 steps_in_trigger_frame=" + u31StepsInTriggerFrame
                        + " new_component(AddComponent in FixedUpdate@order1000): " + newInfo
                        + " | pre_created(enabled=true in same FixedUpdate): " + preInfo
                        + (u31StepsInTriggerFrame < 2 ? " (트리거 프레임에 스텝이 1개뿐이라 판정 불가)" : "")
                        + " (YES = 둘 다 같은 프레임의 2번째 스텝(f" + u31TriggerFrame + "/s2)부터 호출)");
                    newStarter.enabled = false;
                    preStarter.enabled = false;
                    Time.captureDeltaTime = 1f / 60f;
                    Advance(Phase.U5);
                }
                break;

            case Phase.U35:
                if (phaseFrame == 1)
                {
                    LogScreenInfo("U35", "SetResolution 호출 직전");
                    try { Screen.SetResolution(1280, 720, FullScreenMode.Windowed); }
                    catch (Exception ex) { Log("[SPIKE][U35] SetResolution 예외: " + ex.GetType().Name + " " + ex.Message); }
                }
                else if (phaseFrame == 2)
                {
                    Trace("U35 +1 frame Screen=" + Screen.width + "x" + Screen.height);
                }
                else if (phaseFrame == 3)
                {
                    bool ok = Screen.width == 1280 && Screen.height == 720;
                    Log("[SPIKE][U35] RESULT=" + (Application.isEditor ? "UNKNOWN" : YN(ok))
                        + " detail=SetResolution(1280,720,Windowed) +2 frames: Screen.width/height=" + Screen.width + "x" + Screen.height
                        + " fullScreenMode=" + Screen.fullScreenMode + " mainWindowPosition=" + SafeWindowPosition()
                        + (Application.isBatchMode ? " (배치 모드 값, 의미 없음 - 빌드에서 확인)"
                           : Application.isEditor ? " (에디터 값, 참고용 - Game 뷰 크기라 의미 없음, 빌드에서 확인)" : " (빌드 값)")
                        + " (YES = 1280x720)");
                    LogScreenInfo("U35", "SetResolution +2프레임 뒤");
                    Advance(Phase.Done);
                    Finish();
                }
                break;
        }
    }

    // U2b: real 0.1 s frames made with Thread.Sleep while captureDeltaTime = 0, so the
    // maximumDeltaTime clamp actually applies. Records the frames that follow each stall.
    private void FrameU2b()
    {
        if (phaseFrame == 1)
        {
            Time.captureDeltaTime = 0f;
            Trace("U2b captureDeltaTime=0 at frame " + SpikeClock.FrameIndex + "; warmup frames 2..4");
        }
        else if (phaseFrame >= 6 && phaseFrame <= 11)
        {
            // Frames 6..10 follow a sleep made in frames 5..9; frame 11 follows a normal frame.
            u2bSteps.Add(SpikeClock.StepsInLastFrame);
            u2bDelta.Add(Time.deltaTime);
            u2bUnscaled.Add(Time.unscaledDeltaTime);
        }
        if (phaseFrame >= 5 && phaseFrame <= 9)
        {
            System.Threading.Thread.Sleep(100);
        }
        if (phaseFrame == 11)
        {
            int max = 0;
            float maxDelta = 0f;
            var sp = new List<string>();
            var dp = new List<string>();
            var up = new List<string>();
            for (int i = 0; i < u2bSteps.Count; i++)
            {
                max = Mathf.Max(max, u2bSteps[i]);
                maxDelta = Mathf.Max(maxDelta, u2bDelta[i]);
                sp.Add(u2bSteps[i].ToString());
                dp.Add(F(u2bDelta[i]));
                up.Add(F(u2bUnscaled[i]));
            }
            bool ok = max <= 3 && maxDelta <= 0.05f + 1e-4f;
            Log("[SPIKE][U2b] RESULT=" + YN(ok) + " detail=expected_max=3 actual_max=" + max
                + " per_frame=[" + string.Join(",", sp.ToArray()) + "]"
                + " deltaTime=[" + string.Join(",", dp.ToArray()) + "]"
                + " unscaledDeltaTime=[" + string.Join(",", up.ToArray()) + "]"
                + " maximumDeltaTime=" + F(Time.maximumDeltaTime) + " captureDeltaTime=0 (Thread.Sleep(100) x5 frames)"
                + " (YES = 정지 프레임 뒤 프레임당 FixedUpdate 최대 3회이고 deltaTime<=0.05)");
            Advance(Phase.U23);
        }
    }

    // U23: timeScale = 3 at targetFrameRate 60 with captureDeltaTime = 0. Observed values only:
    // the editor Game view / vsync can make frame times irregular.
    private void FrameU23()
    {
        if (phaseFrame == 1)
        {
            prevTargetFrameRate = Application.targetFrameRate;
            prevVSyncCount = QualitySettings.vSyncCount;
            Time.captureDeltaTime = 0f;
            QualitySettings.vSyncCount = 0;     // targetFrameRate is ignored while vsync is on
            Application.targetFrameRate = 60;
            Time.timeScale = 3f;
            Trace("U23 start: prev targetFrameRate=" + prevTargetFrameRate + " prev vSyncCount=" + prevVSyncCount);
        }
        else if (phaseFrame >= 5 && phaseFrame <= 64)
        {
            u23Steps.Add(SpikeClock.StepsInLastFrame);
            u23MaxDelta = Mathf.Max(u23MaxDelta, Time.deltaTime);
            u23MaxUnscaled = Mathf.Max(u23MaxUnscaled, Time.unscaledDeltaTime);
            if (phaseFrame == 64)
            {
                int max = 0, min = int.MaxValue, sum = 0;
                var parts = new List<string>();
                foreach (var c in u23Steps) { max = Mathf.Max(max, c); min = Mathf.Min(min, c); sum += c; parts.Add(c.ToString()); }
                float avg = u23Steps.Count > 0 ? (float)sum / u23Steps.Count : 0f;
                bool ok = max <= 3 && avg >= 2.5f && avg <= 3.0f;
                Log("[SPIKE][U23] RESULT=" + YN(ok) + " detail=frames=" + u23Steps.Count
                    + " steps_per_frame max=" + max + " min=" + min + " avg=" + F(avg)
                    + " max_deltaTime(scaled)=" + F(u23MaxDelta) + " max_unscaledDeltaTime=" + F(u23MaxUnscaled)
                    + " timeScale=3 targetFrameRate=60 vSyncCount=0 captureDeltaTime=0 maximumDeltaTime=" + F(Time.maximumDeltaTime)
                    + " per_frame=[" + string.Join(",", parts.ToArray()) + "]"
                    + " (관찰값: 에디터 Game 뷰·vsync·배치 모드에 따라 프레임 길이가 들쭉날쭉할 수 있음)"
                    + " (YES = 최대 3회 이하이고 평균 2.5~3.0)");
                Time.timeScale = 1f;
                Application.targetFrameRate = prevTargetFrameRate;
                QualitySettings.vSyncCount = prevVSyncCount;
                Time.captureDeltaTime = 0.1f;   // U31 needs multi-step frames
                Advance(Phase.U31);
            }
        }
    }

    private static string DescribeStarter(SpikeLateStarter st)
    {
        if (st == null) return "null";
        return "first=" + (st.FirstFrame < 0 ? "never" : "f" + st.FirstFrame + "/s" + st.FirstStepInFrame + "(globalStep " + st.FirstStep + ")")
               + " calls=[" + string.Join(",", st.Calls.ToArray()) + "]";
    }

    private void Finish()
    {
        Time.captureDeltaTime = 0f;
        Log("[SPIKE][U36] RESULT=UNKNOWN detail=수동 항목. 빌드 실행 중 Alt+Enter를 누르세요. 전환되면 '[SPIKE][U36] changed' 로그가 추가로 찍힙니다."
            + " 추가 로그가 없으면 Allow Fullscreen Switch 끔이 Alt+Enter를 막은 것(=YES). 현재 fullScreenMode=" + Screen.fullScreenMode
            + (Application.isEditor ? " (에디터에서는 의미 없음)" : ""));
        Log("[SPIKE][DONE] 측정 끝. 콘솔에서 [SPIKE]로 필터해 전체를 복사하세요. captureDeltaTime을 0으로 되돌렸습니다.");
        var sb = new StringBuilder("[SPIKE][SUMMARY]\n");
        foreach (var r in resultLines) sb.AppendLine(r);
        Debug.Log(sb.ToString());
        try
        {
            resultFilePath = Path.Combine(Application.persistentDataPath, "Wave0Spike_result.txt");
            File.WriteAllLines(resultFilePath, spikeLines.ToArray());
            Log("[SPIKE][FILE] 결과 파일: " + resultFilePath);
        }
        catch (Exception ex) { Log("[SPIKE][FILE] 결과 파일 쓰기 실패: " + ex.GetType().Name + " " + ex.Message); }
        IsDone = true;
    }

    private void WatchScreen()
    {
        if (Screen.fullScreenMode != lastMode || Screen.fullScreen != lastFullScreen)
        {
            string line = "[SPIKE][U36] changed: fullScreenMode " + lastMode + " -> " + Screen.fullScreenMode
                          + " fullScreen=" + Screen.fullScreen + " size=" + Screen.width + "x" + Screen.height
                          + " frame=" + SpikeClock.FrameIndex + (Application.isEditor ? " (에디터 값, 참고용)" : "");
            Log(line);
            if (resultFilePath.Length > 0) { try { File.AppendAllText(resultFilePath, line + "\n"); } catch (Exception) { } }
            lastMode = Screen.fullScreenMode;
            lastFullScreen = Screen.fullScreen;
        }
        if (Screen.width != lastW || Screen.height != lastH)
        {
            Trace("screen size " + lastW + "x" + lastH + " -> " + Screen.width + "x" + Screen.height + " frame=" + SpikeClock.FrameIndex);
            lastW = Screen.width;
            lastH = Screen.height;
        }
    }

    // On-screen copy of the latest [SPIKE] lines so a player build is readable without Player.log.
    private void OnGUI()
    {
        if (Application.isBatchMode) return;
        if (overlayStyle == null)
        {
            overlayStyle = new GUIStyle(GUI.skin.box);
            overlayStyle.alignment = TextAnchor.UpperLeft;
            overlayStyle.wordWrap = true;
            overlayStyle.fontSize = 12;
            overlayStyle.normal.textColor = Color.white;
        }
        GUI.Box(new Rect(8f, 8f, Screen.width - 16f, Screen.height - 16f), overlay, overlayStyle);
    }
}
