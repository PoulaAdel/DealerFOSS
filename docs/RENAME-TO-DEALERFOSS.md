# Renaming to DealerFOSS

The code, tests, documentation, databases, containers, and cookies were renamed
in one commit. **FOSS is Free and Open-Source Software.**

This file is the part that happens outside the repository: GitHub, the running
containers, and the databases left behind under the old name. Nothing here has
been done for you — every command is yours to run, and each says what it changes
and how to undo it.

Delete this file once the moves are done. It documents a one-off.

---

## 1. GitHub

The remote is untouched. Renaming a repository on GitHub **keeps every issue,
pull request, star, and commit**, and GitHub redirects the old URL — so clones,
existing links, and CI keep working until you change them.

**In the browser:** Settings → General → Repository name → `DealerFOSS` → Rename.

Then repoint this clone. `git remote -v` first to see what is there now:

```bash
git remote -v
```

```bash
git remote set-url origin https://github.com/<owner>/DealerFOSS.git
```

Confirm it took:

```bash
git remote -v
```

**To undo:** rename it back in the same place, and set the URL back. GitHub
redirects both ways, so nothing breaks in between.

### While you are in Settings

- **Description** — suggested: *Free and open-source Dealer Management System for
  independent dealers and dealer groups. AGPLv3.*
- **Topics** — `dms`, `dealership`, `automotive`, `dotnet`, `agplv3`,
  `multi-tenant`, `foss`.
- **Do not** enable Discussions, Wiki, or Pages yet — each is a surface to
  maintain, and there is nothing to put in them.

> One caution. If anybody has ever pushed a fork or cloned this, the old name
> lives in their remote until they change it. GitHub's redirect covers them, but
> a fresh `git clone` of the *old* URL will land in a directory called
> `OpenDealer360`. That is cosmetic.

---

## 2. The containers

The compose project and container names changed, so the containers you have
running now are **orphans** — they keep working under their old names, and
`docker compose` no longer knows about them.

| Was | Now |
|---|---|
| project `opendealer360-dev` | `dealerfoss-dev` |
| `odms-sql` | `dealerfoss-sql` |
| `odms-node` | `dealerfoss-node` |
| volume `odms-sql-data` | `dealerfoss-sql-data` |
| volume `odms-node-modules` | `dealerfoss-node-modules` |

Stop and remove the old ones, then bring the new ones up:

```bash
docker compose -p opendealer360-dev -f deploy/docker-compose.yml --profile node down
```

```bash
docker compose -f deploy/docker-compose.yml up -d sql
```

```bash
docker compose -f deploy/docker-compose.yml --profile node up -d node
```

**What this creates:** two containers and two named volumes under the new names.
**What it costs:** the old `odms-sql-data` volume is *not* deleted by the command
above, so nothing is lost — but the new SQL container starts empty, and
`node_modules` is reinstalled on first use. Both are development conveniences.

**To undo:** `docker compose -p dealerfoss-dev ... down`, then bring the old ones
back with an older checkout of the compose file.

Once you are sure you want the old data gone:

```bash
docker volume rm opendealer360-dev_odms-sql-data opendealer360-dev_odms-node-modules
```

> **This deletes the old SQL container's databases permanently.** Only run it
> after the new stack is up and you have not missed anything.

---

## 3. The databases

The host catalog and every tenant database are named from the product, so they
changed too:

| Was | Now |
|---|---|
| `OpenDealer360_Host` | `DealerFOSS_Host` |
| `OpenDealer360_Tenant_northgroup` | `DealerFOSS_Tenant_northgroup` |
| `OpenDealer360_Tenant_citymotors` | `DealerFOSS_Tenant_citymotors` |

Nothing was migrated. The new ones are created and seeded on the next run, which
has already happened — `verify-e2e.ps1` passes against `DealerFOSS_Host`.

The old LocalDB databases are still there, taking up space and nothing else. To
list them:

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "SELECT name FROM sys.databases WHERE name LIKE 'OpenDealer360%'"
```

To remove them once you are content:

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "DROP DATABASE OpenDealer360_Host"
```

Repeat for each tenant database the query listed. **This is permanent**, and it
is development data only — the real check is that `verify-e2e.ps1` passes, which
it does.

---

## 4. Two things that changed shape, not just name

**Session cookies.** `odms_session` → `dfoss_session`, and the same for
`odms_csrf`, `odms_admin`, `odms_admin_csrf`. Any browser holding an old cookie
is simply signed out; nothing is broken, and signing in again is the whole fix.

**The envelope-encryption format tag.** `odms.v1` → `dfoss.v1`. This one matters
if it ever reaches a deployment: the tag is stored *inside* every encrypted
tenant connection string, and `EnvelopeSecretProtector` will not decrypt a value
whose tag it does not recognise. Because the databases were recreated under new
names, nothing carried over and there is nothing to migrate today. **If this had
been running in production, renaming that tag would have made every tenant
connection string unreadable** — a real migration, not a rename. It is called out
here so the next person to change it knows what they are touching.

---

## 5. One file that now expects the new container

`src/App/appsettings.Development.json` is git-ignored and holds a real password,
so it was updated in place rather than left stale. It now names
`DealerFOSS_Host` and the SA password `DealerFOSS_dev!` — both of which match
`deploy/docker-compose.yml`.

**It will not connect until you have recreated the SQL container** (section 2),
because the container running right now was created with the old password and
SQL Server sets that at first start.

If you want the backend working before then, point it at LocalDB, which is what
`verify-e2e.ps1` uses and what passes today:

```
Server=(localdb)\MSSQLLocalDB;Database=DealerFOSS_Host;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False
```

## 6. Leftover disk clutter, safe to delete

Not code, not referenced, just occupying space under the old name:

- `deploy/.data/sql/log/` — crash dumps and logs from the container that used the
  Windows bind mount, the setup `CLAUDE.md` records as the cause of an earlier
  failure. Nothing reads them.
- `frontend/dist/` — a stale production build. Regenerated by `npm run build`.

## 7. What was deliberately left alone

- **The working directory** is still `D:\Projects\OpenDMS`. Renaming it would
  break every open editor, terminal, and tool path for no benefit — the folder
  name is not shipped anywhere. Rename it whenever it suits you; nothing reads it.
- **Git history.** Commits before the rename say the old name in their messages.
  Rewriting them would change every hash for a cosmetic gain, and the rule in
  `CLAUDE.md` is to never rewrite pushed history.
