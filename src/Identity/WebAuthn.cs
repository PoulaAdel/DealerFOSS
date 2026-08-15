// WebAuthn — the cryptography behind a passkey, and nothing else.
//
// Use:  internal to Identity. PasskeyDirectory calls it; nothing else may.
// Edit: READ THIS BEFORE CHANGING A LINE. Every check below is the reason a
//       passkey cannot be phished, replayed, or forged. Removing one does not
//       break a test that says "removing this breaks sign-in" — it silently
//       turns strong authentication into a formality, which is the worst
//       failure mode a security control has.
//
//       The checks, and what each one stops:
//
//       * Type. "webauthn.create" on registration, "webauthn.get" on sign-in.
//         Without it a registration response can be replayed as a sign-in.
//       * Challenge. Must equal the one WE issued, and it is single-use. This
//         is what makes the ceremony fresh rather than a recording.
//       * Origin. Must be one we expect. THIS is the anti-phishing property:
//         the browser puts the real origin in, and an authenticator on
//         evil-example.com cannot produce a signature that names ours.
//       * RP ID hash. The first 32 bytes of authenticator data are SHA-256 of
//         the relying party id. Binds the credential to this application.
//       * User present. Bit 0 of the flags. Somebody physically touched it.
//       * Signature. Over authenticatorData || SHA-256(clientDataJSON), with
//         the public key recorded at registration.
//       * Sign count. If the authenticator counts, a count that does not
//         advance means a cloned credential.
//
//       Attestation is deliberately NOT verified. We accept "none", which is
//       what platform passkeys send by default, and refuse the rest. Verifying
//       attestation proves which authenticator model was used — a fleet-control
//       question, not an authentication one — and doing it badly is worse than
//       not doing it. Named here so nobody assumes it happens.

using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DealerFOSS.Identity;

/// <summary>What an authenticator sent back, once it has been proven genuine.</summary>
internal sealed record VerifiedRegistration(
    byte[] CredentialId,
    byte[] PublicKeySpki,
    CoseAlgorithm Algorithm,
    uint SignCount);

/// <summary>The two algorithms a passkey realistically uses.</summary>
internal enum CoseAlgorithm
{
    /// <summary>ECDSA over P-256 with SHA-256. COSE -7. What almost everything sends.</summary>
    Es256 = -7,

    /// <summary>RSASSA-PKCS1-v1_5 with SHA-256. COSE -257. Some security keys.</summary>
    Rs256 = -257,
}

/// <summary>
/// Why a ceremony was refused. Deliberately coarse at the edge — a caller is
/// told "that did not work" — but precise here, because whoever is debugging a
/// failing authenticator needs to know which check rejected it.
/// </summary>
internal enum WebAuthnFailure
{
    None = 0,
    MalformedResponse,
    WrongCeremonyType,
    ChallengeMismatch,
    UntrustedOrigin,
    WrongRelyingParty,
    UserNotPresent,
    UnsupportedAlgorithm,
    UnsupportedAttestation,
    BadSignature,
    CounterWentBackwards,
}

internal static class WebAuthn
{
    /// <summary>Bit 0 of the authenticator-data flags byte.</summary>
    private const byte UserPresentFlag = 0x01;

    /// <summary>Bit 6 — attested credential data is present.</summary>
    private const byte AttestedCredentialDataFlag = 0x40;

    /// <summary>RP ID hash (32) + flags (1) + counter (4).</summary>
    private const int AuthenticatorDataHeaderLength = 37;

