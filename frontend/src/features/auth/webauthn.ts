// webauthn — the browser half of a passkey ceremony.
//
// Use:  const outcome = await createPasskey(challenge, 'Work laptop');
//       if (outcome.kind === 'ready') await post('/auth/passkeys/register/finish', outcome.response);
// Edit: three things here are load-bearing.
//
//       ONE. Everything on the wire is base64url, and everything the WebAuthn
//       API touches is an ArrayBuffer. JSON cannot carry bytes, so the two
//       converters below are the whole reason this file exists. Base64url is
//       NOT base64: `-` and `_` replace `+` and `/`, and the padding is dropped.
//       Feeding plain base64 to an authenticator produces a challenge mismatch
//       the server reports as a forgery, which is a miserable thing to debug.
//
//       TWO. Registration asks for a RESIDENT (discoverable) credential, and
//       must. The sign-in ceremony sends no email and no allowCredentials list —
//       that is deliberate, so the server never confirms whether an account
//       exists — which means the authenticator has to be able to find the
//       credential on its own. A non-resident key registered here would work
//       once, in the enrolment screen, and then never be usable to sign in.
//
//       THREE. A cancelled ceremony is not a failure. Somebody who dismisses
//       the operating system's prompt, or walks away until it times out, gets
//       `NotAllowedError` — and the honest response is to put the screen back
//       the way it was, not to shout an error at them. Hence `kind: 'cancelled'`
//       as a first-class outcome rather than a thrown exception.

import type {
  PasskeyRegistrationChallenge,
  PasskeySignInChallenge,
} from '../../shared/contracts';

/** The algorithms the server can verify: ES256 and RS256 (see WebAuthn.cs). */
const SupportedAlgorithms: PublicKeyCredentialParameters[] = [
  { type: 'public-key', alg: -7 },
  { type: 'public-key', alg: -257 },
];

/** Long enough to find a phone and unlock it, short enough to not hang a screen. */
const CeremonyTimeoutMs = 120_000;

/**
 * Whether this browser can do passkeys at all. False in an insecure context —
 * WebAuthn is unavailable over plain http other than on localhost — and in the
 * test environment, which is why every caller has to render without it.
 */
export function passkeysAvailable(): boolean {
  return (
    typeof window !== 'undefined' &&
    typeof window.PublicKeyCredential === 'function' &&
    navigator.credentials !== undefined
  );
}

/**
 * Returns `Uint8Array<ArrayBuffer>` rather than a bare `Uint8Array`, and is
 * backed by an ArrayBuffer allocated here. TypeScript 5.7 made the typed-array
 * types generic over their buffer, and `BufferSource` — what every WebAuthn
 * field takes — excludes `SharedArrayBuffer`. Constructing the buffer
 * explicitly is what proves it is not shared.
 */
export function fromBase64Url(value: string): Uint8Array<ArrayBuffer> {
  const base64 = value.replaceAll('-', '+').replaceAll('_', '/');
  // atob rejects a string whose length is not a multiple of four.
  const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=');
  const binary = atob(padded);

  const bytes = new Uint8Array(new ArrayBuffer(binary.length));
  for (let i = 0; i < binary.length; i += 1) {
    bytes[i] = binary.charCodeAt(i);
  }

  return bytes;
}

export function toBase64Url(buffer: ArrayBuffer): string {
  const bytes = new Uint8Array(buffer);

  // Built up in chunks rather than String.fromCharCode(...bytes): spreading a
  // large attestation object over the argument list overflows the stack.
  let binary = '';
  for (const byte of bytes) {
    binary += String.fromCharCode(byte);
  }

  return btoa(binary).replaceAll('+', '-').replaceAll('/', '_').replaceAll('=', '');
}

/** What the server's register/finish endpoint takes. */
export interface RegistrationResponseBody {
  challengeId: string;
  clientDataJson: string;
  attestationObject: string;
  label: string;
}

