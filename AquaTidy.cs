// Aqua Tidy: little pixel Aquas fly over, grab each terminal by the top edge and carry it
// into the same grid Tidy Terminals uses. Loaded by aqua-tidy.ps1 (C# 5, .NET Framework).
// The overlay is a transparent, click-through, topmost window that closes itself when done.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace AquaTidy {

static class Native {
    public delegate bool EnumProc(IntPtr h, IntPtr p);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }

    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int ht, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern IntPtr SetProcessDpiAwarenessContext(IntPtr ctx);
    [DllImport("user32.dll")] public static extern bool SystemParametersInfo(int a, int b, ref RECT r, int c);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int i);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vk);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT r, int size);
    [DllImport("kernel32.dll")] public static extern IntPtr GetConsoleWindow();
}

// ---------------------------------------------------------------- sprites

public static class Sprites {
    public const int W = 24, H = 32, Scale = 2;

    class Layer { public string Name; public int Row; public List<string> Rows = new List<string>(); }

    static Dictionary<char, Color> pal = new Dictionary<char, Color>();
    static Dictionary<string, Layer> layers = new Dictionary<string, Layer>();
    static List<KeyValuePair<string, string[]>> recipes = new List<KeyValuePair<string, string[]>>();
    static Dictionary<string, Bitmap[]> frames = new Dictionary<string, Bitmap[]>();
    public static Bitmap Heart;

    public static void Load(string path) {
        pal.Clear(); layers.Clear(); recipes.Clear(); frames.Clear();
        Layer cur = null;
        int n = 0;
        foreach (string raw in File.ReadAllLines(path)) {
            n++;
            string line = raw.TrimEnd();
            if (line.Length == 0 || line.StartsWith("#")) { cur = null; continue; }
            if (line.StartsWith("@")) {
                pal[line[1]] = ColorTranslator.FromHtml(line.Substring(2).Trim());
                cur = null;
            } else if (line.StartsWith("[")) {
                int close = line.IndexOf(']');
                cur = new Layer();
                cur.Name = line.Substring(1, close - 1);
                cur.Row = int.Parse(line.Substring(close + 1).Trim());
                layers[cur.Name] = cur;
            } else if (line.StartsWith("=")) {
                string[] parts = line.Substring(1).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                string[] rest = new string[parts.Length - 1];
                Array.Copy(parts, 1, rest, 0, rest.Length);
                recipes.Add(new KeyValuePair<string, string[]>(parts[0], rest));
                cur = null;
            } else if (cur != null) {
                if (cur.Rows.Count > 0 && line.Length != cur.Rows[0].Length)
                    throw new Exception(string.Format("aqua-sprites.txt line {0}: layer [{1}] row is {2} wide, expected {3}",
                        n, cur.Name, line.Length, cur.Rows[0].Length));
                foreach (char c in line)
                    if (c != '.' && !pal.ContainsKey(c))
                        throw new Exception(string.Format("aqua-sprites.txt line {0}: unknown color '{1}'", n, c));
                cur.Rows.Add(line);
            } else {
                throw new Exception(string.Format("aqua-sprites.txt line {0}: pixels outside a [layer]", n));
            }
        }
        foreach (var r in recipes) {
            char[,] g = Compose(r.Value);
            frames[r.Key] = new[] { Render(g, Scale, false), Render(g, Scale, true) };
        }
        if (layers.ContainsKey("heart")) Heart = Render(LayerGrid(layers["heart"]), Scale, false);
    }

    public static IEnumerable<string> FrameNames { get { foreach (var r in recipes) yield return r.Key; } }

    public static Bitmap Get(string name, bool mirror) { return frames[name][mirror ? 1 : 0]; }

    static char[,] Compose(string[] names) {
        var g = new char[H, W];
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) g[y, x] = '.';
        foreach (string full in names) {
            bool mirror = full.EndsWith(":m");
            string name = mirror ? full.Substring(0, full.Length - 2) : full;
            Layer l;
            if (!layers.TryGetValue(name, out l)) throw new Exception("aqua-sprites.txt: unknown layer " + name);
            if (l.Rows[0].Length != W || l.Row + l.Rows.Count > H)
                throw new Exception("aqua-sprites.txt: layer " + name + " does not fit the 24 x 32 frame");
            for (int y = 0; y < l.Rows.Count; y++)
                for (int x = 0; x < W; x++) {
                    char c = l.Rows[y][mirror ? W - 1 - x : x];
                    if (c != '.') g[l.Row + y, x] = c;
                }
        }
        return g;
    }

    static char[,] LayerGrid(Layer l) {
        var g = new char[l.Rows.Count, l.Rows[0].Length];
        for (int y = 0; y < l.Rows.Count; y++)
            for (int x = 0; x < l.Rows[0].Length; x++) g[y, x] = l.Rows[y][x];
        return g;
    }

    public static Bitmap Render(char[,] g, int scale, bool mirror) {
        int h = g.GetLength(0), w = g.GetLength(1);
        var bmp = new Bitmap(w * scale, h * scale, PixelFormat.Format32bppArgb);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++) {
                char c = g[y, mirror ? w - 1 - x : x];
                if (c == '.') continue;
                Color col = pal[c];
                for (int dy = 0; dy < scale; dy++)
                    for (int dx = 0; dx < scale; dx++)
                        bmp.SetPixel(x * scale + dx, y * scale + dy, col);
            }
        return bmp;
    }

    // Frame grid, for the icon and the preview sheet
    public static Bitmap RenderFrame(string name, int scale, int cropRows) {
        foreach (var r in recipes)
            if (r.Key == name) {
                char[,] g = Compose(r.Value);
                if (cropRows > 0 && cropRows < H) {
                    var c = new char[cropRows, W];
                    for (int y = 0; y < cropRows; y++) for (int x = 0; x < W; x++) c[y, x] = g[y, x];
                    g = c;
                }
                return Render(g, scale, false);
            }
        throw new Exception("unknown frame " + name);
    }
}

