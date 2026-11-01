using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Hoard.Host.Startup;

/// <summary>
/// Loads and checks the <c>https</c> certificate before the host exists (research R5). Every
/// reason is Hoard's own wording: exception messages are never shown, so no value is echoed.
/// </summary>
internal static class CertificateLoader
{
    private const string Prefix = "certificate could not be opened: ";

    public static X509Certificate2? Load(
        OptionValue certificate,
        OptionValue? key,
        OptionValue? password,
        string workingDirectory,
        DateTimeOffset now,
        List<ConfigProblem> problems)
    {
        var outcome = BoundedFile.TryRead(Path.GetFullPath(certificate.Value, workingDirectory), out var content);
        if (outcome != BoundedFile.Outcome.Read)
        {
            problems.Add(new(certificate.Source.Location, Prefix + BoundedFile.Describe(outcome)));
            return null;
        }

        var loaded = IsPem(content)
            ? LoadPem(certificate, content, key, password?.Value, workingDirectory, problems)
            : LoadPkcs12(certificate, content, key, password?.Value, problems);
        if (loaded is null)
        {
            return null;
        }

        var reason = !loaded.HasPrivateKey ? "the certificate has no private key"
            : now.UtcDateTime < loaded.NotBefore.ToUniversalTime() ? $"the certificate is not valid until {loaded.NotBefore.ToUniversalTime():yyyy-MM-dd HH:mm} UTC"
            : now.UtcDateTime > loaded.NotAfter.ToUniversalTime() ? $"the certificate expired on {loaded.NotAfter.ToUniversalTime():yyyy-MM-dd HH:mm} UTC"
            : null;
        if (reason is not null)
        {
            loaded.Dispose();
            problems.Add(new(certificate.Source.Location, Prefix + reason));
            return null;
        }

        return loaded;
    }

    private static bool IsPem(byte[] content) =>
        Encoding.UTF8.GetString(content, 0, Math.Min(content.Length, 64)).TrimStart('﻿', ' ', '\t', '\r', '\n').StartsWith("-----BEGIN", StringComparison.Ordinal);

    private static X509Certificate2? LoadPem(OptionValue certificate, byte[] content, OptionValue? key, string? password, string workingDirectory, List<ConfigProblem> problems)
    {
        var certificateText = Encoding.UTF8.GetString(content);
        var keyBytes = content;
        if (key is not null)
        {
            var outcome = BoundedFile.TryRead(Path.GetFullPath(key.Value, workingDirectory), out keyBytes);
            if (outcome != BoundedFile.Outcome.Read)
            {
                problems.Add(new(key.Source.Location, "key could not be opened: " + BoundedFile.Describe(outcome)));
                return null;
            }
        }

        var keyText = Encoding.UTF8.GetString(keyBytes);
        if (key is not null)
        {
            CryptographicOperations.ZeroMemory(keyBytes);
        }

        try
        {
            using (X509Certificate2.CreateFromPem(certificateText))
            {
            }
        }
        catch (CryptographicException)
        {
            problems.Add(new(certificate.Source.Location, Prefix + "the file holds no readable PEM certificate"));
            return null;
        }

        var encrypted = keyText.Contains("-----BEGIN ENCRYPTED PRIVATE KEY-----", StringComparison.Ordinal);
        if (encrypted && password is null)
        {
            problems.Add(new(certificate.Source.Location, Prefix + $"the private key is encrypted; set {OptionNames.Password.Key} ({OptionNames.Password.EnvironmentName} or the config file)"));
            return null;
        }

        X509Certificate2 loaded;
        try
        {
            loaded = encrypted
                ? X509Certificate2.CreateFromEncryptedPem(certificateText, keyText, password)
                : X509Certificate2.CreateFromPem(certificateText, keyText);
        }
        catch (Exception e) when (e is CryptographicException or ArgumentException)
        {
            var reason = !keyText.Contains("PRIVATE KEY-----", StringComparison.Ordinal)
                ? $"no private key was found; put it in the certificate file or set {OptionNames.KeyFile.Key}"
                : encrypted
                    ? "the private key could not be decrypted with the configured password, or does not match the certificate"
                    : "the private key could not be read, or does not match the certificate";
            problems.Add(new(certificate.Source.Location, Prefix + reason));
            return null;
        }

        if (!OperatingSystem.IsWindows())
        {
            return loaded;
        }

        // Windows SslStream refuses the ephemeral key a PEM load gives; Kestrel's own loader
        // round-trips it through PKCS#12 the same way.
        var exported = loaded.Export(X509ContentType.Pkcs12);
        try
        {
            return X509CertificateLoader.LoadPkcs12(exported, password: null);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(exported);
            loaded.Dispose();
        }
    }

    private static X509Certificate2? LoadPkcs12(OptionValue certificate, byte[] content, OptionValue? key, string? password, List<ConfigProblem> problems)
    {
        if (key is not null)
        {
            problems.Add(new(key.Source.Location, $"is set, but the certificate is a PKCS#12 file, which holds its own key; remove {OptionNames.KeyFile.Key}"));
            return null;
        }

        try
        {
            return X509CertificateLoader.LoadPkcs12(content, password);
        }
        catch (CryptographicException)
        {
            var reason = password is null
                ? $"the file is neither PEM nor a PKCS#12 file that opens without a password; set {OptionNames.Password.Key} if it has one"
                : "the file is neither PEM nor a PKCS#12 file that opens with the configured password";
            problems.Add(new(certificate.Source.Location, Prefix + reason));
            return null;
        }
    }
}
