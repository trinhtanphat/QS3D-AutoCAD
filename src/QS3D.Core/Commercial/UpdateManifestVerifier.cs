using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QS3D.Core.Commercial;

public sealed record UpdateManifestEnvelope(
    [property: JsonPropertyName("payload")] string PayloadBase64,
    [property: JsonPropertyName("signature")] string SignatureBase64);

public sealed record UpdateManifestPayload(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("channel")] string Channel,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("minimumAutoCadGeneration")] int MinimumAutoCadGeneration,
    [property: JsonPropertyName("maximumAutoCadGeneration")] int MaximumAutoCadGeneration,
    [property: JsonPropertyName("packageUri")] string PackageUri,
    [property: JsonPropertyName("packageSha256")] string PackageSha256,
    [property: JsonPropertyName("publishedAtUtc")] DateTimeOffset PublishedAtUtc);

public static class UpdateManifestVerifier
{
    public static UpdateManifestPayload Verify(
        string envelopeJson,
        string publicKeyPem,
        int autoCadGeneration,
        string expectedChannel)
    {
        if (string.IsNullOrWhiteSpace(envelopeJson))
        {
            throw new ArgumentException("Signed update manifest is required.", nameof(envelopeJson));
        }
        if (string.IsNullOrWhiteSpace(publicKeyPem))
        {
            throw new ArgumentException("Updater public key is required.", nameof(publicKeyPem));
        }
        if (string.IsNullOrWhiteSpace(expectedChannel))
        {
            throw new ArgumentException("Expected update channel is required.", nameof(expectedChannel));
        }

        UpdateManifestEnvelope envelope;
        byte[] payloadBytes;
        byte[] signatureBytes;
        try
        {
            envelope = JsonSerializer.Deserialize<UpdateManifestEnvelope>(envelopeJson)
                ?? throw new InvalidDataException("Update manifest envelope is empty.");
            payloadBytes = Convert.FromBase64String(envelope.PayloadBase64);
            signatureBytes = Convert.FromBase64String(envelope.SignatureBase64);
        }
        catch (Exception exception) when (exception is JsonException or FormatException or ArgumentNullException)
        {
            throw new InvalidDataException("Update manifest envelope is malformed.", exception);
        }

        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem);
            if (!rsa.VerifyData(
                    payloadBytes,
                    signatureBytes,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pss))
            {
                throw new InvalidDataException("Update manifest signature is invalid.");
            }
        }
        catch (Exception exception) when (exception is CryptographicException or ArgumentException)
        {
            throw new InvalidDataException("Updater public key or signature is invalid.", exception);
        }

        UpdateManifestPayload payload;
        try
        {
            payload = JsonSerializer.Deserialize<UpdateManifestPayload>(payloadBytes)
                ?? throw new InvalidDataException("Update manifest payload is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Update manifest payload is malformed.", exception);
        }

        ValidatePayload(payload, autoCadGeneration, expectedChannel.Trim());
        return payload;
    }

    public static void VerifyPackage(ReadOnlySpan<byte> packageBytes, string expectedSha256)
    {
        byte[] expected;
        try
        {
            expected = Convert.FromHexString(NormalizeSha256(expectedSha256));
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("Expected package SHA-256 is malformed.", exception);
        }

        var actual = SHA256.HashData(packageBytes);
        if (!CryptographicOperations.FixedTimeEquals(actual, expected))
        {
            throw new InvalidDataException("Downloaded update package SHA-256 does not match the signed manifest.");
        }
    }

    private static void ValidatePayload(
        UpdateManifestPayload payload,
        int autoCadGeneration,
        string expectedChannel)
    {
        if (payload.SchemaVersion != 1)
        {
            throw new InvalidDataException($"Unsupported update manifest schema: {payload.SchemaVersion}.");
        }
        if (string.IsNullOrWhiteSpace(payload.Channel) ||
            !string.Equals(payload.Channel, expectedChannel, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Update channel does not match the configured channel.");
        }
        if (string.IsNullOrWhiteSpace(payload.Version))
        {
            throw new InvalidDataException("Update version is required.");
        }
        if (payload.MinimumAutoCadGeneration > payload.MaximumAutoCadGeneration ||
            autoCadGeneration < payload.MinimumAutoCadGeneration ||
            autoCadGeneration > payload.MaximumAutoCadGeneration)
        {
            throw new InvalidDataException("Update is not compatible with this AutoCAD generation.");
        }
        if (!Uri.TryCreate(payload.PackageUri, UriKind.Absolute, out var packageUri) ||
            !string.Equals(packageUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Update package URI must be absolute HTTPS.");
        }

        _ = NormalizeSha256(payload.PackageSha256);
    }

    private static string NormalizeSha256(string value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length != 64 || normalized.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidDataException("Package SHA-256 must contain exactly 64 hexadecimal characters.");
        }
        return normalized;
    }
}
