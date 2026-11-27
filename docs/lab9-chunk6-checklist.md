# Lab 9 chunk 6: persistent Data Protection keys

Status: implementation only, unverified. No builds, tests, application launches,
or filesystem permission checks were run. Every check below is pending for
another model.

Auth now requires DataProtection:KeyPath (environment variable
DataProtection__KeyPath), an absolute directory path. Choose persistent storage
outside the repository. The service uses the stable application name
parabolandust-auth. On Windows, keys are encrypted with current-user DPAPI.
On other platforms, explicit filesystem persistence does not encrypt the XML
keys at rest; restrict filesystem access and supply an appropriate encrypted
storage/protection mechanism for deployment. Do not claim that encryption is
provided by this chunk on Linux.

This configuration is active in Program. Identity provider/store registration,
migrations, account endpoints, AppHost references and deployment volumes remain
pending. Tests of Identity confirmation/reset tokens require the separate Identity
integration harness; the current application only exposes the antiforgery route.

## Configuration and startup

- [ ] Restore/build Auth.Api on .NET 10; check the Windows platform guard covers
      ProtectKeysWithDpapi and Linux builds do not call it.
- [ ] Supply existing JWT settings plus DataProtection__KeyPath. This additional
      setting is now required for startup checks in earlier chunk checklists.
- [ ] Missing/empty/relative paths fail startup with a configuration error and no
      credential disclosure. An absolute path containing spaces works.
- [ ] With an inaccessible directory, attempt an antiforgery token request and
      record the visible failure. Directory access is not proactively validated
      during registration and must not be reported as guaranteed at startup.
- [ ] Inspect actual key creation during token issuance: keys live only in the
      configured directory, outside Git. Retain old keys for decryption.

## Restart and isolation

- [ ] On HTTPS obtain /auth/csrf-token and retain its cookie/header pair. Restart
      Auth under the same OS identity, application name and key directory. In a
      harness protected by SessionAntiforgeryFilter the pair remains valid for
      the same authenticated principal after restart.
- [ ] In the Identity harness, issue confirmation/reset tokens and restart with
      the same key ring and Identity database. Still-unexpired tokens validate;
      expired tokens fail. Persistence does not override token expiration or
      implement one-use semantics on its own.
- [ ] Run two intended replicas with the same accessible key ring and application
      name; a value protected on one is readable on the other. Windows DPAPI
      requires an appropriate shared user/machine boundary; cross-machine
      portability is not implemented by this chunk.
- [ ] A different application name or unrelated key directory must fail to
      unprotect existing values. Do not delete production keys for this check.
- [ ] On Windows inspect stored XML: the key material is protected with DPAPI;
      another unprivileged OS user cannot decrypt it. Verify access permissions.
- [ ] On Linux inspect and document directory/volume permissions and the actual
      at-rest protection supplied by deployment. Do not commit key XML or expose
      it in logs, screenshots or check artifacts.
- [ ] Once AppHost/container deployment is wired, restart/recreate the service
      without replacing its persistent key directory and repeat the checks.

Reference: [Microsoft Data Protection configuration](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview).