// ---------------------------------------------------------------- windows and grid

public class Job {
    public IntPtr H;
    public Rectangle Target;   // where the visible frame should end up
    public Rectangle Vis;      // visible frame right now
    public int PadL, PadT, PadR, PadB;
    public bool Claimed, Done;
    public bool Fake;          // demo window: only exists as a rectangle
    public string Title;

    public void Measure() {
        if (Fake) return;
        Native.RECT o, v;
        Native.GetWindowRect(H, out o);
        PadL = PadT = PadR = PadB = 0;
        // Windows 10/11 frames have invisible borders; track the visible edges
        if (Native.DwmGetWindowAttribute(H, 9, out v, 16) == 0) {
            PadL = v.L - o.L; PadT = v.T - o.T; PadR = o.R - v.R; PadB = o.B - v.B;
        } else v = o;
        Vis = Rectangle.FromLTRB(v.L, v.T, v.R, v.B);
    }

    public void MoveVisible(int x, int y) {
        if (Fake) { Vis.Location = new Point(x, y); return; }
        // SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_ASYNCWINDOWPOS
        Native.SetWindowPos(H, IntPtr.Zero, x - PadL, y - PadT, 0, 0, 0x0001 | 0x0004 | 0x0010 | 0x4000);
    }

    public void SetVisible(int x, int y, int w, int h) {
        if (Fake) { Vis = new Rectangle(x, y, w, h); return; }
        // SWP_NOZORDER | SWP_NOACTIVATE | SWP_ASYNCWINDOWPOS
        Native.SetWindowPos(H, IntPtr.Zero, x - PadL, y - PadT, w + PadL + PadR, h + PadT + PadB, 0x0004 | 0x0010 | 0x4000);
    }

    public void Snap() {
        Done = true;
        if (Fake) { Vis = Target; return; }
        if (!Native.IsWindow(H)) return;
        Measure();
        Native.SetWindowPos(H, IntPtr.Zero, Target.X - PadL, Target.Y - PadT,
            Target.Width + PadL + PadR, Target.Height + PadT + PadB, 0x0004 | 0x0010 | 0x0040);
    }

    public bool AtTarget() {
        return Math.Abs(Vis.X - Target.X) <= 2 && Math.Abs(Vis.Y - Target.Y) <= 2 &&
               Math.Abs(Vis.Width - Target.Width) <= 2 && Math.Abs(Vis.Height - Target.Height) <= 2;
    }
}

public static class Grid {
    const int Gap = 4;

    public static Rectangle WorkArea() {
        var r = new Native.RECT();
        Native.SystemParametersInfo(0x0030, 0, ref r, 0);   // primary monitor minus taskbar
        return Rectangle.FromLTRB(r.L, r.T, r.R, r.B);
    }

    // Same picks and same layout as tidy-terminals.ps1
    public static List<Job> Build() {
        var wins = new List<IntPtr>();
        IntPtr self = Native.GetConsoleWindow();
        Native.EnumWindows(delegate(IntPtr h, IntPtr p) {
            if (h == self || !Native.IsWindowVisible(h)) return true;
            var cls = new StringBuilder(256); Native.GetClassName(h, cls, 256);
            var title = new StringBuilder(512); Native.GetWindowText(h, title, 512);
            string c = cls.ToString(), t = title.ToString();
            bool term = c == "CASCADIA_HOSTING_WINDOW_CLASS" || c == "ConsoleWindowClass";
            bool agentview = t.IndexOf("agentview", StringComparison.OrdinalIgnoreCase) >= 0 || t == "python";
            // no-op move as a probe: fails on admin windows, so they don't leave a hole in the grid
            if (term && t.Length > 0 && !agentview && Native.SetWindowPos(h, IntPtr.Zero, 0, 0, 0, 0, 0x0017))
                wins.Add(h);
            return true;
        }, IntPtr.Zero);
        wins.Reverse();   // oldest first

        var jobs = new List<Job>();
        List<Rectangle> slots = Layout(wins.Count, WorkArea());
        for (int i = 0; i < wins.Count; i++) {
            var j = new Job();
            j.H = wins[i];
            j.Target = slots[i];
            if (Native.IsIconic(j.H) || Native.IsZoomed(j.H)) Native.ShowWindow(j.H, 9);   // restore first
            j.Measure();
            jobs.Add(j);
        }
        return jobs;
    }

