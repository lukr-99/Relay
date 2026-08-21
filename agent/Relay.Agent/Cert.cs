using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Relay.Agent;

/// <summary>The agent's self-signed TLS certificate for WSS, plus its SHA-256 fingerprint (which the
/// phone pins). Generated once and persisted so the fingerprint is stable across restarts.</summary>
public sealed class Cert
{
    private const string Pwd = "relay"; // local PFX on disk; the security is the pinned fingerprint

    public X509Certificate2 Certificate { get; }

    /// <summary>Lower-case hex SHA-256 of the DER cert — matches what the phone computes from the
    /// presented certificate's encoded bytes.</summary>
    public string FingerprintHex { get; }

    private Cert(X509Certificate2 cert)
    {
        Certificate = cert;
        FingerprintHex = Convert.ToHexString(SHA256.HashData(cert.RawData)).ToLowerInvariant();
    }

    public static Cert LoadOrCreate(AppConfig config, Log log)
    {
        var dir = Path.Combine(config.DataDir, "certs");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "relay.pfx");
        var flags = X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.UserKeySet;

        try
        {
            if (File.Exists(path))
                return new Cert(X509CertificateLoader.LoadPkcs12(File.ReadAllBytes(path), Pwd, flags));
        }
        catch (Exception ex) { log.Warn($"cert load failed, regenerating: {ex.Message}"); }

        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=Relay Agent", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("localhost");
        try { san.AddDnsName(Environment.MachineName); } catch { }
        san.AddIpAddress(IPAddress.Loopback);
        try { san.AddIpAddress(IPAddress.Parse(Pairing.Pairing.LocalIpv4())); } catch { }
        req.CertificateExtensions.Add(san.Build());
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));

        using var selfSigned = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(10));
        var pfx = selfSigned.Export(X509ContentType.Pfx, Pwd);
        try { File.WriteAllBytes(path, pfx); } catch (Exception ex) { log.Warn($"cert save failed: {ex.Message}"); }

        var loaded = X509CertificateLoader.LoadPkcs12(pfx, Pwd, flags);
        var c = new Cert(loaded);
        log.Info($"generated self-signed cert (fp {c.FingerprintHex[..16]}…)");
        return c;
    }
}
