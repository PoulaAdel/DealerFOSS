// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   FakeAuthenticator — a passkey authenticator in software, for the tests.
//
// Usage:
//   New FakeAuthenticator(relyingPartyId), then Register(...) and Sign(...).
//
// Coding Instructions:
//   This exists because the alternative was worthless. A passkey test built
//   on hand-written byte arrays and a stubbed verifier proves that the stub
//   returns what it was told to; it cannot fail when the real verification
//   is broken, which is the only thing worth testing here.
//
//   So this holds a real P-256 key, writes real CBOR, assembles real
//   authenticator data, and produces real ECDSA signatures. Breaking a check
//   in WebAuthn.cs makes tests using this fail, and that is the whole point.
//
//   It is deliberately capable of misbehaving — wrong origin, wrong relying
//   party, a counter that goes backwards, a signature over the wrong bytes —
//   because a verifier is only proven by what it refuses.

using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DealerFOSS.UnitTests;

internal sealed class FakeAuthenticator(string relyingPartyId) : IDisposable
{
    private const byte UserPresent = 0x01;
    private const byte AttestedCredentialData = 0x40;

    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly string _relyingPartyId = relyingPartyId;

    public byte[] CredentialId { get; } = RandomNumberGenerator.GetBytes(32);

    public uint Counter { get; set; }

    /// <summary>A registration response, as the browser would hand it over.</summary>
    public (byte[] ClientDataJson, byte[] AttestationObject) Register(
        byte[] challenge, string origin, string? attestationFormat = "none")
    {
        var clientData = ClientData("webauthn.create", challenge, origin);
        var authData = AuthenticatorData(includeCredential: true, _relyingPartyId);

        var writer = new CborWriter(CborConformanceMode.Canonical);
        writer.WriteStartMap(3);
        writer.WriteTextString("fmt");
        writer.WriteTextString(attestationFormat ?? "none");
        writer.WriteTextString("attStmt");
        writer.WriteStartMap(0);
        writer.WriteEndMap();
        writer.WriteTextString("authData");
        writer.WriteByteString(authData);
        writer.WriteEndMap();

        return (clientData, writer.Encode());
    }

    /// <summary>
    /// A sign-in response. <paramref name="relyingPartyId"/> and
    /// <paramref name="signOverJunk"/> exist so a test can produce a response
    /// that is well formed and still wrong.
    /// </summary>
    public (byte[] ClientDataJson, byte[] AuthenticatorData, byte[] Signature) Sign(
        byte[] challenge,
        string origin,
        string? relyingPartyId = null,
        bool userPresent = true,
        bool signOverJunk = false)
    {
        var clientData = ClientData("webauthn.get", challenge, origin);
        var authData = AuthenticatorData(
            includeCredential: false, relyingPartyId ?? _relyingPartyId, userPresent);

        var signed = new byte[authData.Length + 32];
        authData.CopyTo(signed, 0);
        SHA256.HashData(clientData).CopyTo(signed, authData.Length);

        if (signOverJunk)
        {
            signed = Encoding.UTF8.GetBytes("not the bytes the authenticator was asked to sign");
        }

        var signature = _key.SignData(
            signed, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

        return (clientData, authData, signature);
    }

    private static byte[] ClientData(string type, byte[] challenge, string origin) =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            type,
            challenge = Convert.ToBase64String(challenge).TrimEnd('=').Replace('+', '-').Replace('/', '_'),
            origin,
            crossOrigin = false,
        });

    private byte[] AuthenticatorData(bool includeCredential, string relyingPartyId, bool userPresent = true)
    {
        var data = new List<byte>();
        data.AddRange(SHA256.HashData(Encoding.UTF8.GetBytes(relyingPartyId)));

        byte flags = 0;
        if (userPresent) { flags |= UserPresent; }
        if (includeCredential) { flags |= AttestedCredentialData; }
        data.Add(flags);

        data.AddRange(
        [
            (byte)(Counter >> 24), (byte)(Counter >> 16), (byte)(Counter >> 8), (byte)Counter,
        ]);

        if (!includeCredential)
        {
            return [.. data];
        }

        data.AddRange(new byte[16]);                       // AAGUID, all zeroes
        data.Add((byte)(CredentialId.Length >> 8));
        data.Add((byte)CredentialId.Length);
        data.AddRange(CredentialId);
        data.AddRange(CoseKey());

        return [.. data];
    }

    /// <summary>
    /// The public key as COSE_Key. Canonical ordering, so the labels arrive as
    /// 1 (kty), 3 (alg), -1 (crv), -2 (x), -3 (y) — which is the order the
    /// verifier's sequential reader relies on to know an EC2 key from an RSA one.
    /// </summary>
    private byte[] CoseKey()
    {
        var parameters = _key.ExportParameters(includePrivateParameters: false);

        var writer = new CborWriter(CborConformanceMode.Canonical);
        writer.WriteStartMap(5);
        writer.WriteInt32(1);
        writer.WriteInt32(2);      // kty: EC2
        writer.WriteInt32(3);
        writer.WriteInt32(-7);     // alg: ES256
        writer.WriteInt32(-1);
        writer.WriteInt32(1);      // crv: P-256
        writer.WriteInt32(-2);
        writer.WriteByteString(parameters.Q.X!);
        writer.WriteInt32(-3);
        writer.WriteByteString(parameters.Q.Y!);
        writer.WriteEndMap();

        return writer.Encode();
    }

    public void Dispose() => _key.Dispose();
}