    // Wide screen: fewer rows than columns. 2 -> 1x2, 4 -> 2x2, 6 -> 2x3, 9 -> 3x3.
    // A partly filled last row stretches to the full width.
    public static List<Rectangle> Layout(int n, Rectangle wa) {
        var slots = new List<Rectangle>();
        if (n == 0) return slots;
        int rows = Math.Max(1, (int)Math.Floor(Math.Sqrt(n)));
        int cols = (int)Math.Ceiling(n / (double)rows);
        int cellH = wa.Height / rows;
        for (int i = 0; i < n; i++) {
            int row = i / cols, col = i % cols;
            int inRow = row == rows - 1 ? n - row * cols : cols;
            int w = wa.Width / inRow;
            slots.Add(new Rectangle(wa.Left + col * w + Gap, wa.Top + row * cellH + Gap, w - 2 * Gap, cellH - 2 * Gap));
        }
        return slots;
    }
}

// ---------------------------------------------------------------- overlay

enum St { Wait, Spawn, Walk, Fly, Grab, Carry, ToCorner, Resize, Release, Fall, Victory, Poof, Done }

class Helper {
    public float X, Y;           // sprite top-left, screen pixels
    public bool Left;            // facing left (mirrored sprite)
    public St S = St.Wait;
    public float T, Anim, Delay, Trail;
    public bool Visible;
    public Job Job;
    public float FX, FY, TX, TY, Dur, Arc, VY;
    public float W0, H0, W1, H1;   // window size before and after the resize step
    public int OffX, OffY;       // window visible top-left minus hands point
    public string Frame = "idle0";
    public int Bob;
}

class Particle {
    public float X, Y, VX, VY, Life, Max, G;
    public Color C;
    public int Size;
    public bool Star;
}

public class Overlay : Form {
    const int SW = Sprites.W * Sprites.Scale, SH = Sprites.H * Sprites.Scale;
    const int HandX = SW / 2, HandY = 6;   // where her hands meet, inside the sprite
    const double TimeLimit = 20.0;

    static readonly Color[] SparkleColors = {
        ColorTranslator.FromHtml("#f6c64a"), Color.White, ColorTranslator.FromHtml("#c4f0ff"),
        ColorTranslator.FromHtml("#6cc8f0"), ColorTranslator.FromHtml("#ff9bb3") };

    List<Job> jobs;
    List<Helper> helpers = new List<Helper>();
    List<Particle> parts = new List<Particle>();
    List<Rectangle> drawnLast = new List<Rectangle>();
    Dictionary<int, SolidBrush> brushes = new Dictionary<int, SolidBrush>();
    Stopwatch clock = Stopwatch.StartNew();
    double last, simTime;
    Timer timer;
    Random rng = new Random();
    int vx, vy, vw, vh;
    Rectangle work;
    float ground;
    bool finishing;

    public Overlay(List<Job> jobs) : this(jobs, Rectangle.Empty, Rectangle.Empty, 0) { }

    // screen/work empty = the real desktop. The demo passes its own canvas and a fixed seed.
    public Overlay(List<Job> jobs, Rectangle screen, Rectangle workArea, int seed) {
        this.jobs = jobs;
        if (seed != 0) rng = new Random(seed);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        DoubleBuffered = true;
        Text = "Aqua Tidy";

        if (screen.IsEmpty) {
            vx = Native.GetSystemMetrics(76); vy = Native.GetSystemMetrics(77);
            vw = Native.GetSystemMetrics(78); vh = Native.GetSystemMetrics(79);
            Bounds = new Rectangle(vx, vy, vw, vh);
            work = Grid.WorkArea();
        } else {
            vx = screen.X; vy = screen.Y; vw = screen.Width; vh = screen.Height;
            work = workArea;
        }
        ground = work.Bottom - SH;
        int todo = 0;
        foreach (var j in jobs) if (j.AtTarget()) j.Done = true; else todo++;

        // Up to 3 helpers. With nothing to move, one Aqua still pops in and celebrates.
        int count = Math.Max(1, Math.Min(3, todo));
        float mid = work.Left + work.Width / 2f;
        for (int i = 0; i < count; i++) {
            var h = new Helper();
            h.X = mid + (i - (count - 1) / 2f) * 90 - SW / 2;
            h.Y = ground;
            h.Delay = i * 0.3f;
            h.Left = i % 2 == 1;
            helpers.Add(h);
        }

        timer = new Timer();
        timer.Interval = 15;
        timer.Tick += Tick;
    }

