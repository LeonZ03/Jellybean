using System.Drawing;
using Forms=System.Windows.Forms;
using Jellybean.Engine;

namespace Jellybean.Services;

public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ContextMenuStrip _menu = new();
    private readonly List<(Forms.ToolStripMenuItem Item,Func<bool> Read)> _checks = new();

    public TrayService(DesktopSession session)
    {
        _menu.Font=new Font("Microsoft YaHei UI",9);
        var title=new Forms.ToolStripMenuItem("糖豆 Jellybean") { Enabled=false };
        _menu.Items.Add(title);
        Add("摸摸糖豆",()=>session.Perform(session.Engine.Pet));
        Add("逗猫棒（20 秒）",()=>session.Perform(session.Engine.ToggleToy));
        Add("伸个懒腰",()=>session.Perform(session.Engine.Stretch));
        Add("打个盹",()=>session.Perform(session.Engine.Doze));
        Add("背过身坐坐",()=>session.Perform(session.Engine.Perch));
        Add("跳到附近窗口",()=>session.Perform(()=>session.Engine.JumpToWindow()));
        Add("从窗沿探头",()=>session.Perform(session.Engine.Peek));
        var views=new Forms.ToolStripMenuItem("看看其他角度");
        var names=new[]{"正面","正面斜侧","左侧","右侧","背面","背面斜侧"};
        var clips=new[]{"front","front-three-quarter","left-view","right-view","rear","rear-three-quarter"};
        for(var i=0;i<names.Length;i++) { var clip=clips[i]; views.DropDownItems.Add(names[i],null,(_,_)=>session.ShowAngle(clip)); }
        _menu.Items.Add(views);
        _menu.Items.Add(new Forms.ToolStripSeparator());
        Check("显示糖豆",()=>!session.Hidden,()=>session.SetHidden(!session.Hidden));
        Check("自己活动",()=>session.Settings.Autonomous,()=> { session.Settings.Autonomous=!session.Settings.Autonomous; session.SaveSettings(); });
        Check("置顶",()=>session.Settings.Topmost,()=> { session.Settings.Topmost=!session.Settings.Topmost; session.SaveSettings(); });
        Check("全屏游戏时收起",()=>session.Settings.HideInFullscreen,()=> { session.Settings.HideInFullscreen=!session.Settings.HideInFullscreen; session.SaveSettings(); });
        _menu.Items.Add(new Forms.ToolStripSeparator());
        Add("把糖豆叫回来",session.ResetPosition);
        Add("设置…",session.OpenSettings);
        Add("退出",session.Exit);
        _menu.Opening+=(_,_)=> { foreach(var (item,read) in _checks) item.Checked=read(); };
        _icon=new Forms.NotifyIcon
        {
            Icon=new Icon(Path.Combine(AppContext.BaseDirectory,"Assets/jellybean.ico")),
            Text="糖豆 · 双击打开设置",ContextMenuStrip=_menu,Visible=true
        };
        _icon.MouseDoubleClick+=(_,e)=> { if(e.Button==Forms.MouseButtons.Left) session.OpenSettings(); };
    }

    private void Add(string text,Action action) => _menu.Items.Add(text,null,(_,_)=>action());
    private void Check(string text,Func<bool> read,Action action)
    {
        var item=new Forms.ToolStripMenuItem(text); item.Click+=(_,_)=>action();
        _checks.Add((item,read)); _menu.Items.Add(item);
    }

    public void ShowMenu()
    {
        _menu.Show(Forms.Cursor.Position);
    }

    public void Dispose()
    {
        _icon.Visible=false; _icon.Icon?.Dispose(); _icon.Dispose(); _menu.Dispose();
    }
}