    /// <summary>
    /// Proves a registration response and pulls out the credential to store.
    /// </summary>
    public static WebAuthnFailure TryVerifyRegistration(
        byte[] clientDataJson,
        byte[] attestationObject,
        byte[] expectedChallenge,
        string relyingPartyId,
        IReadOnlyCollection<string> allowedOrigins,
        out VerifiedRegistration? registration)
    {
        registration = null;

        var clientData = CheckClientData(
            clientDataJson, "webauthn.create", expectedChallenge, allowedOrigins);

        if (clientData != WebAuthnFailure.None)
        {
            return clientData;
        }

        byte[] authenticatorData;
        try
        {
            var reader = new CborReader(attestationObject);
            var count = reader.ReadStartMap();
            string? format = null;
            byte[]? authData = null;

            for (var i = 0; i < count; i++)
            {
                var key = reader.ReadTextString();
                switch (key)
                {
                    case "fmt":
                        format = reader.ReadTextString();
                        break;
                    case "authData":
                        authData = reader.ReadByteString();
                        break;
                    default:
                        reader.SkipValue();
                        break;
                }
            }

            // "none" only, and said out loud. See the header.
            if (format != "none")
            {
                return WebAuthnFailure.UnsupportedAttestation;
            }

            if (authData is null)
            {
                return WebAuthnFailure.MalformedResponse;
            }

            authenticatorData = authData;
        }
        catch (Exception ex) when (ex is CborContentException or InvalidOperationException or ArgumentException)
        {
            return WebAuthnFailure.MalformedResponse;
        }

        if (authenticatorData.Length < AuthenticatorDataHeaderLength)
        {
            return WebAuthnFailure.MalformedResponse;
        }

        if (!RelyingPartyMatches(authenticatorData, relyingPartyId))
        {
            return WebAuthnFailure.WrongRelyingParty;
        }

        var flags = authenticatorData[32];
        if ((flags & UserPresentFlag) == 0)
        {
            return WebAuthnFailure.UserNotPresent;
        }

        if ((flags & AttestedCredentialDataFlag) == 0)
        {
            return WebAuthnFailure.MalformedResponse;
        }

        // attestedCredentialData: aaguid(16) | credIdLen(2, big-endian) | credId | COSE key
        var offset = AuthenticatorDataHeaderLength;
        if (authenticatorData.Length < offset + 18)
        {
            return WebAuthnFailure.MalformedResponse;
        }

        offset += 16;
        var credentialIdLength = (authenticatorData[offset] << 8) | authenticatorData[offset + 1];
        offset += 2;

        if (credentialIdLength <= 0 || authenticatorData.Length < offset + credentialIdLength)
        {
            return WebAuthnFailure.MalformedResponse;
        }

        var credentialId = authenticatorData[offset..(offset + credentialIdLength)];
        offset += credentialIdLength;

        var failure = TryReadCoseKey(
            authenticatorData[offset..], out var spki, out var algorithm);

        if (failure != WebAuthnFailure.None)
        {
            return failure;
        }

        registration = new VerifiedRegistration(
            credentialId, spki!, algorithm, ReadCounter(authenticatorData));

        return WebAuthnFailure.None;
    }

    /// <summary>
    /// Proves a sign-in response against the key recorded at registration.
    /// </summary>
    public static WebAuthnFailure TryVerifyAssertion(
        byte[] clientDataJson,
        byte[] authenticatorData,
        byte[] signature,
        byte[] expectedChallenge,
        string relyingPartyId,
        IReadOnlyCollection<string> allowedOrigins,
        byte[] publicKeySpki,
        CoseAlgorithm algorithm,
        uint storedSignCount,
        out uint newSignCount)
    {
        newSignCount = storedSignCount;

        var clientData = CheckClientData(
            clientDataJson, "webauthn.get", expectedChallenge, allowedOrigins);

        if (clientData != WebAuthnFailure.None)
        {
            return clientData;
        }

        if (authenticatorData.Length < AuthenticatorDataHeaderLength)
        {
            return WebAuthnFailure.MalformedResponse;
        }

        if (!RelyingPartyMatches(authenticatorData, relyingPartyId))
        {
            return WebAuthnFailure.WrongRelyingParty;
        }

        if ((authenticatorData[32] & UserPresentFlag) == 0)
        {
            return WebAuthnFailure.UserNotPresent;
        }

        // The signed message: authenticator data, then the hash of the client
        // data. Concatenated exactly this way and in this order by the spec.
        var signed = new byte[authenticatorData.Length + 32];
        authenticatorData.CopyTo(signed, 0);
        SHA256.HashData(clientDataJson).CopyTo(signed, authenticatorData.Length);

        if (!SignatureIsGenuine(signed, signature, publicKeySpki, algorithm))
        {
            return WebAuthnFailure.BadSignature;
        }

        var counter = ReadCounter(authenticatorData);

        // Zero on both sides means the authenticator does not count, which is
        // allowed and common for platform passkeys. Anything else must advance:
        // a counter that repeats or goes backwards is the signature of a cloned
        // credential, and it is the only clone detection the standard offers.
        if (counter != 0 || storedSignCount != 0)
        {
            if (counter <= storedSignCount)
            {
                return WebAuthnFailure.CounterWentBackwards;
            }
        }

        newSignCount = counter;
        return WebAuthnFailure.None;
    }

    private static WebAuthnFailure CheckClientData(
        byte[] clientDataJson,
        string expectedType,
        byte[] expectedChallenge,
        IReadOnlyCollection<string> allowedOrigins)
    {
        string? type;
        string? challenge;
        string? origin;

        try
        {
            using var document = JsonDocument.Parse(clientDataJson);
            var root = document.RootElement;
            type = root.TryGetProperty("type", out var t) ? t.GetString() : null;
            challenge = root.TryGetProperty("challenge", out var c) ? c.GetString() : null;
            origin = root.TryGetProperty("origin", out var o) ? o.GetString() : null;
        }
        catch (JsonException)
        {
            return WebAuthnFailure.MalformedResponse;
        }

        if (type != expectedType)
        {
            return WebAuthnFailure.WrongCeremonyType;
        }

        if (challenge is null || origin is null)
        {
            return WebAuthnFailure.MalformedResponse;
        }

        byte[] sent;
        try
        {
            sent = Base64Url.Decode(challenge);
        }
        catch (FormatException)
        {
            return WebAuthnFailure.MalformedResponse;
        }

        // Fixed-time: the challenge is a secret we issued, and comparing it with
        // an early return would leak how much of it a caller had guessed.
        if (!CryptographicOperations.FixedTimeEquals(sent, expectedChallenge))
        {
            return WebAuthnFailure.ChallengeMismatch;
        }

        if (!allowedOrigins.Contains(origin, StringComparer.Ordinal))
        {
            return WebAuthnFailure.UntrustedOrigin;
        }

        return WebAuthnFailure.None;
    }