    protected override CreateParams CreateParams {
        get {
            var cp = base.CreateParams;
            // layered, click-through, no taskbar button, never takes focus, topmost
            cp.ExStyle |= 0x80000 | 0x20 | 0x80 | 0x8000000 | 0x8;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    protected override void OnShown(EventArgs e) {
        base.OnShown(e);
        Native.SetWindowPos(Handle, new IntPtr(-1), vx, vy, vw, vh, 0x0010);   // exact physical bounds, topmost
        last = clock.Elapsed.TotalSeconds;
        timer.Start();
    }

    // ------------------------------------------------------------ loop

    void Tick(object sender, EventArgs e) {
        try {
            double now = clock.Elapsed.TotalSeconds;
            float dt = (float)Math.Min(0.05, now - last);
            last = now;
            if (!finishing && (Native.GetAsyncKeyState(0x1B) & 0x8000) != 0) Finish();
            Advance(dt);
            Redraw();
            if (AllDone) { timer.Stop(); Close(); }
        } catch (Exception) {
            timer.Stop();
            foreach (var j in jobs) if (!j.Done) j.Snap();
            Close();
        }
    }

    // One step of the whole scene
    public void Advance(float dt) {
        simTime += dt;
        if (!finishing && simTime > TimeLimit) Finish();
        foreach (var h in helpers) Step(h, dt);
        StepParticles(dt);
    }

    public bool AllDone {
        get {
            if (parts.Count > 0) return false;
            foreach (var h in helpers) if (h.S != St.Done) return false;
            return true;
        }
    }

    // Time's up or Esc: put every window in place right now and send the Aquas home
    void Finish() {
        finishing = true;
        foreach (var j in jobs) if (!j.Done) j.Snap();
        foreach (var h in helpers) {
            if (h.S == St.Wait) h.S = St.Done;
            else if (h.S != St.Done && h.S != St.Poof) StartPoof(h);
        }
    }

    void Go(Helper h, St s) { h.S = s; h.T = 0; h.Bob = 0; }

    bool OnGround(Helper h) { return Math.Abs(h.Y - ground) < 1; }

    void Step(Helper h, float dt) {
        h.T += dt;
        h.Anim += dt;
        switch (h.S) {
        case St.Wait:
            if (h.T >= h.Delay) {
                Go(h, St.Spawn);
                Sparkles(h.X + SW / 2, h.Y + SH / 2, 18, 220);
            }
            break;

        case St.Spawn:
            h.Visible = h.T > 0.12f;
            h.Frame = "idle0";
            h.Bob = h.T < 0.3f ? -(int)(Math.Sin(h.T / 0.3 * Math.PI) * 10) : 0;
            if (h.T > 0.5f) Think(h);
            break;

        case St.Walk: {
            float k = Math.Min(1f, h.T / h.Dur);
            h.X = Lerp(h.FX, h.TX, k);
            float speed = Math.Abs(h.TX - h.FX) / Math.Max(h.Dur, 0.01f);
            int f = (int)(h.Anim * Math.Min(18, Math.Max(8, speed / 45))) % 4;
            h.Frame = f == 0 ? "walk0" : f == 2 ? "walk2" : "walk1";
            h.Bob = f % 2 == 1 ? -2 : 0;
            if (k >= 1) StartFly(h);
            break;
        }

        case St.Fly: {
            float k = Math.Min(1f, h.T / h.Dur);
            float e = EaseOut(k);
            h.X = Lerp(h.FX, h.TX, e);
            h.Y = Lerp(h.FY, h.TY, e) - (float)Math.Sin(k * Math.PI) * h.Arc;
            h.Frame = "fly";
            TrailSparkles(h, dt);
            if (k >= 1) StartGrab(h);
            break;
        }

        case St.Grab:
            h.Frame = "carry1";
            h.Bob = h.T < 0.1f ? 2 : 0;    // little squash as she catches the edge
            if (h.T > 0.2f) StartCarry(h);
            break;

        case St.Carry: {
            float k = Math.Min(1f, h.T / h.Dur);
            float e = EaseInOut(k);
            float hx = Lerp(h.FX, h.TX, e);
            float hy = Lerp(h.FY, h.TY, e) - (float)Math.Sin(k * Math.PI) * h.Arc;
            h.X = hx - HandX;
            h.Y = hy - HandY;
            h.Job.MoveVisible((int)Math.Round(hx) + h.OffX, (int)Math.Round(hy) + h.OffY);
            h.Frame = ((int)(h.Anim * 7)) % 2 == 0 ? "carry0" : "carry1";
            TrailSparkles(h, dt);
            if (k >= 1) StartToCorner(h);
            break;
        }

        case St.ToCorner: {
            float k = Math.Min(1f, h.T / h.Dur);
            float e = EaseOut(k);
            h.X = Lerp(h.FX, h.TX, e);
            h.Y = Lerp(h.FY, h.TY, e) - (float)Math.Sin(k * Math.PI) * h.Arc;
            h.Frame = "fly";
            TrailSparkles(h, dt);
            if (k >= 1) StartResize(h);
            break;
        }

        case St.Resize: {
            // Hold a beat on the corner, then drag it until the window is the right size
            float k = Math.Min(1f, Math.Max(0f, h.T - 0.15f) / h.Dur);
            float e = EaseInOut(k);
            int w = (int)Math.Round(Lerp(h.W0, h.W1, e)), ht = (int)Math.Round(Lerp(h.H0, h.H1, e));
            if (h.T > 0.15f) h.Job.SetVisible(h.Job.Target.X, h.Job.Target.Y, w, ht);
            h.X = h.Job.Target.X + w - CornerIn - HandX;
            h.Y = h.Job.Target.Y + ht - HandY;
            h.Bob = h.T < 0.1f ? 2 : 0;
            h.Frame = h.T < 0.15f ? "carry1" : ((int)(h.Anim * 10)) % 2 == 0 ? "carry0" : "carry1";
            if (h.T > 0.15f) TrailSparkles(h, dt);
            if (k >= 1) StartRelease(h);
            break;
        }

        case St.Release:
            h.Frame = "carry1";
            if (h.T > 0.18f) Think(h);
            break;

        case St.Fall:
            h.VY += 2600 * dt;
            h.Y += h.VY * dt;
            h.Frame = "fly";
            if (h.Y >= ground) {
                h.Y = ground;
                Dust(h.X + SW / 2, ground + SH);
                StartVictory(h);
            }
            break;

        case St.Victory:
            h.Frame = h.T < 0.12f ? "idle0" : "victory";
            h.Bob = h.T < 0.3f ? -(int)(Math.Sin(h.T / 0.3 * Math.PI) * 8) : 0;
            if (h.T > 1.1f) StartPoof(h);
            break;

        case St.Poof:
            h.Visible = false;
            if (h.T > 0.6f) Go(h, St.Done);
            break;
        }
    }

    // Grab the nearest window nobody has claimed yet, or wrap up
    void Think(Helper h) {
        Job best = null;
        double bestD = double.MaxValue;
        if (!finishing)
            foreach (var j in jobs) {
                if (j.Done || j.Claimed) continue;
                if (!j.Fake && !Native.IsWindow(j.H)) { j.Done = true; continue; }
                j.Measure();
                double dx = j.Vis.X + j.Vis.Width / 2.0 - (h.X + HandX), dy = j.Vis.Y - (h.Y + HandY);
                double d = dx * dx + dy * dy;
                if (d < bestD) { bestD = d; best = j; }
            }
        if (best != null) {
            best.Claimed = true;
            h.Job = best;
            if (OnGround(h)) StartWalk(h); else StartFly(h);
        } else if (!OnGround(h)) {
            h.VY = -260;
            Go(h, St.Fall);
        } else StartVictory(h);
    }

    void StartWalk(Helper h) {
        float gx = h.Job.Vis.X + h.Job.Vis.Width / 2f;
        h.FX = h.X;
        h.TX = Clamp(gx - HandX, vx, vx + vw - SW);
        float dx = h.TX - h.FX;
        if (Math.Abs(dx) < 8) { StartFly(h); return; }
        h.Left = dx < 0;
        h.Dur = Math.Min(1.1f, Math.Abs(dx) / 420f);
        Go(h, St.Walk);
    }

    void StartFly(Helper h) {
        h.Job.Measure();
        float gx = h.Job.Vis.X + h.Job.Vis.Width / 2f, gy = h.Job.Vis.Y + 2;
        h.FX = h.X; h.FY = h.Y;
        h.TX = gx - HandX; h.TY = gy - HandY;
        float dist = Dist(h.FX, h.FY, h.TX, h.TY);
        if (Math.Abs(h.TX - h.FX) > 4) h.Left = h.TX < h.FX;
        h.Dur = Clamp(dist / 1100f, 0.3f, 0.8f);
        h.Arc = OnGround(h) ? 0 : Math.Min(50, dist * 0.1f);
        Go(h, St.Fly);
    }

    void StartGrab(Helper h) {
        h.Job.Measure();
        int hx = (int)Math.Round(h.X) + HandX, hy = (int)Math.Round(h.Y) + HandY;
        h.OffX = h.Job.Vis.X - hx;
        h.OffY = h.Job.Vis.Y - hy;
        Sparkles(hx, hy, 8, 140);
        Go(h, St.Grab);
    }

    void StartCarry(Helper h) {
        h.FX = h.X + HandX; h.FY = h.Y + HandY;
        h.TX = h.Job.Target.X - h.OffX; h.TY = h.Job.Target.Y - h.OffY;
        float dist = Dist(h.FX, h.FY, h.TX, h.TY);
        if (dist < 3) { StartToCorner(h); return; }
        if (Math.Abs(h.TX - h.FX) > 4) h.Left = h.TX < h.FX;
        h.Dur = Clamp(dist / 850f, 0.45f, 1.3f);
        h.Arc = Math.Min(70, dist * 0.15f);
        Go(h, St.Carry);
    }

    const int CornerIn = 6;   // she grabs just inside the bottom-right corner

    // Window is in place but the wrong size: fly down to its bottom-right corner
    void StartToCorner(Helper h) {
        h.Job.Measure();
        Rectangle v = h.Job.Vis, t = h.Job.Target;
        if (Math.Abs(v.Width - t.Width) <= 4 && Math.Abs(v.Height - t.Height) <= 4) { StartRelease(h); return; }
        h.FX = h.X; h.FY = h.Y;
        h.TX = v.Right - CornerIn - HandX; h.TY = v.Bottom - HandY;
        float dist = Dist(h.FX, h.FY, h.TX, h.TY);
        if (Math.Abs(h.TX - h.FX) > 4) h.Left = h.TX < h.FX;
        h.Dur = Clamp(dist / 1000f, 0.25f, 0.6f);
        h.Arc = Math.Min(30, dist * 0.08f);
        Go(h, St.ToCorner);
    }

    void StartResize(Helper h) {
        h.Job.Measure();
        h.W0 = h.Job.Vis.Width; h.H0 = h.Job.Vis.Height;
        h.W1 = h.Job.Target.Width; h.H1 = h.Job.Target.Height;
        h.Left = h.W1 < h.W0;   // face the way she's pulling
        h.Dur = Clamp(Math.Max(Math.Abs(h.W1 - h.W0), Math.Abs(h.H1 - h.H0)) / 700f, 0.35f, 0.9f);
        Sparkles(h.X + HandX, h.Y + HandY, 8, 140);
        Go(h, St.Resize);
    }

    void StartRelease(Helper h) {
        h.Job.Snap();
        Sparkles(h.X + HandX, h.Y + HandY, 14, 200);
        h.Job = null;
        Go(h, St.Release);
    }

    void StartVictory(Helper h) {
        Sparkles(h.X + SW / 2, h.Y + 10, 10, 160);
        Go(h, St.Victory);
    }

    void StartPoof(Helper h) {
        Poof(h.X + SW / 2, h.Y + SH / 2);
        Go(h, St.Poof);
    }

    // ------------------------------------------------------------ particles

    void Add(float x, float y, float vxp, float vyp, float life, Color c, int size, bool star, float g) {
        var p = new Particle();
        p.X = x; p.Y = y; p.VX = vxp; p.VY = vyp; p.Life = p.Max = life;
        p.C = c; p.Size = size; p.Star = star; p.G = g;
        parts.Add(p);
    }

    float R(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

    void Sparkles(float x, float y, int n, float speed) {
        for (int i = 0; i < n; i++) {
            float a = R(0, (float)(Math.PI * 2)), s = R(speed * 0.3f, speed);
            Add(x, y, (float)Math.Cos(a) * s, (float)Math.Sin(a) * s, R(0.35f, 0.7f),
                SparkleColors[rng.Next(SparkleColors.Length)], rng.Next(2) == 0 ? 2 : 4, rng.Next(2) == 0, 160);
        }
    }

    void TrailSparkles(Helper h, float dt) {
        h.Trail += dt;
        while (h.Trail > 0.035f) {
            h.Trail -= 0.035f;
            Add(h.X + SW / 2 + R(-10, 10), h.Y + SH - 10 + R(-4, 4), R(-30, 30), R(10, 60), R(0.25f, 0.45f),
                SparkleColors[rng.Next(SparkleColors.Length)], 2, rng.Next(3) == 0, 0);
        }
    }

    void Poof(float x, float y) {
        Color cloud = ColorTranslator.FromHtml("#e8f4ff");
        for (int i = 0; i < 16; i++) {
            float a = R(0, (float)(Math.PI * 2)), s = R(40, 130);
            Add(x, y, (float)Math.Cos(a) * s, (float)Math.Sin(a) * s, R(0.4f, 0.6f),
                rng.Next(2) == 0 ? Color.White : cloud, 6 + 2 * rng.Next(3), false, -40);
        }
        Sparkles(x, y, 10, 200);
    }

    void Dust(float x, float y) {
        Color c = ColorTranslator.FromHtml("#d8d8e0");
        for (int i = 0; i < 8; i++)
            Add(x + R(-12, 12), y - 2, R(-90, 90), R(-40, -10), R(0.2f, 0.35f), c, 4, false, 100);
    }

    void StepParticles(float dt) {
        for (int i = parts.Count - 1; i >= 0; i--) {
            var p = parts[i];
            p.Life -= dt;
            if (p.Life <= 0) { parts.RemoveAt(i); continue; }
            p.VY += p.G * dt;
            p.X += p.VX * dt;
            p.Y += p.VY * dt;
        }
    }

    // ------------------------------------------------------------ drawing

    // Screen rect of each thing on screen this frame, in overlay coordinates
    List<Rectangle> Footprint() {
        var list = new List<Rectangle>();
        foreach (var h in helpers) {
            if (!h.Visible || h.S == St.Done || h.S == St.Poof) continue;
            int x = (int)Math.Round(h.X) - vx, y = (int)Math.Round(h.Y) + h.Bob - vy;
            list.Add(new Rectangle(x, y, SW, SH));
            if (h.S == St.Victory && Sprites.Heart != null) list.Add(HeartRect(h));
        }
        foreach (var p in parts) {
            int s = PartSize(p) * 3;
            list.Add(new Rectangle((int)p.X - vx - s, (int)p.Y - vy - s, s * 2, s * 2));
        }
        return list;
    }

    Rectangle HeartRect(Helper h) {
        int bounce = (int)(Math.Abs(Math.Sin(h.T * 6)) * 4);
        int x = (int)Math.Round(h.X) + SW / 2 - Sprites.Heart.Width / 2 - vx;
        int y = (int)Math.Round(h.Y) + h.Bob - Sprites.Heart.Height - 6 - bounce - vy;
        return new Rectangle(x, y, Sprites.Heart.Width, Sprites.Heart.Height);
    }

    int PartSize(Particle p) {
        float k = p.Life / p.Max;
        int s = k > 0.4f ? p.Size : Math.Max(2, p.Size / 2);
        return s - s % 2;
    }

    void Redraw() {
        var now = Footprint();
        foreach (var r in drawnLast) Invalidate(Rectangle.Inflate(r, 2, 2));
        foreach (var r in now) Invalidate(Rectangle.Inflate(r, 2, 2));
        drawnLast = now;
    }

    SolidBrush Brush(Color c) {
        SolidBrush b;
        if (!brushes.TryGetValue(c.ToArgb(), out b)) { b = new SolidBrush(c); brushes[c.ToArgb()] = b; }
        return b;
    }

    protected override void OnPaint(PaintEventArgs e) { Draw(e.Graphics); }

    public void Draw(Graphics g) {
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.SmoothingMode = SmoothingMode.None;

        foreach (var h in helpers) {
            if (!h.Visible || h.S == St.Done || h.S == St.Poof) continue;
            Bitmap bmp = Sprites.Get(h.Frame, h.Left);
            int x = (int)Math.Round(h.X) - vx, y = (int)Math.Round(h.Y) + h.Bob - vy;
            g.DrawImage(bmp, new Rectangle(x, y, bmp.Width, bmp.Height));
            if (h.S == St.Victory && h.T > 0.15f && Sprites.Heart != null)
                g.DrawImage(Sprites.Heart, HeartRect(h));
        }

        foreach (var p in parts) {
            int s = PartSize(p);
            int x = (int)p.X - vx, y = (int)p.Y - vy;
            var b = Brush(p.C);
            if (p.Star && s >= 2) {
                g.FillRectangle(b, x - s / 2, y - s * 3 / 2, s, s * 3);
                g.FillRectangle(b, x - s * 3 / 2, y - s / 2, s * 3, s);
            } else g.FillRectangle(b, x - s / 2, y - s / 2, s, s);
        }
    }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            if (timer != null) timer.Dispose();
            foreach (var b in brushes.Values) b.Dispose();
        }
        base.Dispose(disposing);
    }

    // ------------------------------------------------------------ math

    static float Lerp(float a, float b, float k) { return a + (b - a) * k; }
    static float Clamp(float v, float lo, float hi) { return v < lo ? lo : v > hi ? hi : v; }
    static float Dist(float x1, float y1, float x2, float y2) { float dx = x2 - x1, dy = y2 - y1; return (float)Math.Sqrt(dx * dx + dy * dy); }
    static float EaseOut(float k) { float u = 1 - k; return 1 - u * u * u; }
    static float EaseInOut(float k) { return k < 0.5f ? 4 * k * k * k : 1 - (float)Math.Pow(-2 * k + 2, 3) / 2; }
}

// ---------------------------------------------------------------- entry points

public static class Engine {
    public static void Run(string spritesPath) {
        Native.SetProcessDpiAwarenessContext(new IntPtr(-4));   // per-monitor aware: real pixels
        Sprites.Load(spritesPath);
        var jobs = Grid.Build();
        if (jobs.Count == 0) return;
        using (var form = new Overlay(jobs)) Application.Run(form);
        foreach (var j in jobs) if (!j.Done) j.Snap();   // never leave a window half-moved
    }

