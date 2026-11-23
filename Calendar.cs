using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

class Holiday {
    public bool holiday { get; set; }
    public string name { get; set; }
}
class Schedule {
    public int code { get; set; }
    public Dictionary<string, Holiday> holiday { get; set; }
}
sealed class CalendarTodo {
    public string Date { get; set; }
    public string Time { get; set; }
    public string Title { get; set; }
    public override string ToString() {
        return Date + (string.IsNullOrEmpty(Time) ? "" : " " + Time) + "   " + Title;
    }
}
sealed class DayCell : Control {
    static readonly Color Blue = Color.FromArgb(65,141,255);
    public static readonly Color RangeFill = Color.FromArgb(28,86,150);
    readonly Font number = new Font("Microsoft YaHei UI", 16, FontStyle.Bold);
    readonly Font lunarFont = new Font("Microsoft YaHei UI", 9);
    readonly Font badgeFont = new Font("Microsoft YaHei UI", 7, FontStyle.Bold);
    readonly Font endpointFont = new Font("Microsoft YaHei UI", 9, FontStyle.Bold);

    public DateTime Date { get; private set; }
    bool selected;
    bool hasTodo;
    string endpoint;

    public DayCell() {
        SetStyle(ControlStyles.StandardClick, true);
        SetStyle(ControlStyles.StandardDoubleClick, false);
    }

    public void UpdateDay(DateTime date, string lunar, Holiday holiday, bool current, bool inRange,
        string endpointLabel, bool hasTasks) {
        Date = date;
        Lunar = lunar;
        Holiday = holiday;
        Current = current;
        selected = inRange;
        hasTodo = hasTasks;
        endpoint = endpointLabel;
        AccessibleName = date.ToString("yyyy-MM-dd") + (holiday == null ? "" : holiday.holiday ? " 休" : " 班")
            + (date == DateTime.Today ? " 今" : "") + (endpoint == null ? "" : " " + endpoint)
            + (hasTodo ? " 有待办" : "");
        Text = AccessibleName;
        Invalidate();
    }