/** What the server's sign-in/finish endpoint takes. */
export interface SignInResponseBody {
  challengeId: string;
  credentialId: string;
  clientDataJson: string;
  authenticatorData: string;
  signature: string;
}

/**
 * The three ways a ceremony ends. `unsupported` and `cancelled` are both
 * ordinary; only `failed` carries a message worth showing, and even then the
 * authenticator's own wording is usually more accurate than anything invented
 * here.
 */
export type Ceremony<T> =
  | { kind: 'ready'; response: T }
  | { kind: 'cancelled' }
  | { kind: 'unsupported' }
  | { kind: 'failed'; reason: string };

export async function createPasskey(
  challenge: PasskeyRegistrationChallenge,
  label: string,
): Promise<Ceremony<RegistrationResponseBody>> {
  if (!passkeysAvailable()) {
    return { kind: 'unsupported' };
  }

  try {
    const credential = (await navigator.credentials.create({
      publicKey: {
        challenge: fromBase64Url(challenge.challenge),
        rp: { id: challenge.relyingPartyId, name: challenge.relyingPartyName },
        user: {
          id: fromBase64Url(challenge.userHandle),
          name: challenge.userName,
          displayName: challenge.userDisplayName,
        },
        pubKeyCredParams: SupportedAlgorithms,
        // So the same authenticator is not enrolled twice and the person ends
        // up with two entries that behave identically.
        excludeCredentials: challenge.alreadyRegistered.map((id) => ({
          type: 'public-key' as const,
          id: fromBase64Url(id),
        })),
        authenticatorSelection: {
          // Required, not preferred. See point TWO in the file header.
          residentKey: 'required',
          requireResidentKey: true,
          userVerification: 'preferred',
        },
        // 'none' because the server does not verify attestation statements
        // (doc 11 §7). Asking for one we will not check would put a hardware
        // serial number on the wire for nothing.
        attestation: 'none',
        timeout: CeremonyTimeoutMs,
      },
    })) as PublicKeyCredential | null;

    if (credential === null) {
      return { kind: 'cancelled' };
    }

    const response = credential.response as AuthenticatorAttestationResponse;

    return {
      kind: 'ready',
      response: {
        challengeId: challenge.challengeId,
        clientDataJson: toBase64Url(response.clientDataJSON),
        attestationObject: toBase64Url(response.attestationObject),
        label,
      },
    };
  } catch (failure) {
    return describe(failure);
  }
}

export async function usePasskey(
  challenge: PasskeySignInChallenge,
): Promise<Ceremony<SignInResponseBody>> {
  if (!passkeysAvailable()) {
    return { kind: 'unsupported' };
  }

  try {
    const credential = (await navigator.credentials.get({
      publicKey: {
        challenge: fromBase64Url(challenge.challenge),
        rpId: challenge.relyingPartyId,
        // No allowCredentials. The authenticator finds the credential itself,
        // which is what lets this ceremony start without an email address.
        userVerification: 'preferred',
        timeout: CeremonyTimeoutMs,
      },
    })) as PublicKeyCredential | null;

    if (credential === null) {
      return { kind: 'cancelled' };
    }

    const response = credential.response as AuthenticatorAssertionResponse;

    return {
      kind: 'ready',
      response: {
        challengeId: challenge.challengeId,
        credentialId: credential.id,
        clientDataJson: toBase64Url(response.clientDataJSON),
        authenticatorData: toBase64Url(response.authenticatorData),
        signature: toBase64Url(response.signature),
      },
    };
  } catch (failure) {
    return describe(failure);
  }
}

/**
 * `NotAllowedError` covers both "the person said no" and "nobody answered in
 * time", and the specification deliberately does not distinguish them — telling
 * a caller which happened would leak whether a credential was present.
 */
function describe(failure: unknown): Ceremony<never> {
  if (failure instanceof DOMException && failure.name === 'NotAllowedError') {
    return { kind: 'cancelled' };
  }

  return {
    kind: 'failed',
    reason: failure instanceof Error ? failure.message : String(failure),
  };
}
