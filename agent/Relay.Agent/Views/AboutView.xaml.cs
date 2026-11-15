using System.Diagnostics;
using System.Windows.Controls;

namespace Relay.Agent.Views;

/// <summary>The About page. The facts come from <see cref="AppInfo"/> and <see cref="AboutLinks"/>.</summary>
public partial class AboutView : UserControl
{
    public AboutView()
    {
        InitializeComponent();
        Logo.Source = IconFactory.CreateImageSource(96);
        VersionText.Text = $"Version {AppInfo.Version}";
        FactVersion.Text = AppInfo.Version;
        Monogram.Text = AboutLinks.Monogram(AboutLinks.AuthorName);
        AuthorName.Text = AboutLinks.AuthorName;
        InstagramHandle.Text = AboutLinks.InstagramHandle;
        GitHubHandle.Text = AboutLinks.GitHubHandle;
        LicenseText.Text = AboutLinks.LicenseName;
        CreditsList.ItemsSource = AboutLinks.Credits;
    }

    private void Instagram_Click(object sender, System.Windows.RoutedEventArgs e) => Open(AboutLinks.InstagramUrl);
    private void GitHub_Click(object sender, System.Windows.RoutedEventArgs e) => Open(AboutLinks.GitHubUrl);
    private void Issues_Click(object sender, System.Windows.RoutedEventArgs e) => Open(AboutLinks.IssuesUrl);
    private void Repo_Click(object sender, System.Windows.RoutedEventArgs e) => Open(AboutLinks.RepoUrl);

    private static void Open(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }
}
