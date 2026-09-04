// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   authenticator — a fake WebAuthn authenticator, for tests that walk a passkey.
//
// Usage:
//   const fake = stubAuthenticator(); ... fake.restore();
//
// Coding Instructions:
//   Jsdom implements no part of WebAuthn — `window.PublicKeyCredential` is
//   absent and `navigator.credentials` does not exist. That absence is
//   itself worth testing (`passkeysAvailable()` must be false and the
//   screens must degrade), so this stub is opt-in per test rather than
//   installed globally in setup.ts.
//
//   It does NOT sign anything. The cryptography is verified where it lives,
//   in the .NET suite, against a real P-256 key and a real CBOR attestation
//   object (tests/Unit/FakeAuthenticator.cs). What these tests are for is
//   what the SCREEN does with each outcome — including the two that are not
//   failures: somebody dismissing their own operating system's prompt, and a
//   browser that cannot do this at all.
//
//   `navigator.credentials` is defined on the existing navigator rather than
//   replaced wholesale. Replacing it takes `navigator.languages` with it,
//   and the i18n provider reads that at mount — so every test in the file
//   would silently start in whatever language the fallback picked.

/** What the fake authenticator should do when a ceremony starts. */
export type Behaviour =
  | { kind: 'accepts' }
  /** The person dismissed the prompt, or nobody answered. Not a failure. */
  | { kind: 'cancelled' }
  /** Something genuinely went wrong inside the browser. */
  | { kind: 'errors'; message: string };

export interface FakeAuthenticator {
  /** Removes the stub, so a later test sees a browser with no WebAuthn again. */
  restore: () => void;
  /** How many ceremonies were started. */
  calls: () => number;
}

function bytes(text: string): ArrayBuffer {
  return new TextEncoder().encode(text).buffer as ArrayBuffer;
}

function answer(behaviour: Behaviour, credential: Credential): Promise<Credential | null> {
  if (behaviour.kind === 'cancelled') {
    // The exact exception a real browser raises for both "no" and "timed out";
    // the specification deliberately does not distinguish them.
    return Promise.reject(new DOMException('The operation was not allowed.', 'NotAllowedError'));
  }

  if (behaviour.kind === 'errors') {
    return Promise.reject(new Error(behaviour.message));
  }

  return Promise.resolve(credential);
}

export function stubAuthenticator(behaviour: Behaviour = { kind: 'accepts' }): FakeAuthenticator {
  let calls = 0;

  const registration = {
    id: 'Y3JlZC1vbmU',
    rawId: bytes('cred-one'),
    type: 'public-key',
    response: {
      clientDataJSON: bytes('{"type":"webauthn.create"}'),
      attestationObject: bytes('fake-attestation'),
    },
  } as unknown as Credential;

  const assertion = {
    id: 'Y3JlZC1vbmU',
    rawId: bytes('cred-one'),
    type: 'public-key',
    response: {
      clientDataJSON: bytes('{"type":"webauthn.get"}'),
      authenticatorData: bytes('fake-authenticator-data'),
      signature: bytes('fake-signature'),
      userHandle: bytes('user-one'),
    },
  } as unknown as Credential;

  const credentials = {
    create: () => {
      calls += 1;
      return answer(behaviour, registration);
    },
    get: () => {
      calls += 1;
      return answer(behaviour, assertion);
    },
  };

  const hadCredentials = 'credentials' in navigator;
  const previousCredentials = (navigator as { credentials?: unknown }).credentials;

  Object.defineProperty(navigator, 'credentials', {
    value: credentials,
    configurable: true,
    writable: true,
  });

  (globalThis as { PublicKeyCredential?: unknown }).PublicKeyCredential = function () {};

  return {
    calls: () => calls,
    restore: () => {
      if (hadCredentials) {
        Object.defineProperty(navigator, 'credentials', {
          value: previousCredentials,
          configurable: true,
          writable: true,
        });
      } else {
        delete (navigator as { credentials?: unknown }).credentials;
      }

      delete (globalThis as { PublicKeyCredential?: unknown }).PublicKeyCredential;
    },
  };
}