    // Every frame at 4x with its name underneath. For checking the art (and the README).
    public static void SaveSheet(string spritesPath, string pngPath) {
        Sprites.Load(spritesPath);
        var names = new List<string>(Sprites.FrameNames);
        int cw = Sprites.W * 4 + 24, ch = Sprites.H * 4 + 44;
        using (var sheet = new Bitmap(cw * names.Count + 12, ch + 12))
        using (var g = Graphics.FromImage(sheet))
        using (var font = new Font("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Pixel))
        using (var ink = new SolidBrush(Color.FromArgb(70, 76, 110))) {
            g.Clear(Color.FromArgb(234, 242, 252));
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            var center = new StringFormat(); center.Alignment = StringAlignment.Center;
            for (int i = 0; i < names.Count; i++) {
                using (var a = Sprites.RenderFrame(names[i], 4, 0))
                    g.DrawImage(a, new Rectangle(i * cw + 18, 12, a.Width, a.Height));
                g.DrawString(names[i], font, ink, new RectangleF(i * cw + 6, Sprites.H * 4 + 20, cw, 20), center);
            }
            sheet.Save(pngPath, ImageFormat.Png);
        }
    }

    // Renders Aqua tidying four pretend terminals on a pretend desktop, one PNG per frame
    // (30 fps) into outDir. Uses the real animation code; no real windows are touched.
    public static int SaveDemo(string spritesPath, string outDir) {
        Sprites.Load(spritesPath);
        var screen = new Rectangle(0, 0, 960, 540);
        var work = new Rectangle(0, 0, 960, 500);
        string[] titles = { "PowerShell", "Command Prompt", "Windows Terminal", "claude" };
        Rectangle[] start = {
            new Rectangle(40, 60, 360, 210), new Rectangle(520, 30, 300, 180),
            new Rectangle(300, 240, 420, 200), new Rectangle(60, 330, 260, 140) };
        List<Rectangle> slots = Grid.Layout(titles.Length, work);
        var jobs = new List<Job>();
        for (int i = 0; i < titles.Length; i++) {
            var j = new Job();
            j.Fake = true; j.Title = titles[i]; j.Vis = start[i]; j.Target = slots[i];
            jobs.Add(j);
        }
        Directory.CreateDirectory(outDir);
        int frame = 0;
        using (var overlay = new Overlay(jobs, screen, work, 7))
        using (var bmp = new Bitmap(screen.Width, screen.Height, PixelFormat.Format24bppRgb))
        using (var g = Graphics.FromImage(bmp)) {
            Action save = delegate {
                DrawDesktop(g, screen, work, jobs);
                overlay.Draw(g);
                bmp.Save(Path.Combine(outDir, string.Format("f_{0:D4}.png", frame++)), ImageFormat.Png);
            };
            for (int i = 0; i < 15; i++) save();
            for (int i = 0; i < 30 * 30 && !overlay.AllDone; i++) { overlay.Advance(1f / 30); save(); }
            for (int i = 0; i < 30; i++) save();
        }
        return frame;
    }

    static void DrawDesktop(Graphics g, Rectangle screen, Rectangle work, List<Job> jobs) {
        g.SmoothingMode = SmoothingMode.None;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        using (var bg = new LinearGradientBrush(screen, Color.FromArgb(28, 52, 92), Color.FromArgb(70, 120, 180), 90f))
            g.FillRectangle(bg, screen);
        using (var bar = new SolidBrush(Color.FromArgb(24, 24, 30)))
            g.FillRectangle(bar, screen.Left, work.Bottom, screen.Width, screen.Bottom - work.Bottom);
        using (var icon = new SolidBrush(Color.FromArgb(90, 150, 220)))
            for (int i = 0; i < 5; i++) g.FillRectangle(icon, screen.Width / 2 - 70 + i * 30, work.Bottom + 12, 18, 16);

        using (var titleFont = new Font("Segoe UI", 11f, GraphicsUnit.Pixel))
        using (var mono = new Font("Consolas", 12f, GraphicsUnit.Pixel))
        using (var frameB = new SolidBrush(Color.FromArgb(60, 60, 66)))
        using (var titleB = new SolidBrush(Color.FromArgb(32, 32, 36)))
        using (var bodyB = new SolidBrush(Color.FromArgb(12, 12, 14)))
        using (var white = new SolidBrush(Color.FromArgb(225, 225, 230)))
        using (var green = new SolidBrush(Color.FromArgb(120, 220, 140)))
        using (var gray = new SolidBrush(Color.FromArgb(140, 140, 150))) {
            foreach (var j in jobs) {
                Rectangle r = j.Vis;
                g.FillRectangle(frameB, r);
                g.FillRectangle(titleB, r.X + 1, r.Y + 1, r.Width - 2, 22);
                g.FillRectangle(bodyB, r.X + 1, r.Y + 23, r.Width - 2, r.Height - 24);
                var clip = g.Clip; g.SetClip(r);
                g.DrawString(j.Title, titleFont, white, r.X + 10, r.Y + 5);
                // caption buttons: minimize, maximize, close
                g.FillRectangle(gray, r.Right - 62, r.Y + 12, 8, 1);
                using (var pen = new Pen(Color.FromArgb(140, 140, 150))) {
                    g.DrawRectangle(pen, r.Right - 42, r.Y + 7, 8, 8);
                    g.DrawLine(pen, r.Right - 20, r.Y + 7, r.Right - 12, r.Y + 15);
                    g.DrawLine(pen, r.Right - 12, r.Y + 7, r.Right - 20, r.Y + 15);
                }
                string prompt = j.Title == "Command Prompt" ? "C:\\Users\\you>" : j.Title == "claude" ? "> " : "PS C:\\Users\\you>";
                g.DrawString(prompt, mono, green, r.X + 8, r.Y + 30);
                g.DrawString("ready", mono, gray, r.X + 8, r.Y + 46);
                g.Clip = clip;
            }
        }
    }

    // aqua.ico: full body at the big sizes, the face at the small ones
    public static void SaveIcon(string spritesPath, string icoPath) {
        Sprites.Load(spritesPath);
        int[] sizes = { 256, 64, 48, 32, 16 };
        var pngs = new List<byte[]>();
        foreach (int size in sizes) {
            Bitmap art;
            if (size == 256) art = Sprites.RenderFrame("idle0", 8, 0);
            else if (size == 64) art = Sprites.RenderFrame("idle0", 2, 0);
            else if (size == 48) art = Sprites.RenderFrame("idle0", 2, 20);
            else art = Sprites.RenderFrame("idle0", 1, 20);
            using (art)
            using (var canvas = new Bitmap(size, size, PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(canvas))
            using (var ms = new MemoryStream()) {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                int w = art.Width, h = art.Height;
                if (size == 16) { w = 16; h = 16 * art.Height / art.Width; }   // only size that shrinks
                g.DrawImage(art, new Rectangle((size - w) / 2, (size - h) / 2, w, h));
                canvas.Save(ms, ImageFormat.Png);
                pngs.Add(ms.ToArray());
            }
        }
        using (var fs = new FileStream(icoPath, FileMode.Create))
        using (var bw = new BinaryWriter(fs)) {
            bw.Write((short)0); bw.Write((short)1); bw.Write((short)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++) {
                bw.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                bw.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                bw.Write((byte)0); bw.Write((byte)0);
                bw.Write((short)1); bw.Write((short)32);
                bw.Write(pngs[i].Length); bw.Write(offset);
                offset += pngs[i].Length;
            }
            foreach (var p in pngs) bw.Write(p);
        }
    }
}

}