    string Lunar;
    Holiday Holiday;
    bool Current;

    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        if (selected) {
            using (var fill = new SolidBrush(RangeFill))
            using (var shape = new GraphicsPath()) {
                const int diameter = 22;
                int bottom = Height - 2;
                if (endpoint == "开始") {
                    shape.AddArc(0, 1, diameter, diameter, 180, 90);
                    shape.AddLine(Width, 1, Width, bottom);
                    shape.AddArc(0, bottom - diameter, diameter, diameter, 90, 90);
                } else if (endpoint == "结束") {
                    shape.AddLine(0, bottom, 0, 1);
                    shape.AddArc(Width - diameter, 1, diameter, diameter, 270, 90);
                    shape.AddArc(Width - diameter, bottom - diameter, diameter, diameter, 0, 90);
                } else {
                    shape.AddRectangle(new Rectangle(0, 1, Width, Height - 3));
                }
                shape.CloseFigure();
                e.Graphics.FillPath(fill, shape);
            }
        } else using (var path = new GraphicsPath()) {
            const int d = 22;
            int right = Width - 2, bottom = Height - 2;
            path.AddArc(1, 1, d, d, 180, 90);
            path.AddArc(right - d, 1, d, d, 270, 90);
            path.AddArc(right - d, bottom - d, d, d, 0, 90);
            path.AddArc(1, bottom - d, d, d, 90, 90);
            path.CloseFigure();
            if (Holiday != null) {
                using (var fill = new SolidBrush(Holiday.holiday ? Color.FromArgb(12,24,41) : Color.FromArgb(33,32,32)))
                    e.Graphics.FillPath(fill, path);
            }
            if (Date == DateTime.Today) {
                using (var pen = new Pen(Blue, 1.5f)) e.Graphics.DrawPath(pen, path);
            }
        }
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        bool weekend = Date.DayOfWeek == DayOfWeek.Saturday || Date.DayOfWeek == DayOfWeek.Sunday;
        Color numberColor = selected ? Color.WhiteSmoke : !Current ? Color.DimGray : (Holiday == null ? weekend : Holiday.holiday) ? Blue : Color.WhiteSmoke;
        const TextFormatFlags center = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;
        if (endpoint != null)
            TextRenderer.DrawText(e.Graphics, endpoint, endpointFont, new Rectangle(0,2,Width,19), Color.WhiteSmoke, center);
        TextRenderer.DrawText(e.Graphics, Date.Day.ToString(), number, new Rectangle(0,endpoint == null ? 8 : 17,Width,28), numberColor, center);
        TextRenderer.DrawText(e.Graphics, Lunar ?? "", lunarFont, new Rectangle(0,endpoint == null ? 35 : 42,Width,17),
            selected ? Color.LightSteelBlue : Current ? Color.DarkGray : Color.DimGray, center);
        int badgeIndex = 0;
        if (Holiday != null) {
            PaintBadge(e.Graphics, Holiday.holiday ? "休" : "班",
                Holiday.holiday ? Blue : Color.FromArgb(244,117,34), badgeIndex++);
        }
        if (Date == DateTime.Today) PaintBadge(e.Graphics, "今", Color.FromArgb(26,139,112), badgeIndex++);
        if (hasTodo) PaintBadge(e.Graphics, "待", Color.FromArgb(109,77,188), badgeIndex);
    }

    void PaintBadge(Graphics graphics, string text, Color color, int index) {
        int left = Width - 17 - (endpoint == null ? 16 * index : 0);
        int top = 4 + (endpoint == null ? 0 : 16 * index);
        var bounds = new Rectangle(left, top, 14, 14);
        using (var fill = new SolidBrush(color)) graphics.FillEllipse(fill, bounds);
        TextRenderer.DrawText(graphics, text, badgeFont, bounds, Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    protected override void Dispose(bool disposing) {
        if (disposing) { number.Dispose(); lunarFont.Dispose(); badgeFont.Dispose(); endpointFont.Dispose(); }
        base.Dispose(disposing);
    }
}

class CalendarApp : Form {
    const int TodoHeight = 96, GridTop = 134 + TodoHeight, RowHeight = 69;
    readonly Color tile = Color.FromArgb(25,25,25);
    readonly DayCell[] cells = new DayCell[42];
    readonly Label title, clock, rangeDates, rangeTotals;
    readonly FlowLayoutPanel todoList;
    readonly Label todoHeading;
    int todoExtraHeight, todoOffset, visibleTodoRows;
    List<CalendarTodo> todos = new List<CalendarTodo>();
    readonly HashSet<string> todoDates = new HashSet<string>();
    bool todoStoreReadable = true;
    bool editingTodo;
    DateTime? rangeStart, rangeEnd;
    readonly System.Windows.Forms.Timer clockTimer = new System.Windows.Forms.Timer { Interval = 250 };
    DateTime displayedDate = DateTime.Today;
    readonly ToolTip tips = new ToolTip();
    readonly string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RestWorkCalendar");
    readonly Dictionary<int, Dictionary<string, Holiday>> schedules = new Dictionary<int, Dictionary<string, Holiday>>();
    readonly HashSet<int> loading = new HashSet<int>();
    readonly HashSet<int> failed = new HashSet<int>();
    DateTime firstVisibleDate = FirstWeekOfMonth(DateTime.Today);
    DateTime DisplayedMonth {
        get {
            DateTime end = firstVisibleDate.AddDays(cells.Length);
            DateTime bestMonth = firstVisibleDate;
            int mostDays = 0;
            for (DateTime day = firstVisibleDate; day < end;) {
                DateTime month = new DateTime(day.Year, day.Month, 1);
                DateTime nextMonth = month.AddMonths(1);
                DateTime next = nextMonth < end ? nextMonth : end;
                int visibleDays = (next - day).Days;
                if (visibleDays > mostDays) {
                    bestMonth = month;
                    mostDays = visibleDays;
                }
                day = next;
            }
            return bestMonth;
        }
    }
    readonly ChineseLunisolarCalendar lunar = new ChineseLunisolarCalendar();
    readonly string[] lunarDays = "初一 初二 初三 初四 初五 初六 初七 初八 初九 初十 十一 十二 十三 十四 十五 十六 十七 十八 十九 二十 廿一 廿二 廿三 廿四 廿五 廿六 廿七 廿八 廿九 三十".Split(' ');
    delegate IntPtr MouseHook(int code, IntPtr message, IntPtr data);
    readonly MouseHook clockHook;
    IntPtr hookHandle;
    bool clockPressed;
    int wheelRemainder;

    [StructLayout(LayoutKind.Sequential)]
    struct ScreenPoint {
        public int X, Y;
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr SetWindowsHookEx(int type, MouseHook callback, IntPtr module, uint thread);
    [DllImport("user32.dll")]
    static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")]
    static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll")]
    static extern IntPtr WindowFromPoint(ScreenPoint point);
    [DllImport("user32.dll")]
    static extern bool IsChild(IntPtr parent, IntPtr window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int GetClassName(IntPtr window, StringBuilder name, int capacity);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr GetModuleHandle(string name);

    [STAThread]
    static void Main(string[] args) {
        bool first;
        using (var mutex = new Mutex(true, @"Local\RestWorkCalendar", out first)) {
            if (!first) return;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            bool background = Array.IndexOf(args, "--background") >= 0;
            using (var window = new CalendarApp())
            using (var context = new ApplicationContext()) {
                if (!window.Start(background)) return;
                window.FormClosed += (s,e) => context.ExitThread();
                Application.Run(context);
            }
        }
    }

    CalendarApp() {
        Text = "调休日历";
        ClientSize = new Size(453, 625 + TodoHeight);
        BackColor = Color.Black;
        ForeColor = Color.WhiteSmoke;
        Font = new Font("Microsoft YaHei UI", 10);
        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        KeyPreview = true;
        KeyDown += (s,e) => { if (e.KeyCode == Keys.Escape) Hide(); };
        Deactivate += (s,e) => { if (!editingTodo) Hide(); };
        VisibleChanged += (s,e) => {
            if (Visible) {
                clockTimer.Start();
                return;
            }
            clockTimer.Stop();
            wheelRemainder = 0;
            rangeStart = rangeEnd = null;
            UpdateRange();
        };
        MaximizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Directory.CreateDirectory(folder);

        todoHeading = AddLabel(16, 6, 270, 27, 12);
        todoHeading.Text = "待办日程";
        todoList = new FlowLayoutPanel {
            Bounds = new Rectangle(16, 37, 421, 54),
            BackColor = tile, ForeColor = ForeColor,
            FlowDirection = FlowDirection.TopDown, WrapContents = false, AccessibleName = "待办日程列表"
        };
        Controls.Add(todoList);
        LoadTodos();

        clock = AddLabel(16, 9 + TodoHeight, 421, 42, 19);
        clockTimer.Tick += (s,e) => UpdateClock();
        title = AddLabel(16, 57 + TodoHeight, 220, 34, 16);
        AddButton("‹", 272, 59 + TodoHeight, 42, 30, () => ChangeMonth(-1));
        AddButton("今天", 321, 59 + TodoHeight, 68, 30, () => {
            ShowMonth(DateTime.Today);
        });
        AddButton("›", 396, 59 + TodoHeight, 42, 30, () => ChangeMonth(1));
        for (int i = 0; i < 7; i++) {
            var weekday = AddLabel(16 + i * 61, 108 + TodoHeight, 54, 21, 10);
            weekday.Text = "一二三四五六日"[i].ToString();
            weekday.TextAlign = ContentAlignment.MiddleCenter;
        }
        for (int i = 0; i < cells.Length; i++) {
            cells[i] = new DayCell { Bounds = new Rectangle(15 + i % 7 * 61, GridTop + i / 7 * RowHeight, 57, 60),
                AccessibleRole = AccessibleRole.Cell, TabStop = false, Cursor = Cursors.Hand };
            cells[i].MouseClick += (s,e) => {
                DateTime date = ((DayCell)s).Date;
                if (e.Button == MouseButtons.Left) SelectDate(date);
                else if (e.Button == MouseButtons.Right) EditTodo(date);
            };
            Controls.Add(cells[i]);
        }
        rangeDates = AddLabel(16, 546 + TodoHeight, 350, 24, 10);
        rangeDates.ForeColor = Color.Silver;
        rangeTotals = AddLabel(16, 572 + TodoHeight, 421, 46, 11);
        rangeDates.BackColor = rangeTotals.BackColor = Color.Black;
        rangeDates.Hide();
        rangeTotals.Hide();
        clockHook = HandleClockClick;
        UpdateTodos();
        PositionWindow();
    }

    bool Start(bool background) {
        // Create the message target for the mouse hook without showing the form.
        IntPtr window = Handle;
        hookHandle = SetWindowsHookEx(14, clockHook, GetModuleHandle(null), 0);
        if (background) return hookHandle != IntPtr.Zero;
        OpenCalendar();
        return true;
    }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            if (hookHandle != IntPtr.Zero) {
                UnhookWindowsHookEx(hookHandle);
                hookHandle = IntPtr.Zero;
            }
            tips.Dispose();
            clockTimer.Dispose();
        }
        base.Dispose(disposing);
    }

    IntPtr HandleClockClick(int code, IntPtr message, IntPtr data) {
        if (editingTodo) return CallNextHookEx(hookHandle, code, message, data);
        const int LeftDown = 0x0201, LeftUp = 0x0202, RightDown = 0x0204, MiddleDown = 0x0207, MouseWheel = 0x020A;
        if (code >= 0 && message.ToInt64() == MouseWheel && Visible) {
            var point = (ScreenPoint)Marshal.PtrToStructure(data, typeof(ScreenPoint));
            IntPtr target = WindowFromPoint(point);
            if (target == todoList.Handle || IsChild(todoList.Handle, target)) {
                int delta = Marshal.ReadInt16(data, 10);
                if (todos.Count > visibleTodoRows)
                    BeginInvoke((Action)(() => ScrollTodos(delta)));
                return new IntPtr(1);
            }
            if (target == Handle || IsChild(Handle, target)) {
                int delta = Marshal.ReadInt16(data, 10);
                BeginInvoke((Action)(() => ScrollWeeks(delta)));
                return new IntPtr(1);
            }
        }
        if (code >= 0 && message.ToInt64() == LeftDown) {
            clockPressed = IsTaskbarClock(data);
            if (clockPressed) return new IntPtr(1);
        }
        if (code >= 0 && Visible && (message.ToInt64() == LeftDown || message.ToInt64() == RightDown || message.ToInt64() == MiddleDown)) {
            var point = (ScreenPoint)Marshal.PtrToStructure(data, typeof(ScreenPoint));
            IntPtr target = WindowFromPoint(point);
            if (target != Handle && !IsChild(Handle, target)) BeginInvoke((Action)Hide);
        }
        if (code >= 0 && message.ToInt64() == LeftUp && clockPressed) {
            clockPressed = false;
            if (IsTaskbarClock(data))
                BeginInvoke((Action)(() => { if (Visible && ContainsFocus) Hide(); else OpenCalendar(); }));
            return new IntPtr(1);
        }
        return CallNextHookEx(hookHandle, code, message, data);
    }

    protected override void OnMouseWheel(MouseEventArgs e) {
        base.OnMouseWheel(e);
        Point point = PointToClient(Cursor.Position);
        if (todoList.Bounds.Contains(point)) ScrollTodos(e.Delta);
        else if (ClientRectangle.Contains(point)) ScrollWeeks(e.Delta);
    }

    void ScrollWeeks(int delta) {
        if (!Visible) return;
        wheelRemainder += delta;
        int steps = wheelRemainder / 120;
        wheelRemainder %= 120;
        if (steps == 0) return;
        DateTime first = firstVisibleDate.AddDays(-7 * steps);
        int year = first.AddDays(6).Year;
        if (year < 1902 || year > 2100) return;
        firstVisibleDate = first;
        RenderMonth();
    }
    void UpdateClock() {
        DateTime now = DateTime.Now;
        clock.Text = now.ToString("yyyy'年'M'月'd'日' HH:mm:ss ddd", CultureInfo.GetCultureInfo("zh-CN"));
        if (displayedDate != now.Date) {
            displayedDate = now.Date;
            RenderMonth();
        }
    }


    static bool IsTaskbarClock(IntPtr data) {
        var point = (ScreenPoint)Marshal.PtrToStructure(data, typeof(ScreenPoint));
        var name = new StringBuilder(64);
        GetClassName(WindowFromPoint(point), name, name.Capacity);
        return name.ToString() == "TrayClockWClass";
    }

    void PositionWindow() {
        var area = Screen.PrimaryScreen.WorkingArea;
        Location = new Point(Math.Max(area.Left, area.Right - Width - 10), Math.Max(area.Top, area.Bottom - Height - 10));
    }
    void OpenCalendar() {
        PositionWindow();
        WindowState = FormWindowState.Normal;
        RenderMonth();
        UpdateClock();
        Show();
        BringToFront();
        Activate();
    }
    Label AddLabel(int x, int y, int w, int h, int size) {
        var label = new Label { Bounds = new Rectangle(x,y,w,h), Font = new Font(Font.FontFamily,size),
            TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
        Controls.Add(label);
        return label;
    }
    Button AddButton(string text, int x, int y, int w, int h, Action click) {
        var button = new Button { Bounds = new Rectangle(x,y,w,h), Text = text, BackColor = tile,
            FlatStyle = FlatStyle.Flat, Cursor = click == null ? Cursors.Default : Cursors.Hand };
        button.FlatAppearance.BorderSize = 0;
        if (click != null) button.Click += (s,e) => click();
        Controls.Add(button);
        return button;
    }
    void ChangeMonth(int step) {
        ShowMonth(DisplayedMonth.AddMonths(step));
    }
    void ShowMonth(DateTime date) {
        if (date.Year < 1902 || date.Year > 2100) return;
        firstVisibleDate = FirstWeekOfMonth(date);
        wheelRemainder = 0;
        RenderMonth();
    }
    static DateTime FirstWeekOfMonth(DateTime date) {
        var first = new DateTime(date.Year, date.Month, 1);
        return first.AddDays(-(((int)first.DayOfWeek + 6) % 7));
    }
    IEnumerable<int> VisibleYears() {
        var last = firstVisibleDate.AddDays(cells.Length - 1);
        yield return firstVisibleDate.Year;
        if (last.Year != firstVisibleDate.Year) yield return last.Year;
    }
    void RenderMonth() {
        DateTime now = DateTime.Now;
        DateTime month = DisplayedMonth;
        title.Text = month.Year == now.Year && month.Month == now.Month ? "本月" : month.ToString("yyyy年M月");
        for (int i = 0; i < cells.Length; i++) {
            DateTime date = firstVisibleDate.AddDays(i);
            bool current = date.Year == month.Year && date.Month == month.Month;
            var cell = cells[i];
            Holiday entry = FindHoliday(date);
            string sub = date >= lunar.MinSupportedDateTime && date <= lunar.MaxSupportedDateTime
                ? lunarDays[lunar.GetDayOfMonth(date) - 1] : "";
            string endpoint = date == rangeStart ? "开始" : date == rangeEnd ? "结束" : null;
            bool inRange = endpoint != null || (date > rangeStart && date < rangeEnd);
            cell.UpdateDay(date, sub, entry, current, inRange, endpoint, todoDates.Contains(DateKey(date)));
            tips.SetToolTip(cell, date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + (entry == null ? "" : " · " + entry.name));
        }
        foreach (int year in VisibleYears()) Fetch(year);
        UpdateRange();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        const int borderWidth = 5;
        ControlPaint.DrawBorder(e.Graphics, ClientRectangle,
            Color.Red, borderWidth, ButtonBorderStyle.Solid,
            Color.Red, borderWidth, ButtonBorderStyle.Solid,
            Color.Red, borderWidth, ButtonBorderStyle.Solid,
            Color.Red, borderWidth, ButtonBorderStyle.Solid);
        if (!rangeStart.HasValue || !rangeEnd.HasValue) return;
        using (var band = new SolidBrush(DayCell.RangeFill)) {
            for (int i = 0; i < cells.Length - 1; i++) {
                DateTime date = firstVisibleDate.AddDays(i);
                if (i % 7 == 6 || date < rangeStart || date >= rangeEnd) continue;
                var left = cells[i].Bounds;
                e.Graphics.FillRectangle(band, left.Right, left.Top + 1,
                    cells[i + 1].Left - left.Right, left.Height - 3);
            }
        }
    }

    Holiday FindHoliday(DateTime date) {
        Dictionary<string, Holiday> schedule;
        Holiday entry;
        return schedules.TryGetValue(date.Year, out schedule)
            && schedule.TryGetValue(date.ToString("MM-dd", CultureInfo.InvariantCulture), out entry) ? entry : null;
    }

    void SelectDate(DateTime date) {
        if (rangeStart == date) {
            rangeStart = rangeEnd = null;
        } else if (!rangeStart.HasValue || rangeEnd.HasValue || date < rangeStart.Value) {
            rangeStart = date;
            rangeEnd = null;
        } else {
            rangeEnd = date;
        }
        RenderMonth();
    }

    static string DateKey(DateTime date) { return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
    string TodoFile { get { return Path.Combine(folder, "todos.json"); } }

    void LoadTodos() {
        try {
            if (File.Exists(TodoFile))
                todos = new JavaScriptSerializer().Deserialize<List<CalendarTodo>>(File.ReadAllText(TodoFile));
            if (todos == null || todos.Exists(item => item == null || item.Date == null || item.Title == null))
                throw new InvalidDataException();
        } catch {
            todos = new List<CalendarTodo>();
            todoStoreReadable = false;
        }
    }

    void UpdateTodos() {
        const int rowHeight = 26;
        int available = Screen.PrimaryScreen.WorkingArea.Height - (625 + TodoHeight) - 10;
        int maxRows = 2 + Math.Max(0, available / rowHeight);
        visibleTodoRows = Math.Min(Math.Max(2, todos.Count), maxRows);
        int extra = (visibleTodoRows - 2) * rowHeight;
        if (extra != todoExtraHeight) {
            int shift = extra - todoExtraHeight;
            foreach (Control control in Controls)
                if (control != todoList && control != todoHeading) control.Top += shift;
            todoList.Height = 54 + extra;
            ClientSize = new Size(ClientSize.Width, ClientSize.Height + shift);
            todoExtraHeight = extra;
            PositionWindow();
        }
        todoOffset = Math.Min(todoOffset, Math.Max(0, todos.Count - visibleTodoRows));
        todoDates.Clear();
        foreach (CalendarTodo item in todos) todoDates.Add(item.Date);
        todoList.SuspendLayout();
        while (todoList.Controls.Count > 0) todoList.Controls[0].Dispose();
        int rowWidth = todoList.Width;
        int end = Math.Min(todos.Count, todoOffset + visibleTodoRows);
        for (int index = todoOffset; index < end; index++) {
            CalendarTodo item = todos[index];
            var row = new Panel { Size = new Size(rowWidth, rowHeight), Margin = Padding.Empty,
                BackColor = index % 2 == 0 ? Color.FromArgb(27,34,45) : Color.FromArgb(43,51,64) };
            row.Paint += (s,e) => {
                using (var divider = new Pen(Color.FromArgb(59,69,84)))
                    e.Graphics.DrawLine(divider, 0, row.Height - 1, row.Width - 1, row.Height - 1);
            };
            var label = new Label { Text = item.ToString(), Bounds = new Rectangle(7, 0, rowWidth - 44, 26),
                AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
            var delete = new Button { Bounds = new Rectangle(rowWidth - 35, 0, 32, 25), FlatStyle = FlatStyle.Flat,
                AccessibleName = "删除日程 " + item.Title, Cursor = Cursors.Hand };
            delete.FlatAppearance.BorderSize = 0;
            delete.FlatAppearance.MouseOverBackColor = Color.FromArgb(107,45,53);
            delete.FlatAppearance.MouseDownBackColor = Color.FromArgb(130,52,62);
            tips.SetToolTip(label, item.ToString());
            tips.SetToolTip(delete, "删除 " + item.Title);
            delete.Paint += (s,e) => {
                using (var pen = new Pen(Color.Silver, 1.3f)) {
                    e.Graphics.DrawLine(pen, 10, 8, 22, 8);
                    e.Graphics.DrawRectangle(pen, 12, 9, 8, 10);
                    e.Graphics.DrawLine(pen, 14, 6, 18, 6);
                    e.Graphics.DrawLine(pen, 15, 11, 15, 17);
                    e.Graphics.DrawLine(pen, 17, 11, 17, 17);
                }
            };
            delete.Click += (s,e) => DeleteTodo(item);
            row.Controls.AddRange(new Control[] { label, delete });
            todoList.Controls.Add(row);
        }
        if (todos.Count == 0) todoList.Controls.Add(new Label {
            Text = todoStoreReadable ? "暂无待办，右键点击日期添加" : "无法读取本机待办",
            Size = new Size(rowWidth - 8, 29), ForeColor = Color.Silver, TextAlign = ContentAlignment.MiddleLeft
        });
        todoList.ResumeLayout();
    }

    void ScrollTodos(int delta) {
        if (delta == 0) return;
        int next = Math.Max(0, Math.Min(todoOffset - Math.Sign(delta), todos.Count - visibleTodoRows));
        if (next == todoOffset) return;
        todoOffset = next;
        UpdateTodos();
    }

    void EditTodo(DateTime date) {
        if (!todoStoreReadable) return;
        editingTodo = true;
        try {
            using (var dialog = new Form {
                Text = "添加日程 · " + DateKey(date), Font = Font, ClientSize = new Size(350,157),
                StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog,
                ShowInTaskbar = false, MaximizeBox = false, MinimizeBox = false, TopMost = true
            }) {
                var label = new Label { Text = "日程名称", Bounds = new Rectangle(14,53,320,22) };
                var input = new TextBox { Bounds = new Rectangle(14,77,322,26), MaxLength = 120, AccessibleName = "日程名称" };
                var timeLabel = new Label { Text = "时间", Bounds = new Rectangle(14,15,47,24),
                    TextAlign = ContentAlignment.MiddleLeft };
                var hour = TimeInput(23, 12, 66, "小时");
                var minute = TimeInput(59, 0, 153, "分钟");
                var hourLabel = new Label { Text = "时", Bounds = new Rectangle(128,15,23,24), TextAlign = ContentAlignment.MiddleLeft };
                var minuteLabel = new Label { Text = "分", Bounds = new Rectangle(216,15,23,24), TextAlign = ContentAlignment.MiddleLeft };
                var save = new Button { Text = "添加", Bounds = new Rectangle(178,115,75,29), DialogResult = DialogResult.OK, Enabled = false };
                var cancel = new Button { Text = "取消", Bounds = new Rectangle(261,115,75,29), DialogResult = DialogResult.Cancel };
                input.TextChanged += (s,e) => save.Enabled = !string.IsNullOrWhiteSpace(input.Text);
                dialog.Controls.AddRange(new Control[] { timeLabel, hour, hourLabel, minute, minuteLabel, label, input, save, cancel });
                dialog.AcceptButton = save;
                dialog.CancelButton = cancel;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                var updated = new List<CalendarTodo>(todos) { new CalendarTodo { Date = DateKey(date), Title = input.Text.Trim(),
                    Time = string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", hour.Value, minute.Value) } };
                updated.Sort((left,right) => {
                    int byDate = string.CompareOrdinal(left.Date, right.Date);
                    return byDate != 0 ? byDate : string.CompareOrdinal(left.Time, right.Time);
                });
                SaveTodos(updated);
            }
        } finally { editingTodo = false; }
    }

    NumericUpDown TimeInput(int maximum, int value, int left, string name) {
        return new NumericUpDown { Minimum = 0, Maximum = maximum, Value = value,
            Bounds = new Rectangle(left,15,59,26), AccessibleName = name, TextAlign = HorizontalAlignment.Center };
    }

    void DeleteTodo(CalendarTodo item) {
        var updated = new List<CalendarTodo>(todos);
        updated.Remove(item);
        SaveTodos(updated);
    }

    void SaveTodos(List<CalendarTodo> updated) {
        try {
            Save(TodoFile, new JavaScriptSerializer().Serialize(updated));
            todos = updated;
            UpdateTodos();
            RenderMonth();
        } catch (IOException) { ShowTodoError(); }
          catch (UnauthorizedAccessException) { ShowTodoError(); }
    }

    void ShowTodoError() {
        bool wasEditing = editingTodo;
        editingTodo = true;
        try { MessageBox.Show(this, "本机待办保存失败，请重试。", Text); }
        finally { editingTodo = wasEditing; }
    }

    void UpdateRange() {
        if (!rangeStart.HasValue) {
            rangeDates.Hide();
            rangeTotals.Hide();
            rangeDates.Text = "";
            rangeTotals.Text = "";
            return;
        }
        SetRangeSummary();
        rangeDates.Show();
        rangeTotals.Show();
    }

    void SetRangeSummary() {
        DateTime start = rangeStart.Value;
        if (!rangeEnd.HasValue) {
            rangeDates.Text = "开始  " + start.ToString("yyyy-MM-dd");
            rangeTotals.Text = "请选择结束日期";
            return;
        }
        DateTime end = rangeEnd.Value;
        rangeDates.Text = start.ToString("yyyy-MM-dd") + "  至  " + end.ToString("yyyy-MM-dd");
        int count = (end - start).Days + 1;
        bool missing = false;
        for (int year = start.Year; year <= end.Year; year++) {
            if (schedules.ContainsKey(year)) continue;
            if (failed.Contains(year)) { missing = true; continue; }
            rangeTotals.Text = string.Format("共 {0} 天 · 正在获取调休数据", count);
            Fetch(year);
            return;
        }
        int rest = 0;
        for (DateTime day = start; day <= end; day = day.AddDays(1)) {
            Holiday entry = FindHoliday(day);
            if (entry != null ? entry.holiday : day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday)
                rest++;
        }
        rangeTotals.Text = string.Format("共 {0} 天   工作日 {1} 天   休息日 {2} 天", count, count - rest, rest)
            + (missing ? "\n部分调休数据缺失，按周一至周五估算" : "");
    }
    async void Fetch(int year) {
        if (loading.Contains(year) || schedules.ContainsKey(year) || failed.Contains(year)) return;
        loading.Add(year);
        string path = Path.Combine(folder, year + ".json");
        try {
            schedules[year] = await Task.Run(() => {
                var request = (HttpWebRequest)WebRequest.Create("https://timor.tech/api/holiday/year/" + year + "/");
                request.Timeout = 8000;
                request.ReadWriteTimeout = 8000;
                using (var response = request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream())) {
                    string body = reader.ReadToEnd();
                    var data = Parse(body);
                    Save(path, body);
                    return data;
                }
            });
            failed.Remove(year);
        } catch {
            failed.Add(year);
            try {
                if (!schedules.ContainsKey(year) && File.Exists(path)) schedules[year] = Parse(File.ReadAllText(path));
            } catch (IOException) {} catch (InvalidOperationException) {} catch (ArgumentException) {}
        }
        loading.Remove(year);
        if (!IsDisposed) RenderMonth();
    }
    static Dictionary<string, Holiday> Parse(string body) {
        var data = new JavaScriptSerializer().Deserialize<Schedule>(body);
        if (data == null || data.code != 0 || data.holiday == null || data.holiday.Count == 0)
            throw new InvalidDataException("没有有效的调休数据");
        return data.holiday;
    }
    static void Save(string path, string body) {
        string temp = path + ".tmp";
        File.WriteAllText(temp, body);
        if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
    }
}
