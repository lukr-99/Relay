using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Relay.Agent.Views;

public partial class DevicesView : UserControl
{
    private readonly AppServices _svc;
    private readonly string _uri;

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

        Refresh();
    }

    public void Refresh()
    {
        PhonesList.Items.Clear();
        foreach (var s in _svc.Sessions.All)
        {
            PhonesList.Items.Add(new TextBlock
            {
                Text = $"•  {s.DeviceName ?? "phone"}   ({s.Id})",
                Margin = new Thickness(0, 3, 0, 3),
                FontSize = 14,
            });
        }
        NoPhones.Visibility = PhonesList.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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
