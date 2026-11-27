# Lab 9 Accounts identity-link schema

Status: SQL written only; neither the fresh schema nor the upgrade was executed.
The fresh `db/p1/schema.sql` defines the change for a newly initialized database;
existing volumes require an explicit one-time application of
`db/p1/migrations/20261125_identity_links.sql`. Back up the target and inspect it
before applying. The upgrade contains a transaction and does not delete rows.

`identity_managed=true` records have no local password or legacy OAuth fields;
existing rows default false and retain their prior password/OAuth constraint.
`external_identities` stores an exact, case-sensitive `(issuer, subject)` pair
and a numeric user FK. Its primary key retains revoked pairs as tombstones.
Multiple issuers and multiple subjects per account are representable; policy and
privileged proof-of-control are the future provisioning layer's responsibility.

This storage does not create links, require every managed row to have one, verify
issuer signatures or confirmed emails, prevent linking to soft-deleted accounts,
or authorize requests. Provision the user and its first link in one transaction;
resolver logic must reject absent/revoked links and deleted users. A flag alone
must never grant access. No existing endpoint accepts managed accounts yet.

## Verification for another model

- [ ] In an isolated empty PostgreSQL database, apply fresh schema, procedures and
      seeds; existing tests and legacy account insertion behavior still work.
- [ ] In a separate disposable copy of the old schema with representative rows,
      apply the upgrade with `psql -v ON_ERROR_STOP=1 -f ...`; verify all existing
      IDs, passwords/OAuth, unique indexes, row versions and data are unchanged.
- [ ] Repeat the upgrade in the isolated copy and inspect exact constraints/indexes;
      an existing incompatible table definition is not reconciled automatically.
- [ ] Legacy row without password/OAuth fails; legacy rows with valid password or
      OAuth pair pass. Managed row with neither passes, but one with password or
      OAuth credentials fails. Null managed flag and malformed OAuth pair fail.
- [ ] Issuer/subject comparison is byte-sensitive and case-sensitive; same pair
      twice fails even after revocation. Different issuers with same subject can
      be stored. Unknown user FK, empty/oversized byte fields and revocation before
      creation fail. Verify 1024/512-byte boundary and PostgreSQL index behavior.
- [ ] Rollback a user-plus-link transaction and confirm neither row remains; check
      concurrent same-pair inserts leave at most one link.
- [ ] Check soft-deleted users and revoked links are denied by the future resolver;
      this schema intentionally does not implement that resolver.
- [ ] Rollback must be planned from a backup. Reinstating the old authentication
      constraint requires first dealing with any managed users, because they lack
      local credentials. Dropping the link table discards ownership bindings and
      revoked-pair tombstones; never do so as an automatic rollback.
