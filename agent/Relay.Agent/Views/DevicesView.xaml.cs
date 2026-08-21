using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Relay.Agent.Views;

public partial class DevicesView : UserControl
{
    private static readonly Brush Green = new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60));
    private static readonly Brush Red = new SolidColorBrush(Color.FromRgb(0xC0, 0x39, 0x2B));

    private readonly AppServices _svc;
    private readonly string _uri;
    private readonly HashSet<string> _known = new();
    private readonly DispatcherTimer _timer;

    public DevicesView(AppServices svc)
    {
        InitializeComponent();
        _svc = svc;

        var ip = Pairing.Pairing.LocalIpv4();
        _uri = Pairing.Pairing.BuildUri(ip, svc.Config.Port, svc.Config.Token, svc.Config.AgentId);

        using (var bmp = Pairing.Pairing.Qr(_uri))
            Qr.Source = ToImageSource(bmp);

        HostLine.Text = $"Host    {ip}";
        PortLine.Text = $"Port    {svc.Config.Port}";
        TokenLine.Text = $"Token   {svc.Config.Token}";

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
        Refresh();
    }

    public void Refresh()
    {
        var connected = _svc.Sessions.All.Select(s => s.DeviceName ?? "phone").ToHashSet();
        foreach (var n in connected) _known.Add(n);

        PhonesList.Items.Clear();
        foreach (var name in _known.OrderBy(x => x))
            PhonesList.Items.Add(DeviceRow(name, connected.Contains(name)));

        NoPhones.Visibility = _known.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static StackPanel DeviceRow(string name, bool connected)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 5) };
        row.Children.Add(new Ellipse
        {
            Width = 11, Height = 11, VerticalAlignment = VerticalAlignment.Center,
            Fill = connected ? Green : Red,
        });
        row.Children.Add(new TextBlock
        {
            Text = name, Margin = new Thickness(10, 0, 0, 0), FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
        });
        return row;
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private void CopyUri_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(_uri); } catch { }
    }

    private static ImageSource ToImageSource(System.Drawing.Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        ms.Position = 0;
        var img = new BitmapImage();
        img.BeginInit();
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.StreamSource = ms;
        img.EndInit();
        img.Freeze();
        return img;
    }
}