    private static bool RelyingPartyMatches(byte[] authenticatorData, string relyingPartyId) =>
        CryptographicOperations.FixedTimeEquals(
            authenticatorData.AsSpan(0, 32),
            SHA256.HashData(Encoding.UTF8.GetBytes(relyingPartyId)));

    /// <summary>Bytes 33..36, big-endian.</summary>
    private static uint ReadCounter(byte[] authenticatorData) =>
        ((uint)authenticatorData[33] << 24) |
        ((uint)authenticatorData[34] << 16) |
        ((uint)authenticatorData[35] << 8) |
        authenticatorData[36];

    /// <summary>
    /// Turns a COSE_Key into SPKI, which is what .NET's importers take. Storing
    /// SPKI rather than the raw COSE map means the CBOR reader is needed once, at
    /// registration, and never again on the sign-in path.
    /// </summary>
    private static WebAuthnFailure TryReadCoseKey(
        byte[] cose, out byte[]? spki, out CoseAlgorithm algorithm)
    {
        spki = null;
        algorithm = CoseAlgorithm.Es256;

        try
        {
            var reader = new CborReader(cose);
            var count = reader.ReadStartMap();

            long? keyType = null;
            long? alg = null;
            byte[]? x = null, y = null, modulus = null, exponent = null;

            for (var i = 0; i < count; i++)
            {
                var label = reader.ReadInt64();
                switch (label)
                {
                    case 1: keyType = reader.ReadInt64(); break;
                    case 3: alg = reader.ReadInt64(); break;
                    case -1:
                        // Curve for EC2, modulus for RSA. Same label, different
                        // meaning — which is why key type is read first.
                        if (keyType == 3) { modulus = reader.ReadByteString(); }
                        else { reader.SkipValue(); }
                        break;
                    case -2:
                        if (keyType == 3) { exponent = reader.ReadByteString(); }
                        else { x = reader.ReadByteString(); }
                        break;
                    case -3: y = reader.ReadByteString(); break;
                    default: reader.SkipValue(); break;
                }
            }

            if (alg == (long)CoseAlgorithm.Es256 && x is not null && y is not null)
            {
                using var ecdsa = ECDsa.Create(new ECParameters
                {
                    Curve = ECCurve.NamedCurves.nistP256,
                    Q = new ECPoint { X = x, Y = y },
                });

                spki = ecdsa.ExportSubjectPublicKeyInfo();
                algorithm = CoseAlgorithm.Es256;
                return WebAuthnFailure.None;
            }

            if (alg == (long)CoseAlgorithm.Rs256 && modulus is not null && exponent is not null)
            {
                using var rsa = RSA.Create(new RSAParameters { Modulus = modulus, Exponent = exponent });
                spki = rsa.ExportSubjectPublicKeyInfo();
                algorithm = CoseAlgorithm.Rs256;
                return WebAuthnFailure.None;
            }

            return WebAuthnFailure.UnsupportedAlgorithm;
        }
        catch (Exception ex) when (ex is CborContentException or CryptographicException
            or InvalidOperationException or ArgumentException)
        {
            return WebAuthnFailure.MalformedResponse;
        }
    }

    private static bool SignatureIsGenuine(
        byte[] signed, byte[] signature, byte[] spki, CoseAlgorithm algorithm)
    {
        try
        {
            if (algorithm == CoseAlgorithm.Es256)
            {
                using var ecdsa = ECDsa.Create();
                ecdsa.ImportSubjectPublicKeyInfo(spki, out _);

                // WebAuthn sends ECDSA signatures DER-encoded, not as a raw r||s pair.
                return ecdsa.VerifyData(signed, signature, HashAlgorithmName.SHA256,
                    DSASignatureFormat.Rfc3279DerSequence);
            }

            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(spki, out _);
            return rsa.VerifyData(signed, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (CryptographicException)
        {
            // A malformed signature is a failed verification, not an outage.
            return false;
        }
    }
}

/// <summary>
/// Base64url without padding — what WebAuthn puts on the wire everywhere.
/// </summary>
internal static class Base64Url
{
    public static string Encode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] Decode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch { 2 => "==", 3 => "=", _ => "" };
        return Convert.FromBase64String(padded);
    }
}
